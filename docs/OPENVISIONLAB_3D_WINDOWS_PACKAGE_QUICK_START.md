# OpenVisionLab 3D Studio - Windows Package Quick Start

This folder is a self-contained Windows x64 application package. Keep the
folder contents together; recipes and bundled samples use relative paths.

## Requirements

- Windows 10 build 19041 or later, or Windows 11, on x64
- OpenGL-compatible GPU with a current vendor driver

The package includes the .NET runtime. You do not need to install Git, Python,
the .NET SDK, Visual Studio, or FFmpeg to run the application.

## Run the application

1. Extract the complete package to a local folder.
2. Run `OpenVisionLab.ThreeD.Shell.exe`.
3. Open **Recipe Center** and choose **Open existing recipe**.
4. Select the included tutorial recipe:

   ```text
   3D\Samples\ThicknessCouponV1\inspection-recipe.ov3d-recipe.json
   ```

5. Select **Pad 1 Thickness**, choose **Preview**, review the metric and
   overlay, then choose **Publish** if the preview is acceptable.
6. Choose **Run all** to execute the complete eight-step recipe.

The complete walkthrough is in `documentation\USER_TUTORIAL.md`.

## Important operation rules

- Preview, Publish, Run all, and Run sample set are explicit actions.
- Changing Viewer display settings or reopening saved setup does not run an
  inspection.
- The tutorial source declares `raw-height`; it is not automatically a
  calibrated physical unit.
- Keep the entire package folder together when moving it to another location.

## Package integrity

`openvisionlab-3d-studio-manifest.json` records the application version, source
commit, prerequisites, and SHA-256 for every payload file. The package also
contains `LICENSE` and `NOTICE`; retain them when redistributing the package.

## Troubleshooting

- **The application does not start:** confirm supported 64-bit Windows and
  extract the package before running it.
- **The Viewer is blank or unstable:** install the current GPU driver and retry
  the included Thickness Coupon.
- **The recipe reports a missing source:** restore the complete
  `3D\Samples\ThicknessCouponV1` folder; do not copy only its recipe JSON.
- **A tolerance appears physically incorrect:** verify the source unit and
  calibration before applying physical limits.
