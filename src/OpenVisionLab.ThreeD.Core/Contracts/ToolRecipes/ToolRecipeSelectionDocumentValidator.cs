namespace OpenVisionLab.ThreeD.Core;

internal static class ToolRecipeSelectionDocumentValidator
{
    // Selection payload policy only; document identity and step graph policy remain in ToolRecipeValidator.
    private const string GridCellLocatorKind = "grid-cell";

    internal static void Validate(
        ToolRecipeSelection selection,
        ToolRecipeSource source,
        bool hasCorrespondenceDescriptor,
        bool supportsArtifactOwnedSelections,
        bool supportsOrientedBox3D,
        bool supportsGridCircle,
        bool supportsGridPolygon,
        List<string> errors,
        List<string> warnings,
        List<(string SelectionId, string SelectionLabel, ToolRecipeLandmarkCorrespondence Row)> correspondenceRows)
    {
        var label = $"Selection '{Clean(selection.Id)}'";
        if (string.IsNullOrWhiteSpace(selection.Id)) errors.Add("Selection ID is required.");
        if (string.IsNullOrWhiteSpace(selection.Name)) errors.Add($"{label} name is required.");
        if (string.IsNullOrWhiteSpace(selection.Kind)) errors.Add($"{label} kind is required.");
        if (string.IsNullOrWhiteSpace(selection.RootSourceId)) errors.Add($"{label} root source ID is required.");
        if (string.IsNullOrWhiteSpace(selection.FrameId)) errors.Add($"{label} frame ID is required.");

        if (!string.IsNullOrWhiteSpace(selection.RootSourceId)
            && !string.Equals(selection.RootSourceId.Trim(), source.Id?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"{label} root source '{selection.RootSourceId.Trim()}' does not match recipe source '{Clean(source.Id)}'.");
        }

        var binding = selection.SourceBinding;
        if (binding is null)
        {
            errors.Add($"{label} source binding is required.");
            return;
        }

        var isRawSourceBinding = string.Equals(binding.Format, "C3D", StringComparison.OrdinalIgnoreCase);
        var isArtifactBinding = binding.Format is "HeightField" or "TransformedHeightField";
        if (!isRawSourceBinding && !isArtifactBinding)
        {
            errors.Add($"{label} binding format must be C3D, HeightField, or TransformedHeightField.");
        }
        if (isRawSourceBinding)
        {
            if (!string.Equals(selection.FrameId?.Trim(), source.FrameId?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{label} frame '{Clean(selection.FrameId)}' does not match source frame '{Clean(source.FrameId)}'.");
            }
            if (!string.Equals(binding.Format?.Trim(), source.Format?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{label} source binding format '{Clean(binding.Format)}' does not match source format '{Clean(source.Format)}'.");
            }
            if (!string.IsNullOrWhiteSpace(binding.OwnerEntityId)
                || !string.IsNullOrWhiteSpace(binding.RootSourceContentSha256)
                || !string.IsNullOrWhiteSpace(binding.Unit)
                || !string.IsNullOrWhiteSpace(binding.FrameId))
            {
                errors.Add($"{label} raw C3D binding cannot declare artifact ownership fields.");
            }
        }
        if (isArtifactBinding)
        {
            if (!supportsArtifactOwnedSelections)
            {
                errors.Add($"{label} artifact-owned binding requires recipe schema {ToolRecipeDocument.ArtifactOwnedSelectionSchemaVersion} or newer.");
            }
            if (selection.Kind is not (
                    ToolRecipeSelectionKinds.GridRectangle
                    or ToolRecipeSelectionKinds.PointSet
                    or ToolRecipeSelectionKinds.OrientedBox3D
                    or ToolRecipeSelectionKinds.GridCircle
                    or ToolRecipeSelectionKinds.GridPolygon))
            {
                errors.Add($"{label} artifact HeightField binding supports GridRectangle, GridCircle, GridPolygon, PointSet, or OrientedBox3D geometry only.");
            }
            if (string.IsNullOrWhiteSpace(binding.OwnerEntityId)) errors.Add($"{label} artifact owner entity ID is required.");
            if (!IsSha256(binding.RootSourceContentSha256)) errors.Add($"{label} artifact root-source SHA-256 is required.");
            if (!string.Equals(binding.RootSourceContentSha256, source.ContentSha256, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{label} artifact root-source SHA-256 does not match the recipe source.");
            }
            if (string.IsNullOrWhiteSpace(binding.Unit)) errors.Add($"{label} artifact unit is required.");
            if (string.IsNullOrWhiteSpace(binding.FrameId)) errors.Add($"{label} artifact frame ID is required.");
            if (!string.Equals(selection.FrameId, binding.FrameId, StringComparison.Ordinal))
            {
                errors.Add($"{label} frame must match the owned artifact HeightField frame.");
            }
        }

        if (!IsSha256(binding.ContentSha256))
        {
            errors.Add($"{label} source binding SHA-256 must contain exactly 64 hexadecimal characters.");
        }

        if (binding.GridWidth <= 0 || binding.GridHeight <= 0)
        {
            errors.Add($"{label} source binding grid dimensions must be positive.");
        }

        if (string.Equals(selection.Kind, ToolRecipeSelectionKinds.GridRectangle, StringComparison.Ordinal))
        {
            ValidateGridRectangle(selection, binding, label, errors);
            return;
        }

        if (string.Equals(selection.Kind, ToolRecipeSelectionKinds.PointSet, StringComparison.Ordinal))
        {
            ValidatePointSet(selection, binding, label, errors);
            return;
        }

        if (string.Equals(selection.Kind, ToolRecipeSelectionKinds.LandmarkCorrespondenceSet, StringComparison.Ordinal))
        {
            ValidateCorrespondenceSet(selection, label, hasCorrespondenceDescriptor, errors, warnings, correspondenceRows);
            return;
        }

        if (string.Equals(selection.Kind, ToolRecipeSelectionKinds.OrientedBox3D, StringComparison.Ordinal))
        {
            ValidateOrientedBox3D(selection, label, supportsOrientedBox3D, errors);
            return;
        }

        if (string.Equals(selection.Kind, ToolRecipeSelectionKinds.GridCircle, StringComparison.Ordinal))
        {
            ValidateGridCircle(selection, binding, label, supportsGridCircle, errors);
            return;
        }

        if (string.Equals(selection.Kind, ToolRecipeSelectionKinds.GridPolygon, StringComparison.Ordinal))
        {
            ValidateGridPolygon(selection, binding, label, supportsGridPolygon, errors);
            return;
        }

        if (!string.IsNullOrWhiteSpace(selection.Kind))
        {
            errors.Add($"{label} kind '{selection.Kind.Trim()}' is not supported.");
        }
    }

    private static void ValidateGridRectangle(
        ToolRecipeSelection selection,
        ToolRecipeSelectionSourceBinding binding,
        string label,
        List<string> errors)
    {
        if (HasItems(selection.Points)
            || HasItems(selection.Rows)
            || selection.OrientedBox3D is not null
            || selection.GridCircle is not null
            || selection.GridPolygon is not null)
        {
            errors.Add($"{label} grid rectangle cannot contain circle, polygon, point-set, correspondence, or oriented-box payloads.");
        }

        foreach (var geometryError in ToolRecipeGridRectangleGeometry.Validate(
                     selection.GridRectangle,
                     binding.GridWidth,
                     binding.GridHeight))
        {
            errors.Add($"{label} {geometryError}.");
        }
    }

    private static void ValidatePointSet(
        ToolRecipeSelection selection,
        ToolRecipeSelectionSourceBinding binding,
        string label,
        List<string> errors)
    {
        if (selection.GridRectangle is not null
            || HasItems(selection.Rows)
            || selection.OrientedBox3D is not null
            || selection.GridCircle is not null
            || selection.GridPolygon is not null)
        {
            errors.Add($"{label} point set cannot contain rectangle, circle, polygon, correspondence, or oriented-box payloads.");
        }

        var points = selection.Points ?? [];
        if (points.Count is not (2 or 3))
        {
            errors.Add($"{label} point set must contain exactly two or three points.");
        }

        var cells = new HashSet<(int Row, int Column)>();
        var finitePositions = new List<ToolRecipeXyz>();
        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            var pointLabel = $"{label} point {index + 1}";
            if (point is null)
            {
                errors.Add($"{pointLabel} is required.");
                continue;
            }

            var locator = point.Locator;
            if (locator is null)
            {
                errors.Add($"{pointLabel} locator is required.");
            }
            else
            {
                if (!string.Equals(locator.Kind, GridCellLocatorKind, StringComparison.Ordinal))
                {
                    errors.Add($"{pointLabel} locator kind must be '{GridCellLocatorKind}'.");
                }

                if (locator.Row < 0 || locator.Column < 0
                    || locator.Row >= binding.GridHeight || locator.Column >= binding.GridWidth)
                {
                    errors.Add($"{pointLabel} locator is outside the recorded {binding.GridWidth} x {binding.GridHeight} C3D grid.");
                }

                if (!cells.Add((locator.Row, locator.Column)))
                {
                    errors.Add($"{label} repeats grid cell ({locator.Row}, {locator.Column}).");
                }
            }

            if (point.CapturedPosition is null || !IsFinite(point.CapturedPosition))
            {
                errors.Add($"{pointLabel} captured XYZ position must be finite.");
            }
            else
            {
                finitePositions.Add(point.CapturedPosition);
            }

            if (!double.IsFinite(point.RawHeight))
            {
                errors.Add($"{pointLabel} raw height must be finite.");
            }
        }

        if (points.Count == 3 && finitePositions.Count == 3 && AreCollinear(finitePositions[0], finitePositions[1], finitePositions[2]))
        {
            errors.Add($"{label} three captured XYZ positions must not be collinear.");
        }
    }

    private static void ValidateOrientedBox3D(
        ToolRecipeSelection selection,
        string label,
        bool supportsOrientedBox3D,
        List<string> errors)
    {
        if (!supportsOrientedBox3D)
        {
            errors.Add(
                $"{label} OrientedBox3D requires teaching recipe schema {ToolRecipeDocument.OrientedBox3DSchemaVersion} or newer.");
        }

        if (selection.GridRectangle is not null
            || HasItems(selection.Points)
            || HasItems(selection.Rows)
            || selection.CorrespondenceDescriptor is not null
            || selection.GridCircle is not null
            || selection.GridPolygon is not null)
        {
            errors.Add(
                $"{label} oriented box cannot contain rectangle, circle, polygon, point-set, or correspondence payloads.");
        }

        foreach (var geometryError in ToolRecipeOrientedBox3DGeometry.Validate(selection.OrientedBox3D))
        {
            errors.Add($"{label} {geometryError}.");
        }
    }

    private static void ValidateGridCircle(
        ToolRecipeSelection selection,
        ToolRecipeSelectionSourceBinding binding,
        string label,
        bool supportsGridCircle,
        List<string> errors)
    {
        if (!supportsGridCircle)
        {
            errors.Add(
                $"{label} GridCircle requires teaching recipe schema {ToolRecipeDocument.GridCircleSchemaVersion} or newer.");
        }

        if (selection.GridRectangle is not null
            || HasItems(selection.Points)
            || HasItems(selection.Rows)
            || selection.CorrespondenceDescriptor is not null
            || selection.OrientedBox3D is not null
            || selection.GridPolygon is not null)
        {
            errors.Add(
                $"{label} grid circle cannot contain rectangle, polygon, point-set, correspondence, or oriented-box payloads.");
        }

        foreach (var geometryError in ToolRecipeGridCircleGeometry.Validate(
                     selection.GridCircle,
                     binding.GridWidth,
                     binding.GridHeight))
        {
            errors.Add($"{label} {geometryError}.");
        }
    }

    private static void ValidateGridPolygon(
        ToolRecipeSelection selection,
        ToolRecipeSelectionSourceBinding binding,
        string label,
        bool supportsGridPolygon,
        List<string> errors)
    {
        if (!supportsGridPolygon)
        {
            errors.Add(
                $"{label} GridPolygon requires teaching recipe schema {ToolRecipeDocument.GridPolygonSchemaVersion} or newer.");
        }

        if (selection.GridRectangle is not null
            || HasItems(selection.Points)
            || HasItems(selection.Rows)
            || selection.CorrespondenceDescriptor is not null
            || selection.OrientedBox3D is not null
            || selection.GridCircle is not null)
        {
            errors.Add(
                $"{label} grid polygon cannot contain rectangle, circle, point-set, correspondence, or oriented-box payloads.");
        }

        foreach (var geometryError in ToolRecipeGridPolygonGeometry.Validate(
                     selection.GridPolygon,
                     binding.GridWidth,
                     binding.GridHeight))
        {
            errors.Add($"{label} {geometryError}.");
        }
    }

    private static void ValidateCorrespondenceSet(
        ToolRecipeSelection selection,
        string label,
        bool isCurrentSchema,
        List<string> errors,
        List<string> warnings,
        List<(string SelectionId, string SelectionLabel, ToolRecipeLandmarkCorrespondence Row)> correspondenceRows)
    {
        if (selection.GridRectangle is not null
            || HasItems(selection.Points)
            || selection.OrientedBox3D is not null
            || selection.GridCircle is not null
            || selection.GridPolygon is not null)
        {
            errors.Add($"{label} correspondence set cannot contain rectangle, circle, polygon, point-set, or oriented-box payloads.");
        }

        var descriptor = selection.CorrespondenceDescriptor;
        if (isCurrentSchema)
        {
            ValidateCorrespondenceDescriptor(descriptor, label, errors);
        }
        else if (descriptor is not null)
        {
            errors.Add($"{label} correspondence descriptor requires teaching recipe schema {ToolRecipeDocument.GenericMeasurementSchemaVersion} or newer.");
        }

        var rows = selection.Rows ?? [];
        var sourceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var referenceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var referenceFrames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var rowLabel = $"{label} correspondence row {index + 1}";
            if (row is null)
            {
                errors.Add($"{rowLabel} is required.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.SourceEntityId))
            {
                errors.Add($"{rowLabel} source entity ID is required.");
            }
            else if (!sourceIds.Add(row.SourceEntityId.Trim()))
            {
                errors.Add($"{label} repeats correspondence source entity '{row.SourceEntityId.Trim()}'.");
            }

            if (string.IsNullOrWhiteSpace(row.ReferenceLandmarkId))
            {
                errors.Add($"{rowLabel} reference landmark ID is required.");
            }
            else if (!referenceIds.Add(row.ReferenceLandmarkId.Trim()))
            {
                errors.Add($"{label} repeats reference landmark '{row.ReferenceLandmarkId.Trim()}'.");
            }

            if (row.ReferencePosition is null || !IsFinite(row.ReferencePosition))
            {
                errors.Add($"{rowLabel} reference XYZ position must be finite.");
            }

            if (string.IsNullOrWhiteSpace(row.ReferenceFrameId))
            {
                errors.Add($"{rowLabel} reference frame ID is required.");
            }
            else
            {
                referenceFrames.Add(row.ReferenceFrameId.Trim());
                if (descriptor is not null
                    && !string.Equals(row.ReferenceFrameId.Trim(), descriptor.ReferenceFrameId.Trim(), StringComparison.Ordinal))
                {
                    errors.Add($"{rowLabel} reference frame must match the correspondence descriptor.");
                }
            }

            correspondenceRows.Add((selection.Id, label, row));
        }

        if (referenceFrames.Count > 1)
        {
            errors.Add($"{label} correspondence rows must use one explicit reference frame.");
        }

        if (isCurrentSchema && rows.Count != 4)
        {
            errors.Add($"{label} Landmark Correspondence v1 requires exactly four rows.");
        }
        else if (rows.Count < 4)
        {
            warnings.Add($"{label} is taught only: at least four correspondence rows are required before XYZ affine execution.");
        }
    }

    private static void ValidateCorrespondenceDescriptor(
        ToolRecipeLandmarkCorrespondenceDescriptor? descriptor,
        string label,
        List<string> errors)
    {
        if (descriptor is null)
        {
            errors.Add($"{label} Landmark Correspondence v1 descriptor is required in schema {ToolRecipeDocument.GenericMeasurementSchemaVersion} or newer.");
            return;
        }

        if (string.IsNullOrWhiteSpace(descriptor.ReferenceFrameId)) errors.Add($"{label} reference frame ID is required.");
        if (string.IsNullOrWhiteSpace(descriptor.ReferenceUnit)) errors.Add($"{label} reference unit is required.");
        if (string.IsNullOrWhiteSpace(descriptor.ReferenceProvenance)) errors.Add($"{label} reference provenance is required.");
        if (string.IsNullOrWhiteSpace(descriptor.ReferenceRevision)) errors.Add($"{label} reference revision is required.");
        if (!string.Equals(descriptor.PairCountPolicy, "ExactlyFour", StringComparison.Ordinal)) errors.Add($"{label} PairCountPolicy must be ExactlyFour.");
        if (!string.Equals(descriptor.SourceArtifactPolicy, "CurrentPublishedCornerAnchor", StringComparison.Ordinal)) errors.Add($"{label} SourceArtifactPolicy must be CurrentPublishedCornerAnchor.");
        if (!string.Equals(descriptor.AffineIndependencePolicy, "RequireNonDegenerateTetrahedra", StringComparison.Ordinal)) errors.Add($"{label} AffineIndependencePolicy must be RequireNonDegenerateTetrahedra.");
        if (descriptor.MinimumNormalizedTetrahedronVolume is not { } minimum
            || !double.IsFinite(minimum) || minimum <= 0d || minimum >= 1d)
        {
            errors.Add($"{label} MinimumNormalizedTetrahedronVolume must be finite, greater than zero, and less than one.");
        }
    }

    private static bool AreCollinear(ToolRecipeXyz first, ToolRecipeXyz second, ToolRecipeXyz third)
    {
        var abX = second.X - first.X;
        var abY = second.Y - first.Y;
        var abZ = second.Z - first.Z;
        var acX = third.X - first.X;
        var acY = third.Y - first.Y;
        var acZ = third.Z - first.Z;
        var crossX = abY * acZ - abZ * acY;
        var crossY = abZ * acX - abX * acZ;
        var crossZ = abX * acY - abY * acX;
        return crossX == 0.0 && crossY == 0.0 && crossZ == 0.0;
    }

    private static bool IsFinite(ToolRecipeXyz point) =>
        double.IsFinite(point.X) && double.IsFinite(point.Y) && double.IsFinite(point.Z);

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static bool HasItems<T>(IReadOnlyList<T>? values) => values is { Count: > 0 };

    private static string Clean(string? value) => string.IsNullOrWhiteSpace(value) ? "<missing>" : value.Trim();
}
