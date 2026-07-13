#!/usr/bin/env python3
"""Independently verify an OpenVisionLab C3D viewer-frame ASCII PLY export."""

from __future__ import annotations

import argparse
from array import array
import hashlib
import math
from pathlib import Path
import struct
import sys


VIEWER_HORIZONTAL_SPAN = 10.0
VIEWER_HEIGHT_SCALE = 0.0006
MAX_COORDINATE_ERROR = 1e-6
MAX_ELEVATION_ANGLE_ERROR_DEGREES = 0.001


def float32(value: float) -> float:
    return struct.unpack("<f", struct.pack("<f", value))[0]


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def load_c3d(path: Path) -> tuple[int, int, array]:
    with path.open("rb") as stream:
        header = stream.read(8)
        if len(header) != 8:
            raise ValueError("C3D header is incomplete")
        width, height = struct.unpack("<ii", header)
        if width <= 0 or height <= 0:
            raise ValueError("C3D dimensions must be positive")
        payload = stream.read()

    expected_bytes = width * height * 4
    if len(payload) != expected_bytes:
        raise ValueError(f"C3D payload is {len(payload)} bytes; expected {expected_bytes}")

    samples = array("f")
    samples.frombytes(payload)
    if sys.byteorder != "little":
        samples.byteswap()
    return width, height, samples


def read_ply_header(stream) -> tuple[int, int]:
    if stream.readline().rstrip("\r\n") != "ply":
        raise ValueError("PLY magic is missing")
    if stream.readline().rstrip("\r\n") != "format ascii 1.0":
        raise ValueError("Only ASCII PLY 1.0 is supported")

    vertex_count = -1
    face_count = 0
    vertex_properties: list[str] = []
    reading_vertices = False
    for line in stream:
        line = line.rstrip("\r\n")
        if line == "end_header":
            break
        if line.startswith("element vertex "):
            vertex_count = int(line.removeprefix("element vertex "))
            reading_vertices = True
        elif line.startswith("element face "):
            face_count = int(line.removeprefix("element face "))
            reading_vertices = False
        elif reading_vertices and line.startswith("property "):
            vertex_properties.append(line.split()[-1])
    else:
        raise ValueError("PLY header has no end_header")

    if vertex_count < 0:
        raise ValueError("PLY vertex declaration is missing")
    if vertex_properties != ["x", "y", "z", "red", "green", "blue"]:
        raise ValueError(f"Unexpected PLY vertex properties: {vertex_properties}")
    return vertex_count, face_count


def height_color(value: float) -> tuple[int, int, int]:
    value = min(1.0, max(0.0, value))
    if value < 0.5:
        local = value / 0.5
        color = (0.05, 0.35 + 0.55 * local, 0.95 - 0.30 * local)
    else:
        high = (value - 0.5) / 0.5
        color = (0.05 + 0.95 * high, 0.90 - 0.20 * high, 0.65 - 0.55 * high)
    return tuple(int(min(1.0, max(0.0, channel)) * 255) for channel in color)


def parse_cell(value: str) -> tuple[int, int]:
    try:
        row_text, column_text = value.split(",", maxsplit=1)
        row, column = int(row_text), int(column_text)
    except ValueError as error:
        raise argparse.ArgumentTypeError("cell must be ROW,COLUMN") from error
    if row < 0 or column < 0:
        raise argparse.ArgumentTypeError("cell coordinates must be non-negative")
    return row, column


def point_pair_metrics(
    first: tuple[float, float, float],
    second: tuple[float, float, float],
) -> tuple[float, float, float, float]:
    dx = second[0] - first[0]
    dy = second[1] - first[1]
    dz = second[2] - first[2]
    width = math.hypot(dx, dz)
    distance = math.sqrt(dx * dx + dy * dy + dz * dz)
    angle_degrees = math.degrees(math.atan2(dy, width))
    return distance, width, dy, angle_degrees


def verify(
    source: Path,
    ply: Path,
    max_sampled_points: int,
    point_pair: tuple[tuple[int, int], tuple[int, int]] | None = None,
) -> list[str]:
    if max_sampled_points <= 0:
        raise ValueError("max sampled points must be positive")

    width, height, samples = load_c3d(source)
    valid_count = 0
    zero_count = 0
    minimum = math.inf
    maximum = -math.inf
    total = 0.0
    for value in samples:
        if not math.isfinite(value):
            continue
        if value == 0.0:
            zero_count += 1
            continue
        valid_count += 1
        minimum = min(minimum, value)
        maximum = max(maximum, value)
        total += value
    if valid_count == 0:
        raise ValueError("C3D has no finite non-zero samples")

    mean = total / valid_count
    sample_count = width * height
    stride = max(1, math.ceil(math.sqrt(sample_count / max_sampled_points)))
    horizontal_scale = float32(VIEWER_HORIZONTAL_SPAN / max(1, width - 1, height - 1))
    height_scale = float32(VIEWER_HEIGHT_SCALE)
    center_x = float32((width - 1) / 2.0)
    center_z = float32((height - 1) / 2.0)
    color_span = max(0.0001, float32(maximum - minimum))

    requested_cells = set(point_pair or ())
    for row, column in requested_cells:
        if row >= height or column >= width:
            raise ValueError(f"requested cell ({row},{column}) is outside the {height}x{width} grid")
        value = samples[row * width + column]
        if not math.isfinite(value) or value == 0.0:
            raise ValueError(f"requested cell ({row},{column}) is not a finite non-zero sample")
        if row % stride != 0 or column % stride != 0:
            raise ValueError(f"requested cell ({row},{column}) is not present at sampling stride {stride}")

    expected_point_count = sum(
        1
        for row in range(0, height, stride)
        for column in range(0, width, stride)
        if math.isfinite(samples[row * width + column]) and samples[row * width + column] != 0.0
    )

    max_coordinate_error = 0.0
    max_color_error = 0
    expected_requested_positions: dict[tuple[int, int], tuple[float, float, float]] = {}
    actual_requested_positions: dict[tuple[int, int], tuple[float, float, float]] = {}
    with ply.open("r", encoding="utf-8", newline="") as stream:
        declared_points, declared_faces = read_ply_header(stream)
        if declared_points != expected_point_count:
            raise ValueError(f"PLY declares {declared_points} vertices; expected {expected_point_count}")

        read_points = 0
        for row in range(0, height, stride):
            for column in range(0, width, stride):
                value = samples[row * width + column]
                if not math.isfinite(value) or value == 0.0:
                    continue

                fields = stream.readline().split()
                if len(fields) != 6:
                    raise ValueError(f"PLY vertex {read_points} must contain XYZ and RGB")
                actual_position = tuple(float(field) for field in fields[:3])
                actual_color = tuple(int(field) for field in fields[3:])

                expected_position = (
                    float32(float32(column - center_x) * horizontal_scale),
                    float32((value - mean) * height_scale),
                    float32(float32(row - center_z) * horizontal_scale),
                )
                height_scalar = min(1.0, max(0.0, float32(value - minimum) / color_span))
                expected_color = height_color(height_scalar)

                cell = (row, column)
                if cell in requested_cells:
                    expected_requested_positions[cell] = expected_position
                    actual_requested_positions[cell] = actual_position

                max_coordinate_error = max(
                    max_coordinate_error,
                    *(abs(actual - expected) for actual, expected in zip(actual_position, expected_position)),
                )
                max_color_error = max(
                    max_color_error,
                    *(abs(actual - expected) for actual, expected in zip(actual_color, expected_color)),
                )
                read_points += 1

        for face_index in range(declared_faces):
            fields = stream.readline().split()
            if len(fields) != 4 or fields[0] != "3":
                raise ValueError(f"PLY face {face_index} is not a triangle")
        if stream.read().strip():
            raise ValueError("PLY has data after the declared vertices and faces")

    point_pair_lines: list[str] = []
    point_pair_passed = True
    if point_pair is not None:
        first_cell, second_cell = point_pair
        expected_metrics = point_pair_metrics(
            expected_requested_positions[first_cell],
            expected_requested_positions[second_cell],
        )
        actual_metrics = point_pair_metrics(
            actual_requested_positions[first_cell],
            actual_requested_positions[second_cell],
        )
        metric_errors = tuple(
            abs(actual - expected) for actual, expected in zip(actual_metrics, expected_metrics)
        )
        maximum_distance_error = 2.0 * math.sqrt(3.0) * MAX_COORDINATE_ERROR
        maximum_width_error = 2.0 * math.sqrt(2.0) * MAX_COORDINATE_ERROR
        maximum_height_delta_error = 2.0 * MAX_COORDINATE_ERROR
        maximum_raw_height_delta_error = maximum_height_delta_error / abs(height_scale)
        expected_raw_height_delta = (
            samples[second_cell[0] * width + second_cell[1]]
            - samples[first_cell[0] * width + first_cell[1]]
        )
        actual_raw_height_delta = actual_metrics[2] / height_scale
        raw_height_delta_error = abs(actual_raw_height_delta - expected_raw_height_delta)
        point_pair_passed = (
            metric_errors[0] <= maximum_distance_error
            and metric_errors[1] <= maximum_width_error
            and metric_errors[2] <= maximum_height_delta_error
            and metric_errors[3] <= MAX_ELEVATION_ANGLE_ERROR_DEGREES
            and raw_height_delta_error <= maximum_raw_height_delta_error
        )
        pair_status = "Pass" if point_pair_passed else "Fail"
        point_pair_lines = [
            f"PointPair|{pair_status}|first=({first_cell[0]},{first_cell[1]})|second=({second_cell[0]},{second_cell[1]})|physicalScale=Unverified",
            f"PointPairReference|distance={expected_metrics[0]:.15g}|widthXZ={expected_metrics[1]:.15g}|modelYDelta={expected_metrics[2]:.15g}|rawHeightDelta={expected_raw_height_delta:.15g}|elevationAngleDegrees={expected_metrics[3]:.15g}",
            f"PointPairPLY|distance={actual_metrics[0]:.15g}|widthXZ={actual_metrics[1]:.15g}|modelYDelta={actual_metrics[2]:.15g}|rawHeightDelta={actual_raw_height_delta:.15g}|elevationAngleDegrees={actual_metrics[3]:.15g}",
            f"PointPairError|distance={metric_errors[0]:.9g}|widthXZ={metric_errors[1]:.9g}|modelYDelta={metric_errors[2]:.9g}|rawHeightDelta={raw_height_delta_error:.9g}|elevationAngleDegrees={metric_errors[3]:.9g}",
        ]

    passed = (
        read_points == expected_point_count
        and max_coordinate_error <= MAX_COORDINATE_ERROR
        and max_color_error == 0
        and point_pair_passed
    )
    status = "Pass" if passed else "Fail"
    return [
        f"IndependentC3DPlyVerification|{status}|implementation=python-stdlib|physicalScale=Unverified",
        f"Source|path={source.resolve()}|sha256={sha256(source)}|width={width}|height={height}|valid={valid_count}|zero={zero_count}",
        f"Sampling|budget={max_sampled_points}|stride={stride}|expectedPoints={expected_point_count}|readPoints={read_points}",
        f"Mapping|frame=right-handed-y-up|x=column|y=raw-height|z=row|horizontalScale={horizontal_scale:.9g}|heightScale={height_scale:.9g}|meanRaw={mean:.15g}",
        f"PLY|path={ply.resolve()}|sha256={sha256(ply)}|declaredPoints={declared_points}|declaredFaces={declared_faces}",
        f"Comparison|maxCoordinateError={max_coordinate_error:.9g}|maxColorChannelError={max_color_error}|tolerance={MAX_COORDINATE_ERROR:.9g}",
        *point_pair_lines,
    ]


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", required=True, type=Path)
    parser.add_argument("--ply", required=True, type=Path)
    parser.add_argument("--report", required=True, type=Path)
    parser.add_argument("--max-sampled-points", required=True, type=int)
    parser.add_argument("--first-cell", type=parse_cell)
    parser.add_argument("--second-cell", type=parse_cell)
    args = parser.parse_args()

    try:
        if (args.first_cell is None) != (args.second_cell is None):
            raise ValueError("--first-cell and --second-cell must be provided together")
        point_pair = (
            (args.first_cell, args.second_cell) if args.first_cell is not None else None
        )
        lines = verify(args.source, args.ply, args.max_sampled_points, point_pair)
    except (OSError, ValueError, OverflowError, struct.error) as error:
        lines = [f"IndependentC3DPlyVerification|Fail|error={str(error).replace('|', '/')}"]

    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(lines[0])
    return 0 if lines[0].startswith("IndependentC3DPlyVerification|Pass|") else 1


if __name__ == "__main__":
    raise SystemExit(main())
