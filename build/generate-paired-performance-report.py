#!/usr/bin/env python3
from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
import subprocess
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


def prepare_output(path: Path) -> None:
    path.mkdir(parents=True, exist_ok=True)
    for name in GENERATED_FILES:
        target = path / name
        if target.exists():
            target.unlink()


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
    repository = args.repository_root.resolve()
    output = args.output_directory.resolve()
    prepare_output(output)

    fixture = load_json(args.fixture_manifest.resolve())
    environment = load_json(args.environment.resolve())
    legacy_build = load_json(args.legacy_build_manifest.resolve())
    require(fixture.get("schema") == "paired-benchmark-fixture-v2", "Unexpected fixture schema.")
    require(fixture.get("status") == "PASS", "Fixture preparation did not pass.")
    require(fixture["dataParity"]["exact"] is True, "Data copies are not exact.")
    require(fixture["messageParity"]["exact"] is True, "Logical message projections differ.")
    require(int(fixture["cppDatabaseVersion"]) == 5708, "Legacy database is not version 5708.")
    require(int(fixture["net10DatabaseVersion"]) == 6000, "Net10 database is not version 6000.")
    require(legacy_build.get("status") == "PASS", "Legacy Release build did not pass.")
    require(legacy_build.get("postBuildRegistrationDisabled") is True, "Legacy build allowed post-build registration.")
    require(args.net10_executable.is_file(), "Net10 Release executable is missing.")

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
    net10_hash = sha256(args.net10_executable.resolve())

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
            "The legacy C++ 1,000-session wave failed, so no 1,000-session latency or throughput ratio is valid.",
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
        f"Net10 reduced p95 SMTP command latency by `{protocol_findings[0]['cppOverNet10Ratio']:.2f}x` and p95 IMAP SEARCH/SORT latency by `{protocol_findings[1]['cppOverNet10Ratio']:.2f}x`. POP3 p95 regressed by `{1 / protocol_findings[2]['cppOverNet10Ratio']:.2f}x`. Net10 passed 1,000/1,000 concurrent IMAP sessions; legacy C++ completed only `{concurrent['cpp'][1000]['summary']['successes']}/1000` and failed that gate.",
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
        "- Legacy C++ failed the 1,000-session wave, so no C++/Net10 latency or throughput ratio is published at that load.",
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
