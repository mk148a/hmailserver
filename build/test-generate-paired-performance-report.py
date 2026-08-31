#!/usr/bin/env python3
from __future__ import annotations

import copy
import hashlib
import importlib.util
import json
import tempfile
import unittest
from datetime import datetime, timedelta, timezone
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

    def descriptor(self, input_root: Path | None = None) -> dict[str, object]:
        return {
            "schema": "paired-cpp-net10-run-v1",
            "status": "SEALED",
            "runId": self.run_id,
            "createdUtc": "2026-08-31T00:00:00+00:00",
            "sealedUtc": "2026-08-31T00:00:01+00:00",
            "fixtureId": self.fixture["fixtureId"],
            "manifestSha256": self.manifest_sha256,
            "inputRoot": str(input_root or (self.root / "inputs")),
            "artifactSlots": [
                {
                    "name": name,
                    "implementation": implementation,
                    "relativePath": relative_path,
                    "sha256": "E" * 64,
                    "artifacts": {
                        "json": "E" * 64,
                        "csv": "E" * 64,
                        "markdown": "E" * 64,
                    },
                }
                for name, (implementation, relative_path, _csv_path, _markdown_path) in REPORT_MODULE.REPORT_SLOTS.items()
            ],
        }

    def protocol_workload(self) -> dict[str, object]:
        samples = []
        summary = []
        for scenario_index, scenario in enumerate(REPORT_MODULE.SCENARIOS, start=1):
            rows = [
                {
                    "scenario": scenario,
                    "iteration": iteration,
                    "ok": True,
                    "ms": round(scenario_index + iteration / 1000, 3),
                    **(
                        {
                            "searchResponseIdentifier": "SEARCH",
                            "searchResultCount": 1000,
                            "searchExactSequence": True,
                            "sortResponseIdentifier": "SORT",
                            "sortResultCount": 1000,
                            "sortExactSequence": True,
                        }
                        if scenario == "imap"
                        else {}
                    ),
                }
                for iteration in range(1, 201)
            ]
            samples.extend(rows)
            values = [row["ms"] for row in rows]
            summary.append(
                {
                    "scenario": scenario,
                    "iterations": 200,
                    "successes": 200,
                    "errors": 0,
                    "p50_ms": REPORT_MODULE.percentile(values, 50),
                    "p95_ms": REPORT_MODULE.percentile(values, 95),
                    "p99_ms": REPORT_MODULE.percentile(values, 99),
                }
            )
        return {
            "schema": "live-protocol-v1",
            "status": "PASS",
            "messageCount": 1000,
            "bind": "127.0.0.1",
            "ports": "SMTP 2525, IMAP 1143, POP3 25110",
            "readinessFailures": [],
            "shutdownFailures": [],
            "summary": summary,
            "samples": samples,
        }

    def concurrent_workload(self, concurrency: int = 4, waves: int = 1) -> dict[str, object]:
        samples = [
            {
                "wave": wave,
                "session": sequence,
                "ok": True,
                "timedOut": False,
                "ms": float(wave * 10 + sequence),
                "searchResultValid": True,
                "searchResultCount": 1000,
                "searchExactSequence": True,
                "sortResultValid": True,
                "sortResultCount": 1000,
                "sortExactSequence": True,
            }
            for wave in range(1, waves + 1)
            for sequence in range(1, concurrency + 1)
        ]
        values = [row["ms"] for row in samples]
        start = datetime(2026, 8, 31, tzinfo=timezone.utc)
        wave_metrics = []
        for wave in range(1, waves + 1):
            wave_start = start + timedelta(seconds=(wave - 1) * 6)
            wave_end = wave_start + timedelta(seconds=1)
            wave_metrics.append(
                {
                    "wave": wave,
                    "startedUtc": wave_start.isoformat(),
                    "endedUtc": wave_end.isoformat(),
                    "workloadSeconds": 1.0,
                    "successes": concurrency,
                    "errors": 0,
                    "processBefore": {"privateBytes": 1000, "handles": 10, "threads": 2},
                    "processAfterImmediate": {"privateBytes": 1100, "handles": 10, "threads": 2},
                    "processAfterSettle": {"privateBytes": 1050, "handles": 10, "threads": 2},
                }
            )
        workload_seconds = float(waves)
        requested = concurrency * waves
        return {
            "schema": "live-concurrent-imap-v2",
            "status": "PASS",
            "concurrency": concurrency,
            "waves": waves,
            "requestedSessions": requested,
            "timeoutMilliseconds": 30000,
            "postWorkloadSettleSeconds": 5,
            "messageCount": 1000,
            "bind": "127.0.0.1",
            "port": 1143,
            "database": "hmail_perf_net10_fixture",
            "sqlConnectionSettings": {
                "provider": "Microsoft.Data.SqlClient",
                "server": "localhost",
                "database": "hmail_perf_net10_fixture",
                "integratedSecurity": True,
                "trustServerCertificate": True,
                "pooling": True,
                "maxPoolSize": 100,
                "connectionTimeoutSeconds": 15,
            },
            "probeConfiguration": {
                "scheduler": "Task.Run with a ManualResetEventSlim start barrier",
                "perSessionCommands": "greeting; LOGIN; SELECT INBOX; SEARCH; SORT; LOGOUT",
                "concurrentSessionsPerWave": concurrency,
                "waves": waves,
                "socketTimeoutMilliseconds": 30000,
                "fanOut": "one TCP client and one sequential IMAP session per sample",
            },
            "readinessFailures": [],
            "shutdownFailures": [],
            "workloadStartedUtc": wave_metrics[0]["startedUtc"],
            "workloadEndedUtc": wave_metrics[-1]["endedUtc"],
            "waveMetrics": wave_metrics,
            "summary": {
                "requested": requested,
                "completed": requested,
                "successes": requested,
                "errors": 0,
                "timeouts": 0,
                "p50_ms": REPORT_MODULE.percentile(values, 50),
                "p95_ms": REPORT_MODULE.percentile(values, 95),
                "p99_ms": REPORT_MODULE.percentile(values, 99),
                "workload_seconds": workload_seconds,
                "throughput_sessions_per_second": round(requested / workload_seconds, 3),
            },
            "samples": samples,
        }

    def smtp_workload(self) -> dict[str, object]:
        samples = [
            {"sequence": sequence, "ok": True, "ms": round(1 + sequence / 1000, 3)}
            for sequence in range(1, 501)
        ]
        values = [row["ms"] for row in samples]
        workload_seconds = 25.0
        workload_started = datetime(2026, 8, 31, tzinfo=timezone.utc)
        workload_ended = workload_started + timedelta(seconds=workload_seconds)
        before_sql = {"available": True, "fixtureValid": True, "messages": 1000}
        after_sql = {"available": True, "fixtureValid": True, "messages": 1500}
        before_data = {"available": True, "fileCount": 1000}
        after_data = {"available": True, "fileCount": 1500}
        return {
            "schema": "live-smtp-message-acceptance-v1",
            "status": "PASS",
            "requestedMessages": 500,
            "acceptedMessages": 500,
            "errors": 0,
            "postWorkloadSettleSeconds": 5,
            "bind": "127.0.0.1",
            "port": 2525,
            "readinessFailures": [],
            "shutdownFailures": [],
            "p50_ms": REPORT_MODULE.percentile(values, 50),
            "p95_ms": REPORT_MODULE.percentile(values, 95),
            "p99_ms": REPORT_MODULE.percentile(values, 99),
            "workloadStartedUtc": workload_started.isoformat(),
            "workloadEndedUtc": workload_ended.isoformat(),
            "workloadSeconds": workload_seconds,
            "throughput_messages_per_second": 20.0,
            "postRunAccounting": {
                "valid": True,
                "sqlAvailable": True,
                "dataAvailable": True,
                "fixtureValidBefore": True,
                "fixtureValidAfter": True,
                "messageRowDelta": 500,
                "dataFileDelta": 500,
                "acceptedStatesObserved": 500,
            },
            "fixture": {
                "before": {"sql": before_sql, "data": before_data},
                "after": {"sql": after_sql, "data": after_data},
            },
            "acceptedMessageStates": [
                {
                    "observed": True,
                    "expectedNewMessages": sequence,
                    "messages": sequence,
                    "queuedMessages": 0,
                    "deliveredMessages": sequence,
                    "snapshot": {
                        "available": True,
                        "fixtureValid": True,
                        "messages": 1000 + sequence,
                    },
                }
                for sequence in range(1, 501)
            ],
            "samples": samples,
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

    def test_accepts_sealed_run_descriptor(self) -> None:
        input_root = self.root / "inputs"
        descriptor_path = self.root / "run.json"
        descriptor_path.write_text(json.dumps(self.descriptor(input_root)), encoding="utf-8")
        run_id, descriptor_hash, slots = REPORT_MODULE.validate_run_descriptor(
            self.descriptor(input_root),
            descriptor_path,
            input_root,
            self.fixture,
            self.manifest_path,
        )
        self.assertEqual(self.run_id, run_id)
        self.assertEqual(hashlib.sha256(descriptor_path.read_bytes()).hexdigest().upper(), descriptor_hash)
        self.assertEqual(set(REPORT_MODULE.REPORT_SLOTS), set(slots))

    def test_rejects_open_or_mismatched_run_descriptor(self) -> None:
        input_root = self.root / "inputs"
        descriptor_path = self.root / "run.json"
        descriptor = self.descriptor(input_root)
        descriptor_path.write_text(json.dumps(descriptor), encoding="utf-8")
        descriptor["status"] = "OPEN"
        with self.assertRaisesRegex(ValueError, "not sealed"):
            REPORT_MODULE.validate_run_descriptor(
                descriptor, descriptor_path, input_root, self.fixture, self.manifest_path
            )
        descriptor = self.descriptor(input_root)
        descriptor["artifactSlots"] = descriptor["artifactSlots"][:-1]
        with self.assertRaisesRegex(ValueError, "incomplete"):
            REPORT_MODULE.validate_run_descriptor(
                descriptor, descriptor_path, input_root, self.fixture, self.manifest_path
            )

    def test_reconciles_sealed_raw_report_hashes_and_rejects_drift(self) -> None:
        input_root = self.root / "inputs"
        descriptor = self.descriptor(input_root)
        slots = {}
        reports = {}
        for slot in descriptor["artifactSlots"]:
            report_path = input_root / Path(slot["relativePath"])
            report_path.parent.mkdir(parents=True, exist_ok=True)
            report_path.write_bytes(slot["name"].encode("ascii"))
            slot["sha256"] = hashlib.sha256(report_path.read_bytes()).hexdigest().upper()
            slot["artifacts"]["json"] = slot["sha256"]
            for artifact_name, suffix in (("csv", ".csv"), ("markdown", ".md")):
                companion_path = report_path.with_suffix(suffix)
                companion_path.write_bytes((slot["name"] + artifact_name).encode("ascii"))
                slot["artifacts"][artifact_name] = hashlib.sha256(companion_path.read_bytes()).hexdigest().upper()
            slots[slot["name"]] = {
                "path": report_path,
                "sha256": slot["sha256"],
                "artifacts": slot["artifacts"],
            }
            reports[slot["name"]] = {"runId": self.run_id}
        REPORT_MODULE.validate_run_descriptor_reports(self.run_id, slots, reports)
        first_path = next(iter(slots.values()))["path"]
        first_path.write_bytes(b"drifted")
        with self.assertRaisesRegex(ValueError, "json hash"):
            REPORT_MODULE.validate_run_descriptor_reports(self.run_id, slots, reports)
        first_path.write_bytes(next(iter(slots)).encode("ascii"))
        first_csv = first_path.with_suffix(".csv")
        first_csv.write_bytes(b"drifted companion")
        with self.assertRaisesRegex(ValueError, "csv hash"):
            REPORT_MODULE.validate_run_descriptor_reports(self.run_id, slots, reports)

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

    def test_accepts_reconciled_acceptance_workloads(self) -> None:
        REPORT_MODULE.validate_protocol_workload(self.protocol_workload())
        REPORT_MODULE.validate_concurrent_workload(
            self.concurrent_workload(concurrency=4, waves=2),
            4,
            2,
            require_pass=True,
        )
        REPORT_MODULE.validate_smtp_workload(self.smtp_workload())

    def test_rejects_under_sampled_or_tampered_protocol_metrics(self) -> None:
        report = self.protocol_workload()
        report["samples"].pop()
        with self.assertRaisesRegex(ValueError, "600 samples"):
            REPORT_MODULE.validate_protocol_workload(report)
        report = self.protocol_workload()
        report["summary"][0]["p95_ms"] += 1
        with self.assertRaisesRegex(ValueError, "p95 does not reconcile"):
            REPORT_MODULE.validate_protocol_workload(report)
        report = self.protocol_workload()
        report["samples"][0]["ms"] = -1
        with self.assertRaisesRegex(ValueError, "must be positive"):
            REPORT_MODULE.validate_protocol_workload(report)

    def test_rejects_wrong_concurrent_methodology_or_throughput(self) -> None:
        report = self.concurrent_workload()
        report["timeoutMilliseconds"] = 5000
        with self.assertRaisesRegex(ValueError, "timeout must be 30 seconds"):
            REPORT_MODULE.validate_concurrent_workload(report, 4, 1, require_pass=True)
        report = self.concurrent_workload()
        report["summary"]["throughput_sessions_per_second"] += 1
        with self.assertRaisesRegex(ValueError, "throughput does not reconcile"):
            REPORT_MODULE.validate_concurrent_workload(report, 4, 1, require_pass=True)
        report = self.concurrent_workload()
        report["samples"][0]["sortExactSequence"] = False
        with self.assertRaisesRegex(ValueError, "exact SEARCH/SORT"):
            REPORT_MODULE.validate_concurrent_workload(report, 4, 1, require_pass=True)
        report = self.concurrent_workload()
        report["samples"][0]["timedOut"] = True
        with self.assertRaisesRegex(ValueError, "cannot succeed and time out"):
            REPORT_MODULE.validate_concurrent_workload(report, 4, 1, require_pass=True)
        report = self.concurrent_workload()
        report["samples"][1]["session"] = 1
        with self.assertRaisesRegex(ValueError, "wave/session membership"):
            REPORT_MODULE.validate_concurrent_workload(report, 4, 1, require_pass=True)
        report = self.concurrent_workload()
        report["timeoutMilliseconds"] = 30000.9
        with self.assertRaisesRegex(ValueError, "not an integer"):
            REPORT_MODULE.validate_concurrent_workload(report, 4, 1, require_pass=True)
        report = self.concurrent_workload()
        report["waveMetrics"][0]["processAfterSettle"]["handles"] = 0
        with self.assertRaisesRegex(ValueError, "handles must be positive"):
            REPORT_MODULE.validate_concurrent_workload(report, 4, 1, require_pass=True)

    def test_accepts_reconciled_diagnostic_concurrent_failure(self) -> None:
        report = self.concurrent_workload()
        failed = report["samples"][-1]
        failed["ok"] = False
        failed["timedOut"] = True
        successful_values = [row["ms"] for row in report["samples"] if row["ok"]]
        report["status"] = "FAIL"
        report["summary"].update(
            {
                "successes": 3,
                "errors": 1,
                "timeouts": 1,
                "p50_ms": REPORT_MODULE.percentile(successful_values, 50),
                "p95_ms": REPORT_MODULE.percentile(successful_values, 95),
                "p99_ms": REPORT_MODULE.percentile(successful_values, 99),
                "throughput_sessions_per_second": 3.0,
            }
        )
        report["waveMetrics"][0]["successes"] = 3
        report["waveMetrics"][0]["errors"] = 1
        REPORT_MODULE.validate_concurrent_workload(report, 4, 1, require_pass=False)

    def test_rejects_under_sampled_or_tampered_smtp_metrics(self) -> None:
        report = self.smtp_workload()
        report["samples"].pop()
        with self.assertRaisesRegex(ValueError, "500 samples"):
            REPORT_MODULE.validate_smtp_workload(report)
        report = self.smtp_workload()
        report["p99_ms"] += 1
        with self.assertRaisesRegex(ValueError, "p99 does not reconcile"):
            REPORT_MODULE.validate_smtp_workload(report)
        report = self.smtp_workload()
        report["postRunAccounting"]["sqlAvailable"] = False
        with self.assertRaisesRegex(ValueError, "SQL/Data accounting"):
            REPORT_MODULE.validate_smtp_workload(report)
        report = self.smtp_workload()
        report["acceptedMessageStates"][0]["observed"] = False
        with self.assertRaisesRegex(ValueError, "accepted-state evidence"):
            REPORT_MODULE.validate_smtp_workload(report)
        report = self.smtp_workload()
        report["workloadEndedUtc"] = (
            datetime.fromisoformat(report["workloadStartedUtc"]) + timedelta(seconds=50)
        ).isoformat()
        with self.assertRaisesRegex(ValueError, "workloadSeconds does not reconcile"):
            REPORT_MODULE.validate_smtp_workload(report)


if __name__ == "__main__":
    unittest.main(verbosity=2)
