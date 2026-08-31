#!/usr/bin/env python3
"""Generate a sanitized C++/.NET 10 diagnostic comparison.

Unlike the release report generator, this report accepts failed load levels. It
publishes ratios only when both sides passed the same scenario.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
from pathlib import Path
from typing import Any

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input-root", type=Path, required=True)
    parser.add_argument("--fixture-manifest", type=Path, required=True)
    parser.add_argument("--legacy-build-manifest", type=Path, required=True)
    parser.add_argument("--repository-root", type=Path, required=True)
    parser.add_argument("--output-directory", type=Path, required=True)
    return parser.parse_args()


def load(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8-sig") as stream:
        value = json.load(stream)
    if not isinstance(value, dict):
        raise ValueError(f"Expected JSON object: {path}")
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


def report(root: Path, relative: str) -> dict[str, Any]:
    path = root / relative
    require(path.is_file(), f"Missing benchmark artifact: {relative}")
    return load(path)


def common_attestation(reports: list[dict[str, Any]], fixture: dict[str, Any], manifest_hash: str) -> None:
    fixture_id = fixture.get("fixtureId") or Path(fixture["outputRoot"]).name
    run_ids = set()
    for item in reports:
        require(item.get("fixtureId") == fixture_id, "Mixed fixture IDs in diagnostic inputs.")
        require(str(item.get("manifestSha256", "")).upper() == str(manifest_hash).upper(), "Mixed fixture manifests.")
        run_ids.add(str(item.get("runId")))
    require(len(run_ids) == 1 and "None" not in run_ids, "Diagnostic inputs must share one run ID.")


def status_text(item: dict[str, Any]) -> str:
    summary = item.get("summary", {})
    requested = summary.get("requested", item.get("requestedMessages", 0))
    successes = summary.get("successes", item.get("acceptedMessages", 0))
    return f"{successes}/{requested}"


def ratio(cpp: float | None, net10: float | None, both_passed: bool) -> float | None:
    if not both_passed or cpp is None or net10 is None or net10 <= 0:
        return None
    return round(cpp / net10, 6)


def main() -> None:
    args = parse_args()
    root = args.input_root.resolve()
    fixture = load(args.fixture_manifest.resolve())
    build = load(args.legacy_build_manifest.resolve())
    require(fixture.get("status") == "PASS", "Fixture manifest is not PASS.")
    require(build.get("status") == "PASS", "Legacy build manifest is not PASS.")
    require(build.get("sourceCommit") == "b00eb7e5231908cef94a281e639e3b2d35bf76ca", "Unexpected legacy source commit.")
    protocol = {
        name: {
            impl: report(root, f"protocol-{impl}/net10-live-protocol.json")
            for impl in ("cpp", "net10")
        }
        for name in ("smtp", "imap", "pop3")
    }
    concurrent = {
        level: {
            impl: report(root, f"concurrent-{impl}-{level}/live-concurrent-imap.json")
            for impl in ("cpp", "net10")
        }
        for level in (100, 500, 1000)
    }
    smtp = {
        impl: report(root, f"smtp-{impl}-500/{impl}-smtp-message-acceptance.json")
        for impl in ("cpp", "net10")
    }
    all_reports = [*sum(([item[impl] for impl in ("cpp", "net10")] for item in protocol.values()), []),
                   *sum(([item[impl] for impl in ("cpp", "net10")] for item in concurrent.values()), []),
                   *smtp.values()]
    manifest_hash = sha256(args.fixture_manifest.resolve())
    common_attestation(all_reports, fixture, manifest_hash)

    output = args.output_directory.resolve()
    output.mkdir(parents=True, exist_ok=True)
    protocol_rows: list[dict[str, Any]] = []
    for scenario, pair in protocol.items():
        cpp = next(row for row in pair["cpp"]["summary"] if row["scenario"] == scenario)
        net = next(row for row in pair["net10"]["summary"] if row["scenario"] == scenario)
        passed = cpp["errors"] == 0 and net["errors"] == 0
        protocol_rows.append({
            "scenario": scenario,
            "cppStatus": pair["cpp"]["status"], "cppSuccesses": cpp["successes"], "cppErrors": cpp["errors"],
            "cppP50Ms": cpp["p50_ms"], "cppP95Ms": cpp["p95_ms"], "cppP99Ms": cpp["p99_ms"],
            "net10Status": pair["net10"]["status"], "net10Successes": net["successes"], "net10Errors": net["errors"],
            "net10P50Ms": net["p50_ms"], "net10P95Ms": net["p95_ms"], "net10P99Ms": net["p99_ms"],
            "cppOverNet10P95Ratio": ratio(cpp["p95_ms"], net["p95_ms"], passed),
        })
    concurrent_rows: list[dict[str, Any]] = []
    for level, pair in concurrent.items():
        cpp = pair["cpp"]["summary"]
        net = pair["net10"]["summary"]
        passed = pair["cpp"]["status"] == "PASS" and pair["net10"]["status"] == "PASS"
        concurrent_rows.append({
            "concurrency": level,
            "cppStatus": pair["cpp"]["status"], "cppSuccesses": cpp["successes"], "cppErrors": cpp["errors"], "cppP95Ms": cpp["p95_ms"],
            "net10Status": pair["net10"]["status"], "net10Successes": net["successes"], "net10Errors": net["errors"], "net10P95Ms": net["p95_ms"],
            "cppOverNet10P95Ratio": ratio(cpp["p95_ms"], net["p95_ms"], passed),
        })
    smtp_rows = []
    for impl in ("cpp", "net10"):
        item = smtp[impl]
        smtp_rows.append({"implementation": impl, "status": item["status"], "accepted": item["acceptedMessages"], "p50Ms": item["p50_ms"], "p95Ms": item["p95_ms"], "p99Ms": item["p99_ms"], "throughput": item["throughput_messages_per_second"]})
    smtp_passed = all(row["status"] == "PASS" and row["accepted"] == 500 for row in smtp_rows)
    smtp_ratio = ratio(smtp_rows[0]["p95Ms"], smtp_rows[1]["p95Ms"], smtp_passed)

    plt.style.use("seaborn-v0_8-whitegrid")
    colors = ["#2F6B9A", "#D8872D"]
    labels = ["Legacy C++", ".NET 10"]
    fig, ax = plt.subplots(figsize=(8, 4.5))
    x = np.arange(3)
    ax.bar(x - 0.19, [row["cppP95Ms"] for row in protocol_rows], 0.38, label=labels[0], color=colors[0])
    ax.bar(x + 0.19, [row["net10P95Ms"] for row in protocol_rows], 0.38, label=labels[1], color=colors[1])
    ax.set_xticks(x, [row["scenario"].upper() for row in protocol_rows]); ax.set_ylabel("p95 milliseconds"); ax.set_title("Protocol p95 latency (paired diagnostic)"); ax.legend(); fig.tight_layout(); fig.savefig(output / "protocol-p95.png", dpi=180); plt.close(fig)
    fig, axes = plt.subplots(1, 2, figsize=(10, 4.5))
    x = np.arange(3)
    axes[0].bar(x - 0.19, [row["cppSuccesses"] for row in concurrent_rows], 0.38, label=labels[0], color=colors[0])
    axes[0].bar(x + 0.19, [row["net10Successes"] for row in concurrent_rows], 0.38, label=labels[1], color=colors[1])
    axes[0].set_xticks(x, [str(row["concurrency"]) for row in concurrent_rows]); axes[0].set_ylabel("successful sessions"); axes[0].set_title("IMAP success count")
    axes[1].bar(x - 0.19, [row["cppP95Ms"] or 0 for row in concurrent_rows], 0.38, label=labels[0], color=colors[0])
    axes[1].bar(x + 0.19, [row["net10P95Ms"] or 0 for row in concurrent_rows], 0.38, label=labels[1], color=colors[1])
    axes[1].set_xticks(x, [str(row["concurrency"]) for row in concurrent_rows]); axes[1].set_ylabel("p95 milliseconds"); axes[1].set_title("IMAP p95 (successful samples)"); axes[1].legend()
    fig.suptitle("Concurrent IMAP (paired diagnostic; failed gates stay visible)"); fig.tight_layout(); fig.savefig(output / "concurrent-imap.png", dpi=180); plt.close(fig)
    fig, axes = plt.subplots(1, 2, figsize=(9, 4.2))
    axes[0].bar(labels, [row["accepted"] for row in smtp_rows], color=colors); axes[0].set_ylabel("accepted messages"); axes[0].set_title("SMTP acceptance")
    axes[1].bar(labels, [row["p95Ms"] for row in smtp_rows], color=colors); axes[1].set_ylabel("p95 milliseconds"); axes[1].set_title("SMTP p95")
    fig.tight_layout(); fig.savefig(output / "smtp-acceptance.png", dpi=180); plt.close(fig)

    data = {"schema": "paired-cpp-net10-performance-diagnostic-v1", "gate": "RED", "fixtureId": fixture.get("fixtureId") or Path(fixture["outputRoot"]).name, "manifestSha256": manifest_hash, "runId": all_reports[0]["runId"], "sourceCommit": build["sourceCommit"], "cppExecutableSha256": build["executableSha256"], "protocol": protocol_rows, "concurrentImap": concurrent_rows, "smtp": {"rows": smtp_rows, "cppOverNet10P95Ratio": smtp_ratio}, "limitations": ["No ratio or winner is published for a failed scenario.", "The C++ executable is a disposable standalone /Debug process, not an installed Windows service.", "The corpus contains 1,000 messages; the 100,000-message acceptance gate remains open.", "A 24-hour service soak and full queue/remote-delivery matrix remain open."]}
    (output / "diagnostic-summary.json").write_text(json.dumps(data, indent=2, ensure_ascii=True) + "\n", encoding="utf-8")
    with (output / "diagnostic-summary.csv").open("w", newline="", encoding="utf-8") as stream:
        fields = ["category", "scenario", "cppStatus", "cppSuccesses", "cppErrors", "cppP95Ms", "net10Status", "net10Successes", "net10Errors", "net10P95Ms", "cppOverNet10P95Ratio"]
        writer = csv.DictWriter(stream, fieldnames=fields); writer.writeheader()
        for row in protocol_rows:
            writer.writerow({"category": "protocol", **{key: row.get(key) for key in fields[1:]}})
        for row in concurrent_rows:
            writer.writerow({"category": "concurrent-imap", "scenario": row["concurrency"], **{key: row.get(key) for key in fields[2:]}})
    fixture_id = fixture.get("fixtureId") or Path(fixture["outputRoot"]).name
    lines = ["# Current C++ vs .NET 10 Performance Diagnostic", "", "**Decision: RED.** This is a paired diagnostic report, not a release acceptance report. Ratios are emitted only for scenarios where both implementations passed.", "", f"Fixture: `{fixture_id}`; source commit: `{build['sourceCommit']}`; C++ build hash: `{build['executableSha256']}`.", "", "## Protocol", "", "![Protocol p95](protocol-p95.png)", "", "| Scenario | C++ | .NET 10 | p95 ratio (C++/.NET 10) |", "| --- | --- | --- | ---: |"]
    lines += [f"| {row['scenario'].upper()} | {row['cppSuccesses']} success, p95 {row['cppP95Ms']} ms | {row['net10Successes']} success, p95 {row['net10P95Ms']} ms | {row['cppOverNet10P95Ratio'] if row['cppOverNet10P95Ratio'] is not None else 'invalid'} |" for row in protocol_rows]
    lines += ["", "## Concurrent IMAP", "", "![Concurrent IMAP](concurrent-imap.png)", "", "| Sessions | C++ | .NET 10 | Ratio |", "| ---: | --- | --- | --- |"]
    lines += [f"| {row['concurrency']} | {row['cppSuccesses']}/{row['concurrency']} ({row['cppStatus']}) | {row['net10Successes']}/{row['concurrency']} ({row['net10Status']}) | {row['cppOverNet10P95Ratio'] if row['cppOverNet10P95Ratio'] is not None else 'invalid'} |" for row in concurrent_rows]
    lines += ["", "## SMTP", "", "![SMTP acceptance](smtp-acceptance.png)", "", "| Implementation | Accepted | p95 ms | Throughput/s |", "| --- | ---: | ---: | ---: |"]
    lines += [f"| {row['implementation']} | {row['accepted']}/500 ({row['status']}) | {row['p95Ms']} | {row['throughput']} |" for row in smtp_rows]
    lines += ["", "## Gate Limitations", "", "- No concurrent IMAP winner or speed-up ratio is valid at 500 or 1,000 because one or both sides failed.", "- The C++ executable is a disposable standalone `/Debug` process; installed service and out-of-process COM lifecycle evidence remain open.", "- The workload corpus has 1,000 messages, not the required 100,000-message mailbox.", "- 24-hour soak, POP3 large-mailbox soak, remote delivery/retry, queue, backup/restore timing, installer, and COM lifecycle gates remain open.", "", "Raw JSON/CSV and charts are generated from the same manifest-bound disposable fixture. Production SQL, Data, service, COM registration, and DCOM permissions were not used or changed."]
    (output / "PERFORMANCE_DIAGNOSTIC.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print("Generated diagnostic report.")


if __name__ == "__main__":
    main()
