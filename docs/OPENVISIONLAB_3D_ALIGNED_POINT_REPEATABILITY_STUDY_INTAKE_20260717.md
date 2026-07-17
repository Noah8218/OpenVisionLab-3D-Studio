# Aligned Point Repeatability Study Intake

Updated: 2026-07-17

## Purpose

This template prepares a real repeated-acquisition package for the existing headless `AlignedPointRepeatabilityStudyLoader` and Runner. It does not create correspondence values, register scans, infer units, or calibrate a sensor. Those steps must come from the acquisition or approved alignment workflow.

## Required Package

Keep the following files together. Relative paths in `study.json` are resolved from its directory.

```text
aligned-repeatability/
  study.json
  acquisitions/
    run-001.<source-extension>
    run-002.<source-extension>
    run-003.<source-extension>
  mappings/
    run-001.mapping.json
    run-002.mapping.json
    run-003.mapping.json
```

Each source must be a distinct capture of the same physical setup. A renamed copy or byte-identical file is rejected. Every mapping must refer to the exact source bytes and must cover every declared correspondence once.

## Study Template

Replace every `REPLACE_*` value before running. Add one `runs` item for every repeated acquisition.

```json
{
  "studyType": "aligned-point-repeatability",
  "schemaVersion": "1.0",
  "studyId": "study.REPLACE_ID",
  "measurementDefinitionId": "measurement.REPLACE_ID",
  "referenceRoiId": "roi.REPLACE_ID",
  "unit": "mm",
  "frameId": "frame.REPLACE_ID",
  "alignmentReferenceId": "alignment.REPLACE_ID",
  "correspondenceDefinitionId": "correspondence.REPLACE_ID",
  "acceptance": {
    "minimumRunCount": 3,
    "minimumCorrespondenceCount": 3,
    "maximumSampleStandardDeviation": 0.005,
    "maximumRange": 0.01
  },
  "referencePoints": [
    {
      "correspondenceId": "point.001",
      "alignedX": 0.0,
      "alignedY": 0.0,
      "alignedZ": 0.0
    }
  ],
  "runs": [
    {
      "runId": "run.001",
      "sourceEntityId": "source.001",
      "sourcePath": "acquisitions/run-001.REPLACE_EXTENSION",
      "sourceByteLength": 0,
      "sourceSha256": "REPLACE_WITH_64_HEX_SHA256",
      "capturedAt": "2026-07-17T00:00:00Z",
      "mappingPath": "mappings/run-001.mapping.json",
      "mappingByteLength": 0,
      "mappingSha256": "REPLACE_WITH_64_HEX_SHA256"
    }
  ]
}
```

`referencePoints` define the aligned XYZ locations used for future Viewer linking. They are not observations. The scalar `value` is stored separately in each Mapping export and is normally a measured height, thickness, or another declared scalar in the Study unit.

## Mapping Template

Create one Mapping file per run. The Study and Mapping unit/frame/alignment/correspondence IDs must match exactly.

```json
{
  "mappingType": "aligned-point-repeatability-mapping",
  "schemaVersion": "1.0",
  "runId": "run.001",
  "sourceEntityId": "source.001",
  "sourceByteLength": 0,
  "sourceSha256": "REPLACE_WITH_64_HEX_SHA256",
  "unit": "mm",
  "frameId": "frame.REPLACE_ID",
  "alignmentReferenceId": "alignment.REPLACE_ID",
  "correspondenceDefinitionId": "correspondence.REPLACE_ID",
  "alignmentMethodId": "alignment.REPLACE_APPROVED_METHOD",
  "alignmentEvidenceId": "alignment-evidence.REPLACE_RUN_ID",
  "observations": [
    {
      "correspondenceId": "point.001",
      "value": 0.0
    }
  ]
}
```

The Mapping's `sourceByteLength` and `sourceSha256` describe the raw source file. Its own file length and hash are recorded in the corresponding `runs` item only after the Mapping file is final.

## Hashing Order

1. Preserve each raw acquisition without modification.
2. Calculate each raw file's byte length and SHA-256.
3. Generate the Mapping from the approved alignment/correspondence workflow, copying the raw source identity into it.
4. Freeze the Mapping file, then calculate its byte length and SHA-256.
5. Write or update `study.json` with both source and Mapping identities.
6. Run the headless command before opening a future ViewModel workflow.

```powershell
$source = 'acquisitions/run-001.REPLACE_EXTENSION'
$mapping = 'mappings/run-001.mapping.json'
(Get-Item -LiteralPath $source).Length
(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
(Get-Item -LiteralPath $mapping).Length
(Get-FileHash -LiteralPath $mapping -Algorithm SHA256).Hash

dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --aligned-point-repeatability-study study.json --report artifacts\aligned_point_repeatability\real_study_run.txt
```

## Intake Checklist

- At least the configured minimum number of real, distinct raw acquisitions exists.
- Source unit and frame are known from an independent acquisition record.
- All runs use the same approved alignment reference and correspondence definition.
- Every Mapping has an alignment method ID and an evidence ID that locates its alignment record.
- Every Mapping contains each Study correspondence exactly once, with a finite scalar value.
- The Study and Mapping hashes are calculated after the files are finalized.
- The Runner report is retained with the raw acquisition and Mapping package; it records the exact parsed Study path/length/SHA-256 identity as well.

## Claim Boundary

A passing headless result establishes only deterministic validation of the supplied files and point-level statistics. It does not prove that the Mapping was derived from the raw source, registration quality, physical calibration, uncertainty, Gauge R&R, or metrology certification.
