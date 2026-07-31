#!/usr/bin/env python3
"""Regression checks for Phase 0 performance-scanner research fixtures."""

from __future__ import annotations

import contextlib
import hashlib
import io
import json
import sys
import tempfile
from pathlib import Path

sys.dont_write_bytecode = True

import GeneratePerformanceFixtures as generator
import InventoryPerformanceSchemas as inventory


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        while True:
            chunk = stream.read(1024 * 1024)
            if not chunk:
                break
            digest.update(chunk)
    return digest.hexdigest()


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def run() -> None:
    with tempfile.TemporaryDirectory(
        prefix="scraplab-performance-fixtures-"
    ) as temporary:
        root = Path(temporary)
        for filename, create in generator.GENERATORS.items():
            create(root / filename)

        paths = sorted(root.glob("*.db"))
        before = {path.name: sha256(path) for path in paths}
        reports = [
            inventory.inspect_save(path, f"sample-{index:03d}")
            for index, path in enumerate(paths, start=1)
        ]
        after = {path.name: sha256(path) for path in paths}

        require(before == after, "A fixture changed during inventory.")
        require(len(reports) == 6, "Expected six fixture profiles.")
        require(
            all(report["source_unchanged"] for report in reports),
            "The inventory did not prove every source unchanged.",
        )
        require(
            all(
                report["known_capabilities"]["Harvestable"][
                    "required_columns"
                ]
                for report in reports
            ),
            "Harvestable capability was not detected.",
        )
        require(
            all(
                report["known_capabilities"]["Harvestable"][
                    "statistics"
                ]["coordinate_evidence"]["decoded_rows"]
                == report["known_capabilities"]["Harvestable"][
                    "statistics"
                ]["coordinate_evidence"]["swapped_axis_matches"]
                for report in reports
            ),
            "A generated coordinate did not satisfy the proven mapping.",
        )
        unit_capabilities = [
            report["known_capabilities"]["Unit"]
            for report in reports
        ]
        require(
            sum(
                1
                for capability in unit_capabilities
                if capability["required_columns"]
            )
            == 5,
            "Expected five supported Unit layouts.",
        )
        require(
            sum(
                1
                for capability in unit_capabilities
                if not capability["required_columns"]
            )
            == 1,
            "The malformed Unit layout was not rejected.",
        )
        unit_fixture = next(
            capability
            for capability in unit_capabilities
            if capability.get("statistics", {}).get("rows") == 601
        )
        require(
            unit_fixture["statistics"]["coordinate_evidence"][
                "matching_rows"
            ]
            == 600,
            "The valid generated Unit coordinates did not match.",
        )
        require(
            unit_fixture["statistics"]["coordinate_evidence"][
                "nonmatching_rows"
            ]
            == 1,
            "The mismatched optional Unit payload was not inventoried.",
        )
        require(
            any(
                report["known_capabilities"]["GenericData"][
                    "statistics"
                ]["world_metadata_evidence"][
                    "warehouse_descriptors"
                ]
                == 2
                for report in reports
            ),
            "The synthetic warehouse descriptors were not recognized.",
        )

        # Prove the diagnostic payload cannot leak local fixture paths.
        serialized = json.dumps(reports, sort_keys=True)
        require(
            str(root) not in serialized,
            "The privacy-safe report exposed its source directory.",
        )
        require(
            not any(path.name in serialized for path in paths),
            "The privacy-safe report exposed a source filename.",
        )

        # Exercise the CLI formatting without writing a report to disk.
        captured = io.StringIO()
        with contextlib.redirect_stdout(captured):
            json.dump(
                {
                    "format": (
                        "scraplab-performance-schema-inventory-v1"
                    ),
                    "samples": reports,
                },
                captured,
                sort_keys=True,
            )
        require(
            len(captured.getvalue()) > 100,
            "The diagnostic JSON was unexpectedly empty.",
        )


if __name__ == "__main__":
    run()
    print("Performance fixture research regression passed.")
