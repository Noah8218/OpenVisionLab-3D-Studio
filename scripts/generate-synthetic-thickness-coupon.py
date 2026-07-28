#!/usr/bin/env python3
"""Generate the public Synthetic Thickness Coupon v1 C3D validation package.

The source is intentionally fictional and deterministic.  It is not derived
from a scan, customer part, company fixture, or previously captured C3D file.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import struct
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


RESOLUTION_SCALE = 2
WIDTH = 640 * RESOLUTION_SCALE
HEIGHT = 420 * RESOLUTION_SCALE
SOURCE_NAME = "Synthetic Thickness Coupon v1"
SOURCE_FILE = "synthetic-thickness-coupon-v1.C3D"
RECIPE_FILE = "inspection-recipe.ov3d-recipe.json"
TRUTH_FILE = "ground-truth.json"
PREVIEW_FILE = "source-height-preview.png"
SOURCE_ID = "source.synthetic-thickness-coupon-v1"
SOURCE_UNIT = "synthetic-height-unit"
SOURCE_FRAME = "frame.c3d-grid-index"

PAD_THICKNESSES = (8.0, 12.0, 16.0, 20.0, 10.0, 14.0, 18.0, 22.0)
PAD_COLUMNS = tuple(value * RESOLUTION_SCALE for value in (54, 198, 342, 486))
PAD_ROWS = tuple(value * RESOLUTION_SCALE for value in (58, 226))
PAD_WIDTH = 100 * RESOLUTION_SCALE
PAD_HEIGHT = 112 * RESOLUTION_SCALE


def font(size: int, bold: bool = False) -> ImageFont.ImageFont:
    name = "seguisb.ttf" if bold else "segoeui.ttf"
    path = Path("C:/Windows/Fonts") / name
    return ImageFont.truetype(str(path), size) if path.exists() else ImageFont.load_default()


def base_height(row: int, column: int) -> float:
    """Exact affine datum used by every taught reference ROI."""

    return 100.0 + column * (0.0125 / RESOLUTION_SCALE) - row * (0.0075 / RESOLUTION_SCALE)


def inside_coupon(row: int, column: int) -> bool:
    left = top = 20 * RESOLUTION_SCALE
    right, bottom = WIDTH - 1 - left, HEIGHT - 1 - top
    if not (left <= column <= right and top <= row <= bottom):
        return False
    chamfer = 24 * RESOLUTION_SCALE
    if column + row < left + top + chamfer:
        return False
    if (right - column) + row < chamfer:
        return False
    if column + (bottom - row) < left + chamfer:
        return False
    if (right - column) + (bottom - row) < chamfer:
        return False
    return True


def inside_circle(row: int, column: int, center_row: int, center_column: int, radius: int) -> bool:
    return (row - center_row) ** 2 + (column - center_column) ** 2 <= radius**2


def pad_geometry(index: int) -> dict:
    grid_row, grid_column = divmod(index, 4)
    top = PAD_ROWS[grid_row]
    left = PAD_COLUMNS[grid_column]
    return {
        "index": index + 1,
        "top": top,
        "left": left,
        "bottom": top + PAD_HEIGHT - 1,
        "right": left + PAD_WIDTH - 1,
        "referenceRoi": {
            "row": top + 20 * RESOLUTION_SCALE,
            "column": left + 7 * RESOLUTION_SCALE,
            "rowCount": 72 * RESOLUTION_SCALE,
            "columnCount": 16 * RESOLUTION_SCALE,
        },
        "measurementRoi": {
            "row": top + 20 * RESOLUTION_SCALE,
            "column": left + 35 * RESOLUTION_SCALE,
            "rowCount": 72 * RESOLUTION_SCALE,
            "columnCount": 55 * RESOLUTION_SCALE,
        },
        "thickness": PAD_THICKNESSES[index],
    }


def in_rectangle(row: int, column: int, rectangle: dict) -> bool:
    return (
        rectangle["row"] <= row < rectangle["row"] + rectangle["rowCount"]
        and rectangle["column"] <= column < rectangle["column"] + rectangle["columnCount"]
    )


def rounded_rectangle_contains(
    row: int,
    column: int,
    top: int,
    left: int,
    bottom: int,
    right: int,
    radius: int,
) -> bool:
    if not (top <= row <= bottom and left <= column <= right):
        return False
    nearest_row = min(max(row, top + radius), bottom - radius)
    nearest_column = min(max(column, left + radius), right - radius)
    return (row - nearest_row) ** 2 + (column - nearest_column) ** 2 <= radius**2


def create_values() -> tuple[list[float], list[dict]]:
    pads = [pad_geometry(index) for index in range(8)]
    values = [0.0] * (WIDTH * HEIGHT)
    fiducials = tuple(
        tuple(value * RESOLUTION_SCALE for value in fiducial)
        for fiducial in ((48, 48, 13), (198, 320, 12), (370, 594, 14))
    )

    for row in range(HEIGHT):
        for column in range(WIDTH):
            if not inside_coupon(row, column):
                continue

            value = base_height(row, column)
            if (
                194 * RESOLUTION_SCALE <= row <= 213 * RESOLUTION_SCALE
                or 315 * RESOLUTION_SCALE <= column <= 324 * RESOLUTION_SCALE
            ):
                value -= 3.0
            for center_row, center_column, radius in fiducials:
                if inside_circle(row, column, center_row, center_column, radius):
                    value -= 6.0

            for pad in pads:
                if not rounded_rectangle_contains(
                    row,
                    column,
                    pad["top"],
                    pad["left"],
                    pad["bottom"],
                    pad["right"],
                    10 * RESOLUTION_SCALE,
                ):
                    continue

                # The narrow left ledge remains on the exact affine datum and
                # supplies the taught reference plane.  The large plateau is
                # separated from that plane by one known synthetic thickness.
                if column >= pad["left"] + 29 * RESOLUTION_SCALE:
                    value = base_height(row, column) + pad["thickness"]
                else:
                    value = base_height(row, column)

                # A one-cell decorative bevel stays outside both taught ROIs.
                edge_distance = min(
                    row - pad["top"],
                    pad["bottom"] - row,
                    column - pad["left"],
                    pad["right"] - column,
                )
                if edge_distance <= 2 * RESOLUTION_SCALE:
                    value -= 1.5
                break

            values[row * WIDTH + column] = float(value)

    return values, pads


def c3d_bytes(values: list[float]) -> bytes:
    return struct.pack("<ii", WIDTH, HEIGHT) + struct.pack(f"<{len(values)}f", *values)


def source_binding(content_sha256: str) -> dict:
    return {
        "format": "C3D",
        "contentSha256": content_sha256,
        "gridWidth": WIDTH,
        "gridHeight": HEIGHT,
    }


def create_recipe(content_sha256: str, byte_length: int, pads: list[dict]) -> dict:
    selections = []
    steps = []
    binding = source_binding(content_sha256)

    for pad in pads:
        number = pad["index"]
        reference_id = f"selection.synthetic-pad-{number:02d}.reference-roi"
        measurement_id = f"selection.synthetic-pad-{number:02d}.measurement-roi"
        selections.extend(
            [
                {
                    "id": reference_id,
                    "name": f"Pad {number} Reference ROI",
                    "kind": "grid-rectangle",
                    "rootSourceId": SOURCE_ID,
                    "frameId": SOURCE_FRAME,
                    "sourceBinding": binding,
                    "gridRectangle": pad["referenceRoi"],
                },
                {
                    "id": measurement_id,
                    "name": f"Pad {number} Measurement ROI",
                    "kind": "grid-rectangle",
                    "rootSourceId": SOURCE_ID,
                    "frameId": SOURCE_FRAME,
                    "sourceBinding": binding,
                    "gridRectangle": pad["measurementRoi"],
                },
            ]
        )
        thickness = pad["thickness"]
        steps.append(
            {
                "id": f"step.synthetic-pad-thickness.{number:02d}",
                "toolId": "thickness",
                "toolName": f"Pad {number} Thickness",
                "minimumInputCount": 3,
                "inputEntityIds": [SOURCE_ID, reference_id, measurement_id],
                "outputEntityId": f"derived.synthetic-pad-thickness.{number:02d}",
                "parameters": [
                    {"name": "MinimumThickness", "value": f"{thickness - 0.25:g}"},
                    {"name": "MaximumThickness", "value": f"{thickness + 0.25:g}"},
                    {"name": "MinimumValidSampleCount", "value": "3000"},
                ],
                "dualRoiRouting": {
                    "firstRegionSelectionId": reference_id,
                    "secondRegionSelectionId": measurement_id,
                },
            }
        )

    return {
        "schemaVersion": "1.5",
        "name": "Synthetic Thickness Coupon v1 - 8 Pad",
        "source": {
            "id": SOURCE_ID,
            "name": SOURCE_NAME,
            "format": "C3D",
            "unit": SOURCE_UNIT,
            "frameId": SOURCE_FRAME,
            "path": SOURCE_FILE,
            "byteLength": byte_length,
            "contentSha256": content_sha256,
            "gridWidth": WIDTH,
            "gridHeight": HEIGHT,
        },
        "references": [],
        "steps": steps,
        "selections": selections,
    }


def color(value: float, minimum: float, maximum: float) -> tuple[int, int, int]:
    if value == 0.0 or not math.isfinite(value):
        return 8, 13, 22
    t = max(0.0, min(1.0, (value - minimum) / (maximum - minimum)))
    stops = (
        (0.00, (20, 45, 135)),
        (0.25, (0, 180, 225)),
        (0.50, (0, 220, 145)),
        (0.75, (230, 225, 35)),
        (1.00, (255, 125, 20)),
    )
    for index in range(len(stops) - 1):
        left_t, left = stops[index]
        right_t, right = stops[index + 1]
        if t <= right_t:
            local = (t - left_t) / (right_t - left_t)
            return tuple(round(left[c] + (right[c] - left[c]) * local) for c in range(3))
    return stops[-1][1]


def draw_preview(output: Path, values: list[float], pads: list[dict], content_sha256: str) -> None:
    scale = 1
    margin_left, margin_top, sidebar, bottom = 44, 92, 280, 50
    map_width, map_height = WIDTH * scale, HEIGHT * scale
    image = Image.new(
        "RGB",
        (margin_left + map_width + sidebar, margin_top + map_height + bottom),
        (15, 23, 36),
    )
    draw = ImageDraw.Draw(image)
    finite = [value for value in values if value != 0.0 and math.isfinite(value)]
    minimum, maximum = min(finite), max(finite)
    pixels = Image.new("RGB", (WIDTH, HEIGHT))
    pixels.putdata([color(value, minimum, maximum) for value in values])
    image.paste(pixels.resize((map_width, map_height), Image.Resampling.NEAREST), (margin_left, margin_top))

    draw.text((margin_left, 20), SOURCE_NAME, fill=(238, 244, 252), font=font(28, True))
    draw.text(
        (margin_left, 58),
        "Deterministic fictional C3D | cyan reference ROI | orange measurement ROI",
        fill=(145, 165, 190),
        font=font(16),
    )

    for pad in pads:
        for roi_name, outline in (
            ("referenceRoi", (55, 235, 235)),
            ("measurementRoi", (255, 165, 45)),
        ):
            roi = pad[roi_name]
            left = margin_left + roi["column"] * scale
            top = margin_top + roi["row"] * scale
            right = left + roi["columnCount"] * scale
            lower = top + roi["rowCount"] * scale
            draw.rectangle((left, top, right, lower), outline=outline, width=3)
        label_x = margin_left + pad["left"] + 35 * RESOLUTION_SCALE
        label_y = margin_top + pad["top"] + 47 * RESOLUTION_SCALE
        draw.rounded_rectangle(
            (label_x, label_y, label_x + 92, label_y + 31),
            radius=5,
            fill=(10, 17, 28),
        )
        draw.text(
            (label_x + 8, label_y + 5),
            f"P{pad['index']}  {pad['thickness']:g}",
            fill=(255, 245, 180),
            font=font(15, True),
        )

    sidebar_x = margin_left + map_width + 28
    draw.text((sidebar_x, margin_top), "KNOWN-GROUND-TRUTH", fill=(225, 235, 248), font=font(17, True))
    lines = [
        f"Grid  {WIDTH} x {HEIGHT}",
        f"Valid  {len(finite):,}",
        f"Missing  {WIDTH * HEIGHT - len(finite):,}",
        f"Range  {minimum:.3f} .. {maximum:.3f}",
        "",
        "Pad separations",
        "P1  8    P2  12",
        "P3  16   P4  20",
        "P5  10   P6  14",
        "P7  18   P8  22",
        "",
        "Unit: synthetic-height-unit",
        "Not calibrated metrology",
        "",
        f"SHA-256",
        content_sha256[:16],
        content_sha256[16:32],
        content_sha256[32:48],
        content_sha256[48:],
    ]
    y = margin_top + 38
    for line in lines:
        draw.text((sidebar_x, y), line, fill=(160, 180, 205), font=font(15))
        y += 27

    output.parent.mkdir(parents=True, exist_ok=True)
    image.save(output, optimize=True)


def write_json(path: Path, value: dict) -> None:
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--output",
        type=Path,
        default=Path("3D/SyntheticValidation/ThicknessCouponV1"),
        help="Package directory",
    )
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)

    values, pads = create_values()
    payload = c3d_bytes(values)
    content_sha256 = hashlib.sha256(payload).hexdigest().upper()
    source_path = output / SOURCE_FILE
    source_path.write_bytes(payload)

    recipe = create_recipe(content_sha256, len(payload), pads)
    write_json(output / RECIPE_FILE, recipe)

    valid_values = [value for value in values if value != 0.0 and math.isfinite(value)]
    truth = {
        "schemaVersion": "1.0",
        "assetId": "synthetic-thickness-coupon-v1",
        "origin": {
            "kind": "procedurally-generated",
            "derivedFromCapturedData": False,
            "generator": "scripts/generate-synthetic-thickness-coupon.py",
            "designNote": "Fictional eight-pad coupon; AI concept art guided only the non-sensitive visual layout.",
        },
        "source": {
            "path": SOURCE_FILE,
            "format": "C3D",
            "width": WIDTH,
            "height": HEIGHT,
            "byteLength": len(payload),
            "contentSha256": content_sha256,
            "validCellCount": len(valid_values),
            "missingCellCount": WIDTH * HEIGHT - len(valid_values),
            "minimumRawHeight": min(valid_values),
            "maximumRawHeight": max(valid_values),
            "unit": SOURCE_UNIT,
            "frameId": SOURCE_FRAME,
        },
        "measurements": [
            {
                "pad": pad["index"],
                "referenceRoi": pad["referenceRoi"],
                "measurementRoi": pad["measurementRoi"],
                "expectedSignedSeparation": pad["thickness"],
                "acceptance": {
                    "minimum": pad["thickness"] - 0.25,
                    "maximum": pad["thickness"] + 0.25,
                },
            }
            for pad in pads
        ],
    }
    write_json(output / TRUTH_FILE, truth)
    draw_preview(output / PREVIEW_FILE, values, pads, content_sha256)

    print(f"Generated {source_path}")
    print(f"SHA256={content_sha256}")
    print(f"Grid={WIDTH}x{HEIGHT}")
    print(f"Valid={len(valid_values)} Missing={WIDTH * HEIGHT - len(valid_values)}")
    print(f"Recipe={output / RECIPE_FILE}")
    print(f"GroundTruth={output / TRUTH_FILE}")
    print(f"Preview={output / PREVIEW_FILE}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
