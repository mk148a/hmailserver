#!/usr/bin/env python3
from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
import os
import re
import stat
import subprocess
import uuid
from datetime import datetime
from pathlib import Path
from typing import Any

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np


CPP_COLOR = "#2F6B9A"
NET10_COLOR = "#D8872D"
TEXT_COLOR = "#262626"
GRID_COLOR = "#D8D8D8"
FAIL_COLOR = "#B33A3A"
SCENARIOS = ("smtp", "imap", "pop3")
CONCURRENCY_LEVELS = (100, 500, 1000)
GENERATED_FILES = (
    "PERFORMANCE_COMPARISON.md",
    "performance-summary.json",
    "performance-summary.csv",
    "protocol-samples.csv",
    "concurrent-imap-samples.csv",
    "smtp-acceptance-samples.csv",
    "net10-imap-soak-waves.csv",
    "protocol-p95.png",
    "imap-concurrency.png",
    "smtp-acceptance.png",
    "net10-imap-soak-resources.png",
)
REPORT_SLOTS = {
    "protocol-cpp": ("cpp", "protocol-cpp/net10-live-protocol.json", "protocol-cpp/net10-live-protocol.csv", "protocol-cpp/net10-live-protocol.md"),
    "protocol-net10": ("net10", "protocol-net10/net10-live-protocol.json", "protocol-net10/net10-live-protocol.csv", "protocol-net10/net10-live-protocol.md"),
    "concurrent-cpp-100": ("cpp", "concurrent-cpp-100/live-concurrent-imap.json", "concurrent-cpp-100/live-concurrent-imap.csv", "concurrent-cpp-100/live-concurrent-imap.md"),
    "concurrent-cpp-500": ("cpp", "concurrent-cpp-500/live-concurrent-imap.json", "concurrent-cpp-500/live-concurrent-imap.csv", "concurrent-cpp-500/live-concurrent-imap.md"),
    "concurrent-cpp-1000": ("cpp", "concurrent-cpp-1000/live-concurrent-imap.json", "concurrent-cpp-1000/live-concurrent-imap.csv", "concurrent-cpp-1000/live-concurrent-imap.md"),
    "concurrent-net10-100": ("net10", "concurrent-net10-100/live-concurrent-imap.json", "concurrent-net10-100/live-concurrent-imap.csv", "concurrent-net10-100/live-concurrent-imap.md"),
    "concurrent-net10-500": ("net10", "concurrent-net10-500/live-concurrent-imap.json", "concurrent-net10-500/live-concurrent-imap.csv", "concurrent-net10-500/live-concurrent-imap.md"),
    "concurrent-net10-1000": ("net10", "concurrent-net10-1000/live-concurrent-imap.json", "concurrent-net10-1000/live-concurrent-imap.csv", "concurrent-net10-1000/live-concurrent-imap.md"),
    "smtp-cpp-500": ("cpp", "smtp-cpp-500/cpp-smtp-message-acceptance.json", "smtp-cpp-500/cpp-smtp-message-acceptance.csv", "smtp-cpp-500/cpp-smtp-message-acceptance.md"),
    "smtp-net10-500": ("net10", "smtp-net10-500/net10-smtp-message-acceptance.json", "smtp-net10-500/net10-smtp-message-acceptance.csv", "smtp-net10-500/net10-smtp-message-acceptance.md"),
    "soak-net10-1000x20": ("net10", "soak-net10-1000x20/live-concurrent-imap.json", "soak-net10-1000x20/live-concurrent-imap.csv", "soak-net10-1000x20/live-concurrent-imap.md"),
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Generate the sanitized legacy C++/.NET 10 performance report."
    )
    parser.add_argument("--input-root", type=Path, required=True)
    parser.add_argument("--fixture-manifest", type=Path, required=True)
    parser.add_argument("--environment", type=Path, required=True)
    parser.add_argument("--legacy-build-manifest", type=Path, required=True)
    parser.add_argument("--net10-executable", type=Path, required=True)
    parser.add_argument("--repository-root", type=Path, required=True)
    parser.add_argument("--output-directory", type=Path, required=True)
    parser.add_argument("--run-descriptor", type=Path, required=True)
    return parser.parse_args()


def load_json(path: Path) -> dict[str, Any]:
    if not path.is_file():
        raise ValueError(f"Required JSON artifact is missing: {path}")
    with path.open("r", encoding="utf-8-sig") as stream:
        value = json.load(stream)
    if not isinstance(value, dict):
        raise ValueError(f"Expected a JSON object: {path}")
    return value


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def fixture_id(fixture: dict[str, Any], manifest_path: Path) -> str:
    value = str(fixture.get("fixtureId") or manifest_path.parent.name)
    require(
        re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_-]*", value) is not None,
        "Fixture ID is missing or invalid.",
    )
    return value


def normalized_path(value: Any) -> str:
    try:
        path_value = os.fspath(value)
    except TypeError as error:
        raise ValueError("Required path is missing.") from error
    require(isinstance(path_value, str) and path_value.strip() != "", "Required path is missing.")
    return os.path.normcase(os.path.abspath(path_value))


def validate_fixture_executables(
    fixture: dict[str, Any], net10_executable_argument: Path
) -> tuple[Path, Path]:
    cpp_executable = Path(fixture["cppExecutable"]).resolve()
    net10_executable = net10_executable_argument.resolve()
    require(cpp_executable.is_file(), "Fixture C++ executable is missing.")
    require(net10_executable.is_file(), "Net10 Release executable is missing.")
    require(
        normalized_path(net10_executable) == normalized_path(fixture["net10Executable"]),
        "Net10 executable path does not match the fixture manifest.",
    )
    require(
        sha256(cpp_executable) == str(fixture["cppExecutableSha256"]).upper(),
        "Fixture C++ executable changed after provisioning.",
    )
    require(
        sha256(net10_executable) == str(fixture["net10ExecutableSha256"]).upper(),
        "Fixture Net10 executable changed after provisioning.",
    )
    return cpp_executable, net10_executable


def validate_bound_report(
    report: dict[str, Any],
    implementation: str,
    fixture: dict[str, Any],
    manifest_path: Path,
    manifest_sha256: str,
) -> str:
    expected_fixture_id = fixture_id(fixture, manifest_path)
    expected_database = str(fixture[f"{implementation}Database"])
    expected_database_version = int(fixture[f"{implementation}DatabaseVersion"])
    expected_data_root = normalized_path(fixture[f"{implementation}DataRoot"])
    expected_executable = normalized_path(fixture[f"{implementation}Executable"])
    expected_executable_sha256 = str(fixture[f"{implementation}ExecutableSha256"]).upper()

    require(
        report.get("provenanceStatus") == "MANIFEST_BOUND",
        f"{implementation} report is not MANIFEST_BOUND.",
    )
    require(
        report.get("implementation") == implementation,
        f"{implementation} report implementation does not match its input slot.",
    )
    try:
        run_id = str(uuid.UUID(str(report.get("runId"))))
    except (ValueError, AttributeError) as error:
        raise ValueError(f"{implementation} report runId is missing or invalid.") from error
    require(run_id != str(uuid.UUID(int=0)), f"{implementation} report runId is empty.")
    require(
        report.get("fixtureId") == expected_fixture_id,
        f"{implementation} report fixture ID does not match the fixture manifest.",
    )
    require(
        str(report.get("manifestSha256", "")).upper() == manifest_sha256,
        f"{implementation} report manifest SHA-256 does not match the fixture manifest.",
    )
    require(
        report.get("database") == expected_database,
        f"{implementation} report database does not match the fixture manifest.",
    )
    require(
        normalized_path(report.get("dataRoot")) == expected_data_root,
        f"{implementation} report Data root does not match the fixture manifest.",
    )

    executable = report.get("executableProvenance")
    require(isinstance(executable, dict), f"{implementation} executable provenance is missing.")
    require(
        normalized_path(executable.get("path")) == expected_executable,
        f"{implementation} report executable path does not match the fixture manifest.",
    )
    require(
        str(executable.get("sha256", "")).upper() == expected_executable_sha256
        and str(executable.get("expectedSha256", "")).upper() == expected_executable_sha256,
        f"{implementation} report executable SHA-256 does not match the fixture manifest.",
    )
    require(int(executable.get("length", 0)) > 0, f"{implementation} executable length is invalid.")

    attestation = report.get("runStartAttestation")
    require(isinstance(attestation, dict), f"{implementation} run-start attestation is missing.")
    require(attestation.get("status") == "PASS", f"{implementation} run-start attestation did not pass.")
    require(
        str(attestation.get("manifestSha256", "")).upper() == manifest_sha256,
        f"{implementation} run-start manifest SHA-256 does not match.",
    )
    require(
        attestation.get("database") == expected_database
        and int(attestation.get("databaseVersion", -1)) == expected_database_version,
        f"{implementation} run-start database identity does not match.",
    )
    require(
        int(attestation.get("messageRowCount", -1)) == int(fixture["messageParity"]["rowCount"])
        and str(attestation.get("messageSha256", "")).upper()
        == str(fixture["messageParity"]["sha256"]).upper(),
        f"{implementation} run-start message fingerprint does not match.",
    )
    require(
        int(attestation.get("dataFileCount", -1)) == int(fixture["dataParity"]["fileCount"])
        and int(attestation.get("dataBytes", -1)) == int(fixture["dataParity"]["bytes"])
        and str(attestation.get("dataSha256", "")).upper()
        == str(fixture["dataParity"]["sha256"]).upper(),
        f"{implementation} run-start Data fingerprint does not match.",
    )
    require(
        str(attestation.get("executableSha256", "")).upper() == expected_executable_sha256,
        f"{implementation} run-start executable SHA-256 does not match.",
    )
    require(
        attestation.get("descendantReparsePoints") is False,
        f"{implementation} run-start Data tree contains or did not check reparse points.",
    )
    return run_id


def validate_report_set(
    reports: list[tuple[str, dict[str, Any]]],
    fixture: dict[str, Any],
    manifest_path: Path,
) -> str:
    manifest_sha256 = sha256(manifest_path)
    run_ids = {
        validate_bound_report(report, implementation, fixture, manifest_path, manifest_sha256)
        for implementation, report in reports
    }
    require(len(run_ids) == 1, "Paired performance inputs contain mixed run IDs.")
    return next(iter(run_ids))


def validate_run_descriptor(
    descriptor: dict[str, Any],
    descriptor_path: Path,
    input_root: Path,
    fixture: dict[str, Any],
    manifest_path: Path,
) -> tuple[str, str, dict[str, dict[str, Any]]]:
    require(
        descriptor.get("schema") == "paired-cpp-net10-run-v1",
        "Unexpected paired run descriptor schema.",
    )
    require(descriptor.get("status") == "SEALED", "Paired run descriptor is not sealed.")
    created = parse_timestamp(descriptor.get("createdUtc"), "Paired run descriptor createdUtc")
    sealed = parse_timestamp(descriptor.get("sealedUtc"), "Paired run descriptor sealedUtc")
    require(sealed > created, "Paired run descriptor seal time must be after creation time.")
    try:
        run_id = str(uuid.UUID(str(descriptor.get("runId"))))
    except (ValueError, AttributeError) as error:
        raise ValueError("Paired run descriptor runId is missing or invalid.") from error
    require(run_id != str(uuid.UUID(int=0)), "Paired run descriptor runId is empty.")
    expected_fixture_id = fixture_id(fixture, manifest_path)
    require(
        descriptor.get("fixtureId") == expected_fixture_id,
        "Paired run descriptor fixture ID does not match the fixture manifest.",
    )
    manifest_sha256 = sha256(manifest_path)
    require(
        str(descriptor.get("manifestSha256", "")).upper() == manifest_sha256,
        "Paired run descriptor manifest SHA-256 does not match the fixture manifest.",
    )
    require(
        normalized_path(descriptor.get("inputRoot")) == normalized_path(input_root),
        "Paired run descriptor input root does not match the report input root.",
    )
    slots = descriptor.get("artifactSlots")
    require(isinstance(slots, list), "Paired run descriptor artifact slots are missing.")
    require(len(slots) == len(REPORT_SLOTS), "Paired run descriptor artifact slots are incomplete.")
    observed: dict[str, dict[str, Any]] = {}
    for slot in slots:
        require(isinstance(slot, dict), "Paired run descriptor contains an invalid artifact slot.")
        name = slot.get("name")
        require(name in REPORT_SLOTS, f"Paired run descriptor has an unexpected artifact slot: {name}.")
        require(name not in observed, f"Paired run descriptor repeats artifact slot: {name}.")
        implementation, relative_path, csv_path, markdown_path = REPORT_SLOTS[name]
        relative = Path(str(slot.get("relativePath", "")))
        require(
            slot.get("implementation") == implementation
            and not relative.is_absolute()
            and ".." not in relative.parts
            and relative == Path(relative_path),
            f"Paired run descriptor path or implementation is wrong for {name}.",
        )
        report_path = input_root / relative
        report_hash = str(slot.get("sha256", "")).upper()
        require(
            re.fullmatch(r"[0-9A-F]{64}", report_hash) is not None,
            f"Paired run descriptor raw report hash is missing for {name}.",
        )
        artifacts = slot.get("artifacts")
        require(isinstance(artifacts, dict), f"Paired run descriptor companion hashes are missing for {name}.")
        companion_hashes = {}
        for artifact_name, expected_path in (("json", relative_path), ("csv", csv_path), ("markdown", markdown_path)):
            artifact_hash = str(artifacts.get(artifact_name, "")).upper()
            require(
                re.fullmatch(r"[0-9A-F]{64}", artifact_hash) is not None,
                f"Paired run descriptor {artifact_name} hash is missing for {name}.",
            )
            if artifact_name == "json":
                require(artifact_hash == report_hash, f"Paired run descriptor JSON hash disagrees for {name}.")
            companion_hashes[artifact_name] = artifact_hash
        observed[name] = {
            "implementation": implementation,
            "path": report_path,
            "sha256": report_hash,
            "artifacts": companion_hashes,
        }
    require(
        set(observed) == set(REPORT_SLOTS),
        "Paired run descriptor artifact slots do not match the required matrix.",
    )
    return run_id, sha256(descriptor_path), observed


def validate_run_descriptor_reports(
    descriptor_run_id: str,
    slots: dict[str, dict[str, Any]],
    reports: dict[str, dict[str, Any]],
) -> None:
    require(set(reports) == set(slots), "Paired run descriptor reports do not match the required slots.")
    for name, slot in slots.items():
        report_path = slot["path"]
        for artifact_name, artifact_path in (("json", report_path), ("csv", slots[name]["path"].with_suffix(".csv")), ("markdown", slots[name]["path"].with_suffix(".md"))):
            require(artifact_path.is_file(), f"Paired run descriptor {artifact_name} report is missing for {name}.")
            require(
                sha256(artifact_path) == slot["artifacts"][artifact_name],
                f"Paired run descriptor {artifact_name} hash does not match for {name}.",
            )
        report = reports[name]
        require(
            str(uuid.UUID(str(report.get("runId")))) == descriptor_run_id,
            f"Paired run descriptor runId does not match report {name}.",
        )


def percentile(values: list[float], percent: float) -> float | None:
    if not values:
        return None
    ordered = sorted(values)
    rank = (percent / 100.0) * (len(ordered) - 1)
    lower = math.floor(rank)
    upper = math.ceil(rank)
    if lower == upper:
        return round(ordered[lower], 3)
    return round(
        ordered[lower] + (ordered[upper] - ordered[lower]) * (rank - lower),
        3,
    )


def require_number(value: Any, label: str, *, positive: bool = False) -> float:
    require(not isinstance(value, bool), f"{label} is not numeric.")
    try:
        number = float(value)
    except (TypeError, ValueError) as error:
        raise ValueError(f"{label} is not numeric.") from error
    require(math.isfinite(number), f"{label} is not finite.")
    require(not positive or number > 0, f"{label} must be positive.")
    return number


def require_integer(value: Any, label: str) -> int:
    require(type(value) is int, f"{label} is not an integer.")
    return value


def parse_timestamp(value: Any, label: str) -> datetime:
    require(isinstance(value, str) and value.strip() != "", f"{label} is missing.")
    try:
        timestamp = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError as error:
        raise ValueError(f"{label} is invalid.") from error
    require(timestamp.tzinfo is not None, f"{label} must include an offset.")
    return timestamp


def validate_process_metrics(value: Any, label: str) -> None:
    require(isinstance(value, dict), f"{label} is missing.")
    require_integer(value.get("privateBytes"), f"{label} privateBytes")
    require_integer(value.get("handles"), f"{label} handles")
    require_integer(value.get("threads"), f"{label} threads")
    require(value["privateBytes"] > 0, f"{label} privateBytes must be positive.")
    require(value["handles"] > 0, f"{label} handles must be positive.")
    require(value["threads"] > 0, f"{label} threads must be positive.")


def require_metric(actual: Any, expected: float | None, label: str) -> None:
    if expected is None:
        require(actual is None, f"{label} must be null when there are no successful samples.")
        return
    actual_number = require_number(actual, label)
    require(abs(actual_number - expected) <= 0.001, f"{label} does not reconcile with samples.")


def validate_protocol_workload(report: dict[str, Any]) -> None:
    require(report.get("schema") == "live-protocol-v1", "Unexpected protocol schema.")
    require(report.get("status") == "PASS", "Protocol workload did not pass.")
    require(require_integer(report.get("messageCount"), "Protocol messageCount") == 1000, "Protocol corpus is not 1,000 messages.")
    require(report.get("bind") == "127.0.0.1", "Protocol workload is not loopback-only.")
    require(
        report.get("ports") == "SMTP 2525, IMAP 1143, POP3 25110",
        "Protocol ports do not match the paired methodology.",
    )
    samples = report.get("samples")
    require(isinstance(samples, list) and len(samples) == 600, "Protocol workload must contain 600 samples.")
    require(
        isinstance(report.get("summary"), list)
        and len(report["summary"]) == 3
        and {row.get("scenario") for row in report["summary"]} == set(SCENARIOS),
        "Protocol summary must contain exactly SMTP, IMAP, and POP3.",
    )
    require(not report.get("readinessFailures"), "Protocol readiness failures are present.")
    require(not report.get("shutdownFailures"), "Protocol shutdown failures are present.")
    for scenario in SCENARIOS:
        rows = [row for row in samples if row.get("scenario") == scenario]
        require(len(rows) == 200, f"Protocol {scenario} must contain 200 samples.")
        require(
            {require_integer(row.get("iteration"), f"Protocol {scenario} iteration") for row in rows}
            == set(range(1, 201)),
            f"Protocol {scenario} iteration sequence is incomplete or duplicated.",
        )
        require(all(type(row.get("ok")) is bool for row in rows), f"Protocol {scenario} ok is not boolean.")
        require(all(row.get("ok") is True for row in rows), f"Protocol {scenario} contains failures.")
        if scenario == "imap":
            require(
                all(
                    row.get("searchResponseIdentifier") == "SEARCH"
                    and row.get("searchResultCount") == 1000
                    and row.get("searchExactSequence") is True
                    and row.get("sortResponseIdentifier") == "SORT"
                    and row.get("sortResultCount") == 1000
                    and row.get("sortExactSequence") is True
                    for row in rows
                ),
                "Protocol IMAP samples do not contain exact SEARCH/SORT 1..1000 results.",
            )
        values = [require_number(row.get("ms"), f"Protocol {scenario} sample ms", positive=True) for row in rows]
        summary = summary_row(report, scenario)
        require(require_integer(summary.get("iterations"), f"Protocol {scenario} iterations") == 200, f"Protocol {scenario} iteration summary is wrong.")
        require(require_integer(summary.get("successes"), f"Protocol {scenario} successes") == 200, f"Protocol {scenario} success summary is wrong.")
        require(require_integer(summary.get("errors"), f"Protocol {scenario} errors") == 0, f"Protocol {scenario} error summary is wrong.")
        for percent in (50, 95, 99):
            require_metric(summary.get(f"p{percent}_ms"), percentile(values, percent), f"Protocol {scenario} p{percent}")


def validate_concurrent_workload(
    report: dict[str, Any],
    expected_concurrency: int,
    expected_waves: int,
    *,
    require_pass: bool,
) -> None:
    require(report.get("schema") == "live-concurrent-imap-v2", "Unexpected concurrent IMAP schema.")
    require(require_integer(report.get("concurrency"), "Concurrent IMAP concurrency") == expected_concurrency, "Concurrent IMAP level mismatch.")
    require(require_integer(report.get("waves"), "Concurrent IMAP waves") == expected_waves, "Concurrent IMAP wave count mismatch.")
    requested = expected_concurrency * expected_waves
    require(require_integer(report.get("requestedSessions"), "Concurrent IMAP requestedSessions") == requested, "Concurrent IMAP requested count mismatch.")
    require(require_integer(report.get("timeoutMilliseconds"), "Concurrent IMAP timeoutMilliseconds") == 30000, "Concurrent IMAP timeout must be 30 seconds.")
    require(require_integer(report.get("postWorkloadSettleSeconds"), "Concurrent IMAP postWorkloadSettleSeconds") == 5, "Concurrent IMAP settle interval must be five seconds.")
    require(require_integer(report.get("messageCount"), "Concurrent IMAP messageCount") == 1000, "Concurrent IMAP corpus is not 1,000 messages.")
    require(report.get("bind") == "127.0.0.1" and require_integer(report.get("port"), "Concurrent IMAP port") == 1143, "Concurrent IMAP is not on the approved loopback port.")
    sql_settings = report.get("sqlConnectionSettings")
    require(isinstance(sql_settings, dict), "Concurrent IMAP SQL connection settings are missing.")
    require(sql_settings.get("server") == "localhost", "Concurrent IMAP SQL server is not attested as localhost.")
    require(sql_settings.get("database") == report.get("database"), "Concurrent IMAP SQL database attestation does not match the report.")
    if report.get("implementation") == "net10":
        require(sql_settings.get("provider") == "Microsoft.Data.SqlClient", "Concurrent IMAP SQL provider is not attested.")
        require(sql_settings.get("integratedSecurity") is True and sql_settings.get("trustServerCertificate") is True, "Concurrent IMAP SQL security settings are not attested.")
        require(sql_settings.get("pooling") is True, "Concurrent IMAP SQL pooling is not attested.")
        require_integer(sql_settings.get("maxPoolSize"), "Concurrent IMAP SQL maxPoolSize")
        require(require_integer(sql_settings.get("connectionTimeoutSeconds"), "Concurrent IMAP SQL connectionTimeoutSeconds") == 15, "Concurrent IMAP SQL connection timeout must be 15 seconds.")
    else:
        require(sql_settings.get("provider") == "legacy native hMailServer SQL layer", "Concurrent C++ SQL provider is not attested as the legacy native layer.")
        require(sql_settings.get("maxPoolSize") is None, "Concurrent C++ report must not claim a Net10 SQL pool size.")
    probe = report.get("probeConfiguration")
    require(isinstance(probe, dict), "Concurrent IMAP probe configuration is missing.")
    require(isinstance(probe.get("scheduler"), str) and probe["scheduler"], "Concurrent IMAP scheduler attestation is missing.")
    require(isinstance(probe.get("perSessionCommands"), str) and probe["perSessionCommands"], "Concurrent IMAP per-session command attestation is missing.")
    require(isinstance(probe.get("fanOut"), str) and probe["fanOut"], "Concurrent IMAP fan-out attestation is missing.")
    require(require_integer(probe.get("concurrentSessionsPerWave"), "Concurrent IMAP probe concurrency") == expected_concurrency, "Concurrent IMAP probe concurrency attestation is wrong.")
    require(require_integer(probe.get("waves"), "Concurrent IMAP probe waves") == expected_waves, "Concurrent IMAP probe wave attestation is wrong.")
    samples = report.get("samples")
    require(isinstance(samples, list) and len(samples) == requested, "Concurrent IMAP sample count mismatch.")
    wave_sessions: dict[int, set[int]] = {}
    for row in samples:
        require(type(row.get("ok")) is bool, "Concurrent IMAP sample ok is not boolean.")
        require(type(row.get("timedOut")) is bool, "Concurrent IMAP sample timedOut is not boolean.")
        require(not (row["ok"] and row["timedOut"]), "Concurrent IMAP sample cannot succeed and time out.")
        require_number(row.get("ms"), "Concurrent IMAP sample ms", positive=True)
        wave = require_integer(row.get("wave"), "Concurrent IMAP sample wave")
        session = require_integer(row.get("session"), "Concurrent IMAP sample session")
        wave_sessions.setdefault(wave, set()).add(session)
    require(
        wave_sessions
        == {wave: set(range(1, expected_concurrency + 1)) for wave in range(1, expected_waves + 1)},
        "Concurrent IMAP wave/session membership is incomplete or duplicated.",
    )
    successful = [row for row in samples if row.get("ok") is True]
    timed_out = [row for row in samples if row.get("timedOut") is True]
    require(
        all(
            row.get("searchResultValid") is True
            and row.get("searchResultCount") == 1000
            and row.get("searchExactSequence") is True
            and row.get("sortResultValid") is True
            and row.get("sortResultCount") == 1000
            and row.get("sortExactSequence") is True
            for row in successful
        ),
        "Successful concurrent IMAP samples do not contain exact SEARCH/SORT 1..1000 results.",
    )
    values = [float(row["ms"]) for row in successful]
    summary = report.get("summary")
    require(isinstance(summary, dict), "Concurrent IMAP summary is missing.")
    require(require_integer(summary.get("requested"), "Concurrent IMAP summary requested") == requested, "Concurrent IMAP requested summary is wrong.")
    require(require_integer(summary.get("completed"), "Concurrent IMAP summary completed") == len(samples), "Concurrent IMAP completed summary is wrong.")
    require(require_integer(summary.get("successes"), "Concurrent IMAP summary successes") == len(successful), "Concurrent IMAP success summary is wrong.")
    require(require_integer(summary.get("errors"), "Concurrent IMAP summary errors") == requested - len(successful), "Concurrent IMAP error summary is wrong.")
    require(require_integer(summary.get("timeouts"), "Concurrent IMAP summary timeouts") == len(timed_out), "Concurrent IMAP timeout summary is wrong.")
    for percent in (50, 95, 99):
        require_metric(summary.get(f"p{percent}_ms"), percentile(values, percent), f"Concurrent IMAP p{percent}")

    wave_metrics = report.get("waveMetrics")
    require(isinstance(wave_metrics, list) and len(wave_metrics) == expected_waves, "Concurrent IMAP waveMetrics count mismatch.")
    exact_workload_seconds = 0.0
    for expected_wave, metric in enumerate(wave_metrics, start=1):
        require(require_integer(metric.get("wave"), "Concurrent IMAP metric wave") == expected_wave, "Concurrent IMAP metric wave order is wrong.")
        started = parse_timestamp(metric.get("startedUtc"), "Concurrent IMAP metric startedUtc")
        ended = parse_timestamp(metric.get("endedUtc"), "Concurrent IMAP metric endedUtc")
        duration = (ended - started).total_seconds()
        require(duration > 0, "Concurrent IMAP metric duration must be positive.")
        require_metric(metric.get("workloadSeconds"), round(duration, 6), "Concurrent IMAP metric workloadSeconds")
        exact_workload_seconds += duration
        wave_rows = [row for row in samples if row["wave"] == expected_wave]
        wave_successes = sum(row["ok"] is True for row in wave_rows)
        require(require_integer(metric.get("successes"), "Concurrent IMAP metric successes") == wave_successes, "Concurrent IMAP wave successes do not reconcile.")
        require(require_integer(metric.get("errors"), "Concurrent IMAP metric errors") == expected_concurrency - wave_successes, "Concurrent IMAP wave errors do not reconcile.")
        validate_process_metrics(metric.get("processBefore"), "Concurrent IMAP processBefore")
        validate_process_metrics(metric.get("processAfterImmediate"), "Concurrent IMAP processAfterImmediate")
        validate_process_metrics(metric.get("processAfterSettle"), "Concurrent IMAP processAfterSettle")
    require(
        parse_timestamp(report.get("workloadStartedUtc"), "Concurrent IMAP workloadStartedUtc")
        == parse_timestamp(wave_metrics[0].get("startedUtc"), "Concurrent IMAP first wave startedUtc")
        and parse_timestamp(report.get("workloadEndedUtc"), "Concurrent IMAP workloadEndedUtc")
        == parse_timestamp(wave_metrics[-1].get("endedUtc"), "Concurrent IMAP last wave endedUtc"),
        "Concurrent IMAP workload bounds do not match waveMetrics.",
    )
    require_metric(summary.get("workload_seconds"), round(exact_workload_seconds, 6), "Concurrent IMAP workload seconds")
    expected_throughput = round(len(successful) / exact_workload_seconds, 3)
    require_metric(summary.get("throughput_sessions_per_second"), expected_throughput, "Concurrent IMAP throughput")
    derived_pass = (
        len(successful) == requested
        and not timed_out
        and not report.get("readinessFailures")
        and not report.get("shutdownFailures")
    )
    require(report.get("status") == ("PASS" if derived_pass else "FAIL"), "Concurrent IMAP status does not reconcile.")
    if require_pass:
        require(derived_pass, "Required concurrent IMAP workload did not pass.")


def validate_smtp_workload(report: dict[str, Any]) -> None:
    require(report.get("schema") == "live-smtp-message-acceptance-v1", "Unexpected SMTP schema.")
    require(report.get("status") == "PASS", "SMTP acceptance workload did not pass.")
    require(require_integer(report.get("requestedMessages"), "SMTP requestedMessages") == 500, "SMTP acceptance must request 500 messages.")
    require(require_integer(report.get("postWorkloadSettleSeconds"), "SMTP postWorkloadSettleSeconds") == 5, "SMTP settle interval must be five seconds.")
    require(report.get("bind") == "127.0.0.1" and require_integer(report.get("port"), "SMTP port") == 2525, "SMTP acceptance is not on the approved loopback port.")
    samples = report.get("samples")
    require(isinstance(samples, list) and len(samples) == 500, "SMTP acceptance must contain 500 samples.")
    require(
        {require_integer(row.get("sequence"), "SMTP sample sequence") for row in samples} == set(range(1, 501)),
        "SMTP acceptance sequence is incomplete or duplicated.",
    )
    successful = [row for row in samples if row.get("ok") is True]
    require(all(type(row.get("ok")) is bool for row in samples), "SMTP sample ok is not boolean.")
    require(len(successful) == 500, "SMTP acceptance contains failed samples.")
    require(require_integer(report.get("acceptedMessages"), "SMTP acceptedMessages") == 500, "SMTP accepted-message summary is wrong.")
    require(require_integer(report.get("errors"), "SMTP errors") == 0, "SMTP error summary is wrong.")
    values = [require_number(row.get("ms"), "SMTP sample ms", positive=True) for row in successful]
    for percent in (50, 95, 99):
        require_metric(report.get(f"p{percent}_ms"), percentile(values, percent), f"SMTP p{percent}")
    workload_started = parse_timestamp(report.get("workloadStartedUtc"), "SMTP workloadStartedUtc")
    workload_ended = parse_timestamp(report.get("workloadEndedUtc"), "SMTP workloadEndedUtc")
    workload_seconds = (workload_ended - workload_started).total_seconds()
    require(workload_seconds > 0, "SMTP workload duration must be positive.")
    require_metric(report.get("workloadSeconds"), round(workload_seconds, 6), "SMTP workloadSeconds")
    require_metric(
        report.get("throughput_messages_per_second"),
        round(500 / workload_seconds, 3),
        "SMTP throughput",
    )
    require(not report.get("readinessFailures"), "SMTP readiness failures are present.")
    require(not report.get("shutdownFailures"), "SMTP shutdown failures are present.")
    accounting = report.get("postRunAccounting")
    require(isinstance(accounting, dict) and accounting.get("valid") is True, "SMTP post-run accounting did not pass.")
    require(
        accounting.get("sqlAvailable") is True
        and accounting.get("dataAvailable") is True
        and accounting.get("fixtureValidBefore") is True
        and accounting.get("fixtureValidAfter") is True
        and require_integer(accounting.get("messageRowDelta"), "SMTP messageRowDelta") == 500
        and require_integer(accounting.get("dataFileDelta"), "SMTP dataFileDelta") == 500
        and require_integer(accounting.get("acceptedStatesObserved"), "SMTP acceptedStatesObserved") == 500,
        "SMTP SQL/Data accounting is not exactly +500/+500.",
    )
    fixture = report.get("fixture")
    require(isinstance(fixture, dict), "SMTP fixture evidence is missing.")
    before = fixture.get("before")
    after = fixture.get("after")
    require(isinstance(before, dict) and isinstance(after, dict), "SMTP before/after evidence is missing.")
    before_sql, after_sql = before.get("sql"), after.get("sql")
    before_data, after_data = before.get("data"), after.get("data")
    require(
        all(isinstance(value, dict) for value in (before_sql, after_sql, before_data, after_data))
        and before_sql.get("available") is True
        and after_sql.get("available") is True
        and before_sql.get("fixtureValid") is True
        and after_sql.get("fixtureValid") is True
        and before_data.get("available") is True
        and after_data.get("available") is True
        and require_integer(after_sql.get("messages"), "SMTP after SQL messages")
        - require_integer(before_sql.get("messages"), "SMTP before SQL messages")
        == 500
        and require_integer(after_data.get("fileCount"), "SMTP after Data files")
        - require_integer(before_data.get("fileCount"), "SMTP before Data files")
        == 500,
        "SMTP before/after SQL/Data evidence does not reconcile.",
    )
    accepted_states = report.get("acceptedMessageStates")
    require(isinstance(accepted_states, list) and len(accepted_states) == 500, "SMTP accepted-state evidence count mismatch.")
    require(
        all(
            state.get("observed") is True
            and require_integer(state.get("expectedNewMessages"), "SMTP accepted-state sequence") == sequence
            and require_integer(state.get("messages"), "SMTP accepted-state messages") >= sequence
            and require_integer(state.get("queuedMessages"), "SMTP accepted-state queuedMessages")
            + require_integer(state.get("deliveredMessages"), "SMTP accepted-state deliveredMessages")
            >= sequence
            and isinstance(state.get("snapshot"), dict)
            and state["snapshot"].get("available") is True
            and state["snapshot"].get("fixtureValid") is True
            and require_integer(state["snapshot"].get("messages"), "SMTP accepted snapshot messages")
            == before_sql["messages"] + state["messages"]
        for sequence, state in enumerate(accepted_states, start=1)
        ),
        "SMTP accepted-state evidence is incomplete or out of order.",
    )


def git_text(repository: Path, *args: str) -> str:
    return subprocess.check_output(
        ["git", "-C", str(repository), *args],
        text=True,
        encoding="utf-8",
    ).strip()


def summary_row(report: dict[str, Any], scenario: str) -> dict[str, Any]:
    rows = [row for row in report["summary"] if row["scenario"] == scenario]
    require(len(rows) == 1, f"Expected one {scenario} summary row.")
    return rows[0]


def latency_comparison(cpp_value: float, net10_value: float) -> tuple[float, str]:
    require(cpp_value > 0 and net10_value > 0, "Latency values must be positive.")
    ratio = cpp_value / net10_value
    if ratio >= 1:
        return ratio, f"Net10 {ratio:.2f}x faster"
    regression = 1 / ratio
    return ratio, f"Net10 {regression:.2f}x slower"


def throughput_comparison(cpp_value: float, net10_value: float) -> tuple[float, str]:
    require(cpp_value > 0 and net10_value > 0, "Throughput values must be positive.")
    ratio = net10_value / cpp_value
    if ratio >= 1:
        return ratio, f"Net10 {ratio:.2f}x higher"
    regression = 1 / ratio
    return ratio, f"Net10 {regression:.2f}x lower"


def classify_error(sample: dict[str, Any]) -> str:
    if sample.get("ok"):
        return ""
    if sample.get("timedOut") or sample.get("timed_out"):
        return "timeout"
    text = str(sample.get("error") or "").lower()
    if "transport connection" in text or "bağlantı" in text:
        return "transport-read-failure"
    if "address" in text or "yuva adresi" in text:
        return "client-address-exhaustion"
    return "protocol-failure"


def has_reparse_point(path: Path) -> bool:
    current = Path(os.path.abspath(path))
    while True:
        try:
            info = os.lstat(current)
        except FileNotFoundError:
            pass
        else:
            attributes = getattr(info, "st_file_attributes", 0)
            if current.is_symlink() or attributes & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400):
                return True
        if current.parent == current:
            return False
        current = current.parent


def prepare_output(path: Path, repository: Path) -> None:
    path = Path(os.path.abspath(path))
    benchmark_root = Path(os.path.abspath(repository / "artifacts" / "benchmarks"))
    require(not has_reparse_point(path), f"Output directory must not use a symlink or reparse point: {path}")
    require(not has_reparse_point(benchmark_root), f"Approved benchmark root must not use a symlink or reparse point: {benchmark_root}")
    try:
        relative = path.relative_to(benchmark_root)
    except ValueError as error:
        raise ValueError(f"Output directory is not an approved paired benchmark directory: {path}") from error
    require(
        len(relative.parts) == 1 and relative.name.lower().startswith("paired-cpp-net10-") and
        all(character.isalnum() or character in "-_" for character in relative.name),
        f"Output directory is not an approved paired benchmark directory: {path}",
    )
    if path.exists() and not path.is_dir():
        raise ValueError(f"Output directory is not a directory: {path}")
    path.mkdir(parents=True, exist_ok=True)
    for name in GENERATED_FILES:
        target = path / name
        require(
            not target.exists() and not target.is_symlink(),
            f"Refusing to overwrite pre-existing report artifact: {target}",
        )


def apply_chart_style() -> None:
    plt.rcParams.update(
        {
            "figure.facecolor": "white",
            "axes.facecolor": "white",
            "axes.edgecolor": TEXT_COLOR,
            "axes.labelcolor": TEXT_COLOR,
            "axes.titlecolor": TEXT_COLOR,
            "font.size": 10,
            "text.color": TEXT_COLOR,
            "xtick.color": TEXT_COLOR,
            "ytick.color": TEXT_COLOR,
            "axes.grid": True,
            "axes.axisbelow": True,
            "grid.color": GRID_COLOR,
            "grid.linewidth": 0.7,
            "grid.alpha": 0.8,
        }
    )


def label_bars(axis: plt.Axes, bars: Any, suffix: str = "") -> None:
    for bar in bars:
        height = bar.get_height()
        if not math.isfinite(height):
            continue
        axis.annotate(
            f"{height:,.2f}{suffix}",
            (bar.get_x() + bar.get_width() / 2, height),
            xytext=(0, 4),
            textcoords="offset points",
            ha="center",
            va="bottom",
            fontsize=9,
        )


def save_protocol_chart(
    output: Path,
    protocol: dict[str, dict[str, Any]],
) -> None:
    fig, axes = plt.subplots(1, 3, figsize=(13.2, 4.3))
    for axis, scenario in zip(axes, SCENARIOS, strict=True):
        values = [
            float(summary_row(protocol["cpp"], scenario)["p95_ms"]),
            float(summary_row(protocol["net10"], scenario)["p95_ms"]),
        ]
        bars = axis.bar(
            ["Legacy C++", ".NET 10"],
            values,
            color=[CPP_COLOR, NET10_COLOR],
            width=0.62,
        )
        axis.set_title(f"{scenario.upper()} p95 latency")
        axis.set_ylabel("Milliseconds")
        axis.set_ylim(0, max(values) * 1.28)
        label_bars(axis, bars, " ms")
    fig.suptitle(
        "Loopback protocol latency on the paired 1,000-message fixture",
        fontsize=14,
        fontweight="bold",
    )
    fig.text(
        0.5,
        0.01,
        "200 successful iterations per protocol and implementation; lower is better.",
        ha="center",
        fontsize=9,
    )
    fig.tight_layout(rect=(0, 0.05, 1, 0.92))
    fig.savefig(output / "protocol-p95.png", dpi=180, bbox_inches="tight")
    plt.close(fig)


def save_concurrency_chart(
    output: Path,
    concurrent: dict[str, dict[int, dict[str, Any]]],
) -> None:
    fig, axes = plt.subplots(1, 2, figsize=(13.2, 4.7))
    x = np.arange(len(CONCURRENCY_LEVELS))
    width = 0.35
    success_cpp = [
        100.0
        * float(concurrent["cpp"][level]["summary"]["successes"])
        / level
        for level in CONCURRENCY_LEVELS
    ]
    success_net = [
        100.0
        * float(concurrent["net10"][level]["summary"]["successes"])
        / level
        for level in CONCURRENCY_LEVELS
    ]
    bars_cpp = axes[0].bar(x - width / 2, success_cpp, width, color=CPP_COLOR, label="Legacy C++")
    bars_net = axes[0].bar(x + width / 2, success_net, width, color=NET10_COLOR, label=".NET 10")
    axes[0].set_title("Successful sessions")
    axes[0].set_ylabel("Percent")
    axes[0].set_xticks(x, [str(level) for level in CONCURRENCY_LEVELS])
    axes[0].set_xlabel("Concurrent sessions")
    axes[0].set_ylim(0, 112)
    label_bars(axes[0], bars_cpp, "%")
    label_bars(axes[0], bars_net, "%")
    axes[0].legend(frameon=False, loc="lower left")

    cpp_latency: list[float] = []
    net_latency: list[float] = []
    for level in CONCURRENCY_LEVELS:
        cpp_report = concurrent["cpp"][level]
        cpp_latency.append(
            float(cpp_report["summary"]["p95_ms"])
            if cpp_report["status"] == "PASS"
            else math.nan
        )
        net_latency.append(float(concurrent["net10"][level]["summary"]["p95_ms"]))
    bars_cpp = axes[1].bar(x - width / 2, cpp_latency, width, color=CPP_COLOR, label="Legacy C++")
    bars_net = axes[1].bar(x + width / 2, net_latency, width, color=NET10_COLOR, label=".NET 10")
    axes[1].set_title("p95 session latency")
    axes[1].set_ylabel("Milliseconds")
    axes[1].set_xticks(x, [str(level) for level in CONCURRENCY_LEVELS])
    axes[1].set_xlabel("Concurrent sessions")
    accepted_values = [value for value in cpp_latency + net_latency if math.isfinite(value)]
    axes[1].set_ylim(0, max(accepted_values) * 1.24)
    label_bars(axes[1], bars_cpp, " ms")
    label_bars(axes[1], bars_net, " ms")
    failed = concurrent["cpp"][1000]["summary"]
    if concurrent["cpp"][1000]["status"] != "PASS":
        axes[1].annotate(
            f"FAIL\n{failed['successes']}/1000",
            (x[2] - width / 2, max(accepted_values) * 0.16),
            ha="center",
            va="center",
            color=FAIL_COLOR,
            fontweight="bold",
        )
    axes[1].legend(frameon=False, loc="upper left")

    fig.suptitle(
        "Concurrent IMAP SEARCH/SORT acceptance",
        fontsize=14,
        fontweight="bold",
    )
    fig.text(
        0.5,
        0.01,
        "One synchronized wave per level, 30 s socket-operation timeout, 5 s settled resource snapshot.",
        ha="center",
        fontsize=9,
    )
    fig.tight_layout(rect=(0, 0.05, 1, 0.92))
    fig.savefig(output / "imap-concurrency.png", dpi=180, bbox_inches="tight")
    plt.close(fig)


def save_smtp_chart(
    output: Path,
    smtp: dict[str, dict[str, Any]],
) -> None:
    fig, axes = plt.subplots(1, 2, figsize=(10.8, 4.4))
    p95 = [float(smtp["cpp"]["p95_ms"]), float(smtp["net10"]["p95_ms"])]
    throughput = [
        float(smtp["cpp"]["throughput_messages_per_second"]),
        float(smtp["net10"]["throughput_messages_per_second"]),
    ]
    bars = axes[0].bar(["Legacy C++", ".NET 10"], p95, color=[CPP_COLOR, NET10_COLOR], width=0.62)
    axes[0].set_title("SMTP acceptance p95")
    axes[0].set_ylabel("Milliseconds")
    axes[0].set_ylim(0, max(p95) * 1.28)
    label_bars(axes[0], bars, " ms")
    bars = axes[1].bar(
        ["Legacy C++", ".NET 10"],
        throughput,
        color=[CPP_COLOR, NET10_COLOR],
        width=0.62,
    )
    axes[1].set_title("Durable acceptance throughput")
    axes[1].set_ylabel("Messages/second")
    axes[1].set_ylim(0, max(throughput) * 1.28)
    label_bars(axes[1], bars, "/s")
    fig.suptitle(
        "500-message SMTP acceptance with SQL/Data accounting",
        fontsize=14,
        fontweight="bold",
    )
    fig.text(
        0.5,
        0.01,
        "Every accepted message was observed in the isolated SQL/Data state; lower latency and higher throughput are better.",
        ha="center",
        fontsize=9,
    )
    fig.tight_layout(rect=(0, 0.05, 1, 0.91))
    fig.savefig(output / "smtp-acceptance.png", dpi=180, bbox_inches="tight")
    plt.close(fig)


def save_soak_chart(output: Path, soak: dict[str, Any]) -> None:
    wave_rows = soak["waveMetrics"]
    waves = [0] + [int(row["wave"]) for row in wave_rows]
    memory = [float(soak["processBefore"]["privateBytes"]) / 1024 / 1024] + [
        float(row["processAfterSettle"]["privateBytes"]) / 1024 / 1024
        for row in wave_rows
    ]
    handles = [int(soak["processBefore"]["handles"])] + [
        int(row["processAfterSettle"]["handles"]) for row in wave_rows
    ]
    threads = [int(soak["processBefore"]["threads"])] + [
        int(row["processAfterSettle"]["threads"]) for row in wave_rows
    ]
    fig, axes = plt.subplots(1, 3, figsize=(13.2, 4.4))
    for axis, values, title, ylabel in (
        (axes[0], memory, "Private memory", "MiB"),
        (axes[1], handles, "Process handles", "Count"),
        (axes[2], threads, "Threads", "Count"),
    ):
        axis.plot(waves, values, color=NET10_COLOR, marker="o", linewidth=2, markersize=3.5)
        axis.set_title(title)
        axis.set_xlabel("1,000-session wave")
        axis.set_ylabel(ylabel)
        axis.set_xlim(0, max(waves))
        axis.set_ylim(0, max(values) * 1.18)
        axis.annotate(f"{values[0]:.1f}", (waves[0], values[0]), xytext=(3, 6), textcoords="offset points")
        axis.annotate(f"{values[-1]:.1f}", (waves[-1], values[-1]), xytext=(-2, 6), textcoords="offset points", ha="right")
    fig.suptitle(
        ".NET 10 short soak: 20 waves / 20,000 successful IMAP sessions",
        fontsize=14,
        fontweight="bold",
    )
    fig.text(
        0.5,
        0.01,
        "Settled snapshot after each wave; this is short-soak evidence, not the mandatory 24-hour leak gate.",
        ha="center",
        fontsize=9,
    )
    fig.tight_layout(rect=(0, 0.05, 1, 0.92))
    fig.savefig(output / "net10-imap-soak-resources.png", dpi=180, bbox_inches="tight")
    plt.close(fig)


def write_csv(path: Path, fieldnames: list[str], rows: list[dict[str, Any]]) -> None:
    with path.open("w", newline="", encoding="utf-8") as stream:
        writer = csv.DictWriter(stream, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)


def main() -> None:
    args = parse_args()
    input_root = args.input_root.resolve()
    repository = Path(os.path.abspath(args.repository_root))
    output = Path(os.path.abspath(args.output_directory))
    prepare_output(output, repository)

    fixture = load_json(args.fixture_manifest.resolve())
    environment = load_json(args.environment.resolve())
    legacy_build = load_json(args.legacy_build_manifest.resolve())
    descriptor_path = args.run_descriptor.resolve()
    descriptor = load_json(descriptor_path)
    require(fixture.get("schema") == "paired-benchmark-fixture-v2", "Unexpected fixture schema.")
    require(fixture.get("status") == "PASS", "Fixture preparation did not pass.")
    require(fixture["dataParity"]["exact"] is True, "Data copies are not exact.")
    require(fixture["messageParity"]["exact"] is True, "Logical message projections differ.")
    require(int(fixture["cppDatabaseVersion"]) == 5708, "Legacy database is not version 5708.")
    require(int(fixture["net10DatabaseVersion"]) == 6000, "Net10 database is not version 6000.")
    require(legacy_build.get("status") == "PASS", "Legacy Release build did not pass.")
    require(legacy_build.get("postBuildRegistrationDisabled") is True, "Legacy build allowed post-build registration.")
    cpp_executable, net10_executable = validate_fixture_executables(
        fixture, args.net10_executable
    )

    protocol = {
        implementation: load_json(input_root / f"protocol-{implementation}" / "net10-live-protocol.json")
        for implementation in ("cpp", "net10")
    }
    concurrent = {
        implementation: {
            level: load_json(
                input_root
                / f"concurrent-{implementation}-{level}"
                / "live-concurrent-imap.json"
            )
            for level in CONCURRENCY_LEVELS
        }
        for implementation in ("cpp", "net10")
    }
    smtp = {
        implementation: load_json(
            input_root
            / f"smtp-{implementation}-500"
            / f"{implementation}-smtp-message-acceptance.json"
        )
        for implementation in ("cpp", "net10")
    }
    soak = load_json(input_root / "soak-net10-1000x20" / "live-concurrent-imap.json")

    descriptor_run_id, descriptor_sha256, descriptor_slots = validate_run_descriptor(
        descriptor,
        descriptor_path,
        input_root,
        fixture,
        args.fixture_manifest.resolve(),
    )
    descriptor_reports = {
        "protocol-cpp": protocol["cpp"],
        "protocol-net10": protocol["net10"],
        **{
            f"concurrent-{implementation}-{level}": report
            for implementation, reports in concurrent.items()
            for level, report in reports.items()
        },
        "smtp-cpp-500": smtp["cpp"],
        "smtp-net10-500": smtp["net10"],
        "soak-net10-1000x20": soak,
    }
    validate_run_descriptor_reports(descriptor_run_id, descriptor_slots, descriptor_reports)

    paired_run_id = validate_report_set(
        [
            *((implementation, report) for implementation, report in protocol.items()),
            *(
                (implementation, report)
                for implementation, reports in concurrent.items()
                for report in reports.values()
            ),
            *((implementation, report) for implementation, report in smtp.items()),
            ("net10", soak),
        ],
        fixture,
        args.fixture_manifest.resolve(),
    )
    require(paired_run_id == descriptor_run_id, "Paired report runId does not match the run descriptor.")

    for report in protocol.values():
        validate_protocol_workload(report)
    for implementation, reports in concurrent.items():
        for level, report in reports.items():
            validate_concurrent_workload(
                report,
                level,
                1,
                require_pass=not (implementation == "cpp" and level == 1000),
            )
    for report in smtp.values():
        validate_smtp_workload(report)
    validate_concurrent_workload(soak, 1000, 20, require_pass=True)

    for implementation, report in protocol.items():
        require(report.get("schema") == "live-protocol-v1", "Unexpected protocol schema.")
        require(report.get("implementation") == implementation, "Protocol implementation mismatch.")
        require(report.get("status") == "PASS", f"{implementation} protocol run did not pass.")
        require(sum(int(row["errors"]) for row in report["summary"]) == 0, "Protocol errors are present.")
    for implementation, reports in concurrent.items():
        for level, report in reports.items():
            require(report.get("implementation") == implementation, "Concurrent implementation mismatch.")
            require(int(report.get("concurrency")) == level, "Concurrent level mismatch.")
            require(int(report["summary"]["completed"]) == level, "Concurrent sample count mismatch.")
    for implementation, report in smtp.items():
        require(report.get("implementation") == implementation, "SMTP implementation mismatch.")
        require(report.get("status") == "PASS", f"{implementation} SMTP acceptance did not pass.")
        require(int(report["acceptedMessages"]) == 500, "SMTP acceptance is incomplete.")
        require(report["postRunAccounting"]["valid"] is True, "SMTP SQL/Data accounting failed.")
    require(concurrent["net10"][1000]["status"] == "PASS", "Net10 1,000-session acceptance did not pass.")
    require(soak.get("status") == "PASS", "Net10 short soak did not pass.")
    require(int(soak.get("waves", 0)) == 20, "Expected a 20-wave short soak.")
    require(int(soak["summary"]["successes"]) == 20000, "Short-soak success count is incomplete.")
    require(int(soak["summary"]["errors"]) == 0, "Short-soak errors are present.")

    repository_head = git_text(repository, "rev-parse", "HEAD")
    tested_commit = environment["gitCommit"]
    branch = git_text(repository, "branch", "--show-current")
    subprocess.check_call(
        ["git", "-C", str(repository), "cat-file", "-e", f"{tested_commit}^{{commit}}"],
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )
    require(tested_commit == legacy_build["sourceCommit"], "Legacy build commit does not match the tested commit.")
    cpp_hash = legacy_build["executableSha256"].upper()
    require(cpp_hash == fixture["cppExecutableSha256"].upper(), "Fixture C++ executable hash mismatch.")
    net10_hash = sha256(net10_executable)

    protocol_rows: list[dict[str, Any]] = []
    for implementation, report in protocol.items():
        for sample in report["samples"]:
            protocol_rows.append(
                {
                    "implementation": implementation,
                    "scenario": sample["scenario"],
                    "iteration": sample["iteration"],
                    "ok": str(bool(sample["ok"])).lower(),
                    "milliseconds": sample["ms"],
                    "error_class": classify_error(sample),
                }
            )
    write_csv(
        output / "protocol-samples.csv",
        ["implementation", "scenario", "iteration", "ok", "milliseconds", "error_class"],
        protocol_rows,
    )

    concurrent_sample_rows: list[dict[str, Any]] = []
    for implementation, reports in concurrent.items():
        for level, report in reports.items():
            for sample in report["samples"]:
                concurrent_sample_rows.append(
                    {
                        "implementation": implementation,
                        "concurrency": level,
                        "wave": sample.get("wave", 1),
                        "ok": str(bool(sample["ok"])).lower(),
                        "timed_out": str(bool(sample["timedOut"])).lower(),
                        "milliseconds": sample["ms"],
                        "error_class": classify_error(sample),
                    }
                )
    write_csv(
        output / "concurrent-imap-samples.csv",
        ["implementation", "concurrency", "wave", "ok", "timed_out", "milliseconds", "error_class"],
        concurrent_sample_rows,
    )

    smtp_sample_rows: list[dict[str, Any]] = []
    for implementation, report in smtp.items():
        for sample in report["samples"]:
            smtp_sample_rows.append(
                {
                    "implementation": implementation,
                    "sequence": sample["sequence"],
                    "ok": str(bool(sample["ok"])).lower(),
                    "milliseconds": sample["ms"],
                    "error_class": classify_error(sample),
                }
            )
    write_csv(
        output / "smtp-acceptance-samples.csv",
        ["implementation", "sequence", "ok", "milliseconds", "error_class"],
        smtp_sample_rows,
    )

    soak_rows: list[dict[str, Any]] = []
    for row in soak["waveMetrics"]:
        soak_rows.append(
            {
                "wave": row["wave"],
                "successes": row["successes"],
                "errors": row["errors"],
                "workload_seconds": row["workloadSeconds"],
                "private_memory_mib": round(
                    float(row["processAfterSettle"]["privateBytes"]) / 1024 / 1024,
                    3,
                ),
                "handles": row["processAfterSettle"]["handles"],
                "threads": row["processAfterSettle"]["threads"],
            }
        )
    write_csv(
        output / "net10-imap-soak-waves.csv",
        ["wave", "successes", "errors", "workload_seconds", "private_memory_mib", "handles", "threads"],
        soak_rows,
    )

    metric_rows: list[dict[str, Any]] = []
    protocol_findings: list[dict[str, Any]] = []
    for scenario in SCENARIOS:
        cpp_row = summary_row(protocol["cpp"], scenario)
        net_row = summary_row(protocol["net10"], scenario)
        ratio, comparison = latency_comparison(
            float(cpp_row["p95_ms"]),
            float(net_row["p95_ms"]),
        )
        protocol_findings.append(
            {
                "scenario": scenario,
                "cppP95Ms": cpp_row["p95_ms"],
                "net10P95Ms": net_row["p95_ms"],
                "cppOverNet10Ratio": round(ratio, 6),
                "comparison": comparison,
            }
        )
        metric_rows.append(
            {
                "category": "protocol",
                "scenario": scenario,
                "load": 200,
                "metric": "p95_latency",
                "unit": "ms",
                "cpp_value": cpp_row["p95_ms"],
                "cpp_status": protocol["cpp"]["status"],
                "net10_value": net_row["p95_ms"],
                "net10_status": protocol["net10"]["status"],
                "net10_over_cpp_ratio": round(float(net_row["p95_ms"]) / float(cpp_row["p95_ms"]), 6),
                "comparison": comparison,
            }
        )

    concurrent_findings: list[dict[str, Any]] = []
    for level in CONCURRENCY_LEVELS:
        cpp_report = concurrent["cpp"][level]
        net_report = concurrent["net10"][level]
        comparison = "N/A: one or both acceptance artifacts failed"
        ratio: float | None = None
        if cpp_report["status"] == "PASS" and net_report["status"] == "PASS":
            ratio, comparison = latency_comparison(
                float(cpp_report["summary"]["p95_ms"]),
                float(net_report["summary"]["p95_ms"]),
            )
        concurrent_findings.append(
            {
                "concurrency": level,
                "cppStatus": cpp_report["status"],
                "cppSuccesses": cpp_report["summary"]["successes"],
                "cppP95Ms": cpp_report["summary"]["p95_ms"],
                "net10Status": net_report["status"],
                "net10Successes": net_report["summary"]["successes"],
                "net10P95Ms": net_report["summary"]["p95_ms"],
                "cppOverNet10Ratio": None if ratio is None else round(ratio, 6),
                "comparison": comparison,
            }
        )
        metric_rows.append(
            {
                "category": "concurrent_imap",
                "scenario": "search_sort",
                "load": level,
                "metric": "p95_session_latency",
                "unit": "ms",
                "cpp_value": cpp_report["summary"]["p95_ms"],
                "cpp_status": cpp_report["status"],
                "net10_value": net_report["summary"]["p95_ms"],
                "net10_status": net_report["status"],
                "net10_over_cpp_ratio": (
                    "" if ratio is None else round(float(net_report["summary"]["p95_ms"]) / float(cpp_report["summary"]["p95_ms"]), 6)
                ),
                "comparison": comparison,
            }
        )

    smtp_latency_ratio, smtp_latency_text = latency_comparison(
        float(smtp["cpp"]["p95_ms"]),
        float(smtp["net10"]["p95_ms"]),
    )
    smtp_throughput_ratio, smtp_throughput_text = throughput_comparison(
        float(smtp["cpp"]["throughput_messages_per_second"]),
        float(smtp["net10"]["throughput_messages_per_second"]),
    )
    metric_rows.extend(
        [
            {
                "category": "smtp_acceptance",
                "scenario": "500_messages",
                "load": 500,
                "metric": "p95_latency",
                "unit": "ms",
                "cpp_value": smtp["cpp"]["p95_ms"],
                "cpp_status": smtp["cpp"]["status"],
                "net10_value": smtp["net10"]["p95_ms"],
                "net10_status": smtp["net10"]["status"],
                "net10_over_cpp_ratio": round(float(smtp["net10"]["p95_ms"]) / float(smtp["cpp"]["p95_ms"]), 6),
                "comparison": smtp_latency_text,
            },
            {
                "category": "smtp_acceptance",
                "scenario": "500_messages",
                "load": 500,
                "metric": "durable_throughput",
                "unit": "messages/s",
                "cpp_value": smtp["cpp"]["throughput_messages_per_second"],
                "cpp_status": smtp["cpp"]["status"],
                "net10_value": smtp["net10"]["throughput_messages_per_second"],
                "net10_status": smtp["net10"]["status"],
                "net10_over_cpp_ratio": round(smtp_throughput_ratio, 6),
                "comparison": smtp_throughput_text,
            },
        ]
    )
    write_csv(
        output / "performance-summary.csv",
        [
            "category",
            "scenario",
            "load",
            "metric",
            "unit",
            "cpp_value",
            "cpp_status",
            "net10_value",
            "net10_status",
            "net10_over_cpp_ratio",
            "comparison",
        ],
        metric_rows,
    )

    cpp_1000_passed = concurrent["cpp"][1000]["status"] == "PASS"
    thousand_finding = next(row for row in concurrent_findings if row["concurrency"] == 1000)
    if cpp_1000_passed:
        thousand_summary = (
            "Both implementations passed 1,000/1,000 concurrent IMAP sessions; "
            f"{thousand_finding['comparison']}."
        )
        thousand_limitation = (
            "Both implementations passed the 1,000-session wave; the published ratio is "
            "limited to this host, fixture, and loopback methodology."
        )
    else:
        thousand_summary = (
            "Net10 passed 1,000/1,000 concurrent IMAP sessions; legacy C++ completed only "
            f"{concurrent['cpp'][1000]['summary']['successes']}/1000 and failed that gate."
        )
        thousand_limitation = (
            "The legacy C++ 1,000-session wave failed, so no 1,000-session latency or "
            "throughput ratio is valid."
        )

    performance = {
        "schema": "paired-cpp-net10-performance-v1",
        "gate": "RED",
        "gateReason": (
            "The paired protocol/load slice is complete, but mandatory 24-hour soak, "
            "remote delivery, TLS/network, queue, restore, and installer lifecycle gates remain open."
        ),
        "source": {
            "branch": branch,
            "commit": tested_commit,
            "repositoryHeadAtGeneration": repository_head,
            "runId": paired_run_id,
            "runDescriptorSha256": descriptor_sha256,
            "runDescriptorStatus": descriptor["status"],
            "runDescriptorArtifacts": {
                name: {
                    "implementation": slot["implementation"],
                    "jsonSha256": slot["artifacts"]["json"],
                    "csvSha256": slot["artifacts"]["csv"],
                    "markdownSha256": slot["artifacts"]["markdown"],
                }
                for name, slot in descriptor_slots.items()
            },
            "fixtureId": fixture_id(fixture, args.fixture_manifest.resolve()),
            "manifestSha256": sha256(args.fixture_manifest.resolve()),
            "cpp": {
                "configuration": legacy_build["configuration"],
                "platform": legacy_build["platform"],
                "sha256": cpp_hash,
                "bytes": legacy_build["executableBytes"],
                "postBuildRegistrationDisabled": True,
            },
            "net10": {
                "configuration": "Release",
                "platform": "x64",
                "sha256": net10_hash,
                "bytes": args.net10_executable.stat().st_size,
                "sdk": environment["dotnetSdk"],
            },
        },
        "environment": {
            key: environment[key]
            for key in (
                "capturedUtc",
                "osCaption",
                "osVersion",
                "osBuild",
                "osArchitecture",
                "cpu",
                "physicalCores",
                "logicalProcessors",
                "memoryGb",
                "sqlServer",
                "loopback",
                "ports",
            )
        }
        | {"powerPlan": "Balanced (381b4222-f694-41f0-9685-ff5bb260df2e)"},
        "fixture": {
            "cppDatabaseVersion": fixture["cppDatabaseVersion"],
            "net10DatabaseVersion": fixture["net10DatabaseVersion"],
            "backupSha256": fixture["backupSha256"],
            "upgradeScriptSha256": fixture["upgradeScriptSha256"],
            "dataParity": fixture["dataParity"],
            "messageParity": fixture["messageParity"],
        },
        "protocol": {
            "iterationsPerScenario": 200,
            "findings": protocol_findings,
        },
        "concurrentImap": {
            "settleSeconds": 5,
            "timeoutMilliseconds": 30000,
            "findings": concurrent_findings,
        },
        "smtpAcceptance": {
            "messages": 500,
            "cpp": {
                "accepted": smtp["cpp"]["acceptedMessages"],
                "p50Ms": smtp["cpp"]["p50_ms"],
                "p95Ms": smtp["cpp"]["p95_ms"],
                "p99Ms": smtp["cpp"]["p99_ms"],
                "throughputMessagesPerSecond": smtp["cpp"]["throughput_messages_per_second"],
                "messageRowDelta": smtp["cpp"]["postRunAccounting"]["messageRowDelta"],
                "dataFileDelta": smtp["cpp"]["postRunAccounting"]["dataFileDelta"],
            },
            "net10": {
                "accepted": smtp["net10"]["acceptedMessages"],
                "p50Ms": smtp["net10"]["p50_ms"],
                "p95Ms": smtp["net10"]["p95_ms"],
                "p99Ms": smtp["net10"]["p99_ms"],
                "throughputMessagesPerSecond": smtp["net10"]["throughput_messages_per_second"],
                "messageRowDelta": smtp["net10"]["postRunAccounting"]["messageRowDelta"],
                "dataFileDelta": smtp["net10"]["postRunAccounting"]["dataFileDelta"],
            },
            "p95LatencyCppOverNet10Ratio": round(smtp_latency_ratio, 6),
            "throughputNet10OverCppRatio": round(smtp_throughput_ratio, 6),
        },
        "net10ShortSoak": {
            "status": soak["status"],
            "waves": soak["waves"],
            "sessions": soak["summary"]["successes"],
            "errors": soak["summary"]["errors"],
            "p50Ms": soak["summary"]["p50_ms"],
            "p95Ms": soak["summary"]["p95_ms"],
            "p99Ms": soak["summary"]["p99_ms"],
            "baseline": soak["processBefore"],
            "finalSettled": soak["processAfter"],
            "firstWaveSettled": soak["waveMetrics"][0]["processAfterSettle"],
        },
        "limitations": [
            "C++ requires schema 5708 while Net10 requires schema 6000; the logical message projection and Data bytes are exact, not the physical database files.",
            "Runs were sequential on one Windows 11 laptop with the Balanced power plan and loopback networking.",
            thousand_limitation,
            "The 20,000-session Net10 run is a short soak and does not replace the mandatory 24-hour leak test.",
            "Remote DNS/TLS delivery, queue throughput, POP3 soak, external fetch, restore timing, installer/service lifecycle, and SQL wait baselines remain open.",
        ],
    }
    with (output / "performance-summary.json").open("w", encoding="utf-8") as stream:
        json.dump(performance, stream, indent=2, ensure_ascii=True)
        stream.write("\n")

    apply_chart_style()
    save_protocol_chart(output, protocol)
    save_concurrency_chart(output, concurrent)
    save_smtp_chart(output, smtp)
    save_soak_chart(output, soak)

    protocol_lines = []
    for finding in protocol_findings:
        protocol_lines.append(
            f"| {finding['scenario'].upper()} | {finding['cppP95Ms']:.2f} | "
            f"{finding['net10P95Ms']:.2f} | {finding['comparison']} |"
        )
    concurrent_lines = []
    for finding in concurrent_findings:
        ratio_text = finding["comparison"]
        concurrent_lines.append(
            f"| {finding['concurrency']} | {finding['cppStatus']} "
            f"({finding['cppSuccesses']}/{finding['concurrency']}) | "
            f"{finding['cppP95Ms']:.2f} | {finding['net10Status']} "
            f"({finding['net10Successes']}/{finding['concurrency']}) | "
            f"{finding['net10P95Ms']:.2f} | {ratio_text} |"
        )
    first_soak = soak_rows[0]
    last_soak = soak_rows[-1]
    report_lines = [
        "# Legacy C++ vs .NET 10 Performance Comparison",
        "",
        f"**Tested source commit:** `{tested_commit}`  ",
        "**Decision:** paired loopback protocol/load evidence is complete; the overall performance release gate remains **RED**.",
        "",
        "## Technical Summary",
        "",
        "A clean, repository-built legacy C++ Release binary and the .NET 10 Release binary were run sequentially on the same host and SQL Server instance. Both used the same 1,000-message logical corpus, byte-identical Data copies, loopback bindings, accounts, credentials, and protocol commands. The legacy runtime correctly remained on database schema 5708; only the Net10 copy was upgraded to schema 6000.",
        "",
        f"Net10 reduced p95 SMTP command latency by `{protocol_findings[0]['cppOverNet10Ratio']:.2f}x` and p95 IMAP SEARCH/SORT latency by `{protocol_findings[1]['cppOverNet10Ratio']:.2f}x`. POP3 p95 regressed by `{1 / protocol_findings[2]['cppOverNet10Ratio']:.2f}x`. {thousand_summary}",
        "",
        f"Both implementations durably accepted 500/500 SMTP messages with exact +500 SQL rows and +500 Data files. Net10 p95 acceptance latency was `{smtp['net10']['p95_ms']:.2f} ms` versus `{smtp['cpp']['p95_ms']:.2f} ms`; end-to-end durable throughput was effectively tied (`{smtp['net10']['throughput_messages_per_second']:.2f}` vs `{smtp['cpp']['throughput_messages_per_second']:.2f}` messages/s).",
        "",
        "## Key Findings",
        "",
        "![Protocol p95 latency](protocol-p95.png)",
        "",
        "| Scenario | Legacy C++ p95 ms | .NET 10 p95 ms | Comparison |",
        "| --- | ---: | ---: | --- |",
        *protocol_lines,
        "",
        "![Concurrent IMAP results](imap-concurrency.png)",
        "",
        "| Concurrent sessions | Legacy C++ | C++ p95 ms | .NET 10 | Net10 p95 ms | Valid comparison |",
        "| ---: | --- | ---: | --- | ---: | --- |",
        *concurrent_lines,
        "",
        "![SMTP acceptance](smtp-acceptance.png)",
        "",
        "| Metric | Legacy C++ | .NET 10 | Result |",
        "| --- | ---: | ---: | --- |",
        f"| Accepted messages | {smtp['cpp']['acceptedMessages']}/500 | {smtp['net10']['acceptedMessages']}/500 | Both PASS |",
        f"| p95 latency | {smtp['cpp']['p95_ms']:.2f} ms | {smtp['net10']['p95_ms']:.2f} ms | {smtp_latency_text} |",
        f"| Durable throughput | {smtp['cpp']['throughput_messages_per_second']:.3f}/s | {smtp['net10']['throughput_messages_per_second']:.3f}/s | {smtp_throughput_text} |",
        f"| SQL/Data delta | +{smtp['cpp']['postRunAccounting']['messageRowDelta']} / +{smtp['cpp']['postRunAccounting']['dataFileDelta']} | +{smtp['net10']['postRunAccounting']['messageRowDelta']} / +{smtp['net10']['postRunAccounting']['dataFileDelta']} | Exact accounting |",
        "",
        "![Net10 short-soak resources](net10-imap-soak-resources.png)",
        "",
        f"The short soak completed `{soak['summary']['successes']:,}/{soak['summary']['successes']:,}` sessions with zero errors across 20 waves. Settled private memory moved from `{first_soak['private_memory_mib']:.2f} MiB` after wave 1 to `{last_soak['private_memory_mib']:.2f} MiB` after wave 20. Handles moved from `{first_soak['handles']}` to `{last_soak['handles']}`; only a 24-hour service soak can determine whether long-run resource growth stays bounded.",
        "",
        "## Scope And Fixture",
        "",
        "| Item | Evidence |",
        "| --- | --- |",
        f"| Legacy build | Release x64, SHA-256 `{cpp_hash}`, post-build registration disabled |",
        f"| Net10 build | Release x64, SDK {environment['dotnetSdk']}, SHA-256 `{net10_hash}` |",
        f"| SQL versions | C++ `{fixture['cppDatabaseVersion']}`; Net10 `{fixture['net10DatabaseVersion']}` |",
        f"| Logical message parity | {fixture['messageParity']['rowCount']} rows, SHA-256 `{fixture['messageParity']['sha256']}` |",
        f"| Data parity | {fixture['dataParity']['fileCount']} files / {int(fixture['dataParity']['bytes'])} bytes, SHA-256 `{fixture['dataParity']['sha256']}` |",
        f"| Host | {environment['osCaption']} build {environment['osBuild']}; {environment['cpu']}; {environment['memoryGb']} GiB RAM |",
        f"| SQL Server | {environment['sqlServer']} |",
        "| Network | 127.0.0.1; SMTP 2525, IMAP 1143, POP3 25110 |",
        "| Power plan | Balanced |",
        "",
        "## Methodology",
        "",
        "1. Build legacy C++ and Net10 from the same Git commit in Release x64.",
        "2. Restore the same legacy 5708 backup twice; retain 5708 for C++ and run only the checked-in 5708-to-6000 upgrade against Net10.",
        "3. Rewrite only disposable message paths and prove exact Data and normalized SQL message fingerprints.",
        "4. Run each implementation separately on the same loopback ports. Protocol tests execute SMTP EHLO/QUIT, authenticated IMAP SELECT/SEARCH TEXT/SORT/LOGOUT, and authenticated POP3 STAT/LIST/QUIT.",
        "5. Run synchronized IMAP waves at 100, 500, and 1,000 sessions with a 30-second socket-operation timeout and five-second settled process snapshots.",
        "6. Run 500 SMTP deliveries last and require accepted-message visibility plus SQL/Data before/after accounting.",
        "7. Run a Net10-only 20-wave, 20,000-session short soak in one process and record settled memory, handles, and threads after every wave.",
        "",
        "## Limitations And Gate Decision",
        "",
        "- **Overall performance release gate: RED.** This report closes the previously missing clean legacy startup and paired loopback comparison, not the entire release benchmark matrix.",
        "- C++ requires schema 5708 and Net10 requires 6000. Logical message rows and Data bytes are exact; physical SQL files are intentionally not identical.",
        "- Measurements are from one laptop, one SQL Server instance, Balanced power mode, and loopback networking. They are not a capacity-planning baseline.",
        f"- {thousand_limitation}",
        "- POP3 is materially slower in Net10 and remains an optimization target.",
        "- The 20,000-session run is short-soak evidence only. Mandatory 24-hour memory/handle/thread/socket soak remains open.",
        "- Remote SMTP delivery/retry, delivery queue throughput, TLS/DNS, external fetch, rules/scripting, backup/restore timing, and installer/service/COM lifecycle benchmarks remain open.",
        "",
        "## Reproduction",
        "",
        "The commandable harness is in `build/build-disposable-legacy-server.ps1`, `build/provision-paired-benchmark-fixture.ps1`, `build/benchmark-net10-live-protocol.ps1`, `build/benchmark-net10-live-concurrent-imap.ps1`, and `build/benchmark-net10-live-smtp-acceptance.ps1`. Each script refuses non-disposable database/root names and leaves production COM/DCOM/service state untouched.",
        "",
        "Machine-readable summaries and sanitized samples are stored beside this report. The original local fixture paths are intentionally omitted from committed artifacts.",
    ]
    (output / "PERFORMANCE_COMPARISON.md").write_text(
        "\n".join(report_lines) + "\n",
        encoding="utf-8",
    )
    print("Generated paired performance report.")


if __name__ == "__main__":
    main()
