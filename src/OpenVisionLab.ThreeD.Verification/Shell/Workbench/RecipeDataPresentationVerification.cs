using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class RecipeDataPresentationVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var checks = new List<(string Name, bool Passed, string Detail)>();
        var thread = new Thread(() =>
        {
            try { VerifyOnSta(reportPath, checks); }
            catch (Exception exception) { checks.Add(("Execution", false, exception.ToString())); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        var passed = checks.Count > 0 && checks.All(check => check.Passed);
        summary = $"RecipeDataPresentation|pass={passed}|checks={checks.Count(check => check.Passed)}/{checks.Count}";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, checks.Select(check => $"{(check.Passed ? "PASS" : "FAIL")}|{check.Name}|{check.Detail}").Append(summary));
        return passed;
    }

    private static void VerifyOnSta(string reportPath, List<(string Name, bool Passed, string Detail)> checks)
    {
        var app = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new global::Wpf.Ui.Markup.ThemesDictionary { Theme = global::Wpf.Ui.Appearance.ApplicationTheme.Dark });
        app.Resources.MergedDictionaries.Add(new global::Wpf.Ui.Markup.ControlsDictionary());
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/OpenVisionLab.ThreeD.Shell;component/Themes/OpenVisionThreeDTheme.xaml") });
        var frame = C3DHeightImageFrame.Create(C3DHeightFieldSnapshot.CreateForVerification("gallery.source", 3, 2, [1, 2, double.NaN, 4, 5, 6]));
        var converter = new HeightImageFrameConverter();
        var culture = CultureInfo.InvariantCulture;
        var bitmap = converter.Convert([frame.DefaultDisplayFrame, frame.SourceContentSha256, frame.SourceContentSha256.ToLowerInvariant(), true], typeof(ImageSource), null!, culture) as BitmapSource;
        checks.Add(("MatchingIdentityUsesFrozenNativePixels", bitmap is { IsFrozen: true, PixelWidth: 3, PixelHeight: 2 }, $"size={bitmap?.PixelWidth}x{bitmap?.PixelHeight}"));
        checks.Add(("WrongIdentityHasNoPreview", converter.Convert([frame.DefaultDisplayFrame, frame.SourceContentSha256, new string('a', 64), true], typeof(ImageSource), null!, culture) is null, "Cross-source preview rejected"));
        checks.Add(("UnavailableArtifactHasNoPreview", converter.Convert([frame.DefaultDisplayFrame, frame.SourceContentSha256, frame.SourceContentSha256, false], typeof(ImageSource), null!, culture) is null, "Stale/unavailable preview rejected"));
        checks.Add(("ExistingSamplePreviewContractPreserved", ReferenceEquals(bitmap, converter.Convert([frame.DefaultDisplayFrame, "sample.c3d", "SAMPLE.C3D"], typeof(ImageSource), null!, culture))
            && converter.Convert([frame.DefaultDisplayFrame, "a.c3d", "b.c3d"], typeof(ImageSource), null!, culture) is null, "Three-value path guard and bitmap reuse"));
        var cardPreviews = new Dictionary<string, BitmapSource>(StringComparer.OrdinalIgnoreCase) { ["card-a.c3d"] = bitmap! };
        checks.Add(("IndependentCardPreviewPathGuard", ReferenceEquals(bitmap, converter.Convert([cardPreviews, "CARD-A.C3D"], typeof(ImageSource), null!, culture))
            && converter.Convert([cardPreviews, "card-b.c3d"], typeof(ImageSource), null!, culture) is null, "Card dictionary path guard does not borrow selected viewer pixels"));
        var outputFrame = C3DHeightImageFrame.Create(C3DHeightFieldSnapshot.CreateForVerification("gallery.filtered", 3, 2, [6, 5, double.NaN, 3, 2, 1]));
        var outputBitmap = converter.Convert([outputFrame.DefaultDisplayFrame, "gallery.filtered", "GALLERY.FILTERED", true, true], typeof(ImageSource), null!, culture) as BitmapSource;
        checks.Add(("MatchingFlowOutputUsesExistingFrozenPixels", outputBitmap is { IsFrozen: true, PixelWidth: 3, PixelHeight: 2 } && !ReferenceEquals(bitmap, outputBitmap), "Existing output frame and matching entity identity; no viewer-mode load"));
        checks.Add(("FlowOutputGuardRejectsWrongMissingAndUnavailableIdentity", converter.Convert([outputFrame.DefaultDisplayFrame, "gallery.filtered", "gallery.other", true, true], typeof(ImageSource), null!, culture) is null
            && converter.Convert([outputFrame.DefaultDisplayFrame, "", "", true, true], typeof(ImageSource), null!, culture) is null
            && converter.Convert([null!, "gallery.filtered", "gallery.filtered", true, true], typeof(ImageSource), null!, culture) is null
            && converter.Convert([outputFrame.DefaultDisplayFrame, "gallery.filtered", "gallery.filtered", false, true], typeof(ImageSource), null!, culture) is null
            && converter.Convert([outputFrame.DefaultDisplayFrame, "gallery.filtered", "gallery.filtered", true, false], typeof(ImageSource), null!, culture) is null, "Wrong/empty entity, missing frame, unavailable and numeric artifacts fail closed"));
        var outputStep = new ToolWorkbenchPipelineStepItem("step.gallery.filter", ToolWorkbenchToolCatalog.Create().Single(tool => tool.Id == "filter"), "gallery.source", "gallery.filtered");
        outputStep.UpdateFlowPreviews(new Dictionary<string, C3DHeightImageDisplayFrame> { ["gallery.filtered"] = outputFrame.DefaultDisplayFrame }, new Dictionary<string, ToolRecipeSelection>(), new Dictionary<string, string>());
        var longName = "입력 · 매우 긴 실제 시료 이름과 반복 검사 식별자가 있는 높이 맵.C3D";
        ToolWorkbenchDisplayedOutputItem Item(string id, string contract, string hash, string kind, string name, string state = "Published", ToolWorkbenchPipelineStepItem? step = null) => new(new ToolWorkbenchArtifactItem(id, name, contract, state, "gallery.source", "gallery.source", frame.Unit, frame.FrameId, hash, "Actual fixture artifact", step, kind));
        var source = Item("gallery.source", "SourceC3D", frame.SourceContentSha256, "Source", longName);
        source.UpdatePresentation(true, true, string.Empty, false, "Native input available", string.Empty);
        var wrong = Item("gallery.wrong", "HeightField", new string('a', 64), "HeightField", "다른 입력의 출력");
        wrong.UpdatePresentation(true, false, string.Empty, false, "Different input", string.Empty);
        var evidence = Item("gallery.measurement", "Measurement", frame.SourceContentSha256, "Measurement", "숫자 결과");
        var filtered = Item("gallery.filtered", "FilteredHeightField", outputFrame.SourceContentSha256, "FilteredHeightField", "필터 높이 맵", step: outputStep);
        filtered.UpdatePresentation(true, false, string.Empty, false, "Published filtered input", string.Empty);
        var leveled = Item("gallery.leveled", "LeveledHeightField + LevelingTransform + LevelFrame", frame.SourceContentSha256, "LeveledHeightField", "수평 높이 맵");
        leveled.UpdatePresentation(true, false, string.Empty, false, "Published leveled input", string.Empty);
        var declared = Item("gallery.declared", "FilteredHeightField", string.Empty, "DeclaredOutput", "실행 전 필터 높이 맵", "Declared");
        declared.UpdatePresentation(false, false, string.Empty, false, "No current output", string.Empty);
        var stale = Item("gallery.stale", "FilteredHeightField", string.Empty, "FilteredHeightField", "이전 출력 높이 맵", "Stale");
        stale.UpdatePresentation(false, false, string.Empty, false, "No current output", string.Empty);
        var deviation = Item("gallery.deviation", "SignedDeltaHeightField", string.Empty, "SignedDeltaHeightField", "실제 편차 맵");
        source.UpdateGalleryRole(outputStep);
        filtered.UpdateGalleryRole(outputStep);
        var metadataFixture = new ToolWorkbenchDisplayedOutputItem(source.Artifact with
        {
            Minimum = 1, Maximum = 6, ValidSampleCount = 5, MissingSampleCount = 1, GridWidth = 3, GridHeight = 2
        });
        metadataFixture.UpdatePresentation(true, false, string.Empty, false, "Available", string.Empty);
        checks.Add(("GalleryUsesActualStatisticsAndKeepsUnavailableValuesExplicit", metadataFixture.GalleryMetadata.Contains("16.7%")
            && metadataFixture.GalleryPreviewSummary.Contains("1 – 6") && metadataFixture.GalleryPreviewSummary.Contains(frame.Unit)
            && metadataFixture.GalleryPreviewSummary.Contains("3 × 2") && declared.GalleryPreviewSummary.Contains("—"),
            "Independent fixture: five valid cells and one missing cell, range 1..6; unavailable values use a dash"));
        checks.Add(("SelectedToolRolesUseEntityIdentities", source.GalleryRole is "입력" or "Input"
            && filtered.GalleryRole is "출력" or "Output" && source.IsRelatedToSelectedStep && filtered.IsRelatedToSelectedStep
            && !wrong.IsRelatedToSelectedStep, "Input/output badges follow exact selected-step entity identities"));
        var display = new RecordingCommand(parameter => parameter is ToolWorkbenchDisplayedOutputItem { CanShowInViewer: true });
        var view = new RecipeDataView { DataContext = new
        {
            DisplayedOutputs = new[] { source, wrong, evidence, filtered, leveled, declared, stale, deviation },
            HeightImageViewer = new { Frame = frame, DisplayFrame = frame.DefaultDisplayFrame },
            MainViewerPane = new { HeightImage = new { Frame = (C3DHeightImageFrame?)null, DisplayFrame = (C3DHeightImageDisplayFrame?)null } },
            ShowDisplayedOutputInViewerCommand = display,
            ShowGalleryInActivePaneCommand = display,
            ShowGalleryInOtherPaneCommand = display,
            ShowGalleryInLeftPaneCommand = display,
            ShowGalleryInRightPaneCommand = display,
            FocusDisplayedOutputStepCommand = new RecordingCommand(parameter => parameter is ToolWorkbenchDisplayedOutputItem { CanFocusStep: true }),
            Localization = ThreeDLocalization.Shared,
            Source = new { Name = longName, Path = @"D:\fixture\input.C3D" },
            Selections = Array.Empty<object>()
        } };
        view.Measure(new Size(232, 620));
        view.Arrange(new Rect(0, 0, 232, 620));
        view.UpdateLayout();
        var thumbnails = Descendants<Button>(view).Where(button => button.Name == "GalleryThumbnail").ToArray();
        var inputImage = Descendants<Image>(thumbnails[0]).Single(image => image.Name == "GalleryInputImage");
        var otherImage = Descendants<Image>(thumbnails[1]).Single(image => image.Name == "GalleryInputImage");
        checks.Add(("TemplateResolvesInjectedFrame", ReferenceEquals(inputImage.Source, bitmap) && otherImage.Source is null, "Inherited DataContext, source identity, template ancestor binding"));
        var outputImage = Descendants<Image>(thumbnails[3]).Single(image => image.Name == "GalleryOutputImage");
        checks.Add(("TemplateResolvesFlowOutputWithoutHeightPaneFrame", ReferenceEquals(outputImage.Source, outputBitmap), "3D-mode pane has no height frame; existing flow output still renders its own pixels"));
        var cards = Descendants<Border>(view).Where(border => System.Windows.Automation.AutomationProperties.GetAutomationId(border) == "RecipeDataGalleryCard").ToArray();
        checks.Add(("ImageCardsExcludeNumericEvidence", cards.Length == 8 && cards[0].Visibility == Visibility.Visible && cards[2].Visibility == Visibility.Collapsed, $"cards={cards.Length};measurement={cards[2].Visibility}"));
        checks.Add(("PublishedAndDeclaredHeightMapsRemainInGallery", cards[3].Visibility == Visibility.Visible && cards[4].Visibility == Visibility.Visible
            && cards[5].Visibility == Visibility.Visible && thumbnails[3].IsEnabled && thumbnails[4].IsEnabled && !thumbnails[5].IsEnabled
            && !evidence.IsImageData, "Filtered and leveled C3D outputs stay visible; declared maps stay visible and disabled; numeric evidence stays excluded"));
        var title = Descendants<TextBlock>(cards[0]).First(text => text.Text == source.GalleryName);
        checks.Add(("SemanticMapTitlePreservesActualSourceIdentity", title.Text == source.GalleryName && title.TextWrapping == TextWrapping.Wrap
            && source.DisplayName == longName && source.GalleryIdentitySummary.Contains(longName) && cards[0].ActualWidth == 216,
            $"title={title.Text};source={source.DisplayName};cardWidth={cards[0].ActualWidth}"));
        var cardButtons = Descendants<Button>(cards[0]).Where(button => button.Name != "GalleryThumbnail").ToArray();
        var firstPosition = cardButtons[0].TranslatePoint(new Point(), cards[0]);
        var secondPosition = cardButtons[1].TranslatePoint(new Point(), cards[0]);
        checks.Add(("TeachingGalleryTypographyAndButtonGapMatchMockup", title.LineStackingStrategy == LineStackingStrategy.BlockLineHeight
            && Math.Abs(title.LineHeight - 17.4) < 0.001 && title.FontFamily.Source == "Segoe UI Variable Text, Segoe UI, Malgun Gothic"
            && Math.Abs(secondPosition.X - firstPosition.X - cardButtons[0].ActualWidth - 2) < 0.001,
            $"titleLine={title.LineHeight};font={title.FontFamily.Source};gap={secondPosition.X - firstPosition.X - cardButtons[0].ActualWidth}"));
        var typeMarker = Descendants<System.Windows.Shapes.Ellipse>(cards[0]).Single(marker => marker.Name == "GalleryTypeMarker");
        checks.Add(("TeachingTypeMarkerReservesMockupTitleInset", typeMarker.ActualWidth == 8 && typeMarker.ActualHeight == 8 && !typeMarker.IsHitTestVisible
            && ((SolidColorBrush)typeMarker.Fill).Color.ToString() == "#FF65B9AE"
            && Math.Abs(title.TranslatePoint(new Point(), cards[0]).X - typeMarker.TranslatePoint(new Point(), cards[0]).X - 13) < 0.001,
            "Actual height-map type uses 8px dot and 5px gap; full long title keeps wrapping"));
        var deviationMarker = Descendants<System.Windows.Shapes.Ellipse>(cards[7]).Single(marker => marker.Name == "GalleryTypeMarker");
        checks.Add(("SignedDeviationTypeMarkerKeepsItsDistinctMeaning", ((SolidColorBrush)deviationMarker.Fill).Color.ToString() == "#FFF2B35E", "SignedDeltaHeightField uses the mockup deviation color instead of the height-map color"));
        var metadata = Descendants<TextBlock>(cards[4]).Single(text => text.Text == leveled.GalleryMetadata);
        checks.Add(("ProducerMetadataUsesOneLineAndKeepsIdentityInSupplementalInfo", metadata.TextWrapping == TextWrapping.NoWrap
            && metadata.TextTrimming == TextTrimming.CharacterEllipsis && Math.Abs(metadata.ActualHeight - 15.95) < 0.001
            && leveled.GalleryIdentitySummary.Contains(frame.Unit),
            $"metadata={metadata.Text};actualUnit={frame.Unit}"));
        var normalCardRows = string.Join(',', Descendants<Grid>(cards[3]).First(grid => grid.RowDefinitions.Count == 3).RowDefinitions.Select(row => row.ActualHeight));
        var normalCardCaptions = string.Join(';', Descendants<TextBlock>(cards[3]).Where(text => text.GetType() == typeof(TextBlock)).Select(text => $"{text.Name}:{text.Text}:{text.ActualHeight}/{text.DesiredSize.Height}"));
        checks.Add(("SingleLineTeachingCardHeightMatchesMockup", Math.Abs(cards[3].ActualHeight - 92.9375) <= 1.5,
            $"actual={cards[3].ActualHeight};immutableHTML=92.9375;tolerance=1.5;rows={normalCardRows};text={normalCardCaptions}"));
        var normalCardMetadata = Descendants<TextBlock>(cards[3]).Single(text => text.Text == filtered.GalleryMetadata);
        var normalCardActions = Descendants<Button>(cards[3]).Where(button => button.Name != "GalleryThumbnail").ToArray();
        checks.Add(("CompactCardMetadataAndActionsDoNotOverlapOrClip", normalCardActions.All(button =>
            normalCardMetadata.TranslatePoint(new Point(), cards[3]).Y + normalCardMetadata.ActualHeight <= button.TranslatePoint(new Point(), cards[3]).Y
            && button.TranslatePoint(new Point(), cards[3]).Y + button.ActualHeight <= cards[3].ActualHeight - 6),
            "The producer row and two pane actions remain inside the teaching card"));
        var shownMarker = Descendants<Border>(cards[0]).Single(border => border.Name == "GalleryShownMarker");
        var thumbnailSurface = Descendants<Border>(thumbnails[0]).Single(border => border.Name == "ThumbnailSurface");
        checks.Add(("TeachingPaletteAndShownMarkerMatchMockup", ((SolidColorBrush)cards[0].Background).Color.ToString() == "#FF1C2629"
            && ((SolidColorBrush)thumbnailSurface.Background).Color.ToString() == "#FF0F1317" && ((SolidColorBrush)thumbnailSurface.BorderBrush).Color.ToString() == "#FF33404C"
            && ((SolidColorBrush)Descendants<TextBlock>(cards[0]).First(text => text.Text == source.GalleryMetadata).Foreground).Color.ToString() == "#FF8B98A6"
            && shownMarker.Visibility == Visibility.Visible && shownMarker.ActualWidth == 3 && Math.Abs(shownMarker.TranslatePoint(new Point(), cards[0]).X - 1) < 0.001
            && !shownMarker.IsHitTestVisible, "Actual shown flag produces inset3px marker while preserving card surface and pointer hit region"));
        var legacyGallery = new RecipeDataView { DataContext = view.DataContext };
        var legacyParent = new ToolRecipeWorkbenchView { DataContext = new { IsTeachWorkspaceSelected = false }, Content = legacyGallery };
        legacyParent.Measure(new Size(232, 620));
        legacyParent.Arrange(new Rect(0, 0, 232, 620));
        legacyParent.UpdateLayout();
        var legacyCard = Descendants<Border>(legacyGallery).First(border => System.Windows.Automation.AutomationProperties.GetAutomationId(border) == "RecipeDataGalleryCard");
        var legacyThumbnail = Descendants<Button>(legacyCard).Single(button => button.Name == "GalleryThumbnail");
        var legacySurface = Descendants<Border>(legacyThumbnail).Single(border => border.Name == "ThumbnailSurface");
        checks.Add(("LegacyGalleryPaletteAndSelectionContractPreserved", ((SolidColorBrush)legacyCard.Background).Color.ToString() == "#FF243C3C"
            && ((SolidColorBrush)legacySurface.Background).Color.ToString() == "#FF11161C" && ((SolidColorBrush)legacySurface.BorderBrush).Color.ToString() == "#FF37424E"
            && ((SolidColorBrush)Descendants<TextBlock>(legacyCard).First(text => text.Text == source.StateDisplay).Foreground).Color.ToString() == "#FFADB9C6"
            && Descendants<Border>(legacyCard).Single(border => border.Name == "GalleryShownMarker").Visibility == Visibility.Collapsed,
            "Existing parent IsTeachWorkspaceSelected=false path keeps prior Lab palette and selected surface; isolated render, not native Lab flow"));
        var legacyContract = Descendants<TextBlock>(legacyCard).Single(text => text.Name == "GalleryLegacyContract");
        checks.Add(("LegacyGalleryKeepsFullContractAndOriginalTitleInset", Descendants<System.Windows.Shapes.Ellipse>(legacyCard).Single(marker => marker.Name == "GalleryTypeMarker").Visibility == Visibility.Collapsed
            && Descendants<Grid>(legacyCard).Single(grid => grid.Name == "GalleryTeachingContract").Visibility == Visibility.Collapsed
            && legacyContract.Visibility == Visibility.Visible && string.Concat(legacyContract.Inlines.OfType<System.Windows.Documents.Run>().Select(run => run.Text)) == $"{source.ContractDisplay} · {frame.Unit}" && legacyContract.TextWrapping == TextWrapping.Wrap,
            "Lab consumer retains its existing wrapped contract/unit and title width"));
        var legacyContractRow = Descendants<Grid>(legacyCard).First(grid => grid.RowDefinitions.Count == 3).RowDefinitions[2];
        checks.Add(("LegacyGalleryKeepsItsExistingStarRow", legacyContractRow.Height.IsStar && legacyContractRow.MinHeight == 63,
            "Lab keeps its existing 63px row minimum formerly supplied by the spanning thumbnail"));
        var legacyPopup = (ToolTip)legacyThumbnail.ToolTip;
        legacyPopup.PlacementTarget = legacyThumbnail;
        legacyPopup.ApplyTemplate();
        legacyPopup.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        legacyPopup.Arrange(new Rect(new Point(), legacyPopup.DesiredSize));
        legacyPopup.UpdateLayout();
        var implicitPopup = new ToolTip { Style = (Style)app.FindResource(typeof(ToolTip)), Content = "Existing theme tooltip" };
        implicitPopup.ApplyTemplate();
        implicitPopup.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        implicitPopup.Arrange(new Rect(new Point(), implicitPopup.DesiredSize));
        implicitPopup.UpdateLayout();
        var legacyPopupContent = Descendants<StackPanel>(legacyPopup).Single(panel => panel.MaxWidth == 384);
        var legacyPopupImage = (System.Windows.Shapes.Rectangle)legacyPopupContent.Children[1];
        checks.Add(("LegacyPopupKeepsOriginalThemeTemplateAndContentOrder", ReferenceEquals(legacyPopup.Template, implicitPopup.Template)
            && legacyPopup.Placement == implicitPopup.Placement && legacyPopup.CustomPopupPlacementCallback is null
            && legacyPopup.Padding == implicitPopup.Padding && legacyPopup.MaxWidth == implicitPopup.MaxWidth && double.IsNaN(legacyPopup.Width)
            && legacyPopupContent.Children.Count == 3 && legacyPopupContent.Children[0] is TextBlock { TextWrapping: TextWrapping.Wrap } legacyPopupTitle
            && legacyPopupTitle.Text == longName && legacyPopupImage.Width == 384 && legacyPopupImage.Height == 252,
            $"legacy={legacyPopup.ActualWidth}x{legacyPopup.ActualHeight};padding={legacyPopup.Padding};max={legacyPopup.MaxWidth};placement={legacyPopup.Placement};image={legacyPopupImage.Width}x{legacyPopupImage.Height}"));
        var legacyDeclaredThumbnail = Descendants<Button>(legacyGallery).Where(button => button.Name == "GalleryThumbnail").ElementAt(5);
        checks.Add(("LegacyUnavailableTooltipAndSystemDelayRemainAvailable", ToolTipService.GetIsEnabled(legacyDeclaredThumbnail)
            && ToolTipService.GetShowOnDisabled(legacyDeclaredThumbnail) && ToolTipService.GetInitialShowDelay(legacyThumbnail) == ToolTipService.GetInitialShowDelay(new Button()),
            $"enabled={ToolTipService.GetIsEnabled(legacyDeclaredThumbnail)};showDisabled={ToolTipService.GetShowOnDisabled(legacyDeclaredThumbnail)};delay={ToolTipService.GetInitialShowDelay(legacyThumbnail)}"));
        var legacyPopupRender = new RenderTargetBitmap((int)Math.Ceiling(legacyPopup.ActualWidth), (int)Math.Ceiling(legacyPopup.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        legacyPopupRender.Render(legacyPopup);
        var legacyPopupEncoder = new PngBitmapEncoder();
        legacyPopupEncoder.Frames.Add(BitmapFrame.Create(legacyPopupRender));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        using (var legacyPopupStream = File.Create(Path.ChangeExtension(reportPath, null) + "-legacy-popup.png")) legacyPopupEncoder.Save(legacyPopupStream);
        var popup = (ToolTip)thumbnails[0].ToolTip;
        popup.PlacementTarget = thumbnails[0];
        popup.ApplyTemplate();
        popup.Measure(new Size(306, double.PositiveInfinity));
        popup.Arrange(new Rect(new Point(), popup.DesiredSize));
        popup.UpdateLayout();
        var popupSurface = (Border)popup.Template.FindName("GalleryPopupSurface", popup);
        var popupContent = Descendants<StackPanel>(popup).Single(panel => panel.Width == 288);
        var popupImage = (System.Windows.Shapes.Rectangle)popupContent.Children[0];
        var popupTitle = Descendants<TextBlock>(popupContent).First(text => text.Text == source.GalleryName);
        var popupPosition = RecipeDataView.GalleryPreviewPlacement(new Size(306, 270.734), new Size(96, 63), new Point());
        checks.Add(("PopupKeepsImageFirstAndMockupGeometry", popup.ActualWidth == 306 && popup.Padding == new Thickness(8)
            && popupImage.ActualWidth == 288 && popupImage.ActualHeight == 189 && popupImage.RadiusX == 5 && popupImage.RadiusY == 5
            && popupSurface.CornerRadius == new CornerRadius(8) && popupTitle.Text == source.GalleryName && popupTitle.TextTrimming == TextTrimming.CharacterEllipsis
            && popupTitle.FontSize == 12.5 && Math.Abs(popupTitle.LineHeight - 18.125) < 0.001 && popup.FontFamily.Source == title.FontFamily.Source
            && ((SolidColorBrush)popupSurface.Background).Color.ToString() == "#FF20272F" && ((SolidColorBrush)popupSurface.BorderBrush).Color.ToString() == "#FF4E5D6B",
            $"popup={popup.ActualWidth}x{popup.ActualHeight};image={popupImage.ActualWidth}x{popupImage.ActualHeight};title={popupTitle.Text};font={popup.FontFamily.Source}"));
        checks.Add(("PopupHasScreenEdgeFallbackWithoutStateAccess", popup.Placement == System.Windows.Controls.Primitives.PlacementMode.Custom
            && popupPosition.Length == 2 && popupPosition[0].Point.X == -316 && popupPosition[1].Point.X == 106
            && Math.Abs(popupPosition[0].Point.Y + 103.867) < 0.001 && Math.Abs(popupPosition[1].Point.Y + 103.867) < 0.001,
            "Right-side Data panel opens left inside Compact app bounds, with right-side screen-edge fallback; native selection requires actual popup"));
        var declaredCaption = Descendants<TextBlock>(thumbnails[5]).First();
        var staleCaption = Descendants<TextBlock>(thumbnails[6]).First();
        checks.Add(("UnavailableCardsExplainRequiredRunWithoutPopup", declaredCaption.Text is "실행하면 표시" or "Run to display"
            && staleCaption.Text is "다시 실행 필요" or "Run again" && !ToolTipService.GetIsEnabled(thumbnails[5]) && !ToolTipService.GetIsEnabled(thumbnails[6])
            && ToolTipService.GetIsEnabled(thumbnails[0]) && ToolTipService.GetInitialShowDelay(thumbnails[0]) == 320,
            $"declared={declaredCaption.Text};stale={staleCaption.Text};sourceTooltip={ToolTipService.GetIsEnabled(thumbnails[0])}"));
        var popupRender = new RenderTargetBitmap(306, (int)Math.Ceiling(popup.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        popupRender.Render(popup);
        var popupEncoder = new PngBitmapEncoder();
        popupEncoder.Frames.Add(BitmapFrame.Create(popupRender));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        using (var popupStream = File.Create(Path.ChangeExtension(reportPath, null) + "-popup.png")) popupEncoder.Save(popupStream);
        ((IInvokeProvider)new ButtonAutomationPeer(thumbnails[0]).GetPattern(PatternInterface.Invoke)).Invoke();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
        checks.Add(("ThumbnailRoutesBoundActivePaneCommand", display.Count == 1 && ReferenceEquals(display.Parameter, source), "Bound command receives artifact; fixture has no execution command"));
        source.UpdatePresentation(false, false, string.Empty, false, "Unavailable", string.Empty);
        display.NotifyChanged();
        view.UpdateLayout();
        checks.Add(("UnavailableBindingClearsPixelsAndDisablesButton", inputImage.Source is null && !thumbnails[0].IsEnabled && !ToolTipService.GetIsEnabled(thumbnails[0]) && shownMarker.Visibility == Visibility.Collapsed && display.Count == 1, $"pixelsNull={inputImage.Source is null};enabled={thumbnails[0].IsEnabled};displayCount={display.Count}"));
        filtered.UpdatePresentation(false, false, string.Empty, false, "Unavailable", string.Empty);
        display.NotifyChanged();
        view.UpdateLayout();
        checks.Add(("UnavailableFlowOutputClearsPixelsAndDisablesButton", outputImage.Source is null && !thumbnails[3].IsEnabled && display.Count == 1, "Previously rendered output is cleared when availability changes; no execution"));
        var info = (Expander)view.FindName("RecipeDataInformation");
        var tabs = (TabControl)view.FindName("RecipeDataTabs");
        checks.Add(("InputRoiOutputsRemainReachable", !info.IsExpanded && tabs.Items.Count == 3 && ((TabItem)tabs.Items[1]).Header.ToString() == "ROI", $"expanded={info.IsExpanded};tabs={tabs.Items.Count}"));
        var render = new RenderTargetBitmap(232, 620, 96, 96, PixelFormats.Pbgra32);
        render.Render(view);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(render));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        using var stream = File.Create(Path.ChangeExtension(reportPath, ".png"));
        encoder.Save(stream);
        VerifyActualDisplayComposition(reportPath, checks);
    }

    private static void VerifyActualDisplayComposition(string reportPath, List<(string Name, bool Passed, string Detail)> checks)
    {
        var root = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, Path.GetFileNameWithoutExtension(reportPath) + "-display-fixture");
        Directory.CreateDirectory(root);
        var sourcePath = Path.Combine(root, "source.C3D");
        C3DHeightFieldSnapshot.CreateForVerification("gallery.display.source", 3, 3, [1, 2, 3, 4, 5, 6, 7, 8, 9]).SaveC3D(sourcePath);
        using var workbench = new ToolWorkbenchViewModel(Path.Combine(root, "recent.json"));
        workbench.SetC3DSource(sourcePath, markDirty: false);
        var filterTool = workbench.Tools.Single(tool => tool.Id == "filter");
        workbench.SelectedTool = filterTool;
        workbench.AddSelectedToolCommand.Execute(filterTool);
        var step = workbench.SelectedPipelineStep!;
        var displayRequests = 0;
        workbench.ViewerArtifactDisplayRequested += (_, request) => { displayRequests++; request.WasDisplayed = true; };
        bool WaitUntil(Func<bool> ready)
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!ready() && DateTime.UtcNow < deadline)
            {
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
                Thread.Sleep(10);
            }
            return ready();
        }
        var ready = WaitUntil(() => workbench.RunSelectedStepCommand.CanExecute(null));
        var declared = workbench.DisplayedOutputs.Single(item => item.Id == step.OutputEntityId);
        var beforeRun = workbench.RunLog.Count;
        workbench.ShowDisplayedOutputInViewerCommand.Execute(declared);
        checks.Add(("ActualCompositionRejectsDeclaredDisplayWithoutExecution", ready && !declared.CanShowInViewer && displayRequests == 0
            && workbench.RunLog.Count == beforeRun && workbench.EffectiveMainViewerContentId == workbench.Source.Id,
            $"ready={ready};displayRequests={displayRequests};log={workbench.RunLog.Count};effective={workbench.EffectiveMainViewerContentId}"));
        workbench.RunSelectedStepCommand.Execute(null);
        var published = WaitUntil(() => !workbench.IsSelectedStepPreviewRunning && workbench.RunSelectedStepCommand.CanExecute(null)
            && workbench.DisplayedOutputs.Any(item => item.Id == step.OutputEntityId && item.CanShowInViewer));
        var output = workbench.DisplayedOutputs.Single(item => item.Id == step.OutputEntityId);
        checks.Add(("ActualCompositionPublishesExistingC3DOutputInInputView", published && workbench.HasInspectionStep && workbench.IsInspectionInputView
            && workbench.EffectiveMainViewerContentId == workbench.Source.Id, $"published={published};effective={workbench.EffectiveMainViewerContentId};output={output.Id}"));
        var qualityReady = WaitUntil(() => workbench.SourceQuality.Report is not null);
        var sourceStats = workbench.DisplayedOutputs.Single(item => item.Id == workbench.Source.Id);
        checks.Add(("CachedSdkStatisticsReachCurrentDataCards", qualityReady && sourceStats.Artifact.Minimum == 1 && sourceStats.Artifact.Maximum == 9
            && sourceStats.Artifact.ValidSampleCount == 9 && sourceStats.Artifact.MissingSampleCount == 0
            && sourceStats.Artifact.GridWidth == 3 && sourceStats.Artifact.GridHeight == 3
            && output.Artifact.ValidSampleCount == 9 && output.Artifact.MissingSampleCount == 0,
            $"source={sourceStats.Artifact.Minimum}..{sourceStats.Artifact.Maximum};valid={sourceStats.Artifact.ValidSampleCount};missing={sourceStats.Artifact.MissingSampleCount};outputValid={output.Artifact.ValidSampleCount}"));
        var beforeDisplay = workbench.RunLog.Count;
        var dirty = workbench.IsDirty;
        var inputs = step.InputEntityIds.ToArray();
        workbench.ShowDisplayedOutputInViewerCommand.Execute(output);
        output = workbench.DisplayedOutputs.Single(item => item.Id == step.OutputEntityId);
        checks.Add(("ActualDisplayCommandSelectsEffectiveMainOutput", workbench.ViewerWorkspace.MainContentId == output.Id
            && workbench.EffectiveMainViewerContentId == output.Id && workbench.MainViewerDisplayedContentId == output.Id && output.IsShownInViewer
            && displayRequests == 1 && workbench.RunLog.Count == beforeDisplay && workbench.IsDirty == dirty && step.InputEntityIds.SequenceEqual(inputs),
            $"pin={workbench.ViewerWorkspace.MainContentId};effective={workbench.EffectiveMainViewerContentId};shown={output.IsShownInViewer};log={workbench.RunLog.Count};acceptedRenderer=fixture"));
        var source = workbench.DisplayedOutputs.Single(item => item.Id == workbench.Source.Id);
        workbench.ShowDisplayedOutputInViewerCommand.Execute(source);
        source = workbench.DisplayedOutputs.Single(item => item.Id == workbench.Source.Id);
        output = workbench.DisplayedOutputs.Single(item => item.Id == step.OutputEntityId);
        checks.Add(("ActualDisplaySourceRecoveryPreservesRecipeAndExecution", workbench.EffectiveMainViewerContentId == source.Id
            && source.IsShownInViewer && !output.IsShownInViewer && displayRequests == 2 && workbench.RunLog.Count == beforeDisplay
            && workbench.IsDirty == dirty && step.InputEntityIds.SequenceEqual(inputs),
            $"pin={workbench.ViewerWorkspace.MainContentId};effective={workbench.EffectiveMainViewerContentId};log={workbench.RunLog.Count};dirty={workbench.IsDirty};inputs={string.Join(',', inputs)}"));
        workbench.ShowGalleryInLeftPaneCommand.Execute(source);
        checks.Add(("GalleryLeftCreatesTwoPanesAndFocusesLeft", workbench.IsSplitVerticalViewerLayout
            && workbench.EffectiveMainViewerContentId == source.Id && workbench.ViewerWorkspace.FocusedSlotId == ViewerWorkspaceSession.MainSlotId,
            $"layout={workbench.EffectiveViewerLayout};left={workbench.EffectiveMainViewerContentId};focus={workbench.ViewerWorkspace.FocusedSlotId}"));
        workbench.ShowGalleryInRightPaneCommand.Execute(output);
        checks.Add(("GalleryRightShowsOutputWithoutReplacingLeft", workbench.EffectiveMainViewerContentId == source.Id
            && workbench.EffectiveAuxiliaryViewerContentId == output.Id && source.IsShownInGallery && output.IsShownInGallery
            && workbench.ViewerWorkspace.FocusedSlotId == ViewerWorkspaceSession.AuxiliarySlotId,
            $"left={workbench.EffectiveMainViewerContentId};right={workbench.EffectiveAuxiliaryViewerContentId}"));
        workbench.ShowGalleryInOtherPaneCommand.Execute(output);
        checks.Add(("GalleryOtherPaneUsesOppositeFocus", workbench.EffectiveMainViewerContentId == output.Id
            && workbench.EffectiveAuxiliaryViewerContentId == output.Id && !source.IsShownInGallery
            && workbench.ViewerWorkspace.FocusedSlotId == ViewerWorkspaceSession.MainSlotId,
            "Other-pane gesture changes left while right retains the existing output"));
        workbench.ShowGalleryInActivePaneCommand.Execute(source);
        checks.Add(("GalleryActivePaneRestoresInputWithoutRunningOrEditing", workbench.EffectiveMainViewerContentId == source.Id
            && workbench.EffectiveAuxiliaryViewerContentId == output.Id && displayRequests == 2
            && workbench.RunLog.Count == beforeDisplay && workbench.IsDirty == dirty && step.InputEntityIds.SequenceEqual(inputs),
            "Active-pane display leaves execution, recipe dirty state and graph routing unchanged"));
        workbench.SetInspectionDataViewCommand.Execute(ViewerInspectionDataView.InputOutput);
        workbench.ShowGalleryInActivePaneCommand.Execute(output);
        checks.Add(("GalleryOutputThumbnailKeepsInputOutputView", workbench.IsInspectionInputOutputView
            && workbench.ViewerWorkspace.FocusedSlotId == ViewerWorkspaceSession.AuxiliarySlotId
            && workbench.EffectiveMainViewerContentId == source.Id && workbench.EffectiveAuxiliaryViewerContentId == output.Id,
            "Normal thumbnail activation in Input/Output mode focuses the matching visible output pane"));
        workbench.ShowGalleryInActivePaneCommand.Execute(source);
        checks.Add(("GalleryInputThumbnailKeepsInputOutputView", workbench.IsInspectionInputOutputView
            && workbench.ViewerWorkspace.FocusedSlotId == ViewerWorkspaceSession.MainSlotId && workbench.RunLog.Count == beforeDisplay,
            "Input activation keeps Input/Output mode and does not execute"));
        var oldOutput = output;
        var oldPin = workbench.EffectiveMainViewerContentId;
        step.Parameters.Single(parameter => parameter.Name == "KernelSize").Value = "5";
        var staleReady = WaitUntil(() => workbench.DisplayedOutputs.Any(item => item.Id == step.OutputEntityId && !item.CanShowInViewer));
        var staleOutput = workbench.DisplayedOutputs.Single(item => item.Id == step.OutputEntityId);
        workbench.ShowGalleryInActivePaneCommand.Execute(oldOutput);
        checks.Add(("GalleryRejectsStaleIdentityAndClearsCachedStatistics", staleReady
            && staleOutput.Artifact.Minimum is null && staleOutput.Artifact.Maximum is null
            && staleOutput.Artifact.ValidSampleCount is null && staleOutput.Artifact.MissingSampleCount is null
            && !workbench.ShowGalleryInLeftPaneCommand.CanExecute(staleOutput) && !workbench.ShowGalleryInActivePaneCommand.CanExecute(oldOutput)
            && workbench.EffectiveMainViewerContentId == oldPin && workbench.RunLog.Count == beforeDisplay,
            $"state={staleOutput.State};statisticsCleared={staleOutput.Artifact.ValidSampleCount is null};log={workbench.RunLog.Count}"));
        workbench.RunSelectedStepCommand.Execute(null);
        var recovered = WaitUntil(() => !workbench.IsSelectedStepPreviewRunning
            && workbench.DisplayedOutputs.Any(item => item.Id == step.OutputEntityId && item.CanShowInViewer));
        var recoveredOutput = workbench.DisplayedOutputs.Single(item => item.Id == step.OutputEntityId);
        checks.Add(("GalleryStatisticsRecoverOnlyAfterExplicitRun", recovered && recoveredOutput.Artifact.ValidSampleCount == 9
            && recoveredOutput.Artifact.MissingSampleCount == 0 && workbench.ShowGalleryInRightPaneCommand.CanExecute(recoveredOutput),
            "A new explicit Run republishes the changed filter and restores current statistics and display commands"));
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }

    private sealed class RecordingCommand(Func<object?, bool> canExecute) : ICommand
    {
        public event EventHandler? CanExecuteChanged;
        public int Count { get; private set; }
        public object? Parameter { get; private set; }
        public bool CanExecute(object? parameter) => canExecute(parameter);
        public void Execute(object? parameter) { Count++; Parameter = parameter; }
        public void NotifyChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
