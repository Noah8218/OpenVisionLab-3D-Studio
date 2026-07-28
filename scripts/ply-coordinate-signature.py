#!/usr/bin/env python3
"""Create or compare deterministic coordinate signatures for ASCII PLY files."""

from __future__ import annotations

import argparse
import hashlib
import math
from pathlib import Path


DEFAULT_TOLERANCE = 1e-6


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def quantize(value: float, tolerance: float) -> int:
    return int(round(value / tolerance))


def format_tuple(values: tuple[float, float, float]) -> str:
    return f"({values[0]:.9g},{values[1]:.9g},{values[2]:.9g})"


def read_header(stream) -> tuple[int, int, list[str]]:
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
        elif line.startswith("element "):
            reading_vertices = False
        elif reading_vertices and line.startswith("property "):
            vertex_properties.append(line.split()[-1])
    else:
        raise ValueError("PLY header has no end_header")

    if vertex_count < 0:
        raise ValueError("PLY vertex declaration is missing")
    required = ["x", "y", "z"]
    if vertex_properties[:3] != required:
        raise ValueError(f"PLY vertex properties must start with {required}; found {vertex_properties}")
    return vertex_count, face_count, vertex_properties


class Signature:
    def __init__(self, path: Path, tolerance: float) -> None:
        self.path = path
        self.tolerance = tolerance
        self.file_sha256 = sha256(path)
        self.vertex_count = 0
        self.face_count = 0
        self.properties: list[str] = []
        self.min_xyz = [math.inf, math.inf, math.inf]
        self.max_xyz = [-math.inf, -math.inf, -math.inf]
        self.sum_xyz = [0.0, 0.0, 0.0]
        self.sum_squared_xyz = [0.0, 0.0, 0.0]
        self.color_sum = [0, 0, 0]
        self.xyz_digest = hashlib.sha256()
        self.rgb_digest = hashlib.sha256()

    @property
    def centroid(self) -> tuple[float, float, float]:
        if self.vertex_count == 0:
            return (0.0, 0.0, 0.0)
        return tuple(value / self.vertex_count for value in self.sum_xyz)

    @property
    def rms_radius(self) -> float:
        if self.vertex_count == 0:
            return 0.0
        centroid = self.centroid
        total = 0.0
        for index in range(3):
            mean_square = self.sum_squared_xyz[index] / self.vertex_count
            total += max(0.0, mean_square - centroid[index] * centroid[index])
        return math.sqrt(total)

    @property
    def xyz_hash(self) -> str:
        return self.xyz_digest.hexdigest().upper()

    @property
    def rgb_hash(self) -> str:
        return self.rgb_digest.hexdigest().upper()


def iter_vertices(path: Path):
    with path.open("r", encoding="utf-8", newline="") as stream:
        vertex_count, face_count, properties = read_header(stream)
        for index in range(vertex_count):
            fields = stream.readline().split()
            if len(fields) < len(properties):
                raise ValueError(f"PLY vertex {index} has {len(fields)} fields; expected {len(properties)}")
            xyz = tuple(float(fields[i]) for i in range(3))
            rgb = None
            if len(properties) >= 6 and properties[3:6] == ["red", "green", "blue"]:
                rgb = tuple(int(fields[i]) for i in range(3, 6))
            yield vertex_count, face_count, properties, xyz, rgb


def build_signature(path: Path, tolerance: float) -> Signature:
    signature = Signature(path, tolerance)
    for vertex_count, face_count, properties, xyz, rgb in iter_vertices(path):
        if signature.vertex_count == 0:
            signature.face_count = face_count
            signature.properties = properties
        signature.vertex_count += 1
        for index, value in enumerate(xyz):
            signature.min_xyz[index] = min(signature.min_xyz[index], value)
            signature.max_xyz[index] = max(signature.max_xyz[index], value)
            signature.sum_xyz[index] += value
            signature.sum_squared_xyz[index] += value * value
            signature.xyz_digest.update(f"{quantize(value, tolerance)},".encode("ascii"))
        if rgb is not None:
            for index, value in enumerate(rgb):
                signature.color_sum[index] += value
                signature.rgb_digest.update(f"{value},".encode("ascii"))

    if signature.vertex_count == 0:
        raise ValueError("PLY contains no vertices")
    return signature


def signature_lines(signature: Signature) -> list[str]:
    return [
        "PlyCoordinateSignature|Pass|format=ascii|physicalScale=Unverified",
        f"PLY|path={signature.path.resolve()}|sha256={signature.file_sha256}|vertices={signature.vertex_count}|faces={signature.face_count}",
        f"Properties|vertex={','.join(signature.properties)}",
        f"Bounds|min={format_tuple(tuple(signature.min_xyz))}|max={format_tuple(tuple(signature.max_xyz))}",
        f"Centroid|xyz={format_tuple(signature.centroid)}|rmsRadius={signature.rms_radius:.9g}",
        f"Color|sum=({signature.color_sum[0]},{signature.color_sum[1]},{signature.color_sum[2]})|rgbHash={signature.rgb_hash}",
        f"Signature|coordinateTolerance={signature.tolerance:.9g}|xyzHash={signature.xyz_hash}",
    ]


def compare(reference: Path, candidate: Path, tolerance: float, ignore_faces: bool) -> list[str]:
    max_coordinate_error = 0.0
    max_rgb_error = 0
    compared = 0
    ref_iterator = iter_vertices(reference)
    cand_iterator = iter_vertices(candidate)

    try:
        while True:
            ref = next(ref_iterator)
            cand = next(cand_iterator)
            if compared == 0:
                ref_count, ref_faces, ref_properties = ref[:3]
                cand_count, cand_faces, cand_properties = cand[:3]
                if ref_count != cand_count:
                    raise ValueError(f"candidate has {cand_count} vertices; expected {ref_count}")
                if not ignore_faces and ref_faces != cand_faces:
                    raise ValueError(f"candidate has {cand_faces} faces; expected {ref_faces}")
                if ref_properties[:3] != cand_properties[:3]:
                    raise ValueError(f"candidate XYZ properties differ: {cand_properties}")

            ref_xyz = ref[3]
            cand_xyz = cand[3]
            ref_rgb = ref[4]
            cand_rgb = cand[4]
            max_coordinate_error = max(
                max_coordinate_error,
                *(abs(actual - expected) for actual, expected in zip(cand_xyz, ref_xyz)),
            )
            if ref_rgb is not None and cand_rgb is not None:
                max_rgb_error = max(
                    max_rgb_error,
                    *(abs(actual - expected) for actual, expected in zip(cand_rgb, ref_rgb)),
                )
            compared += 1
    except StopIteration:
        pass

    reference_signature = build_signature(reference, tolerance)
    candidate_signature = build_signature(candidate, tolerance)
    hashes_match = (
        reference_signature.xyz_hash == candidate_signature.xyz_hash
        and reference_signature.rgb_hash == candidate_signature.rgb_hash
    )
    passed = max_coordinate_error <= tolerance and max_rgb_error == 0
    status = "Pass" if passed else "Fail"
    mode = "ordered-ascii-ply-ignore-faces" if ignore_faces else "ordered-ascii-ply"
    return [
        f"PlyCoordinateComparison|{status}|mode={mode}|physicalScale=Unverified",
        f"Reference|path={reference.resolve()}|sha256={reference_signature.file_sha256}|vertices={reference_signature.vertex_count}|faces={reference_signature.face_count}",
        f"Candidate|path={candidate.resolve()}|sha256={candidate_signature.file_sha256}|vertices={candidate_signature.vertex_count}|faces={candidate_signature.face_count}",
        f"Comparison|points={compared}|maxCoordinateError={max_coordinate_error:.9g}|maxRgbChannelError={max_rgb_error}|tolerance={tolerance:.9g}",
        f"ReferenceSignature|xyzHash={reference_signature.xyz_hash}|rgbHash={reference_signature.rgb_hash}",
        f"CandidateSignature|xyzHash={candidate_signature.xyz_hash}|rgbHash={candidate_signature.rgb_hash}|hashesMatch={str(hashes_match).lower()}",
    ]


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ply", type=Path, help="PLY file to summarize")
    parser.add_argument("--reference", type=Path, help="Reference PLY for comparison")
    parser.add_argument("--candidate", type=Path, help="Candidate PLY exported by another viewer/tool")
    parser.add_argument("--report", required=True, type=Path)
    parser.add_argument("--tolerance", type=float, default=DEFAULT_TOLERANCE)
    parser.add_argument("--ignore-faces", action="store_true", help="Ignore face-count differences when comparing point-cloud re-saves")
    args = parser.parse_args()

    try:
        if args.reference or args.candidate:
            if not args.reference or not args.candidate:
                raise ValueError("--reference and --candidate must be provided together")
            lines = compare(args.reference, args.candidate, args.tolerance, args.ignore_faces)
        elif args.ply:
            lines = signature_lines(build_signature(args.ply, args.tolerance))
        else:
            raise ValueError("provide either --ply or --reference/--candidate")
    except (OSError, ValueError, OverflowError) as error:
        lines = [f"PlyCoordinateSignature|Fail|error={str(error).replace('|', '/')}"]

    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(lines[0])
    return 0 if lines[0].split("|")[1] == "Pass" else 1


if __name__ == "__main__":
    raise SystemExit(main())
