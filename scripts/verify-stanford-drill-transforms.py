#!/usr/bin/env python3
"""Create an independent Stanford Drill transform reference from the fixed scans."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path
import struct
import subprocess
import tempfile


CONF_SHA256 = "95686B12D5CFF5CC58599D211D81047B92F26186F42EB4AC57CF833FA0A2352F"
ARCHIVE_SHA256 = "2DA2ACABA36903A9893C920C11506E1BD01C531BDF738D037BC505B313DF5E1B"
CLOUDCOMPARE_TOLERANCE = 1e-7
EXPECTED_SCANS = {
    "drill_1.6mm_0_cyb.ply": (272_995, "C4277DC7DDDC63B8977198A60A6B974264F70A0E372B85E3D4E5532192B7B867", 4_223),
    "drill_1.6mm_30_cyb.ply": (271_555, "D3387A9CAC7761B9810CAF9BE8A39E209D7A4E9DAEB66E0D81B2F0D897FF3662", 4_133),
    "drill_1.6mm_60_cyb.ply": (272_947, "FDAFC2324B2745DBA3E14D9DAB926F6D6D067C2263E76EC76A9E274A18C7AE6D", 4_220),
    "drill_1.6mm_90_cyb.ply": (272_259, "CC8E1C8EEA0359E50D87222A62DB7158C7AC729DAAF94760927132E49848D6BD", 4_177),
    "drill_1.6mm_120_cyb.ply": (274_755, "C3AC7BBFE3244A04B8A11AC6BEF51CAFF88B612498CDCB2ED4135DAE4977C83E", 4_333),
    "drill_1.6mm_150_cyb.ply": (273_587, "4BFAE95CE917F0E778A9D6684EC6081EDD0B8C1ECB53BCCE66913DD2AACF1A05", 4_260),
    "drill_1.6mm_180_cyb.ply": (272_835, "FB40DBE9F3B38F0BE13CD83286BA8B2509CC6A7D6EF3352E4ECE16E26794A595", 4_213),
    "drill_1.6mm_210_cyb.ply": (272_819, "4D72AEF373D318B74F1B1703ED5BF771C8299CE7F7D116DF1569DE253F5B07AC", 4_212),
    "drill_1.6mm_240_cyb.ply": (272_835, "B540280F1D28B3BE1E965FEE5E6918BB55D8239D006B067FB17DBBE33777EDCF", 4_213),
    "drill_1.6mm_270_cyb.ply": (274_131, "B2BE2E22A0C77D87947C13199E8F10A40D7036AFCB5617F7F87C7F7BA630A59E", 4_294),
    "drill_1.6mm_300_cyb.ply": (272_803, "7FE9B51FF43AF4423361B5984C8B47D15F5C7E327AFF1C29F21276854FC60746", 4_211),
    "drill_1.6mm_330_cyb.ply": (271_891, "CC689E7D36871D410808477732BC8DCFCD52B21008DC4CF6CBC362456A68E1EF", 4_154),
}


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def finite_float(text: str, context: str) -> float:
    try:
        value = float(text)
    except ValueError as exc:
        raise ValueError(f"{context} is not numeric: {text}") from exc
    if not math.isfinite(value):
        raise ValueError(f"{context} is not finite")
    return value


def parse_conf(path: Path) -> tuple[list[float], list[dict]]:
    camera: list[float] | None = None
    scans: list[dict] = []
    names: set[str] = set()
    for line_number, raw_line in enumerate(path.read_text(encoding="ascii").splitlines(), 1):
        line = raw_line.strip()
        if not line:
            continue
        fields = line.split()
        if fields[0] == "camera":
            if camera is not None or len(fields) != 8:
                raise ValueError(f"Invalid camera declaration at line {line_number}")
            camera = [finite_float(value, f"camera line {line_number}") for value in fields[1:]]
            continue
        if fields[0] != "bmesh" or len(fields) != 9:
            raise ValueError(f"Unsupported configuration line {line_number}: {line}")
        name = fields[1]
        if Path(name).name != name or not name.endswith(".ply") or name in names:
            raise ValueError(f"Invalid or duplicate scan name at line {line_number}: {name}")
        values = [finite_float(value, f"bmesh line {line_number}") for value in fields[2:]]
        quaternion = values[3:]
        norm_squared = sum(value * value for value in quaternion)
        if norm_squared <= 1e-24:
            raise ValueError(f"Zero-length quaternion at line {line_number}")
        names.add(name)
        scans.append(
            {
                "fileName": name,
                "translation": values[:3],
                "quaternionXyzw": quaternion,
            }
        )
    if camera is None:
        raise ValueError("Configuration has no camera declaration")
    if not scans:
        raise ValueError("Configuration has no bmesh declarations")
    return camera, scans


def read_range_ply(path: Path) -> dict:
    with path.open("rb") as stream:
        if stream.readline().rstrip(b"\r\n") != b"ply":
            raise ValueError(f"PLY magic is missing: {path.name}")
        if stream.readline().rstrip(b"\r\n") != b"format binary_big_endian 1.0":
            raise ValueError(f"Only binary big-endian PLY 1.0 is supported: {path.name}")

        elements: dict[str, int] = {}
        properties: dict[str, list[str]] = {}
        object_info: dict[str, str] = {}
        current_element = ""
        for raw_line in stream:
            try:
                line = raw_line.decode("ascii").strip()
            except UnicodeDecodeError as exc:
                raise ValueError(f"Non-ASCII PLY header: {path.name}") from exc
            if line == "end_header":
                break
            fields = line.split()
            if not fields:
                continue
            if fields[0] == "element" and len(fields) == 3:
                current_element = fields[1]
                if current_element in elements:
                    raise ValueError(f"Duplicate PLY element {current_element}: {path.name}")
                elements[current_element] = int(fields[2])
                properties[current_element] = []
            elif fields[0] == "property" and current_element:
                properties[current_element].append(line)
            elif fields[0] == "obj_info" and len(fields) >= 3:
                object_info[fields[1]] = " ".join(fields[2:])
            elif fields[0] != "comment":
                raise ValueError(f"Unsupported PLY header line in {path.name}: {line}")
        else:
            raise ValueError(f"PLY header has no end_header: {path.name}")

        if elements != {"vertex": elements.get("vertex", -1), "range_grid": elements.get("range_grid", -1)}:
            raise ValueError(f"PLY must contain only vertex and range_grid elements: {path.name}")
        if properties.get("vertex") != ["property float x", "property float y", "property float z"]:
            raise ValueError(f"PLY vertex contract differs from x/y/z float: {path.name}")
        if properties.get("range_grid") != ["property list uchar int vertex_indices"]:
            raise ValueError(f"PLY range_grid contract is unsupported: {path.name}")

        vertex_count = elements["vertex"]
        range_count = elements["range_grid"]
        columns = int(object_info.get("num_cols", "-1"))
        rows = int(object_info.get("num_rows", "-1"))
        if vertex_count <= 0 or columns <= 0 or rows <= 0 or range_count != columns * rows:
            raise ValueError(f"Invalid PLY dimensions: {path.name}")

        payload = stream.read()

    vertex_bytes = vertex_count * 12
    if len(payload) < vertex_bytes:
        raise ValueError(f"PLY vertex payload is truncated: {path.name}")
    points = [tuple(row) for row in struct.iter_unpack(">fff", payload[:vertex_bytes])]
    if any(not all(math.isfinite(value) for value in point) for point in points):
        raise ValueError(f"PLY contains non-finite vertices: {path.name}")

    offset = vertex_bytes
    referenced = bytearray(vertex_count)
    for _ in range(range_count):
        if offset >= len(payload):
            raise ValueError(f"PLY range_grid payload is truncated: {path.name}")
        list_count = payload[offset]
        offset += 1
        if list_count > 1:
            raise ValueError(f"PLY range_grid cell contains multiple indices: {path.name}")
        if list_count == 1:
            if offset + 4 > len(payload):
                raise ValueError(f"PLY range_grid index is truncated: {path.name}")
            index = struct.unpack_from(">i", payload, offset)[0]
            offset += 4
            if index < 0 or index >= vertex_count or referenced[index]:
                raise ValueError(f"PLY range_grid index is invalid or duplicated: {path.name}")
            referenced[index] = 1
    if offset != len(payload):
        raise ValueError(f"PLY has trailing payload bytes: {path.name}")
    if sum(referenced) != vertex_count:
        raise ValueError(f"PLY range_grid does not reference every vertex exactly once: {path.name}")

    return {
        "points": points,
        "vertexCount": vertex_count,
        "rangeGridCount": range_count,
        "columns": columns,
        "rows": rows,
    }


def read_point_only_ply(path: Path) -> list[tuple[float, float, float]]:
    with path.open("rb") as stream:
        if stream.readline().rstrip(b"\r\n") != b"ply":
            raise ValueError(f"PLY magic is missing: {path.name}")
        if stream.readline().rstrip(b"\r\n") != b"format binary_little_endian 1.0":
            raise ValueError(f"CloudCompare PLY must be binary little-endian 1.0: {path.name}")

        vertex_count = -1
        properties: list[str] = []
        current_element = ""
        for raw_line in stream:
            line = raw_line.decode("ascii").strip()
            if line == "end_header":
                break
            fields = line.split()
            if not fields:
                continue
            if fields[0] == "element" and len(fields) == 3:
                current_element = fields[1]
                count = int(fields[2])
                if current_element == "vertex":
                    vertex_count = count
                elif count != 0:
                    raise ValueError(f"CloudCompare PLY has a non-empty unsupported element: {current_element}")
            elif fields[0] == "property" and current_element == "vertex":
                properties.append(line)
            elif fields[0] not in {"comment", "obj_info"}:
                raise ValueError(f"Unsupported CloudCompare PLY header line: {line}")
        else:
            raise ValueError(f"PLY header has no end_header: {path.name}")

        if vertex_count <= 0 or properties != ["property float x", "property float y", "property float z"]:
            raise ValueError(f"CloudCompare PLY vertex contract differs: {path.name}")
        payload = stream.read()

    if len(payload) != vertex_count * 12:
        raise ValueError(f"CloudCompare PLY payload length differs: {path.name}")
    points = [tuple(row) for row in struct.iter_unpack("<fff", payload)]
    if any(not all(math.isfinite(value) for value in point) for point in points):
        raise ValueError(f"CloudCompare PLY contains non-finite vertices: {path.name}")
    return points


def rotation_matrix(quaternion: list[float]) -> tuple[tuple[float, float, float], ...]:
    x, y, z, w = quaternion
    scale = 2.0 / (x * x + y * y + z * z + w * w)
    xs, ys, zs = x * scale, y * scale, z * scale
    wx, wy, wz = w * xs, w * ys, w * zs
    xx, xy, xz = x * xs, x * ys, x * zs
    yy, yz, zz = y * ys, y * zs, z * zs
    standard = (
        (1.0 - (yy + zz), xy - wz, xz + wy),
        (xy + wz, 1.0 - (xx + zz), yz - wx),
        (xz - wy, yz + wx, 1.0 - (xx + yy)),
    )
    return tuple(tuple(standard[column][row] for column in range(3)) for row in range(3))


def transform_point(
    point: tuple[float, float, float],
    matrix: tuple[tuple[float, float, float], ...],
    translation: list[float],
) -> tuple[float, float, float]:
    return tuple(
        sum(matrix[row][column] * point[column] for column in range(3)) + translation[row]
        for row in range(3)
    )


def determinant(matrix: tuple[tuple[float, float, float], ...]) -> float:
    return (
        matrix[0][0] * (matrix[1][1] * matrix[2][2] - matrix[1][2] * matrix[2][1])
        - matrix[0][1] * (matrix[1][0] * matrix[2][2] - matrix[1][2] * matrix[2][0])
        + matrix[0][2] * (matrix[1][0] * matrix[2][1] - matrix[1][1] * matrix[2][0])
    )


def orthogonality_error(matrix: tuple[tuple[float, float, float], ...]) -> float:
    return max(
        abs(
            sum(matrix[row][axis] * matrix[column][axis] for axis in range(3))
            - (1.0 if row == column else 0.0)
        )
        for row in range(3)
        for column in range(3)
    )


class Statistics:
    def __init__(self) -> None:
        self.count = 0
        self.minimum = [math.inf, math.inf, math.inf]
        self.maximum = [-math.inf, -math.inf, -math.inf]
        self.sum = [0.0, 0.0, 0.0]
        self.sum_squares = [0.0, 0.0, 0.0]
        self.ordered_weighted_sum = [0.0, 0.0, 0.0]

    def add(self, point: tuple[float, float, float], weight: int) -> None:
        self.count += 1
        for axis, value in enumerate(point):
            self.minimum[axis] = min(self.minimum[axis], value)
            self.maximum[axis] = max(self.maximum[axis], value)
            self.sum[axis] += value
            self.sum_squares[axis] += value * value
            self.ordered_weighted_sum[axis] += weight * value

    def to_json(self) -> dict:
        if self.count == 0:
            raise ValueError("Statistics require at least one point")
        return {
            "pointCount": self.count,
            "minimum": self.minimum,
            "maximum": self.maximum,
            "centroid": [value / self.count for value in self.sum],
            "sum": self.sum,
            "sumSquares": self.sum_squares,
            "orderedWeightedSum": self.ordered_weighted_sum,
        }


def build_reference(conf_path: Path) -> dict:
    conf_path = conf_path.resolve()
    if sha256(conf_path) != CONF_SHA256:
        raise ValueError("Configuration SHA-256 does not match the fixed Stanford Drill source")
    camera, configured_scans = parse_conf(conf_path)
    if [item["fileName"] for item in configured_scans] != list(EXPECTED_SCANS):
        raise ValueError("Configuration scan order or membership differs from the fixed Stanford Drill source")

    aggregate = Statistics()
    scans: list[dict] = []
    global_index = 0
    point_reference_count = 0
    for configured in configured_scans:
        name = configured["fileName"]
        path = conf_path.parent / name
        expected_bytes, expected_hash, expected_points = EXPECTED_SCANS[name]
        if not path.is_file() or path.stat().st_size != expected_bytes or sha256(path) != expected_hash:
            raise ValueError(f"Source identity differs from the fixed Stanford scan: {name}")
        parsed = read_range_ply(path)
        if parsed["vertexCount"] != expected_points or parsed["rangeGridCount"] != 204_800:
            raise ValueError(f"Source dimensions differ from the fixed Stanford scan: {name}")

        matrix = rotation_matrix(configured["quaternionXyzw"])
        raw_statistics = Statistics()
        transformed_statistics = Statistics()
        transformed_points: list[tuple[float, float, float]] = []
        for local_index, point in enumerate(parsed["points"]):
            transformed = transform_point(point, matrix, configured["translation"])
            raw_statistics.add(point, local_index + 1)
            transformed_statistics.add(transformed, local_index + 1)
            aggregate.add(transformed, global_index + 1)
            transformed_points.append(transformed)
            global_index += 1

        checkpoint_indices = sorted({0, parsed["vertexCount"] // 2, parsed["vertexCount"] - 1})
        checkpoints = [
            {
                "index": index,
                "source": list(parsed["points"][index]),
                "transformed": list(transformed_points[index]),
            }
            for index in checkpoint_indices
        ]
        point_reference_count += len(checkpoints)
        quaternion = configured["quaternionXyzw"]
        quaternion_norm = math.sqrt(sum(value * value for value in quaternion))
        angle_degrees = math.degrees(2.0 * math.acos(min(1.0, abs(quaternion[3]) / quaternion_norm)))
        scans.append(
            {
                "fileName": name,
                "byteLength": expected_bytes,
                "sha256": expected_hash,
                "vertexCount": parsed["vertexCount"],
                "rangeGridCount": parsed["rangeGridCount"],
                "columns": parsed["columns"],
                "rows": parsed["rows"],
                "translation": configured["translation"],
                "quaternionXyzw": quaternion,
                "quaternionNorm": quaternion_norm,
                "rotationAngleDegrees": angle_degrees,
                "rotationDeterminant": determinant(matrix),
                "rotationOrthogonalityMaxError": orthogonality_error(matrix),
                "sourceStatistics": raw_statistics.to_json(),
                "transformedStatistics": transformed_statistics.to_json(),
                "checkpoints": checkpoints,
            }
        )

    if aggregate.count != 50_643 or point_reference_count != 36:
        raise ValueError("Fixed Stanford Drill point totals differ from the expected source")
    return {
        "schemaVersion": "1.0",
        "dataset": "Stanford Drill 1.6mm Cyberware range scans",
        "contract": {
            "quaternionOrder": "x,y,z,w",
            "application": "transpose(ShoemakeQuaternionMatrix(q))*point+translation",
            "numericPrecision": "binary-float32 input decoded to float64; float64 transform and accumulation",
            "cameraApplied": False,
            "guide": "https://graphics.stanford.edu/software/vrip/guide/",
            "sourceArchive": "https://graphics.stanford.edu/data/3Dscanrep/drill.tar.gz",
        },
        "provenance": {
            "archiveSha256": ARCHIVE_SHA256,
            "configurationFileName": conf_path.name,
            "configurationByteLength": conf_path.stat().st_size,
            "configurationSha256": CONF_SHA256,
            "generatorFileName": Path(__file__).name,
            "generatorVersion": "1.0",
        },
        "camera": camera,
        "scanCount": len(scans),
        "pointReferenceCount": point_reference_count,
        "aggregateTransformedStatistics": aggregate.to_json(),
        "scans": scans,
    }


def format_point(values: list[float]) -> str:
    return f"({values[0]:.17g},{values[1]:.17g},{values[2]:.17g})"


def write_outputs(
    reference: dict,
    reference_path: Path,
    report_path: Path,
    matrix_directory: Path | None,
) -> None:
    reference_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.parent.mkdir(parents=True, exist_ok=True)
    reference_path.write_text(
        json.dumps(reference, indent=2, sort_keys=True, ensure_ascii=True) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    aggregate = reference["aggregateTransformedStatistics"]
    lines = [
        (
            "StanfordTransformReference|Pass"
            f"|scans={reference['scanCount']}"
            f"|points={aggregate['pointCount']}"
            f"|pointReferences={reference['pointReferenceCount']}"
        ),
        (
            "TransformContract|quaternion=x,y,z,w"
            "|application=transpose(ShoemakeQuaternionMatrix(q))*point+translation"
            "|cameraApplied=False|input=binary-big-endian-float32|calculation=float64"
        ),
        (
            f"Provenance|archiveSha256={reference['provenance']['archiveSha256']}"
            f"|confBytes={reference['provenance']['configurationByteLength']}"
            f"|confSha256={reference['provenance']['configurationSha256']}"
            f"|referenceSha256={sha256(reference_path)}"
        ),
        (
            f"Aggregate|points={aggregate['pointCount']}"
            f"|minimum={format_point(aggregate['minimum'])}"
            f"|maximum={format_point(aggregate['maximum'])}"
            f"|centroid={format_point(aggregate['centroid'])}"
            f"|sum={format_point(aggregate['sum'])}"
            f"|orderedWeightedSum={format_point(aggregate['orderedWeightedSum'])}"
        ),
    ]
    for scan in reference["scans"]:
        transformed = scan["transformedStatistics"]
        lines.append(
            f"Scan|file={scan['fileName']}|sha256={scan['sha256']}|points={scan['vertexCount']}"
            f"|angleDegrees={scan['rotationAngleDegrees']:.17g}"
            f"|determinant={scan['rotationDeterminant']:.17g}"
            f"|orthogonalityMaxError={scan['rotationOrthogonalityMaxError']:.17g}"
            f"|minimum={format_point(transformed['minimum'])}"
            f"|maximum={format_point(transformed['maximum'])}"
        )
    report_path.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")

    if matrix_directory is not None:
        matrix_directory.mkdir(parents=True, exist_ok=True)
        for scan in reference["scans"]:
            matrix = rotation_matrix(scan["quaternionXyzw"])
            translation = scan["translation"]
            matrix_lines = [
                " ".join(f"{matrix[row][column]:.17g}" for column in range(3))
                + f" {translation[row]:.17g}"
                for row in range(3)
            ]
            matrix_lines.append("0 0 0 1")
            matrix_path = matrix_directory / f"{Path(scan['fileName']).stem}.matrix.txt"
            matrix_path.write_text("\n".join(matrix_lines) + "\n", encoding="ascii", newline="\n")


def verify_cloudcompare_outputs(
    reference: dict,
    conf_path: Path,
    output_directory: Path,
    report_path: Path,
) -> None:
    aggregate = Statistics()
    total_points = 0
    maximum_coordinate_delta = 0.0
    lines: list[str] = []
    for scan in reference["scans"]:
        source = read_range_ply(conf_path.parent / scan["fileName"])["points"]
        candidate_path = output_directory / f"{Path(scan['fileName']).stem}.cloudcompare.ply"
        candidate = read_point_only_ply(candidate_path)
        if len(candidate) != len(source):
            raise ValueError(f"CloudCompare point count differs: {candidate_path.name}")

        matrix = rotation_matrix(scan["quaternionXyzw"])
        scan_maximum_delta = 0.0
        for point_index, (source_point, candidate_point) in enumerate(zip(source, candidate)):
            expected = transform_point(source_point, matrix, scan["translation"])
            delta = max(abs(actual - wanted) for actual, wanted in zip(candidate_point, expected))
            scan_maximum_delta = max(scan_maximum_delta, delta)
            maximum_coordinate_delta = max(maximum_coordinate_delta, delta)
            if delta > CLOUDCOMPARE_TOLERANCE:
                raise ValueError(
                    f"CloudCompare coordinate delta {delta:.17g} exceeds {CLOUDCOMPARE_TOLERANCE:.9g} "
                    f"at {scan['fileName']} point {point_index}"
                )
            aggregate.add(candidate_point, total_points + 1)
            total_points += 1
        lines.append(
            f"Scan|file={scan['fileName']}|points={len(candidate)}"
            f"|candidateBytes={candidate_path.stat().st_size}|candidateSha256={sha256(candidate_path)}"
            f"|maxCoordinateDelta={scan_maximum_delta:.17g}"
        )

    expected_aggregate = reference["aggregateTransformedStatistics"]
    candidate_aggregate = aggregate.to_json()
    bounds_delta = max(
        abs(actual - expected)
        for field in ("minimum", "maximum")
        for actual, expected in zip(candidate_aggregate[field], expected_aggregate[field])
    )
    centroid_delta = max(
        abs(actual - expected)
        for actual, expected in zip(candidate_aggregate["centroid"], expected_aggregate["centroid"])
    )
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_lines = [
        (
            "CloudCompareStanfordTransformParity|Pass"
            f"|scans={reference['scanCount']}|points={total_points}"
            f"|maxCoordinateDelta={maximum_coordinate_delta:.17g}"
            f"|tolerance={CLOUDCOMPARE_TOLERANCE:.9g}"
        ),
        (
            f"Aggregate|boundsMaxDelta={bounds_delta:.17g}|centroidMaxDelta={centroid_delta:.17g}"
            f"|minimum={format_point(candidate_aggregate['minimum'])}"
            f"|maximum={format_point(candidate_aggregate['maximum'])}"
            f"|centroid={format_point(candidate_aggregate['centroid'])}"
        ),
        *lines,
    ]
    report_path.write_text("\n".join(report_lines) + "\n", encoding="utf-8", newline="\n")


def run_cloudcompare(
    reference: dict,
    conf_path: Path,
    executable: Path,
    matrix_directory: Path,
    output_directory: Path,
) -> None:
    executable = executable.resolve()
    if not executable.is_file():
        raise ValueError(f"CloudCompare executable is missing: {executable}")
    output_directory.mkdir(parents=True, exist_ok=True)
    for scan in reference["scans"]:
        stem = Path(scan["fileName"]).stem
        input_path = conf_path.parent / scan["fileName"]
        matrix_path = matrix_directory / f"{stem}.matrix.txt"
        output_path = output_directory / f"{stem}.cloudcompare.ply"
        log_path = output_directory / f"{stem}.log"
        if output_path.exists():
            output_path.unlink()
        completed = subprocess.run(
            [
                str(executable),
                "-SILENT",
                "-LOG_FILE",
                str(log_path.resolve()),
                "-AUTO_SAVE",
                "OFF",
                "-O",
                str(input_path.resolve()),
                "-APPLY_TRANS",
                str(matrix_path.resolve()),
                "-C_EXPORT_FMT",
                "PLY",
                "-PLY_EXPORT_FMT",
                "BINARY_LE",
                "-SAVE_CLOUDS",
                "FILE",
                str(output_path.resolve()),
            ],
            check=False,
        )
        if completed.returncode != 0 or not output_path.is_file():
            raise ValueError(
                f"CloudCompare failed for {scan['fileName']}: exit={completed.returncode}, output={output_path.is_file()}"
            )


def run_self_test() -> None:
    identity = rotation_matrix([0.0, 0.0, 0.0, 1.0])
    assert transform_point((1.0, 2.0, 3.0), identity, [4.0, 5.0, 6.0]) == (5.0, 7.0, 9.0)

    half = math.sqrt(0.5)
    inverse_quarter_turn = rotation_matrix([0.0, 0.0, half, half])
    rotated = transform_point((1.0, 0.0, 0.0), inverse_quarter_turn, [1.0, 2.0, 3.0])
    assert max(abs(actual - expected) for actual, expected in zip(rotated, (1.0, 1.0, 3.0))) < 1e-15
    scaled = rotation_matrix([0.0, 0.0, half * 3.0, half * 3.0])
    assert max(abs(scaled[row][column] - inverse_quarter_turn[row][column]) for row in range(3) for column in range(3)) < 1e-15

    with tempfile.TemporaryDirectory(prefix="OpenVisionLab.StanfordTransform.") as directory:
        path = Path(directory) / "synthetic.ply"
        header = (
            "ply\nformat binary_big_endian 1.0\n"
            "obj_info num_cols 2\nobj_info num_rows 1\n"
            "element vertex 2\nproperty float x\nproperty float y\nproperty float z\n"
            "element range_grid 2\nproperty list uchar int vertex_indices\nend_header\n"
        ).encode("ascii")
        payload = struct.pack(">ffffffBiBi", 1.0, 2.0, 3.0, -1.0, -2.0, -3.0, 1, 0, 1, 1)
        path.write_bytes(header + payload)
        parsed = read_range_ply(path)
        assert parsed["points"] == [(1.0, 2.0, 3.0), (-1.0, -2.0, -3.0)]
        path.write_bytes(header + payload + b"\x00")
        try:
            read_range_ply(path)
        except ValueError as exc:
            assert "trailing" in str(exc)
        else:
            raise AssertionError("Trailing PLY payload was accepted")

        point_path = Path(directory) / "point-only.ply"
        point_header = (
            "ply\nformat binary_little_endian 1.0\n"
            "element vertex 2\nproperty float x\nproperty float y\nproperty float z\nend_header\n"
        ).encode("ascii")
        point_path.write_bytes(point_header + struct.pack("<ffffff", 1.0, 2.0, 3.0, -1.0, -2.0, -3.0))
        assert read_point_only_ply(point_path) == [(1.0, 2.0, 3.0), (-1.0, -2.0, -3.0)]
    print("Stanford transform Python self-test: Pass (6/6)")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--conf", type=Path)
    parser.add_argument("--reference", type=Path)
    parser.add_argument("--report", type=Path)
    parser.add_argument("--cloudcompare-matrix-dir", type=Path)
    parser.add_argument("--cloudcompare-exe", type=Path)
    parser.add_argument("--cloudcompare-output-dir", type=Path)
    parser.add_argument("--cloudcompare-report", type=Path)
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()

    if args.self_test:
        run_self_test()
    if args.conf is None:
        if args.self_test:
            return 0
        parser.error("--conf is required unless --self-test is used")
    if args.reference is None or args.report is None:
        parser.error("--reference and --report are required with --conf")

    reference = build_reference(args.conf)
    write_outputs(reference, args.reference, args.report, args.cloudcompare_matrix_dir)
    if args.cloudcompare_exe is not None:
        if args.cloudcompare_matrix_dir is None or args.cloudcompare_output_dir is None:
            parser.error(
                "--cloudcompare-matrix-dir and --cloudcompare-output-dir are required with --cloudcompare-exe"
            )
        run_cloudcompare(
            reference,
            args.conf.resolve(),
            args.cloudcompare_exe,
            args.cloudcompare_matrix_dir,
            args.cloudcompare_output_dir,
        )
    if args.cloudcompare_output_dir is not None:
        if args.cloudcompare_report is None:
            parser.error("--cloudcompare-report is required with --cloudcompare-output-dir")
        try:
            verify_cloudcompare_outputs(
                reference,
                args.conf.resolve(),
                args.cloudcompare_output_dir,
                args.cloudcompare_report,
            )
        except (OSError, ValueError) as error:
            args.cloudcompare_report.parent.mkdir(parents=True, exist_ok=True)
            cause = str(error).replace("|", "/").replace("\r", " ").replace("\n", " ")
            args.cloudcompare_report.write_text(
                f"CloudCompareStanfordTransformParity|Fail|cause={cause}\n",
                encoding="utf-8",
                newline="\n",
            )
            raise
        print(f"CloudCompare Stanford transform parity: Pass ({reference['scanCount']} scans)")
    print(
        "Stanford transform reference: Pass "
        f"({reference['scanCount']} scans, {reference['aggregateTransformedStatistics']['pointCount']} points)"
    )
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError) as error:
        print(f"Stanford transform reference: Fail ({error})")
        raise SystemExit(5) from error
