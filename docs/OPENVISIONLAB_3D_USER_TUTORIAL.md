# OpenVisionLab 3D Studio User Tutorial

This tutorial takes a first-time operator through one complete inspection using
the included Thickness Coupon. It uses only files shipped in the repository and
the self-contained Windows package.

## What you will complete

You will:

1. open a saved inspection recipe and its paired height input;
2. identify the main work areas;
3. review the source and the taught ROIs;
4. Preview and Publish one selected Thickness step;
5. Run the complete eight-step recipe;
6. review results and saved evidence;
7. create a privacy-safe support bundle; and
8. understand how validation sample roles are used.

Preview, Publish, Run, and Run sample set are separate explicit actions. Merely
opening a recipe, selecting a step, changing visibility, or restoring saved
settings does not execute inspection.

## 1. Start the application

### From the self-contained Windows package

Keep the extracted folder intact and run:

```text
OpenVisionLab.ThreeD.Shell.exe
```

The package carries its own .NET runtime. You do not need Git, Python, the .NET
SDK, or FFmpeg.

### From a source clone

After a successful Release build, run:

```powershell
dotnet run --no-build --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Release
```

## 2. Open the included Thickness Coupon

1. Open **Recipe Center**.
2. Choose **Open existing recipe**.
3. Select:

   ```text
   3D\Samples\ThicknessCouponV1\inspection-recipe.ov3d-recipe.json
   ```

4. Wait until the source state is ready and the Viewer shows the eight-pad
   height surface.

The recipe resolves `thickness-coupon-v1.C3D` from the same folder. If the
source is reported missing, confirm that the complete `ThicknessCouponV1`
folder was copied, not only the recipe JSON file.

## 3. Read the main workspace

The normal inspection workspace links four responsibilities:

| Area | What it is for |
| --- | --- |
| **Inspection Tools** | Add a compatible inspection step to a recipe. |
| **Inspection Flow** | Select, reorder, or remove authored recipe steps. |
| **Viewer** | Inspect geometry, height display, ROI overlays, and result overlays. |
| **Selected Tool** | Review inputs and edit typed parameters or ROI assignments for the selected step. |

Additional evidence panes show validation, run records, output comparison, and
tool-specific diagnostics. Selecting or expanding a pane is presentation-only;
it does not run a tool.

## 4. Review the input before measuring

1. Confirm that the current source is **Valid** rather than Missing or
   incompatible.
2. Open the detailed **Source Quality** information when you need coverage,
   missing-value, unit, coordinate, or frame evidence.
3. In **Acquisition/source provenance**, choose whether acquisition evidence
   is available and record the evidence and limitations. If an acquisition
   direction was supplied, set **Structured acquisition direction** to
   available and enter the XYZ vector using the displayed `Sensor → scene`
   convention and source frame.
4. Choose **Apply source contract**. The direction is normalized before it is
   saved. Draft typing and reset do not change the recipe or run an inspection.
5. Do not treat these values as an inferred camera pose or calibration. If the
   direction was not supplied, keep it unavailable; the application does not
   guess it from the 3D geometry.
6. Inspect the 3D surface and the full-resolution **Height Image**.
7. Use zoom, pan, fit, palette, and display-range controls as needed. These are
   Viewer settings and do not change the recipe or execute it.

The tutorial source declares `raw-height`. Treat the displayed values as the
declared source unit, not automatically as millimetres or another calibrated
physical unit.

## 5. Review one taught Thickness step

1. In **Inspection Flow**, select **Pad 1 Thickness**.
2. In **Selected Tool**, confirm that the step has:

   - the current height input;
   - one Reference ROI on the pad's datum surface;
   - one Measurement ROI on the raised surface of the same pad; and
   - minimum and maximum acceptance parameters.

3. Confirm the two ROI overlays in the Viewer or Height Image.

For a new Thickness step, teach the Reference ROI first and the Measurement ROI
second. A candidate ROI stays in Drawing or Review until you explicitly Apply
it. Use **Enter** to apply the current candidate or **Esc** to cancel it.

## 6. Preview and publish the selected step

1. Press **F5** or choose **Preview**.
2. Wait for the selected step to finish.
3. Review its state, thickness metric, acceptance limits, and Viewer overlay.
4. If the evidence is acceptable, choose **Publish**.

Preview creates temporary evidence for the selected step. Publish accepts the
current non-stale Preview without rerunning it. Editing the step after Preview
makes that preview stale; run Preview again before publishing.

## 7. Run the complete recipe

1. Press **Ctrl+F5** or choose **Run all**.
2. Wait for all eight ordered Thickness steps to complete.
3. Open **Results review**.
4. Review the overall state and each step's metric, overlay, output identity,
   and report evidence.

A failed step means an authored acceptance rule was not satisfied. An error
means the application could not produce a valid inspection result for that
step. Do not treat an error as an out-of-tolerance measurement.

## 8. Create a privacy-safe support bundle

Use this path when a support engineer needs current diagnostic evidence:

1. Open **Results → Run Record** after selecting a current Run Record.
2. Choose **Export privacy-safe support bundle**.
3. Select an output folder. Export starts only after this explicit action.
4. Review the created ZIP before sharing it.

The ZIP contains an auditable manifest, sanitized recipe, at most the newest
200 in-memory session-log entries, source identity, recorded Source Quality,
and the current result. By default it does not contain raw 3D source or mesh
bytes, absolute file paths, the full application log, or user and machine
identity. Missing recipe or legacy Source Quality evidence is reported as
unavailable rather than fabricated. The separate **Export result bundle**
action is a full evidence export and is not the privacy-safe sharing path.

Creating the support bundle does not run Preview, Publish, Run, or Validation,
and it does not change the recipe, selection, or current result.

## 9. Save and reopen

1. Use **Ctrl+Shift+S** to save a copy before changing tutorial parameters.
2. Close and reopen that saved recipe.
3. Confirm that its source, step order, parameters, ROI roles, and saved
   validation setup are restored.

Restoration does not execute Preview, Publish, Run, or validation. Use the
explicit command again when you want new evidence.

## 10. Understand Validation samples

Validation uses operator-assigned expected roles and application-produced run
states. These are different concepts.

| Expected role | Meaning |
| --- | --- |
| **Good** | The operator expects this sample to be accepted. |
| **Bad** | The operator expects this sample to be rejected. |
| **Held-out** | Final replay evidence; excluded from threshold tuning. |

The shortest safe workflow is:

```text
Add samples → assign expected roles → Run sample set → review results
```

- **Samples** is where files and expected roles are prepared.
- **Run results** summarizes Pass, Fail, and Error outcomes after execution.
- **Failure analysis** focuses the failed or errored step and its evidence.
- **Threshold review** can compare Good and Bad development evidence and
  propose explicit candidates; it does not silently change parameters.
- **Held-out** replay checks a chosen correction on evidence that did not
  participate in threshold tuning.

Changing between these review sections does not start inspection. Only **Run
sample set** executes the validation set.

## 11. Common recovery cases

### The recipe opens but the source is missing

Keep `inspection-recipe.ov3d-recipe.json` and
`thickness-coupon-v1.C3D` together in the original `ThicknessCouponV1` folder,
then reopen the recipe.

### Preview is disabled

Select a recipe step and resolve any missing source, input, ROI, or parameter
message shown by **Selected Tool**. Preview remains disabled until the selected
typed step is complete enough to execute safely.

### The Viewer is blank or rendering is unstable

Update the GPU driver, confirm that the machine supports OpenGL, reopen the
application, and try the included Thickness Coupon again. Display controls do
not repair an unreadable or incompatible input.

### A physical tolerance looks wrong

Check the source's declared unit and calibration evidence. The included
Thickness Coupon deliberately uses `raw-height`; do not interpret it as a
physical unit without a verified calibration contract.

## First-run checklist

- [ ] The application starts from the extracted package or current source build.
- [ ] The included Thickness recipe opens with a Valid source.
- [ ] All eight pads and paired ROIs are visible.
- [ ] Pad 1 Preview completes and its metric and overlay are reviewable.
- [ ] Publish accepts the current Preview without rerunning it.
- [ ] Run all produces an eight-step result record.
- [ ] Save/reopen restores setup without executing inspection.
