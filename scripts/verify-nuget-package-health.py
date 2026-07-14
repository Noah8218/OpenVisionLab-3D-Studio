#!/usr/bin/env python3
"""Fail when a solution contains vulnerable or deprecated NuGet packages."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import subprocess
import sys
import xml.etree.ElementTree as ET


CHECKS = ("vulnerable", "deprecated")
PACKAGE_SCOPES = ("topLevelPackages", "transitivePackages")


def normalize_project_path(path: str, base_directory: Path) -> str:
    candidate = Path(path)
    if not candidate.is_absolute():
        candidate = base_directory / candidate
    return str(candidate.resolve()).casefold()


def load_solution_projects(solution: Path) -> set[str]:
    try:
        root = ET.parse(solution).getroot()
    except ET.ParseError as error:
        raise ValueError(f"solution XML is invalid: {error}") from error

    project_paths = {
        normalize_project_path(path, solution.parent)
        for element in root.iter()
        if element.tag.rsplit("}", 1)[-1] == "Project"
        if (path := element.get("Path"))
    }
    if not project_paths:
        raise ValueError("solution contains no projects")
    return project_paths


def require_expected_projects(expected: set[str], actual: set[str], check: str) -> None:
    missing = expected - actual
    unexpected = actual - expected
    if missing or unexpected:
        raise ValueError(
            f"dotnet {check} query project set differs from solution "
            f"(missing={len(missing)}, unexpected={len(unexpected)})"
        )


def validate_payload(payload: dict, check: str) -> set[str]:
    if payload.get("version") != 1:
        raise ValueError(f"dotnet {check} query returned an unsupported JSON version")

    parameters = set(str(payload.get("parameters", "")).split())
    required_parameters = {f"--{check}", "--include-transitive"}
    missing_parameters = required_parameters - parameters
    if missing_parameters:
        missing = ", ".join(sorted(missing_parameters))
        raise ValueError(f"dotnet {check} query omitted required parameters: {missing}")

    projects = payload.get("projects")
    if not isinstance(projects, list) or not projects:
        raise ValueError(f"dotnet {check} query returned no projects")

    project_paths: set[str] = set()
    for project in projects:
        if not isinstance(project, dict) or not isinstance(project.get("path"), str):
            raise ValueError(f"dotnet {check} query returned a project without a path")
        project_path = project["path"].strip()
        if not project_path:
            raise ValueError(f"dotnet {check} query returned an empty project path")
        project_paths.add(project_path)

    if len(project_paths) != len(projects):
        raise ValueError(f"dotnet {check} query returned duplicate project paths")
    return project_paths


def collect_findings(payload: dict, check: str) -> list[dict[str, str]]:
    findings: list[dict[str, str]] = []
    for project in payload.get("projects", []):
        project_path = str(project.get("path", "unknown"))
        for framework in project.get("frameworks", []):
            framework_name = str(framework.get("framework", "unknown"))
            for scope in PACKAGE_SCOPES:
                for package in framework.get(scope, []):
                    findings.append(
                        {
                            "check": check,
                            "project": project_path,
                            "framework": framework_name,
                            "scope": scope,
                            "id": str(package.get("id", "unknown")),
                            "resolvedVersion": str(package.get("resolvedVersion", "unknown")),
                        }
                    )
    return findings


def run_check(solution: Path, check: str) -> tuple[dict, str]:
    command = [
        "dotnet",
        "list",
        str(solution),
        "package",
        f"--{check}",
        "--include-transitive",
        "--format",
        "json",
        "--output-version",
        "1",
    ]
    result = subprocess.run(command, capture_output=True, text=True, encoding="utf-8")
    if result.returncode != 0:
        detail = result.stderr.strip() or result.stdout.strip() or "no diagnostic"
        raise RuntimeError(f"dotnet {check} query failed with exit code {result.returncode}: {detail}")
    return json.loads(result.stdout), result.stdout


def verify(solution: Path, report: Path, json_directory: Path) -> int:
    report.parent.mkdir(parents=True, exist_ok=True)
    json_directory.mkdir(parents=True, exist_ok=True)
    expected_projects = load_solution_projects(solution.resolve())

    all_findings: list[dict[str, str]] = []
    check_lines: list[str] = []
    for check in CHECKS:
        payload, raw_json = run_check(solution, check)
        json_path = json_directory / f"nuget_{check}.json"
        json_path.write_text(raw_json, encoding="utf-8")
        checked_projects = {
            normalize_project_path(path, solution.resolve().parent)
            for path in validate_payload(payload, check)
        }
        require_expected_projects(expected_projects, checked_projects, check)
        findings = collect_findings(payload, check)
        all_findings.extend(findings)
        status = "Pass" if not findings else "Fail"
        check_lines.append(
            f"Check|{check}|{status}|findings={len(findings)}|json={json_path.resolve()}"
        )

    project_count = len(expected_projects)
    status = "Pass" if not all_findings else "Fail"
    lines = [
        f"NuGetPackageHealth|{status}|projects={project_count}|vulnerable={sum(f['check'] == 'vulnerable' for f in all_findings)}|deprecated={sum(f['check'] == 'deprecated' for f in all_findings)}",
        *check_lines,
    ]
    lines.extend(
        "Finding|{check}|project={project}|framework={framework}|scope={scope}|id={id}|resolvedVersion={resolvedVersion}".format(
            **finding
        )
        for finding in all_findings
    )
    report.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(lines[0])
    return 0 if status == "Pass" else 1


def self_test() -> int:
    empty = {
        "version": 1,
        "parameters": "--vulnerable --include-transitive",
        "projects": [{"path": "empty.csproj"}],
    }
    populated = {
        "version": 1,
        "parameters": "--deprecated --include-transitive",
        "projects": [
            {
                "path": "sample.csproj",
                "frameworks": [
                    {
                        "framework": "net10.0",
                        "topLevelPackages": [{"id": "Direct.Bad", "resolvedVersion": "1.0.0"}],
                        "transitivePackages": [
                            {"id": "Transitive.Bad", "resolvedVersion": "2.0.0"}
                        ],
                    }
                ],
            }
        ]
    }
    if validate_payload(empty, "vulnerable") != {"empty.csproj"}:
        raise AssertionError("valid package-health project set was not preserved")
    if validate_payload(populated, "deprecated") != {"sample.csproj"}:
        raise AssertionError("valid package-health project set was not preserved")
    if collect_findings(empty, "vulnerable"):
        raise AssertionError("empty package-health payload produced findings")
    findings = collect_findings(populated, "deprecated")
    if [finding["id"] for finding in findings] != ["Direct.Bad", "Transitive.Bad"]:
        raise AssertionError("direct/transitive package findings were not detected")
    try:
        validate_payload({"version": 1, "parameters": "--vulnerable --include-transitive"}, "vulnerable")
    except ValueError:
        pass
    else:
        raise AssertionError("incomplete package-health payload was accepted")
    try:
        require_expected_projects({"a.csproj", "b.csproj"}, {"a.csproj"}, "vulnerable")
    except ValueError:
        pass
    else:
        raise AssertionError("incomplete solution project set was accepted")
    print("NuGetPackageHealthSelfTest|Pass|cases=4")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--solution", type=Path)
    parser.add_argument("--report", type=Path)
    parser.add_argument("--json-directory", type=Path)
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()

    if args.self_test:
        return self_test()
    if args.solution is None or args.report is None or args.json_directory is None:
        parser.error("--solution, --report, and --json-directory are required")

    try:
        return verify(args.solution, args.report, args.json_directory)
    except (OSError, RuntimeError, ValueError, json.JSONDecodeError) as error:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        line = f"NuGetPackageHealth|Fail|error={str(error).replace('|', '/')}"
        args.report.write_text(line + "\n", encoding="utf-8")
        print(line)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
