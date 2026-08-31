"""Generate a sanitized C++/.NET 10 IMAP query threshold report."""

from __future__ import annotations

import argparse
import csv
import json
import math
import subprocess
from datetime import datetime, timezone
from pathlib import Path


LEVELS = (100, 500, 1000)
PROFILES = ("Search", "Full")
IMPLEMENTATIONS = ("cpp", "net10")


def read_json(path: Path) -> dict:
    with path.open("r", encoding="utf-8-sig") as handle:
        value = json.load(handle)
    if not isinstance(value, dict):
        raise ValueError(f"Expected an object in {path}")
    return value


def git_commit(repository_root: Path) -> str:
    try:
        return subprocess.check_output(
            ["git", "-C", str(repository_root), "rev-parse", "HEAD"],
            text=True,
            stderr=subprocess.DEVNULL,
        ).strip()
    except (OSError, subprocess.CalledProcessError):
        return "unknown"


def load_report(input_root: Path, implementation: str, profile: str, level: int) -> dict:
    report = read_json(input_root / f"{implementation}-{profile.lower()}-{level}" / "live-concurrent-imap.json")
    if report.get("schema") != "live-concurrent-imap-v2":
        raise ValueError(f"Unexpected schema for {implementation}/{profile}/{level}")
    if report.get("implementation") != implementation or report.get("profile") != profile:
        raise ValueError(f"Identity mismatch for {implementation}/{profile}/{level}")
    if report.get("concurrency") != level or report.get("waves") != 1:
        raise ValueError(f"Unexpected workload shape for {implementation}/{profile}/{level}")
    summary = report.get("summary")
    if not isinstance(summary, dict) or summary.get("completed") != level:
        raise ValueError(f"Incomplete report for {implementation}/{profile}/{level}")
    if summary.get("successes", 0) + summary.get("errors", 0) != level:
        raise ValueError(f"Accounting mismatch for {implementation}/{profile}/{level}")
    return report


def finite(value):
    if value is None:
        return None
    value = float(value)
    return value if math.isfinite(value) else None


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input-root", type=Path, required=True)
    parser.add_argument("--output-directory", type=Path, required=True)
    parser.add_argument("--repository-root", type=Path, default=Path("."))
    args = parser.parse_args()

    reports = {
        (implementation, profile, level): load_report(args.input_root, implementation, profile, level)
        for implementation in IMPLEMENTATIONS
        for profile in PROFILES
        for level in LEVELS
    }
    manifest_hashes = {report.get("manifestSha256") for report in reports.values() if report.get("manifestSha256")}
    if len(manifest_hashes) != 1:
        raise ValueError("Threshold reports are not bound to one fixture manifest")

    rows = []
    for profile in PROFILES:
        for level in LEVELS:
            for implementation in IMPLEMENTATIONS:
                report = reports[(implementation, profile, level)]
                summary = report["summary"]
                rows.append(
                    {
                        "implementation": implementation,
                        "profile": profile,
                        "concurrency": level,
                        "status": report["status"],
                        "successes": int(summary["successes"]),
                        "errors": int(summary["errors"]),
                        "timeouts": int(summary["timeouts"]),
                        "p50_ms": finite(summary.get("p50_ms")),
                        "p95_ms": finite(summary.get("p95_ms")),
                        "p99_ms": finite(summary.get("p99_ms")),
                        "throughput_sessions_per_second": finite(summary.get("throughput_sessions_per_second")),
                    }
                )

    by_key = {(row["implementation"], row["profile"], row["concurrency"]): row for row in rows}
    for profile in PROFILES:
        for level in LEVELS:
            cpp = by_key[("cpp", profile, level)]
            net10 = by_key[("net10", profile, level)]
            both_pass = cpp["status"] == "PASS" and net10["status"] == "PASS"
            ratio = None
            if both_pass and cpp["p95_ms"] is not None and net10["p95_ms"] not in (None, 0):
                ratio = round(cpp["p95_ms"] / net10["p95_ms"], 6)
            cpp["ratio_valid"] = both_pass
            net10["ratio_valid"] = both_pass
            cpp["p95_ratio_cpp_over_net10"] = ratio
            net10["p95_ratio_cpp_over_net10"] = ratio

    output = args.output_directory
    output.mkdir(parents=True, exist_ok=True)
    summary = {
        "schema": "paired-imap-threshold-v1",
        "status": "PASS" if all(row["status"] == "PASS" for row in rows) else "RED",
        "generatedUtc": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "gitCommit": git_commit(args.repository_root),
        "fixtureManifestSha256": next(iter(manifest_hashes)),
        "profiles": list(PROFILES),
        "concurrencyLevels": list(LEVELS),
        "rows": rows,
        "claims": {
            "net10AllIndexedProfilesPassed": all(
                by_key[("net10", profile, level)]["status"] == "PASS"
                for profile in PROFILES
                for level in LEVELS
            ),
            "cppLowAndMediumLevelsPassed": all(
                by_key[("cpp", profile, level)]["status"] == "PASS"
                for profile in PROFILES
                for level in LEVELS[:2]
            ),
            "speedRatioPermitted": any(row["ratio_valid"] for row in rows),
        },
        "limitations": [
            "The fixture contains 1,000 messages, not the 100,000-message acceptance corpus.",
            "The C++ binary is a standalone /Debug process, not an installed service or registered COM local server.",
            "A profile ratio is descriptive only and is not an overall release performance claim.",
        ],
    }
    (output / "threshold-summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")

    fields = list(rows[0].keys())
    with (output / "threshold-summary.csv").open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields)
        writer.writeheader()
        writer.writerows(rows)

    markdown = [
        "# Paired IMAP query threshold diagnostic",
        "",
        f"Status: **{summary['status']}**",
        "",
        "This report uses one disposable indexed fixture and one wave at each concurrency level.",
        "Ratios are valid only when both implementations pass the same profile and level.",
        "",
        "| Profile | Sessions | C++ success | Net10 success | C++ p95 ms | Net10 p95 ms | Ratio valid |",
        "| --- | ---: | ---: | ---: | ---: | ---: | :---: |",
    ]
    for profile in PROFILES:
        for level in LEVELS:
            cpp = by_key[("cpp", profile, level)]
            net10 = by_key[("net10", profile, level)]
            markdown.append(
                f"| {profile} | {level} | {cpp['successes']}/{level} | {net10['successes']}/{level} | "
                f"{cpp['p95_ms'] if cpp['p95_ms'] is not None else 'n/a'} | "
                f"{net10['p95_ms'] if net10['p95_ms'] is not None else 'n/a'} | "
                f"{'yes' if cpp['ratio_valid'] else 'no'} |"
            )
    markdown += [
        "",
        "Net10 passed all indexed Search and Full levels. C++ passed 100 and 500 sessions but failed the 1,000-session Search and Full acceptance levels.",
        "The performance release gate remains RED; no overall speed winner is claimed.",
        "",
        "Charts:",
        "",
        "- `threshold-success-count.png`",
        "- `threshold-p95-latency.png`",
        "- `threshold-throughput.png`",
    ]
    (output / "IMAP_QUERY_THRESHOLD_DIAGNOSTIC.md").write_text("\n".join(markdown) + "\n", encoding="utf-8")

    import matplotlib.pyplot as plt

    labels = [f"{profile}\n{level}" for profile in PROFILES for level in LEVELS]
    x = list(range(len(labels)))
    for filename, title, key, ylabel in (
        ("threshold-success-count.png", "IMAP query threshold success count", "successes", "successful sessions"),
        ("threshold-p95-latency.png", "IMAP query threshold p95 latency", "p95_ms", "milliseconds"),
        ("threshold-throughput.png", "IMAP query threshold throughput", "throughput_sessions_per_second", "sessions per second"),
    ):
        figure, axis = plt.subplots(figsize=(11, 5), dpi=140)
        cpp_values = [by_key[("cpp", profile, level)][key] or math.nan for profile in PROFILES for level in LEVELS]
        net10_values = [by_key[("net10", profile, level)][key] or math.nan for profile in PROFILES for level in LEVELS]
        width = 0.36
        axis.bar([value - width / 2 for value in x], cpp_values, width, label="C++", color="#286090")
        axis.bar([value + width / 2 for value in x], net10_values, width, label=".NET 10", color="#d95f02")
        axis.set_xticks(x, labels)
        axis.set_ylabel(ylabel)
        axis.set_title(title)
        axis.grid(axis="y", alpha=0.25)
        axis.legend()
        figure.tight_layout()
        figure.savefig(output / filename)
        plt.close(figure)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
