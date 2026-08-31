#!/usr/bin/env python3
from __future__ import annotations

import copy
import hashlib
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


MODULE_PATH = Path(__file__).with_name("generate-paired-performance-report.py")
SPEC = importlib.util.spec_from_file_location("paired_performance_report", MODULE_PATH)
assert SPEC is not None and SPEC.loader is not None
REPORT_MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(REPORT_MODULE)


class PairedPerformanceProvenanceTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary_directory = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary_directory.name)
        self.manifest_path = self.root / "paired-fixture.json"
        self.cpp_hash = "A" * 64
        self.net10_hash = "B" * 64
        self.data_hash = "C" * 64
        self.message_hash = "D" * 64
        self.fixture = {
            "fixtureId": "paired-fixture-test",
            "cppDatabase": "hmail_perf_pair_cpp_test",
            "net10Database": "hmail_perf_pair_net10_test",
            "cppDatabaseVersion": 5708,
            "net10DatabaseVersion": 6000,
            "cppDataRoot": str(self.root / "cpp" / "Data"),
            "net10DataRoot": str(self.root / "net10" / "Data"),
            "cppExecutable": str(self.root / "cpp" / "Bin" / "hMailServer.exe"),
            "net10Executable": str(self.root / "net10" / "Bin" / "hMailServer.exe"),
            "cppExecutableSha256": self.cpp_hash,
            "net10ExecutableSha256": self.net10_hash,
            "dataParity": {
                "fileCount": 1000,
                "bytes": 209679,
                "sha256": self.data_hash,
                "exact": True,
            },
            "messageParity": {
                "rowCount": 1000,
                "sha256": self.message_hash,
                "exact": True,
            },
        }
        self.manifest_path.write_text(json.dumps(self.fixture), encoding="utf-8")
        self.manifest_sha256 = hashlib.sha256(self.manifest_path.read_bytes()).hexdigest().upper()
        self.run_id = "12345678-1234-4234-9234-1234567890ab"

    def tearDown(self) -> None:
        self.temporary_directory.cleanup()

    def report(self, implementation: str) -> dict[str, object]:
        executable_hash = self.fixture[f"{implementation}ExecutableSha256"]
        executable_path = self.fixture[f"{implementation}Executable"]
        database = self.fixture[f"{implementation}Database"]
        database_version = self.fixture[f"{implementation}DatabaseVersion"]
        return {
            "implementation": implementation,
            "runId": self.run_id,
            "provenanceStatus": "MANIFEST_BOUND",
            "fixtureId": self.fixture["fixtureId"],
            "manifestSha256": self.manifest_sha256,
            "database": database,
            "dataRoot": self.fixture[f"{implementation}DataRoot"],
            "executableProvenance": {
                "path": executable_path,
                "sha256": executable_hash,
                "expectedSha256": executable_hash,
                "length": 42,
            },
            "runStartAttestation": {
                "status": "PASS",
                "manifestSha256": self.manifest_sha256,
                "database": database,
                "databaseVersion": database_version,
                "messageRowCount": self.fixture["messageParity"]["rowCount"],
                "messageSha256": self.message_hash,
                "dataFileCount": self.fixture["dataParity"]["fileCount"],
                "dataBytes": self.fixture["dataParity"]["bytes"],
                "dataSha256": self.data_hash,
                "executableSha256": executable_hash,
                "descendantReparsePoints": False,
            },
        }

    def assert_rejected(self, report: dict[str, object], expected: str) -> None:
        with self.assertRaisesRegex(ValueError, expected):
            REPORT_MODULE.validate_report_set(
                [(str(report["implementation"]), report)],
                self.fixture,
                self.manifest_path,
            )

    def test_accepts_one_manifest_bound_run(self) -> None:
        reports = [("cpp", self.report("cpp")), ("net10", self.report("net10"))]
        self.assertEqual(self.run_id, REPORT_MODULE.validate_report_set(reports, self.fixture, self.manifest_path))

    def test_rejects_unbound_report(self) -> None:
        report = self.report("cpp")
        report["provenanceStatus"] = "UNBOUND"
        self.assert_rejected(report, "not MANIFEST_BOUND")

    def test_rejects_report_in_the_wrong_implementation_slot(self) -> None:
        report = self.report("cpp")
        with self.assertRaisesRegex(ValueError, "implementation does not match"):
            REPORT_MODULE.validate_bound_report(
                report,
                "net10",
                self.fixture,
                self.manifest_path,
                self.manifest_sha256,
            )

    def test_rejects_wrong_manifest_database_data_and_executable(self) -> None:
        mutations = (
            ("manifestSha256", "E" * 64, "manifest SHA-256"),
            ("database", "hmail_perf_pair_cpp_other", "database"),
            ("dataRoot", str(self.root / "other" / "Data"), "Data root"),
        )
        for key, value, expected in mutations:
            with self.subTest(key=key):
                report = self.report("cpp")
                report[key] = value
                self.assert_rejected(report, expected)
        report = self.report("cpp")
        report["executableProvenance"]["sha256"] = "E" * 64
        self.assert_rejected(report, "executable SHA-256")

    def test_rejects_missing_or_drifted_attestation(self) -> None:
        report = self.report("cpp")
        report.pop("runStartAttestation")
        self.assert_rejected(report, "run-start attestation is missing")
        report = self.report("cpp")
        report["runStartAttestation"]["messageSha256"] = "E" * 64
        self.assert_rejected(report, "message fingerprint")

    def test_rejects_mixed_run_ids(self) -> None:
        cpp = self.report("cpp")
        net10 = self.report("net10")
        net10["runId"] = "87654321-4321-4321-8321-ba0987654321"
        with self.assertRaisesRegex(ValueError, "mixed run IDs"):
            REPORT_MODULE.validate_report_set(
                [("cpp", cpp), ("net10", net10)],
                self.fixture,
                self.manifest_path,
            )

    def test_rejects_fixture_file_changed_after_reports(self) -> None:
        report = self.report("cpp")
        changed = copy.deepcopy(self.fixture)
        changed["generatedUtc"] = "later"
        self.manifest_path.write_text(json.dumps(changed), encoding="utf-8")
        self.assert_rejected(report, "manifest SHA-256")

    def test_rejects_executable_changed_after_fixture_provisioning(self) -> None:
        cpp_path = Path(str(self.fixture["cppExecutable"]))
        net10_path = Path(str(self.fixture["net10Executable"]))
        cpp_path.parent.mkdir(parents=True)
        net10_path.parent.mkdir(parents=True)
        cpp_path.write_bytes(b"cpp executable")
        net10_path.write_bytes(b"net10 executable")
        fixture = copy.deepcopy(self.fixture)
        fixture["cppExecutableSha256"] = hashlib.sha256(cpp_path.read_bytes()).hexdigest()
        fixture["net10ExecutableSha256"] = hashlib.sha256(net10_path.read_bytes()).hexdigest()
        REPORT_MODULE.validate_fixture_executables(fixture, net10_path)
        cpp_path.write_bytes(b"changed cpp executable")
        with self.assertRaisesRegex(ValueError, r"C\+\+ executable changed"):
            REPORT_MODULE.validate_fixture_executables(fixture, net10_path)


if __name__ == "__main__":
    unittest.main(verbosity=2)
