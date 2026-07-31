#!/usr/bin/env python3
"""Inventory Scrap Mechanic save schemas without exposing save contents.

This is a development/research tool for Phase 0 of the performance scanner.
It opens every database in immutable read-only mode, reports only schemas and
aggregate statistics, and verifies that the database and SQLite sidecars did
not change while they were inspected.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import sqlite3
import struct
import sys
from pathlib import Path
from typing import Any
from urllib.parse import quote


KNOWN_TABLES: dict[str, tuple[str, ...]] = {
    "Game": ("savegameversion", "gametick"),
    "Harvestable": ("id", "worldId", "x", "y", "size", "data"),
    "Unit": ("id", "worldId", "x", "y", "data"),
    "ScriptData": ("worldId", "data"),
    "GenericData": ("worldId", "data"),
}


def fingerprint(path: Path) -> dict[str, tuple[int, str] | None]:
    result: dict[str, tuple[int, str] | None] = {}
    for suffix in ("", "-wal", "-shm"):
        candidate = Path(str(path) + suffix)
        key = "database" if not suffix else suffix[1:]
        if not candidate.is_file():
            result[key] = None
            continue
        digest = hashlib.sha256()
        size = 0
        with candidate.open("rb") as stream:
            while True:
                chunk = stream.read(1024 * 1024)
                if not chunk:
                    break
                size += len(chunk)
                digest.update(chunk)
        result[key] = (size, digest.hexdigest())
    return result


def open_immutable(path: Path) -> sqlite3.Connection:
    normalized = path.resolve().as_posix()
    uri = "file:" + quote(normalized, safe="/:") + "?mode=ro&immutable=1"
    connection = sqlite3.connect(uri, uri=True, timeout=5.0)
    connection.execute("PRAGMA query_only=ON")
    return connection


def table_columns(
    connection: sqlite3.Connection, table: str
) -> list[dict[str, Any]]:
    rows = connection.execute(
        """
        SELECT name, type, "notnull", pk
        FROM pragma_table_info(?)
        ORDER BY cid
        """,
        (table,),
    )
    return [
        {
            "name": str(name),
            "type": str(data_type or ""),
            "not_null": bool(not_null),
            "primary_key_position": int(primary_key),
        }
        for name, data_type, not_null, primary_key in rows
    ]


def known_layout(
    table: str, columns: list[dict[str, Any]]
) -> tuple[bool, bool]:
    expected = KNOWN_TABLES.get(table)
    if expected is None:
        return False, False
    actual = tuple(column["name"] for column in columns)
    return all(name in actual for name in expected), actual == expected


def harvestable_coordinate_evidence(
    connection: sqlite3.Connection,
) -> dict[str, Any]:
    decoded = 0
    exact_xy = 0
    swapped_xy = 0
    centered_xy = 0
    invalid = 0
    mismatch_sizes: dict[int, int] = {}
    for cell_x, cell_y, size, data in connection.execute(
        "SELECT x, y, size, data FROM Harvestable"
    ):
        if data is None or len(data) < 48:
            invalid += 1
            continue
        position_x, position_y = struct.unpack(">ff", data[40:48])
        if not math.isfinite(position_x) or not math.isfinite(position_y):
            invalid += 1
            continue
        decoded += 1
        expected_x = math.floor(position_x / 64.0)
        expected_y = math.floor(position_y / 64.0)
        if int(cell_x) == expected_x and int(cell_y) == expected_y:
            exact_xy += 1
        if int(cell_x) == expected_y and int(cell_y) == expected_x:
            swapped_xy += 1
        else:
            numeric_size = int(size)
            mismatch_sizes[numeric_size] = (
                mismatch_sizes.get(numeric_size, 0) + 1
            )
        centered_x = math.floor((position_x + 32.0) / 64.0)
        centered_y = math.floor((position_y + 32.0) / 64.0)
        if int(cell_x) == centered_x and int(cell_y) == centered_y:
            centered_xy += 1
    return {
        "hypothesis": (
            "cellX=floor(worldY/64), cellY=floor(worldX/64)"
        ),
        "decoded_rows": decoded,
        "matching_rows": exact_xy,
        "swapped_axis_matches": swapped_xy,
        "centered_cell_matches": centered_xy,
        "nonmatching_rows_by_size": {
            str(size): count
            for size, count in sorted(mismatch_sizes.items())
        },
        "unreadable_rows": invalid,
    }


def unit_coordinate_evidence(
    connection: sqlite3.Connection,
) -> dict[str, Any]:
    decoded = 0
    swapped_xy = 0
    invalid = 0
    for cell_x, cell_y, data in connection.execute(
        "SELECT x, y, data FROM Unit"
    ):
        if data is None or len(data) < 48:
            invalid += 1
            continue
        position_x, position_y = struct.unpack(">ff", data[40:48])
        if not math.isfinite(position_x) or not math.isfinite(position_y):
            invalid += 1
            continue
        decoded += 1
        if (
            int(cell_x) == math.floor(position_y / 64.0)
            and int(cell_y) == math.floor(position_x / 64.0)
        ):
            swapped_xy += 1
    return {
        "hypothesis": (
            "cellX=floor(worldY/64), cellY=floor(worldX/64)"
        ),
        "decoded_rows": decoded,
        "matching_rows": swapped_xy,
        "nonmatching_rows": decoded - swapped_xy,
        "unreadable_rows": invalid,
    }


def decompress_lz4_block(source: bytes) -> bytes:
    output = bytearray()
    index = 0
    while index < len(source):
        token = source[index]
        index += 1
        literal_length = token >> 4
        if literal_length == 15:
            while True:
                if index >= len(source):
                    raise ValueError("invalid LZ4 literal length")
                extension = source[index]
                index += 1
                literal_length += extension
                if extension != 255:
                    break
        if index + literal_length > len(source):
            raise ValueError("invalid LZ4 literal data")
        output.extend(source[index : index + literal_length])
        index += literal_length
        if index >= len(source):
            break
        if index + 2 > len(source):
            raise ValueError("invalid LZ4 match offset")
        offset = source[index] | (source[index + 1] << 8)
        index += 2
        if offset == 0 or offset > len(output):
            raise ValueError("invalid LZ4 back-reference")
        match_length = token & 15
        if match_length == 15:
            while True:
                if index >= len(source):
                    raise ValueError("invalid LZ4 match length")
                extension = source[index]
                index += 1
                match_length += extension
                if extension != 255:
                    break
        match_length += 4
        for _ in range(match_length):
            output.append(output[-offset])
        if len(output) > 128 * 1024 * 1024:
            raise ValueError("unreasonably large LZ4 output")
    return bytes(output)


def read_prefixed_string(data: bytes, cursor: int) -> tuple[str, int]:
    if cursor + 2 > len(data):
        raise ValueError("truncated string length")
    length = int.from_bytes(data[cursor : cursor + 2], "big")
    cursor += 2
    if cursor + length > len(data):
        raise ValueError("truncated string")
    value = data[cursor : cursor + length].decode("utf-8")
    return value, cursor + length


def parse_world_descriptor(
    world_id: int, blob: bytes
) -> tuple[str, str] | None:
    if blob is None or len(blob) < 29:
        return None
    key_length = int.from_bytes(blob[16:18], "big")
    position = 18 + key_length
    if position + 7 > len(blob):
        return None
    stored_world_id = int.from_bytes(
        blob[position : position + 2], "big"
    )
    compressed_length = int.from_bytes(
        blob[position + 3 : position + 7], "big"
    )
    if (
        stored_world_id != world_id
        or position + 7 + compressed_length > len(blob)
    ):
        return None
    decompressed = decompress_lz4_block(
        blob[position + 7 : position + 7 + compressed_length]
    )
    cursor = 4
    script_path, cursor = read_prefixed_string(decompressed, cursor)
    class_name, cursor = read_prefixed_string(decompressed, cursor)
    _, cursor = read_prefixed_string(decompressed, cursor)
    if not class_name:
        return None
    return script_path, class_name


def world_metadata_evidence(
    connection: sqlite3.Connection,
) -> dict[str, int]:
    decoded = 0
    warehouse = 0
    overworld = 0
    chapter_two_markers = 0
    world_ids: set[int] = set()
    for world_id, blob in connection.execute(
        "SELECT worldId, data FROM GenericData"
    ):
        try:
            descriptor = parse_world_descriptor(int(world_id), blob)
        except (UnicodeDecodeError, ValueError):
            descriptor = None
        if descriptor is None:
            continue
        script_path, class_name = descriptor
        decoded += 1
        world_ids.add(int(world_id))
        normalized_class = class_name.casefold()
        normalized_path = script_path.casefold()
        if normalized_class == "warehouseworld":
            warehouse += 1
        if normalized_class == "overworld":
            overworld += 1
        if (
            "survival2" in normalized_path
            or "chapter2" in normalized_path
        ):
            chapter_two_markers += 1
    return {
        "decoded_descriptors": decoded,
        "distinct_world_ids": len(world_ids),
        "overworld_descriptors": overworld,
        "warehouse_descriptors": warehouse,
        "chapter_two_path_markers": chapter_two_markers,
    }


def known_table_stats(
    connection: sqlite3.Connection,
    table: str,
    columns: list[dict[str, Any]],
) -> dict[str, Any] | None:
    required_columns, _ = known_layout(table, columns)
    if not required_columns:
        return None

    # SQL identifiers are constants from KNOWN_TABLES, never save-provided.
    if table == "Harvestable":
        row = connection.execute(
            """
            SELECT COUNT(*), COALESCE(SUM(length(data)), 0),
                   COUNT(DISTINCT worldId),
                   COUNT(DISTINCT printf('%d:%d:%d', worldId, x, y))
            FROM Harvestable
            """
        ).fetchone()
        return {
            "rows": int(row[0]),
            "payload_bytes": int(row[1]),
            "worlds": int(row[2]),
            "populated_cells": int(row[3]),
            "coordinate_evidence": harvestable_coordinate_evidence(
                connection
            ),
        }
    if table == "ScriptData":
        row = connection.execute(
            """
            SELECT COUNT(*), COALESCE(SUM(length(data)), 0),
                   COUNT(DISTINCT worldId)
            FROM ScriptData
            """
        ).fetchone()
        return {
            "rows": int(row[0]),
            "payload_bytes": int(row[1]),
            "worlds": int(row[2]),
        }
    if table == "Unit":
        row = connection.execute(
            """
            SELECT COUNT(*), COALESCE(SUM(length(data)), 0),
                   COUNT(DISTINCT worldId),
                   COUNT(DISTINCT printf('%d:%d:%d', worldId, x, y))
            FROM Unit
            """
        ).fetchone()
        return {
            "rows": int(row[0]),
            "payload_bytes": int(row[1]),
            "worlds": int(row[2]),
            "populated_cells": int(row[3]),
            "coordinate_evidence": unit_coordinate_evidence(
                connection
            ),
        }
    if table == "GenericData":
        row = connection.execute(
            """
            SELECT COUNT(*), COALESCE(SUM(length(data)), 0),
                   COUNT(DISTINCT worldId)
            FROM GenericData
            """
        ).fetchone()
        return {
            "rows": int(row[0]),
            "payload_bytes": int(row[1]),
            "worlds": int(row[2]),
            "world_metadata_evidence": world_metadata_evidence(
                connection
            ),
        }
    if table == "Game":
        row = connection.execute(
            """
            SELECT COUNT(*), MIN(savegameversion), MAX(savegameversion)
            FROM Game
            """
        ).fetchone()
        return {
            "rows": int(row[0]),
            "minimum_save_version": (
                None if row[1] is None else int(row[1])
            ),
            "maximum_save_version": (
                None if row[2] is None else int(row[2])
            ),
        }
    return None


def inspect_save(path: Path, label: str) -> dict[str, Any]:
    before = fingerprint(path)
    if before["database"] is None:
        raise FileNotFoundError("The supplied save is not a regular file.")

    report: dict[str, Any] = {
        "sample": label,
        "file_size_bytes": before["database"][0],
        "integrity": "not_checked",
        "storage": {},
        "tables": [],
        "known_capabilities": {},
        "source_unchanged": False,
    }
    connection = open_immutable(path)
    try:
        integrity_rows = [
            str(row[0])
            for row in connection.execute("PRAGMA quick_check")
        ]
        report["integrity"] = (
            "ok" if integrity_rows == ["ok"] else "failed"
        )
        storage = connection.execute(
            """
            SELECT
              (SELECT page_size FROM pragma_page_size),
              (SELECT page_count FROM pragma_page_count),
              (SELECT freelist_count FROM pragma_freelist_count)
            """
        ).fetchone()
        report["storage"] = {
            "page_size_bytes": int(storage[0]),
            "page_count": int(storage[1]),
            "free_page_count": int(storage[2]),
        }

        table_names = [
            str(row[0])
            for row in connection.execute(
                """
                SELECT name
                FROM sqlite_master
                WHERE type='table' AND name NOT LIKE 'sqlite_%'
                ORDER BY name COLLATE BINARY
                """
            )
        ]
        for table in table_names:
            columns = table_columns(connection, table)
            report["tables"].append(
                {"name": table, "columns": columns}
            )
            if table in KNOWN_TABLES:
                required_columns, exact_layout = known_layout(
                    table, columns
                )
                capability: dict[str, Any] = {
                    "required_columns": required_columns,
                    "exact_layout": exact_layout,
                }
                if required_columns:
                    capability["statistics"] = known_table_stats(
                        connection, table, columns
                    )
                report["known_capabilities"][table] = capability
    finally:
        connection.close()

    after = fingerprint(path)
    report["source_unchanged"] = before == after
    if not report["source_unchanged"]:
        raise RuntimeError(
            f"{label} changed while it was being inspected; "
            "the report was discarded."
        )
    return report


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Create a privacy-safe schema inventory for one or more "
            "Scrap Mechanic saves."
        )
    )
    parser.add_argument(
        "saves",
        nargs="+",
        type=Path,
        help="Save database paths. Paths are never included in output.",
    )
    parser.add_argument(
        "--compact",
        action="store_true",
        help="Emit compact JSON.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    reports = []
    try:
        for index, path in enumerate(args.saves, start=1):
            reports.append(
                inspect_save(path, f"sample-{index:03d}")
            )
    except (OSError, sqlite3.Error, RuntimeError) as error:
        print(f"inventory failed: {error}", file=sys.stderr)
        return 1

    payload = {
        "format": "scraplab-performance-schema-inventory-v1",
        "privacy": (
            "No paths, filenames, row values, identifiers, or blobs included."
        ),
        "samples": reports,
    }
    json.dump(
        payload,
        sys.stdout,
        indent=None if args.compact else 2,
        sort_keys=True,
    )
    sys.stdout.write("\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
