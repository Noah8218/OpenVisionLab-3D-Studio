#!/usr/bin/env python3
"""Verify CloudCompare C2M output and optionally compare Viewer statistics."""

from __future__ import annotations

import argparse
from array import array
from bisect import bisect_right
import hashlib
import math
from pathlib import Path
import struct
import tempfile


FLOAT_TYPES = {"float", "float32"}
CHUNK_POINTS = 65_536


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def read_header(stream) -> tuple[int, list[tuple[str, str]]]:
    if stream.readline().rstrip(b"\r\n") != b"ply":
        raise ValueError("PLY magic is missing")
    if stream.readline().rstrip(b"\r\n") != b"format binary_little_endian 1.0":
        raise ValueError("Only binary little-endian PLY 1.0 is supported")

    vertex_count = -1
    vertex_properties: list[tuple[str, str]] = []
    current_element = ""
    for raw_line in stream:
        line = raw_line.decode("ascii").strip()
        if line == "end_header":
            break
        if line.startswith("element "):
            _, current_element, count_text = line.split()
            count = int(count_text)
            if current_element == "vertex":
                vertex_count = count
            elif count != 0:
                raise ValueError(f"Unsupported non-empty PLY element: {current_element}")
        elif current_element == "vertex" and line.startswith("property "):
            fields = line.split()
            if len(fields) != 3 or fields[1] not in FLOAT_TYPES:
                raise ValueError(f"Unsupported vertex property: {line}")
            vertex_properties.append((fields[1], fields[2]))
    else:
        raise ValueError("PLY header has no end_header")

    if vertex_count < 0:
        raise ValueError("PLY vertex declaration is missing")
    names = [name for _, name in vertex_properties]
    if names[:3] != ["x", "y", "z"]:
        raise ValueError(f"PLY vertex properties must start with x,y,z; found {names}")
    return vertex_count, vertex_properties


def percentile(sorted_values: list[float], fraction: float) -> float:
    if not sorted_values:
        raise ValueError("No finite scalar values")
    position = fraction * (len(sorted_values) - 1)
    lower = math.floor(position)
    upper = math.ceil(position)
    if lower == upper:
        return sorted_values[lower]
    weight = position - lower
    return sorted_values[lower] * (1.0 - weight) + sorted_values[upper] * weight


def format_xyz(values: list[float]) -> str:
    return f"({values[0]:.9g},{values[1]:.9g},{values[2]:.9g})"


def verify_viewer_contract(
    contract_path: Path,
    statistics: str,
    units: str,
    count: int,
    minimum: float,
    maximum: float,
    mean: float,
    standard_deviation: float,
    rms: float,
    tolerance: float,
) -> str:
    prefix = (
        "NominalActualSignedStatistics"
        if statistics == "signed"
        else "NominalActualUnsignedStatistics"
    )
    matches = [
        line
        for line in contract_path.read_text(encoding="utf-8-sig").splitlines()
        if line.startswith(prefix + "|")
    ]
    if len(matches) != 1:
        raise ValueError(
            f"Viewer contract must contain exactly one {prefix} line; found {len(matches)}"
        )

    fields: dict[str, str] = {}
    for field in matches[0].split("|")[1:]:
        name, separator, value = field.partition("=")
        if not separator or not name:
            raise ValueError(f"Malformed Viewer contract field: {field}")
        fields[name] = value

    required = {"count", "min", "max", "mean", "stdPopulation", "rms", "unit"}
    missing = sorted(required - fields.keys())
    if missing:
        raise ValueError(f"Viewer contract statistics are missing: {','.join(missing)}")
    if int(fields["count"]) != count:
        raise ValueError(
            f"Viewer contract count {fields['count']} differs from independent count {count}"
        )
    if fields["unit"] != units:
        raise ValueError(
            f"Viewer contract unit {fields['unit']} differs from independent unit {units}"
        )

    expected = {
        "min": minimum,
        "max": maximum,
        "mean": mean,
        "stdPopulation": standard_deviation,
        "rms": rms,
    }
    deltas = {name: abs(float(fields[name]) - value) for name, value in expected.items()}
    failed = {name: delta for name, delta in deltas.items() if delta > tolerance}
    if failed:
        details = ",".join(f"{name}={delta:.17g}" for name, delta in failed.items())
        raise ValueError(f"Viewer contract statistic tolerance exceeded: {details}")

    return (
        f"ViewerContractParity|Pass|statistics={statistics}|points={count}"
        f"|maxStatisticDelta={max(deltas.values()):.17g}"
        f"|tolerance={tolerance:.9g}|units={units}"
    )


def verify(
    source: Path,
    candidate: Path,
    scalar_name: str,
    units: str,
    thresholds: list[float],
    expected_mean: float | None,
    expected_std: float | None,
    stat_tolerance: float,
    require_nonnegative: bool,
    viewer_contract: Path | None,
    viewer_statistics: str | None,
) -> list[str]:
    with source.open("rb") as source_stream, candidate.open("rb") as candidate_stream:
        source_count, source_properties = read_header(source_stream)
        candidate_count, candidate_properties = read_header(candidate_stream)
        if source_count != candidate_count:
            raise ValueError(f"Candidate has {candidate_count} vertices; expected {source_count}")

        source_names = [name for _, name in source_properties]
        candidate_names = [name for _, name in candidate_properties]
        if source_names != ["x", "y", "z"]:
            raise ValueError(f"Source must contain only x,y,z; found {source_names}")
        if scalar_name not in candidate_names:
            raise ValueError(f"Candidate scalar field is missing: {scalar_name}")
        scalar_index = candidate_names.index(scalar_name)
        if scalar_index < 3:
            raise ValueError("Scalar field overlaps XYZ properties")

        source_record = struct.Struct("<" + "f" * len(source_properties))
        candidate_record = struct.Struct("<" + "f" * len(candidate_properties))
        remaining = source_count
        compared = 0
        max_coordinate_error = 0.0
        min_xyz = [math.inf, math.inf, math.inf]
        max_xyz = [-math.inf, -math.inf, -math.inf]
        scalar_values = array("f")
        scalar_sum = 0.0
        scalar_sum_squares = 0.0
        nonfinite_count = 0

        while remaining:
            chunk_count = min(CHUNK_POINTS, remaining)
            source_bytes = source_stream.read(chunk_count * source_record.size)
            candidate_bytes = candidate_stream.read(chunk_count * candidate_record.size)
            if len(source_bytes) != chunk_count * source_record.size:
                raise ValueError("Source PLY vertex data is truncated")
            if len(candidate_bytes) != chunk_count * candidate_record.size:
                raise ValueError("Candidate PLY vertex data is truncated")

            source_rows = source_record.iter_unpack(source_bytes)
            candidate_rows = candidate_record.iter_unpack(candidate_bytes)
            for source_row, candidate_row in zip(source_rows, candidate_rows):
                for axis in range(3):
                    source_value = source_row[axis]
                    candidate_value = candidate_row[axis]
                    max_coordinate_error = max(
                        max_coordinate_error,
                        abs(candidate_value - source_value),
                    )
                    min_xyz[axis] = min(min_xyz[axis], source_value)
                    max_xyz[axis] = max(max_xyz[axis], source_value)

                scalar = candidate_row[scalar_index]
                if math.isfinite(scalar):
                    scalar_values.append(scalar)
                    scalar_sum += scalar
                    scalar_sum_squares += scalar * scalar
                else:
                    nonfinite_count += 1
                compared += 1

            remaining -= chunk_count

        if source_stream.read(1) or candidate_stream.read(1):
            raise ValueError("Unexpected data follows the declared PLY vertices")
        if compared != source_count:
            raise ValueError(f"Compared {compared} vertices; expected {source_count}")
        if nonfinite_count:
            raise ValueError(f"Candidate contains {nonfinite_count} non-finite scalar values")
        if max_coordinate_error != 0.0:
            raise ValueError(f"Candidate XYZ changed; max error is {max_coordinate_error:.9g}")

    values = sorted(scalar_values)
    count = len(values)
    mean = scalar_sum / count
    variance = max(0.0, scalar_sum_squares / count - mean * mean)
    standard_deviation = math.sqrt(variance)
    rms = math.sqrt(scalar_sum_squares / count)
    if require_nonnegative and values[0] < 0.0:
        raise ValueError(f"Unsigned scalar contains negative value {values[0]:.9g}")

    expected_checks: list[str] = []
    if expected_mean is not None:
        error = abs(mean - expected_mean)
        if error > stat_tolerance:
            raise ValueError(
                f"Mean {mean:.9g} differs from expected {expected_mean:.9g} by {error:.9g}"
            )
        expected_checks.append(f"expectedMean={expected_mean:.9g}")
        expected_checks.append(f"meanError={error:.9g}")
    if expected_std is not None:
        error = abs(standard_deviation - expected_std)
        if error > stat_tolerance:
            raise ValueError(
                f"Std {standard_deviation:.9g} differs from expected {expected_std:.9g} by {error:.9g}"
            )
        expected_checks.append(f"expectedStd={expected_std:.9g}")
        expected_checks.append(f"stdError={error:.9g}")

    threshold_fields = []
    for threshold in thresholds:
        accepted = bisect_right(values, threshold)
        threshold_fields.append(
            f"le{threshold:.9g}={accepted}/{count}({accepted / count:.9%})"
        )

    lines = [
        f"CloudCompareC2MVerification|Pass|mode=ordered-binary-ply|units={units}",
        f"Source|path={source.resolve()}|sha256={sha256(source)}|vertices={source_count}",
        f"Candidate|path={candidate.resolve()}|sha256={sha256(candidate)}|vertices={candidate_count}",
        f"Properties|source={','.join(source_names)}|candidate={','.join(candidate_names)}|scalar={scalar_name}",
        f"Coordinates|points={compared}|maxAbsError={max_coordinate_error:.9g}|min={format_xyz(min_xyz)}|max={format_xyz(max_xyz)}",
        f"Scalar|finite={count}|nonfinite={nonfinite_count}|min={values[0]:.9g}|max={values[-1]:.9g}|mean={mean:.9g}|stdPopulation={standard_deviation:.9g}|rms={rms:.9g}",
        "Quantiles|"
        + "|".join(
            f"p{int(fraction * 100):02d}={percentile(values, fraction):.9g}"
            for fraction in (0.50, 0.90, 0.95, 0.99)
        ),
        "Thresholds|" + ("|".join(threshold_fields) if threshold_fields else "none"),
        "ExpectedStats|"
        + ("|".join(expected_checks) if expected_checks else "not-provided")
        + f"|tolerance={stat_tolerance:.9g}",
    ]
    if viewer_contract is not None:
        if viewer_statistics is None:
            raise ValueError("Viewer statistics mode is required with a Viewer contract")
        lines.append(
            verify_viewer_contract(
                viewer_contract,
                viewer_statistics,
                units,
                count,
                values[0],
                values[-1],
                mean,
                standard_deviation,
                rms,
                stat_tolerance,
            )
        )
    return lines


def write_test_ply(path: Path, rows: list[tuple[float, ...]], scalar_name: str | None) -> None:
    properties = ["x", "y", "z"]
    if scalar_name:
        properties.append(scalar_name)
    header = [
        "ply",
        "format binary_little_endian 1.0",
        f"element vertex {len(rows)}",
        *(f"property float {name}" for name in properties),
        "end_header",
        "",
    ]
    with path.open("wb") as stream:
        stream.write("\n".join(header).encode("ascii"))
        record = struct.Struct("<" + "f" * len(properties))
        for row in rows:
            stream.write(record.pack(*row))


def self_test() -> None:
    with tempfile.TemporaryDirectory() as directory:
        root = Path(directory)
        source = root / "source.ply"
        candidate = root / "candidate.ply"
        write_test_ply(source, [(0, 0, 0), (1, 2, 3), (2, 4, 6)], None)
        write_test_ply(
            candidate,
            [(0, 0, 0, 0), (1, 2, 3, 1), (2, 4, 6, 2)],
            "distance",
        )
        viewer_contract = root / "viewer.txt"
        viewer_contract.write_text(
            "NominalActualUnsignedStatistics|count=3|min=0|max=2|mean=1"
            "|stdPopulation=0.816496580927726|rms=1.2909944487358056"
            "|unit=test-unit\n",
            encoding="utf-8",
        )
        lines = verify(
            source,
            candidate,
            "distance",
            "test-unit",
            [1.0],
            1.0,
            math.sqrt(2.0 / 3.0),
            1e-6,
            True,
            viewer_contract,
            "unsigned",
        )
        assert lines[0].startswith("CloudCompareC2MVerification|Pass|")
        assert "maxAbsError=0" in lines[4]
        assert "p50=1" in lines[6]
        assert lines[-1].startswith("ViewerContractParity|Pass|statistics=unsigned|")

        changed_candidate = root / "changed-candidate.ply"
        write_test_ply(
            changed_candidate,
            [(0.25, 0, 0, 0), (1, 2, 3, 1), (2, 4, 6, 2)],
            "distance",
        )
        try:
            verify(
                source,
                changed_candidate,
                "distance",
                "test-unit",
                [],
                None,
                None,
                1e-6,
                True,
                None,
                None,
            )
        except ValueError as error:
            assert "Candidate XYZ changed" in str(error)
        else:
            raise AssertionError("Changed candidate XYZ was accepted")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path)
    parser.add_argument("--candidate", type=Path)
    parser.add_argument("--scalar", default="scalar_C2M_absolute_distances")
    parser.add_argument("--units", default="model")
    parser.add_argument("--threshold", type=float, action="append", default=[])
    parser.add_argument("--expected-mean", type=float)
    parser.add_argument("--expected-std", type=float)
    parser.add_argument("--stat-tolerance", type=float, default=1e-6)
    parser.add_argument("--require-nonnegative", action="store_true")
    parser.add_argument("--viewer-contract", type=Path)
    parser.add_argument("--viewer-statistics", choices=("signed", "unsigned"))
    parser.add_argument("--report", type=Path)
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()

    if args.self_test:
        self_test()
        print("CloudCompareC2MVerifierSelfTest|Pass")
        return 0
    if not args.source or not args.candidate or not args.report:
        parser.error("--source, --candidate, and --report are required")
    if bool(args.viewer_contract) != bool(args.viewer_statistics):
        parser.error("--viewer-contract and --viewer-statistics must be used together")

    try:
        lines = verify(
            args.source,
            args.candidate,
            args.scalar,
            args.units,
            args.threshold,
            args.expected_mean,
            args.expected_std,
            args.stat_tolerance,
            args.require_nonnegative,
            args.viewer_contract,
            args.viewer_statistics,
        )
    except (OSError, ValueError, OverflowError, struct.error) as error:
        lines = [f"CloudCompareC2MVerification|Fail|error={str(error).replace('|', '/')}"]

    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(lines[0])
    return 0 if lines[0].split("|")[1] == "Pass" else 1


if __name__ == "__main__":
    raise SystemExit(main())
