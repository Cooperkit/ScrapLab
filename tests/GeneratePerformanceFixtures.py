#!/usr/bin/env python3
"""Generate privacy-safe SQLite fixtures for performance-scanner research."""

from __future__ import annotations

import argparse
import math
import sqlite3
import struct
from pathlib import Path


HARVESTABLE_TABLE = """
CREATE TABLE Harvestable(
    id INTEGER PRIMARY KEY,
    worldId INTEGER,
    x INTEGER,
    y INTEGER,
    size INTEGER,
    data BLOB
);
"""

UNIT_TABLE = """
CREATE TABLE Unit(
    id INTEGER PRIMARY KEY,
    worldId INTEGER,
    x INTEGER,
    y INTEGER,
    data BLOB
);
"""

CURRENT_GAME = """
CREATE TABLE Game(
    savegameversion INTEGER,
    flags INTEGER,
    seed INTEGER,
    gametick INTEGER,
    mods BLOB,
    uniqueIds BLOB
);
"""

LEGACY_GAME = """
CREATE TABLE Game(
    savegameversion INTEGER,
    flags INTEGER,
    seed INTEGER,
    gametick INTEGER,
    mods BLOB
);
"""

CURRENT_STORAGE = """
CREATE TABLE GenericData(
    id INTEGER,
    channel INTEGER,
    worldId INTEGER,
    flags INTEGER,
    data BLOB
);
CREATE TABLE ScriptData(
    id INTEGER,
    channel INTEGER,
    worldId INTEGER,
    flags INTEGER,
    data BLOB
);
"""

LEGACY_STORAGE = """
CREATE TABLE GenericData(
    uid BLOB,
    key BLOB,
    worldId INTEGER,
    flags INTEGER,
    data BLOB
);
CREATE TABLE ScriptData(
    uid BLOB,
    key BLOB,
    worldId INTEGER,
    flags INTEGER,
    data BLOB
);
"""


def harvestable_blob(
    world_x: float,
    world_y: float,
    extra_bytes: int = 0,
) -> bytes:
    data = bytearray(64 + extra_bytes)
    struct.pack_into(">fff", data, 36, 0.0, world_x, world_y)
    return bytes(data)


def unit_blob(
    world_x: float,
    world_y: float,
    current_layout: bool,
    valid_position: bool = True,
) -> bytes:
    data = bytearray(65 if current_layout else 60)
    data[20:36] = bytes.fromhex(
        "00112233445566778899aabbccddeeff"
    )
    if valid_position:
        struct.pack_into(">fff", data, 36, 0.0, world_x, world_y)
    return bytes(data)


def lz4_literal_block(data: bytes) -> bytes:
    output = bytearray()
    literal_length = len(data)
    output.append(min(literal_length, 15) << 4)
    if literal_length >= 15:
        remaining = literal_length - 15
        while remaining >= 255:
            output.append(255)
            remaining -= 255
        output.append(remaining)
    output.extend(data)
    return bytes(output)


def prefixed(value: str) -> bytes:
    encoded = value.encode("utf-8")
    return len(encoded).to_bytes(2, "big") + encoded


def world_descriptor_blob(
    world_id: int,
    class_name: str,
    script_path: str,
    parameters: str = "{}",
) -> bytes:
    decompressed = (
        b"\x00\x00\x00\x00"
        + prefixed(script_path)
        + prefixed(class_name)
        + prefixed(parameters)
    )
    compressed = lz4_literal_block(decompressed)
    return (
        bytes(16)
        + (0).to_bytes(2, "big")
        + world_id.to_bytes(2, "big")
        + b"\x00"
        + len(compressed).to_bytes(4, "big")
        + compressed
    )


def add_world_descriptor(
    connection: sqlite3.Connection,
    record_id: int,
    world_id: int,
    class_name: str,
    script_path: str,
) -> None:
    connection.execute(
        """
        INSERT INTO GenericData(id, channel, worldId, flags, data)
        VALUES(?, 0, ?, 0, ?)
        """,
        (
            record_id,
            world_id,
            world_descriptor_blob(
                world_id, class_name, script_path
            ),
        ),
    )


def add_harvestable(
    connection: sqlite3.Connection,
    entity_id: int,
    world_id: int,
    world_x: float,
    world_y: float,
    extra_bytes: int = 0,
) -> None:
    cell_x = math.floor(world_y / 64.0)
    cell_y = math.floor(world_x / 64.0)
    payload = harvestable_blob(world_x, world_y, extra_bytes)
    connection.execute(
        """
        INSERT INTO Harvestable(id, worldId, x, y, size, data)
        VALUES(?, ?, ?, ?, ?, ?)
        """,
        (
            entity_id,
            world_id,
            cell_x,
            cell_y,
            len(payload),
            payload,
        ),
    )


def add_unit(
    connection: sqlite3.Connection,
    entity_id: int,
    world_id: int,
    world_x: float,
    world_y: float,
    current_layout: bool,
    valid_position: bool = True,
) -> None:
    cell_x = math.floor(world_y / 64.0)
    cell_y = math.floor(world_x / 64.0)
    payload = unit_blob(
        world_x, world_y, current_layout, valid_position
    )
    connection.execute(
        """
        INSERT INTO Unit(id, worldId, x, y, data)
        VALUES(?, ?, ?, ?, ?)
        """,
        (entity_id, world_id, cell_x, cell_y, payload),
    )


def create_base(
    path: Path,
    version: int,
    legacy: bool,
    include_unit_table: bool = True,
) -> sqlite3.Connection:
    connection = sqlite3.connect(path)
    connection.executescript(
        (LEGACY_GAME if legacy else CURRENT_GAME)
        + HARVESTABLE_TABLE
        + (UNIT_TABLE if include_unit_table else "")
        + (LEGACY_STORAGE if legacy else CURRENT_STORAGE)
    )
    if legacy:
        connection.execute(
            """
            INSERT INTO Game(
                savegameversion, flags, seed, gametick, mods
            ) VALUES(?, 0, 12345, 24000, X'')
            """,
            (version,),
        )
    else:
        connection.execute(
            """
            INSERT INTO Game(
                savegameversion, flags, seed, gametick, mods, uniqueIds
            ) VALUES(?, 0, 12345, 24000, X'', X'')
            """,
            (version,),
        )
    return connection


def create_ordinary(path: Path) -> None:
    connection = create_base(path, 28, False)
    try:
        add_world_descriptor(
            connection,
            1,
            1,
            "Overworld",
            "$CONTENT_DATA/Scripts/game/worlds/Overworld.lua",
        )
        for index, (world_x, world_y) in enumerate(
            ((12.0, 18.0), (70.0, 14.0), (-5.0, -70.0)),
            start=1,
        ):
            add_harvestable(
                connection, index, 1, world_x, world_y
            )
        connection.commit()
    finally:
        connection.close()


def create_dense(path: Path, dense_count: int = 50000) -> None:
    connection = create_base(path, 28, False)
    try:
        entity_id = 1
        for cell_x in range(-8, 9):
            for cell_y in range(-8, 9):
                count = (
                    dense_count
                    if (cell_x, cell_y) == (3, -2)
                    else 2
                )
                for offset in range(count):
                    local_offset = (offset % 50) * 0.5
                    world_x = (
                        cell_y * 64.0 + 2.0 + local_offset
                    )
                    world_y = (
                        cell_x * 64.0 + 3.0 + local_offset
                    )
                    add_harvestable(
                        connection,
                        entity_id,
                        1,
                        world_x,
                        world_y,
                        2048 if offset == 0 else 0,
                    )
                    entity_id += 1
        connection.commit()
    finally:
        connection.close()


def create_multi_world(path: Path) -> None:
    connection = create_base(path, 28, False)
    try:
        descriptors = (
            (1, "Overworld"),
            (7, "WarehouseWorld"),
            (8, "WarehouseWorld"),
            (9, "QuestWorld"),
        )
        for record_id, (world_id, class_name) in enumerate(
            descriptors, start=1
        ):
            add_world_descriptor(
                connection,
                record_id,
                world_id,
                class_name,
                (
                    "$CONTENT_DATA/Scripts/game/worlds/"
                    + class_name
                    + ".lua"
                ),
            )
        entity_id = 1
        for world_id in (1, 7, 8, 9):
            for cell_x, cell_y in ((0, 0), (4, -3), (-2, 5)):
                add_harvestable(
                    connection,
                    entity_id,
                    world_id,
                    cell_y * 64.0 + 10.0,
                    cell_x * 64.0 + 20.0,
                )
                entity_id += 1
        connection.commit()
    finally:
        connection.close()


def create_legacy(path: Path) -> None:
    connection = create_base(path, 26, True)
    try:
        add_harvestable(connection, 1, 1, 32.0, 48.0)
        connection.commit()
    finally:
        connection.close()


def create_unit_cells(path: Path) -> None:
    connection = create_base(path, 28, False)
    try:
        add_world_descriptor(
            connection,
            1,
            1,
            "Overworld",
            "$CONTENT_DATA/Scripts/game/worlds/Overworld.lua",
        )
        add_harvestable(connection, 1, 1, 8.0, 8.0)
        for entity_id in range(1, 601):
            offset = (entity_id % 20) * 0.5
            add_unit(
                connection,
                entity_id,
                1,
                (6 * 64.0) + 8.0 + offset,
                (5 * 64.0) + 9.0 + offset,
                True,
            )
        add_unit(
            connection,
            601,
            1,
            (6 * 64.0) + 12.0,
            (5 * 64.0) + 13.0,
            True,
            False,
        )
        connection.commit()
    finally:
        connection.close()


def create_modded(path: Path) -> None:
    connection = create_base(path, 28, False, False)
    try:
        connection.executescript(
            """
            CREATE TABLE Unit(
                id INTEGER PRIMARY KEY,
                region TEXT,
                payload BLOB
            );
            INSERT INTO Unit(region, payload)
            VALUES('unsupported-layout', zeroblob(64));
            CREATE TABLE ModPerformanceMystery(
                secretId TEXT,
                region TEXT,
                payload BLOB
            );
            INSERT INTO ModPerformanceMystery
            VALUES('synthetic-only', 'unknown', zeroblob(8192));
            """
        )
        add_harvestable(
            connection, 1, 42, -65.0, 129.0, 4096
        )
        connection.commit()
    finally:
        connection.close()


GENERATORS = {
    "ordinary-new.db": create_ordinary,
    "dense-long-running.db": create_dense,
    "multi-world.db": create_multi_world,
    "legacy-v26.db": create_legacy,
    "unit-cells.db": create_unit_cells,
    "modded-extra-table.db": create_modded,
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "output_directory",
        type=Path,
        help="Empty destination outside source control.",
    )
    parser.add_argument(
        "--dense-count",
        type=int,
        default=50000,
        help="Rows placed in the synthetic dense cell.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    output = args.output_directory.resolve()
    output.mkdir(parents=True, exist_ok=True)
    if any(output.iterdir()):
        raise SystemExit("Fixture output directory must be empty.")
    if args.dense_count < 1:
        raise SystemExit("--dense-count must be positive.")
    for filename, generator in GENERATORS.items():
        if filename == "dense-long-running.db":
            create_dense(output / filename, args.dense_count)
        else:
            generator(output / filename)
    print(f"Generated {len(GENERATORS)} synthetic fixtures.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
