# First inspection with the Thickness Coupon

Build and start the Shell using [source setup](OPENVISIONLAB_3D_SYSTEM_REQUIREMENTS_AND_SETUP.md).
The following workflow uses the public, generated Thickness Coupon.

## Open and inspect the input

1. Open **Recipe Center** and choose the existing recipe action.
2. Select `3D/Samples/ThicknessCouponV1/inspection-recipe.ov3d-recipe.json`.
3. Wait for the source to become ready. The paired `thickness-coupon-v1.C3D` is
   in the same folder.
4. Review source quality, declared units, and ROI overlays.

Keep the whole sample folder together. Its `raw-height` values are not
calibrated physical measurements.

## Run a tool and the recipe

1. Select **Pad 1 Thickness** in the inspection flow.
2. Check its input, Reference ROI, Measurement ROI, and acceptance limits in
   tool settings.
3. Apply any parameter edits, then choose **Run selected tool** or press **F5**.
4. Review the selected step's value, unit, status, and overlays.
5. Choose **Run all** or press **Ctrl+F5** to execute the eight-step recipe.
6. Open Results and review the overall decision and per-step records.

A failed acceptance limit is different from an execution error. Resolve a
missing or incompatible input before interpreting a value. The selected-step
Run command performs the existing preview and result-acceptance sequence
explicitly; opening or selecting a step does not run it.

## Use the viewer

Use fit, zoom, rotation, and display controls to inspect the surface.
Choose height-image or combined viewing when it helps teach a region. Right-click
for context actions available in the current mode. Two-point height profiles
show sampled heights along the chosen segment.

Viewing and display controls do not change inspection parameters. Apply or
cancel an ROI candidate explicitly. If a tool reports an incomplete region,
finish or repair that region before executing the tool.

## Add a 2D texture image

1. Display the intended C3D height-map source in the 3D pane.
2. Open **Color and height display** (the palette button).
3. Under **2D texture image**, choose a single-frame PNG, JPEG, BMP, or TIFF
   aligned to the complete height-map frame.
4. Keep the image's top-left origin: image columns follow height-map columns
   and image rows follow height-map rows. The viewer maps source sample centres
   across the whole image; it does not register, crop, or infer physical pitch.
5. Use **Show texture** to switch back to height colours, or **Remove** to detach
   the image. The scalar colour legend is hidden while the texture is shown.

The texture is a display preference for the same recipe and source. Reopening
checks the source identity, grid dimensions, and image bytes. If the file has
changed or is missing, select the intended image again. Removing the association
provides the reset path. These actions never run a tool or change raw height,
ROI, or result data. Displayed numbers use at most two decimal places while
calculations and saved numeric values retain their precision.

## Save, reopen, and repeat

Save a copy before changing the sample recipe. Keep the saved recipe, source,
and referenced background or result sidecar files together.

Reopen the saved recipe and verify its input paths, step order, settings, and
regions. Restoring saved setup does not run an inspection. Run explicitly when
new evidence is needed.

For several files, prepare the file list and expected sample roles, start the
sample run, then review recorded outcomes. A held-out sample stays separate
from tuning data.

## Recovery

- Missing source: keep paired files together or update the input path.
- Disabled Run: resolve the missing input, region, or parameters in tool settings.
- Invalid numeric draft: correct the value or cancel the draft before applying.
- Blank or unstable 3D display: check the GPU driver and OpenGL support.
- Unexpected physical tolerance: check units and calibration evidence;
  do not infer millimetres from the sample's raw-height values.

See the [C3D display notes](../README.md#3d-viewing-and-textures) for texture
and sample-data limitations.
