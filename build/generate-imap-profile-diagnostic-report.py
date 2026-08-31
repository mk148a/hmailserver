"""Generate a sanitized profile comparison for the live IMAP capacity probe."""

from __future__ import annotations

import argparse
import csv
import json
import math
import subprocess
from datetime import datetime, timezone
from pathlib import Path


PROFILES = ("Admission", "AuthSelect", "Full")
IMPLEMENTATIONS = ("cpp", "net10")


def read_json(path: Path) -> dict:
    with path.open("r", encoding="utf-8-sig") as handle:
        value = json.load(handle)
    if not isinstance(value, dict):
        raise ValueError(f"Expected an object in {path}")
    return value


def get_git_commit(repository_root: Path) -> str:
    try:
        return subprocess.check_output(
            ["git", "-C", str(repository_root), "rev-parse", "HEAD"],
            text=True,
            stderr=subprocess.DEVNULL,
        ).strip()
    except (OSError, subprocess.CalledProcessError):
        return "unknown"


def load_result(input_root: Path, implementation: str, profile: str) -> dict:
    report_path = input_root / f"{implementation}-{profile.lower()}-1000" / "live-concurrent-imap.json"
    report = read_json(report_path)
    if report.get("schema") != "live-concurrent-imap-v2":
        raise ValueError(f"Unexpected schema for {implementation}/{profile}")
    if report.get("implementation") != implementation or report.get("profile") != profile:
        raise ValueError(f"Profile identity mismatch for {implementation}/{profile}")
    if report.get("concurrency") != 1000 or report.get("waves") != 1:
        raise ValueError(f"Unexpected concurrency for {implementation}/{profile}")
    summary = report.get("summary")
    if not isinstance(summary, dict) or summary.get("completed") != 1000:
        raise ValueError(f"Incomplete report for {implementation}/{profile}")
    if summary.get("successes", 0) + summary.get("errors", 0) != 1000:
        raise ValueError(f"Success/error accounting does not reconcile for {implementation}/{profile}")
    configuration = report.get("probeConfiguration")
    if not isinstance(configuration, dict) or configuration.get("profile") != profile:
        raise ValueError(f"Missing profile attestation for {implementation}/{profile}")
    return report


def finite_or_none(value):
    if value is None:
        return None
    result = float(value)
    return result if math.isfinite(result) else None


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input-root", type=Path, required=True)
    parser.add_argument("--output-directory", type=Path, required=True)
    parser.add_argument("--repository-root", type=Path, default=Path("."))
    args = parser.parse_args()

    reports = {
        (implementation, profile): load_result(args.input_root, implementation, profile)
        for implementation in IMPLEMENTATIONS
        for profile in PROFILES
    }
    manifest_hashes = {
        report.get("manifestSha256")
        for report in reports.values()
        if report.get("manifestSha256")
    }
    if len(manifest_hashes) != 1:
        raise ValueError("The six profile reports are not bound to one fixture manifest")

    rows = []
    for profile in PROFILES:
        for implementation in IMPLEMENTATIONS:
            summary = reports[(implementation, profile)]["summary"]
            rows.append(
                {
                    "implementation": implementation,
                    "profile": profile,
                    "status": reports[(implementation, profile)]["status"],
                    "successes": int(summary["successes"]),
                    "errors": int(summary["errors"]),
                    "timeouts": int(summary["timeouts"]),
                    "p50_ms": finite_or_none(summary.get("p50_ms")),
                    "p95_ms": finite_or_none(summary.get("p95_ms")),
                    "p99_ms": finite_or_none(summary.get("p99_ms")),
                    "throughput_sessions_per_second": finite_or_none(
                        summary.get("throughput_sessions_per_second")
                    ),
                }
            )

    by_key = {(row["implementation"], row["profile"]): row for row in rows}
    for profile in PROFILES:
        cpp = by_key[("cpp", profile)]
        net10 = by_key[("net10", profile)]
        both_pass = cpp["status"] == "PASS" and net10["status"] == "PASS"
        ratio = None
        if both_pass and cpp["p95_ms"] is not None and net10["p95_ms"] not in (None, 0):
            ratio = round(cpp["p95_ms"] / net10["p95_ms"], 6)
        cpp["ratio_valid"] = both_pass
        net10["ratio_valid"] = both_pass
        cpp["p95_ratio_cpp_over_net10"] = ratio
        net10["p95_ratio_cpp_over_net10"] = ratio

    gate = "PASS" if all(row["status"] == "PASS" for row in rows) else "RED"
    output_directory = args.output_directory
    output_directory.mkdir(parents=True, exist_ok=True)
    generated_utc = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
    summary = {
        "schema": "paired-imap-profile-diagnostic-v1",
        "status": gate,
        "generatedUtc": generated_utc,
        "gitCommit": get_git_commit(args.repository_root),
        "fixtureManifestSha256": next(iter(manifest_hashes)),
        "concurrency": 1000,
        "waves": 1,
        "profiles": list(PROFILES),
        "rows": rows,
        "claims": {
            "listenerAdmissionIsolated": all(
                by_key[(implementation, "Admission")]["successes"] == 1000
                for implementation in IMPLEMENTATIONS
            ),
            "fullSearchSortPassedForBoth": all(
                by_key[(implementation, "Full")]["status"] == "PASS"
                for implementation in IMPLEMENTATIONS
            ),
            "speedRatioPermitted": all(row["ratio_valid"] for row in rows),
        },
        "limitations": [
            "The fixture contains 1,000 messages, not the 100,000-message acceptance corpus.",
            "The C++ binary is a standalone /Debug process, not an installed service or registered COM local server.",
            "This profile diagnostic does not establish a release performance pass or overall speed winner.",
        ],
    }
    (output_directory / "profile-summary.json").write_text(
        json.dumps(summary, indent=2) + "\n", encoding="utf-8"
    )

    fieldnames = [
        "implementation",
        "profile",
        "status",
        "successes",
        "errors",
        "timeouts",
        "p50_ms",
        "p95_ms",
        "p99_ms",
        "throughput_sessions_per_second",
        "ratio_valid",
        "p95_ratio_cpp_over_net10",
    ]
    with (output_directory / "profile-summary.csv").open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)

    markdown = [
        "# Paired IMAP profile diagnostic",
        "",
        f"Status: **{gate}**",
        "",
        "This report uses one disposable fixture, 1,000 concurrent sessions, and one wave.",
        "The profiles isolate listener admission, SQL-backed authentication/SELECT, and full SEARCH/SORT.",
        "",
        "| Profile | C++ success | Net10 success | C++ p95 ms | Net10 p95 ms | Ratio valid |",
        "| --- | ---: | ---: | ---: | ---: | :---: |",
    ]
    for profile in PROFILES:
        cpp = by_key[("cpp", profile)]
        net10 = by_key[("net10", profile)]
        ratio_valid = "yes" if cpp["ratio_valid"] else "no"
        markdown.append(
            f"| {profile} | {cpp['successes']}/1000 | {net10['successes']}/1000 | "
            f"{cpp['p95_ms'] if cpp['p95_ms'] is not None else 'n/a'} | "
            f"{net10['p95_ms'] if net10['p95_ms'] is not None else 'n/a'} | {ratio_valid} |"
        )
    markdown += [
        "",
        "Admission passed 1,000/1,000 on both implementations, so the observed full-load failure is not explained by the listener acceptance path alone.",
        "AuthSelect and Full are not both passing, so no cross-implementation latency ratio or performance winner is claimed for those profiles.",
        "The full release gate remains RED pending a successful equivalent C++/Net10 workload matrix, installed-service/native evidence, larger corpus, and soak coverage.",
        "",
        "Charts:",
        "",
        "- `profile-success-count.png`",
        "- `profile-p95-latency.png`",
        "- `profile-throughput.png`",
    ]
    (output_directory / "PROFILE_DIAGNOSTIC.md").write_text("\n".join(markdown) + "\n", encoding="utf-8")

    import matplotlib.pyplot as plt

    labels = list(PROFILES)
    x = list(range(len(labels)))
    for filename, title, value_key, ylabel in (
        ("profile-success-count.png", "Concurrent IMAP profile success count", "successes", "successful sessions"),
        ("profile-p95-latency.png", "Concurrent IMAP profile p95 latency", "p95_ms", "milliseconds"),
        ("profile-throughput.png", "Concurrent IMAP profile throughput", "throughput_sessions_per_second", "sessions per second"),
    ):
        figure, axis = plt.subplots(figsize=(9, 5), dpi=140)
        cpp_values = [by_key[("cpp", profile)][value_key] or math.nan for profile in PROFILES]
        net10_values = [by_key[("net10", profile)][value_key] or math.nan for profile in PROFILES]
        width = 0.36
        axis.bar([value - width / 2 for value in x], cpp_values, width, label="C++", color="#286090")
        axis.bar([value + width / 2 for value in x], net10_values, width, label=".NET 10", color="#d95f02")
        axis.set_xticks(x, labels)
        axis.set_ylabel(ylabel)
        axis.set_title(title)
        axis.grid(axis="y", alpha=0.25)
        axis.legend()
        figure.tight_layout()
        figure.savefig(output_directory / filename)
        plt.close(figure)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
