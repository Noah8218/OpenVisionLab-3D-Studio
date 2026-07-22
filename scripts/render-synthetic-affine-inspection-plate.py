#!/usr/bin/env python3
"""Render human-review PNGs for Synthetic Affine Inspection Plate v1."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import struct
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


def load_c3d(path: Path) -> tuple[int, int, list[float]]:
    data = path.read_bytes()
    if len(data) < 8:
        raise ValueError("C3D header is incomplete")
    width, height = struct.unpack_from("<ii", data, 0)
    if len(data) != 8 + width * height * 4:
        raise ValueError("C3D byte length does not match its dimensions")
    values = list(struct.unpack_from(f"<{width * height}f", data, 8))
    return width, height, [value if math.isfinite(value) and value != 0 else math.nan for value in values]


def font(size: int, bold: bool = False) -> ImageFont.ImageFont:
    name = "seguisb.ttf" if bold else "segoeui.ttf"
    path = Path("C:/Windows/Fonts") / name
    return ImageFont.truetype(str(path), size) if path.exists() else ImageFont.load_default()


def color(value: float, minimum: float, maximum: float) -> tuple[int, int, int]:
    if not math.isfinite(value):
        return 8, 13, 22
    t = 0.5 if maximum <= minimum else max(0.0, min(1.0, (value - minimum) / (maximum - minimum)))
    stops = (
        (0.00, (20, 40, 140)),
        (0.20, (0, 170, 235)),
        (0.42, (0, 220, 155)),
        (0.62, (245, 220, 35)),
        (0.82, (250, 100, 25)),
        (1.00, (210, 25, 70)),
    )
    for index in range(len(stops) - 1):
        left_t, left = stops[index]
        right_t, right = stops[index + 1]
        if t <= right_t:
            local = (t - left_t) / (right_t - left_t)
            return tuple(round(left[channel] + (right[channel] - left[channel]) * local) for channel in range(3))
    return stops[-1][1]


def draw_preview(
    output: Path,
    values: list[float],
    width: int,
    height: int,
    truth: dict,
    title: str,
    subtitle: str,
    value_scale: float,
) -> None:
    scale = 4
    margin_left, margin_top, sidebar, bottom = 72, 104, 300, 72
    map_width, map_height = width * scale, height * scale
    image = Image.new("RGB", (margin_left + map_width + sidebar, margin_top + map_height + bottom), (15, 23, 36))
    draw = ImageDraw.Draw(image)
    finite = [value * value_scale for value in values if math.isfinite(value)]
    minimum, maximum = min(finite), max(finite)

    pixels = Image.new("RGB", (width, height))
    pixels.putdata([color(value * value_scale, minimum, maximum) for value in values])
    pixels = pixels.resize((map_width, map_height), Image.Resampling.NEAREST)
    image.paste(pixels, (margin_left, margin_top))

    draw.text((margin_left, 24), title, fill=(238, 244, 252), font=font(28, True))
    draw.text((margin_left, 62), subtitle, fill=(145, 165, 190), font=font(17))

    def rect(roi: dict, outline: tuple[int, int, int], label: str) -> None:
        left = margin_left + roi["column"] * scale
        top = margin_top + roi["row"] * scale
        right = left + roi["columnCount"] * scale
        lower = top + roi["rowCount"] * scale
        draw.rectangle((left, top, right, lower), outline=outline, width=4)
        draw.rounded_rectangle((left + 6, top + 6, left + 164, top + 34), radius=5, fill=(10, 17, 28))
        draw.text((left + 12, top + 8), label, fill=outline, font=font(15, True))

    measurements = truth["measurements"]
    rect(measurements["thickness"]["roi"], (50, 230, 130), "Thickness ROI")
    rect(measurements["warpage"]["roi"], (225, 90, 245), "Warpage ROI")

    for landmark in truth["landmarks"]:
        source = landmark["expectedSource"]
        x = margin_left + (source["x"] + 0.5) * scale
        y = margin_top + (source["z"] + 0.5) * scale
        draw.ellipse((x - 9, y - 9, x + 9, y + 9), outline=(255, 235, 70), width=4)
        draw.line((x - 18, y, x + 18, y), fill=(255, 235, 70), width=2)
        draw.line((x, y - 18, x, y + 18), fill=(255, 235, 70), width=2)
        draw.text((x + 12, y - 23), f"{landmark['role']}  H={landmark['padHeight']:g}", fill=(255, 244, 150), font=font(14, True))

    sidebar_x = margin_left + map_width + 34
    draw.text((sidebar_x, margin_top), "HEIGHT / DISTRIBUTION", fill=(228, 238, 250), font=font(18, True))
    bar_x, bar_y, bar_w, bar_h = sidebar_x, margin_top + 46, 38, 360
    for row in range(bar_h):
        value = maximum - (maximum - minimum) * row / max(1, bar_h - 1)
        draw.line((bar_x, bar_y + row, bar_x + bar_w, bar_y + row), fill=color(value, minimum, maximum))
    draw.rectangle((bar_x, bar_y, bar_x + bar_w, bar_y + bar_h), outline=(205, 220, 238), width=1)
    draw.text((bar_x + 54, bar_y - 8), f"{maximum:.3f}", fill=(220, 230, 242), font=font(16))
    draw.text((bar_x + 54, bar_y + bar_h - 20), f"{minimum:.3f}", fill=(220, 230, 242), font=font(16))

    thickness = measurements["thickness"]
    warpage = measurements["warpage"]
    info_y = bar_y + bar_h + 42
    lines = [
        ("Known measurement truth", True, (238, 244, 252)),
        (f"Thickness mean  {thickness['mean']:.6f}", False, (50, 230, 130)),
        (f"Thickness range {thickness['range']:.6f}", False, (50, 230, 130)),
        (f"Warpage P2V    {warpage['peakToValley']:.6f}", False, (225, 120, 245)),
        (f"Warpage RMS    {warpage['rms']:.6f}", False, (225, 120, 245)),
        (f"Missing cells  {truth['source']['missingCount']}", False, (175, 192, 214)),
    ]
    for text, bold, fill in lines:
        draw.text((sidebar_x, info_y), text, fill=fill, font=font(16, bold))
        info_y += 31

    draw.text((margin_left, margin_top + map_height + 24),
              "Yellow: four extracted CornerAnchor targets   Green: Thickness ROI   Magenta: Warpage ROI   Dark cells: preserved missing mask",
              fill=(150, 170, 194), font=font(15))
    output.parent.mkdir(parents=True, exist_ok=True)
    image.save(output, "PNG", optimize=True)


def roi_values(values: list[float], width: int, roi: dict) -> list[tuple[float, float, float]]:
    samples = []
    for row in range(roi["row"], roi["row"] + roi["rowCount"]):
        for column in range(roi["column"], roi["column"] + roi["columnCount"]):
            value = values[row * width + column]
            if math.isfinite(value):
                samples.append((float(column), float(row), value * 0.5))
    return samples


def solve3(matrix: list[list[float]]) -> list[float]:
    for pivot in range(3):
        best = max(range(pivot, 3), key=lambda row: abs(matrix[row][pivot]))
        if abs(matrix[best][pivot]) < 1e-18:
            raise ValueError("Independent Python best-fit plane solve is singular")
        matrix[pivot], matrix[best] = matrix[best], matrix[pivot]
        divisor = matrix[pivot][pivot]
        matrix[pivot] = [value / divisor for value in matrix[pivot]]
        for row in range(3):
            if row == pivot:
                continue
            factor = matrix[row][pivot]
            matrix[row] = [matrix[row][column] - factor * matrix[pivot][column] for column in range(4)]
    return [matrix[row][3] for row in range(3)]


def verify_measurements(values: list[float], width: int, truth: dict) -> None:
    thickness_truth = truth["measurements"]["thickness"]
    thickness = [sample[2] for sample in roi_values(values, width, thickness_truth["roi"])]
    thickness_actual = {
        "mean": sum(thickness) / len(thickness),
        "minimum": min(thickness),
        "maximum": max(thickness),
        "range": max(thickness) - min(thickness),
        "validSampleCount": len(thickness),
    }
    for name, actual in thickness_actual.items():
        expected = thickness_truth[name]
        if abs(actual - expected) > 1e-9:
            raise ValueError(f"Independent Python Thickness {name} mismatch: {actual} != {expected}")

    warpage_truth = truth["measurements"]["warpage"]
    samples = roi_values(values, width, warpage_truth["roi"])
    matrix = [[0.0] * 4 for _ in range(3)]
    for x, y, z in samples:
        matrix[0][0] += x * x
        matrix[0][1] += x * y
        matrix[0][2] += x
        matrix[0][3] += x * z
        matrix[1][0] += x * y
        matrix[1][1] += y * y
        matrix[1][2] += y
        matrix[1][3] += y * z
        matrix[2][0] += x
        matrix[2][1] += y
        matrix[2][2] += 1.0
        matrix[2][3] += z
    slope_x, slope_y, intercept = solve3(matrix)
    residuals = [z - (slope_x * x + slope_y * y + intercept) for x, y, z in samples]
    minimum, maximum = min(residuals), max(residuals)
    warpage_actual = {
        "peakToValley": maximum - minimum,
        "rms": math.sqrt(sum(value * value for value in residuals) / len(residuals)),
        "minimumResidual": minimum,
        "maximumResidual": maximum,
        "slopeX": slope_x,
        "slopeY": slope_y,
        "intercept": intercept,
        "validSampleCount": len(residuals),
    }
    for name, actual in warpage_actual.items():
        expected = warpage_truth[name]
        if abs(actual - expected) > 1e-9:
            raise ValueError(f"Independent Python Warpage {name} mismatch: {actual} != {expected}")
    print(
        "Independent Python truth: "
        f"Thickness mean={thickness_actual['mean']:.9f}, range={thickness_actual['range']:.9f}; "
        f"Warpage P2V={warpage_actual['peakToValley']:.9f}, RMS={warpage_actual['rms']:.9f}"
    )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("package", type=Path)
    args = parser.parse_args()
    package = args.package.resolve()
    truth = json.loads((package / "ground-truth.json").read_text(encoding="utf-8"))
    c3d = package / truth["source"]["file"]
    width, height, values = load_c3d(c3d)
    if width != truth["source"]["width"] or height != truth["source"]["height"]:
        raise ValueError("C3D dimensions do not match ground truth")
    if hashlib.sha256(c3d.read_bytes()).hexdigest().upper() != truth["source"]["contentSha256"]:
        raise ValueError("C3D SHA-256 does not match ground truth")
    verify_measurements(values, width, truth)
    draw_preview(
        package / "source-height-preview.png", values, width, height, truth,
        "Synthetic Affine Inspection Plate v1 — Source C3D",
        "240 × 160 raw height | deterministic impulses and missing cells | four L-edge landmark targets",
        1.0,
    )
    draw_preview(
        package / "reference-height-preview.png", values, width, height, truth,
        "Synthetic Affine Inspection Plate v1 — Expected A3 Reference Height",
        "Known full-XYZ affine | anisotropic planar pitch | reference height = 0.5 × raw height",
        0.5,
    )
    print(f"Rendered source and reference previews in {package}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
