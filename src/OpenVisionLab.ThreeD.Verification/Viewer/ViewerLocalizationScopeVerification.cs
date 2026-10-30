using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using OpenVisionLab.ThreeD.Shell.Behaviors;
using OpenVisionLab.ThreeD.Viewer;
using OpenVisionLab.ThreeD.Viewer.Hosting;
using OpenVisionLab.ThreeD.Viewer.Localization;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

internal static class ViewerLocalizationScopeVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var passed = false;
        var staSummary = string.Empty;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                passed = VerifyOnSta(reportPath, out staSummary);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            throw new InvalidOperationException("Viewer localization-scope verification failed on the STA thread.", failure);
        }

        summary = staSummary;
        return passed;
    }

    private static bool VerifyOnSta(string reportPath, out string summary)
    {
        var lines = new List<string>
        {
            "OpenVisionLab 3D Viewer XAML localization-scope verification",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        var passed = 0;
        var total = 0;

        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition)
            {
                passed++;
            }
        }

        var dispatcher = Dispatcher.CurrentDispatcher;
        var localization = new FakeLocalizationProvider();
        OpenVisionThreeDViewerControl? control = null;
        try
        {
            control = new OpenVisionThreeDViewerControl(
                loadDefaultSamples: false,
                recipeDialogHost: null,
                samplePathResolver: null,
                localizationProvider: localization);

            Check(
                "control carries the injected provider scope",
                ReferenceEquals(ViewerLocalizationScope.GetProvider(control), localization),
                $"sameProvider={ReferenceEquals(ViewerLocalizationScope.GetProvider(control), localization)}");

            var menu = control.FindName("ViewerViewMenuRoot") as MenuItem;
            Check(
                "compiled static ViewerText uses the injected provider",
                menu?.ToolTip is string tooltip && tooltip == "custom:ThreeD.Viewer.ViewCommands:rev0",
                $"tooltip={menu?.ToolTip}");

            Drain(dispatcher);
            var footer = (DockPanel)((Border)control.FindName("ViewerBottomStatus")).Child;
            var roiCaption = footer.Children.OfType<TextBlock>().Single();
            var coordinateRun = ((TextBlock)((ScrollViewer)control.FindName("ViewerCoordinateScroll")).Content).Inlines.OfType<Run>().Single();
            var viewModel = (MainWindowViewModel)control.DataContext;
            var rawRoiBefore = viewModel.SelectedTeachingGridRectangleSummary;
            var rawCoordinateBefore = viewModel.PickCoordinate;
            var roiTextBefore = roiCaption.Text;
            var coordinateTextBefore = coordinateRun.Text;
            viewModel.SetTwoPointMeasurement(new System.Numerics.Vector3(1, 2, 3), 2, new System.Numerics.Vector3(4, 6, 3), 6);
            var densityBefore = viewModel.SelectedRenderDensity;
            var densityNotifications = 0;
            System.ComponentModel.PropertyChangedEventHandler densityHandler = (_, args) =>
            {
                if (args.PropertyName == nameof(MainWindowViewModel.SelectedRenderDensity)) densityNotifications++;
            };
            viewModel.PropertyChanged += densityHandler;
            localization.RaiseLanguageChanged();
            viewModel.PropertyChanged -= densityHandler;
            Drain(dispatcher);
            Check(
                "language refresh preserves render density without requesting a source reload",
                densityNotifications == 0 && viewModel.SelectedRenderDensity == densityBefore,
                $"density={viewModel.SelectedRenderDensity};notifications={densityNotifications}");
            Check(
                "language refresh retains a completed measurement without executing inspection",
                viewModel.TwoPointMeasurementVisible && viewModel.TwoPointDistance == 5
                    && viewModel.TwoPointRawHeightDelta == 4 && control.HostState.Inspection.PublishedResultCount == 0,
                $"visible={viewModel.TwoPointMeasurementVisible};distance={viewModel.TwoPointDistance};heightDelta={viewModel.TwoPointRawHeightDelta};published={control.HostState.Inspection.PublishedResultCount}");
            Check(
                "static ViewerText refreshes after provider language change",
                menu?.ToolTip is string refreshed && refreshed == "custom:ThreeD.Viewer.ViewCommands:rev1",
                $"tooltip={menu?.ToolTip}|revision={localization.Revision}");

            Check(
                "footer coordinate follows language revision while the source ROI caption and raw state remain unchanged",
                coordinateTextBefore.EndsWith(":rev0", StringComparison.Ordinal)
                && roiCaption.Text == roiTextBefore
                && coordinateRun.Text.EndsWith(":rev1", StringComparison.Ordinal)
                && viewModel.SelectedTeachingGridRectangleSummary == rawRoiBefore
                && viewModel.PickCoordinate == rawCoordinateBefore
                && control.HostState.Inspection.PublishedResultCount == 0,
                $"roi={roiTextBefore}->{roiCaption.Text};coordinate={coordinateTextBefore}->{coordinateRun.Text};published={control.HostState.Inspection.PublishedResultCount}");

            Check(
                "runtime converter retains the same provider",
                control.Resources["ViewerRuntimeTextConverter"] is ViewerRuntimeTextConverter converter
                    && ReferenceEquals(converter.Localization, localization),
                $"sameProvider={control.Resources["ViewerRuntimeTextConverter"] is ViewerRuntimeTextConverter runtime
                    && ReferenceEquals(runtime.Localization, localization)}");
        }
        finally
        {
            control?.Dispose();
        }

        Check(
            "control disposal detaches provider listeners and scope",
            localization.SubscriberCount == 0
                && control is not null
                && ViewerLocalizationScope.GetProvider(control) is null,
            $"subscribers={localization.SubscriberCount}|scopeCleared={control is not null && ViewerLocalizationScope.GetProvider(control) is null}");

        var fallbackExtension = new ViewerTextExtension
        {
            Key = "ThreeD.Viewer.UnscopedFallback",
            Korean = "Korean fallback",
            English = "English fallback"
        };
        var fallbackValue = fallbackExtension.ProvideValue(new EmptyServiceProvider());
        Check(
            "markup outside a Viewer scope keeps the shared fallback",
            fallbackValue is "Korean fallback" or "English fallback",
            $"value={fallbackValue}");

        var previousLanguage = OpenVisionLab.OpenVisionLanguageService.CurrentLanguage;
        try
        {
            const string cursor = "X 124 · Y 56 · H -123456.789 raw-height";
            const string millimeters = "X 124 · Y 56 · H -123456.789 mm";
            foreach (var language in new[] { OpenVisionLab.OpenVisionLanguage.Korean, OpenVisionLab.OpenVisionLanguage.English })
            {
                OpenVisionLab.OpenVisionLanguageService.SetLanguage(language, save: false);
                var expected = "X 124 · Y 56 · H -123456.789 " + (language == OpenVisionLab.OpenVisionLanguage.Korean ? "원시 높이" : "raw height");
                Check("profile cursor preserves signed values and coordinates through language change",
                    ViewerLocalization.Shared.LocalizeRuntimeText(cursor, "ProfileCursor") == expected, expected);
                Check("empty profile cursor does not fabricate a measurement",
                    ViewerLocalization.Shared.LocalizeRuntimeText(null, "ProfileCursor") == string.Empty
                    && ViewerLocalization.Shared.LocalizeRuntimeText(string.Empty, "ProfileCursor") == string.Empty, language.ToString());
                Check("profile cursor leaves other units and source names unchanged",
                    ViewerLocalization.Shared.LocalizeRuntimeText(millimeters, "ProfileCursor") == millimeters
                    && ViewerLocalization.Shared.LocalizeRuntimeText(@"D:\raw-height.C3D", "ProfileCursor") == @"D:\raw-height.C3D", millimeters);
                Check("profile cursor formatting is isolated from other runtime modes",
                    ViewerLocalization.Shared.LocalizeRuntimeText(cursor, "OtherMode") == cursor, cursor);
                Check("height graph name follows operator terminology",
                    ViewerLocalization.Shared.LocalizeRuntimeText("Profile") == (language == OpenVisionLab.OpenVisionLanguage.Korean ? "높이 단면" : "Profile"), language.ToString());
                Check("redundant Tool Lab ready prose is hidden in both languages",
                    ViewerLocalization.Shared.LocalizeRuntimeText("Ready for explicit Preview. Filter will not run implicitly.", "ToolLabSummary") == string.Empty
                    && ViewerLocalization.Shared.LocalizeRuntimeText("Ready for explicit Preview. Pick capture and source resolution never run implicitly.", "ToolLabSummary") == string.Empty, language.ToString());
                Check("Tool Lab errors and unknown diagnostics remain visible",
                    !string.IsNullOrWhiteSpace(ViewerLocalization.Shared.LocalizeRuntimeText("No Filter Preview output.", "ToolLabSummary"))
                    && ViewerLocalization.Shared.LocalizeRuntimeText("Unsupported sample: diagnostic-17", "ToolLabSummary") == "Unsupported sample: diagnostic-17", language.ToString());
                var missingPoints = ViewerLocalization.Shared.LocalizeRuntimeText("Step 10 selection role 'line-points' requires 1 point-set selection(s); found 0.", "ToolLabSummary");
                Check("missing-points message identifies the required input without schema prose", missingPoints == (language == OpenVisionLab.OpenVisionLanguage.Korean
                    ? "입력점이 연결되지 않았습니다." : "No input point set is connected."), missingPoints);
                var filterCounts = ViewerLocalization.Shared.LocalizeRuntimeText("Preview ready | valid 1,234 | missing 56 | preprocessing only, no OK/NG", "ToolLabSummary");
                Check("Filter summary retains counts and its non-judgment boundary", filterCounts.Contains("1,234", StringComparison.Ordinal)
                    && filterCounts.Contains("56", StringComparison.Ordinal)
                    && filterCounts.Contains(language == OpenVisionLab.OpenVisionLanguage.Korean ? "판정 없음" : "no judgment", StringComparison.Ordinal), filterCounts);
                var output = ViewerLocalization.Shared.LocalizeRuntimeText("Filtered Surface | SHA-256 abcdef123456 | Preview only", "ToolLabSummary");
                Check("Filter summary distinguishes a preview from an applied result", output.Contains(language == OpenVisionLab.OpenVisionLanguage.Korean ? "미적용" : "not applied", StringComparison.Ordinal)
                    && !output.Contains("abcdef123456", StringComparison.Ordinal), output);
                Check("Filter details retain the full hash", ViewerLocalization.Shared.LocalizeRuntimeText("Output SHA-256 abcdef1234567890", "ToolLabSummary").Contains("abcdef1234567890", StringComparison.Ordinal), language.ToString());
                var korean = language == OpenVisionLab.OpenVisionLanguage.Korean;
                const string edgeCounts = "Preview ready | points 4/4 | eligible pairs 12 | missing skips 0 | no OK/NG";
                Check("edge preview retains all independent fixture counts and not-applied state",
                    ViewerLocalization.Shared.LocalizeRuntimeText(edgeCounts, "HeightDifferenceEdgeSummary") == (korean
                        ? "미적용 · 검출 스캔라인 4/4 · 비교 쌍 12 · 결측 제외 0쌍 · 판정 없음"
                        : "Not applied · edges on 4/4 scanlines · eligible pairs 12 · missing pairs skipped 0 · no judgment"), language.ToString());
                const string edgeId = "derived.filter-preserve-this-ID";
                Check("applied edge input retains its full identity and current-state distinction",
                    ViewerLocalization.Shared.LocalizeRuntimeText(edgeId + " | Published | current", "HeightDifferenceEdgeSummary") == edgeId + (korean ? " | 적용됨 · 현재 입력과 일치" : " | Applied · current"), language.ToString());
                Check("unapplied edge input names the required action",
                    ViewerLocalization.Shared.LocalizeRuntimeText(edgeId + " | Preview only | publish required", "HeightDifferenceEdgeSummary") == edgeId + (korean ? " | 미적용 · 필터 결과 적용 필요" : " | Not applied · apply the filter result"), language.ToString());
                Check("missing edge input remains a blocking condition",
                    ViewerLocalization.Shared.LocalizeRuntimeText(edgeId + " | no current Filter Preview", "HeightDifferenceEdgeSummary") == edgeId + (korean ? " | 현재 필터 결과 없음 · 미리보기와 적용 필요" : " | No current filter result · preview and apply required"), language.ToString());
                var fullHash = new string('A', 64);
                var edgeApplied = $"Published exact Preview as derived.edge.01 | SHA-256 {fullHash} | feature extraction only, no OK/NG";
                Check("applied edge status separates concise copy from raw diagnostic identity",
                    ViewerLocalization.Shared.LocalizeRuntimeText(edgeApplied, "HeightDifferenceEdgeSummary") == (korean ? "적용됨 · 에지 추출 · 판정 없음" : "Applied · edge extraction · no judgment")
                    && ViewerLocalization.Shared.LocalizeRuntimeText(edgeApplied, "OtherMode") == edgeApplied
                    && ViewerLocalization.Shared.LocalizeRuntimeText("Output SHA-256 " + fullHash, "HeightDifferenceEdgeSummary").Contains(fullHash, StringComparison.Ordinal), language.ToString());
                const string rows = "4/4 authored rows | derived.anchor.01: Published | derived.anchor.02: missing/stale | derived.anchor.03: Published | derived.anchor.04: missing/stale";
                Check("landmark input summary preserves mixed applied and missing row states",
                    ViewerLocalization.Shared.LocalizeRuntimeText(rows, "LandmarkCorrespondenceSummary") == (korean ? "4/4쌍 · 적용 2쌍 · 결과 없음·갱신 필요: 2, 4행" : "4/4 pairs · 2 applied · missing or stale: rows 2, 4")
                    && ViewerLocalization.Shared.LocalizeRuntimeText(rows, "OtherMode") == rows, language.ToString());
                const string unknownRows = "4/4 authored rows | derived.anchor.01: diagnostic-17";
                Check("unknown landmark diagnostics are not compressed into a success state",
                    ViewerLocalization.Shared.LocalizeRuntimeText(unknownRows, "LandmarkCorrespondenceSummary") == unknownRows, language.ToString());
                Check("landmark missing and excess pair counts keep corrective actions",
                    ViewerLocalization.Shared.LocalizeRuntimeText("3/4 rows taught. Correspondence Preview remains blocked.", "LandmarkCorrespondenceSummary") == (korean ? "3/4쌍 · 미리보기에는 4쌍이 필요합니다." : "3/4 pairs · preview requires four pairs.")
                    && ViewerLocalization.Shared.LocalizeRuntimeText("5/4 rows taught. v1 accepts exactly four; remove the extra rows.", "LandmarkCorrespondenceSummary") == (korean ? "5/4쌍 · 초과한 대응 쌍을 삭제하세요." : "5/4 pairs · remove the extra pairs."), language.ToString());
                var descriptor = ViewerLocalization.Shared.LocalizeRuntimeText("Four rows exist, but reference unit/provenance/revision and a normalized tetrahedron-volume threshold are required.", "LandmarkCorrespondenceSummary");
                Check("landmark incomplete metadata retains every required field",
                    new[] { korean ? "단위" : "unit", korean ? "출처" : "provenance", korean ? "리비전" : "revision", korean ? "체적" : "volume" }.All(field => descriptor.Contains(field, StringComparison.Ordinal)), descriptor);
                const string evidence = "Preview ready | 4 pairs | source rank 4/4 | reference rank 4/4 | normalized volume 0.12 / 0.34 | no affine matrix or OK/NG";
                Check("landmark preview preserves rank, volume and absence of a transform or judgment",
                    ViewerLocalization.Shared.LocalizeRuntimeText(evidence, "LandmarkCorrespondenceSummary") == (korean
                        ? "미적용 · 4쌍 · 입력 랭크 4/4 · 기준 랭크 4/4 · 정규화 체적 0.12 / 0.34 · 변환 행렬 계산·판정 없음"
                        : "Not applied · 4 pairs · source rank 4/4 · reference rank 4/4 · normalized volume 0.12 / 0.34 · no transform matrix or judgment"), language.ToString());
                Check("changed landmark inputs require a new explicit preview",
                    ViewerLocalization.Shared.LocalizeRuntimeText("Recipe, correspondence row, descriptor, or published CornerAnchor changed. Preview again before Publish.", "LandmarkCorrespondenceSummary") == (korean
                        ? "레시피·대응 쌍·기준 정보·적용된 교점이 바뀌었습니다. 적용 전에 다시 미리보기를 실행하세요."
                        : "Recipe, pairs, reference information or applied anchors changed. Preview again before applying."), language.ToString());
                Check("new copy stays isolated from other Tool Labs sharing the feature-extraction diagnostic",
                    ViewerLocalization.Shared.LocalizeRuntimeText(edgeApplied, "ToolLabSummary") == edgeApplied, language.ToString());
                var optionConverter = new OpenVisionLab.ThreeD.Shell.PropertyGrid.PropertyGridOptionTextConverter();
                Check("Affine Apply display removes internal stage names while unrelated option identities stay intact",
                    Equals(optionConverter.Convert("Raw source + Published A1", typeof(string), null!, System.Globalization.CultureInfo.InvariantCulture), korean ? "원본 데이터와 적용된 변환 행렬" : "Raw source + applied transform matrix")
                    && Equals(optionConverter.Convert("custom-A1-A2-identity", typeof(string), null!, System.Globalization.CultureInfo.InvariantCulture), "custom-A1-A2-identity"), language.ToString());
                var coverageLimit = ViewerLocalization.Shared.LocalizeRuntimeText("Ready for explicit Preview. A3 preserves holes, rejects out-of-bounds points, and only enables Publish after the authored coverage gate.", "ToolLabSummary");
                Check("Tool Lab coverage restriction survives copy cleanup",
                    coverageLimit.Contains(language == OpenVisionLab.OpenVisionLanguage.Korean ? "유효 셀 비율" : "coverage", StringComparison.Ordinal)
                    && !coverageLimit.Contains(language == OpenVisionLab.OpenVisionLanguage.Korean ? "준비" : "ready", StringComparison.OrdinalIgnoreCase), coverageLimit);
            }
        }
        finally
        {
            OpenVisionLab.OpenVisionLanguageService.SetLanguage(previousLanguage, save: false);
        }

        summary = $"Viewer localization-scope verification: {(passed == total ? "Pass" : "Fail")} ({passed}/{total} checks)";
        lines.Add(summary);
        var fullPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllLines(fullPath, lines);
        return passed == total;
    }

    private static void Drain(Dispatcher dispatcher)
    {
        var frame = new DispatcherFrame();
        _ = dispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    public static bool VerifyToolLabResidualCopy(string sourceRoot, string baselineRoot, string reportPath, out string summary)
    {
        var lines = new List<string>();
        var total = 0;
        var passed = 0;
        var thread = new Thread(() =>
        {
            var previousLanguage = OpenVisionLab.OpenVisionLanguageService.CurrentLanguage;
            var windowCount = Application.Current?.Windows.Count ?? 0;
            void Check(string name, bool condition, string detail)
            {
                total++;
                if (condition) passed++;
                lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            }
            try
            {
                XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
                XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
                const string viewerNamespace = "clr-namespace:OpenVisionLab.ThreeD.Viewer.Localization;assembly=OpenVisionLab.ThreeD.Viewer";
                const string behaviorNamespace = "clr-namespace:OpenVisionLab.ThreeD.Shell.Behaviors";
                var theme = XDocument.Load(Path.Combine(sourceRoot, "src/OpenVisionLab.ThreeD.Shell/Themes/OpenVisionThreeDTheme.xaml"));
                XElement Caption(XDocument document, string binding) => document.Descendants(presentation + "TextBlock")
                    .Single(element => element.Descendants(presentation + "Binding").Any(item => (string?)item.Attribute("Path") == binding));
                StackPanel Host(params XElement[] captions)
                {
                    var resources = new XElement(presentation + "StackPanel.Resources",
                        theme.Descendants(presentation + "SolidColorBrush").Where(element => (string?)element.Attribute(xaml + "Key") is "ThreeD.MutedTextBrush" or "ThreeD.SecondaryTextBrush").Select(element => new XElement(element)),
                        new XElement(theme.Descendants(presentation + "Style").Single(element => (string?)element.Attribute(xaml + "Key") == "ThreeD.DockPanelCaptionTextStyle")),
                        new XElement(XName.Get("ViewerRuntimeTextConverter", viewerNamespace), new XAttribute(xaml + "Key", "ToolLabRuntimeText")));
                    var markup = new XElement(presentation + "StackPanel", resources, captions.Select(element => new XElement(element)));
                    foreach (var element in markup.Descendants().Where(element => element.Name.NamespaceName == behaviorNamespace))
                        element.Name = XName.Get(element.Name.LocalName, behaviorNamespace + ";assembly=OpenVisionLab.ThreeD.Shell");
                    markup.SetAttributeValue(XNamespace.Xmlns + "x", xaml.NamespaceName);
                    markup.SetAttributeValue(XNamespace.Xmlns + "viewerLocalization", viewerNamespace);
                    markup.SetAttributeValue(XNamespace.Xmlns + "behaviors", behaviorNamespace + ";assembly=OpenVisionLab.ThreeD.Shell");
                    var context = new ParserContext();
                    context.XmlnsDictionary.Add("", presentation.NamespaceName);
                    context.XmlnsDictionary.Add("x", xaml.NamespaceName);
                    context.XmlnsDictionary.Add("viewerLocalization", viewerNamespace);
                    var markupText = markup.ToString();
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, "last-caption.xaml"), markupText);
                    return (StackPanel)XamlReader.Parse(markupText, context);
                }
                void Render(StackPanel host, double width, string name)
                {
                    var panelColor = theme.Descendants(presentation + "SolidColorBrush").Single(element => (string?)element.Attribute(xaml + "Key") == "ThreeD.PanelBrush").Attribute("Color")!.Value;
                    var surface = new Border { Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(panelColor)), Padding = new Thickness(12), Child = host };
                    for (var pass = 0; pass < 3; pass++)
                    {
                        surface.Measure(new Size(width + 24, double.PositiveInfinity));
                        surface.Arrange(new Rect(0, 0, width + 24, Math.Ceiling(surface.DesiredSize.Height)));
                        surface.UpdateLayout();
                    }
                    var bitmap = new RenderTargetBitmap((int)Math.Ceiling(surface.ActualWidth), Math.Max(1, (int)Math.Ceiling(surface.ActualHeight)), 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(surface);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream = File.Create(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, name + ".png"));
                    encoder.Save(stream);
                }
                const string rawSelection = "Four correspondence rows and reference descriptor are taught. Preview validates only current Published anchors; no affine matrix is calculated.";
                const string upstream = "Route the raw recipe C3D first and one current Published AffineTransform3D second.";
                const string duplicate = "Route the raw recipe C3D first and the current Published AffineTransform3D second. Upstream tools do not run implicitly.";
                foreach (var language in new[] { OpenVisionLab.OpenVisionLanguage.Korean, OpenVisionLab.OpenVisionLanguage.English })
                {
                    OpenVisionLab.OpenVisionLanguageService.SetLanguage(language, save: false);
                    var expected = ViewerLocalization.Shared.LocalizeRuntimeText(rawSelection, "LandmarkCorrespondenceSummary");
                    foreach (var stage in new[] { (Name: "before", Root: baselineRoot), (Name: "after", Root: sourceRoot) })
                    {
                        var landmark = XDocument.Load(Path.Combine(stage.Root, "src/OpenVisionLab.ThreeD.Shell/Views/Tooling/LandmarkCorrespondenceToolLabWindow.xaml"));
                        var affine = XDocument.Load(Path.Combine(stage.Root, "src/OpenVisionLab.ThreeD.Shell/Views/Tooling/XYZAffineApplyToolLabWindow.xaml"));
                        foreach (var width in new[] { 288d, 336d, 616d })
                        {
                            var captionHost = Host(Caption(landmark, "CorrespondenceSelectionSummary"));
                            captionHost.DataContext = new { CorrespondenceSelectionSummary = rawSelection };
                            Render(captionHost, width, $"{stage.Name}-landmark-{language}-{width}");
                            var caption = (TextBlock)captionHost.Children[0];
                            if (stage.Name == "after")
                            {
                                var words = caption.Inlines.OfType<InlineUIContainer>().Select(inline => (TextBlock)inline.Child).ToArray();
                                Check($"whole summary words {language}/{width}", WordBoundaryWrapBehavior.GetText(caption) == expected
                                    && words.Select(word => word.Text).SequenceEqual(expected.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                                    && words.All(word => word.ActualHeight <= caption.FontSize * 1.7 && word.ActualWidth <= width), expected);
                                Check($"full accessible summary {language}/{width}", AutomationProperties.GetName(caption) == expected, AutomationProperties.GetName(caption));
                            }
                        }
                        var affineHost = Host(Caption(affine, "AffineApplyUpstreamSummary"), Caption(affine, "AffineApplyExecutionSummary"));
                        affineHost.DataContext = new { AffineApplyUpstreamSummary = upstream, AffineApplyExecutionSummary = duplicate };
                        Render(affineHost, 300, $"{stage.Name}-affine-{language}");
                        var inputCaption = (TextBlock)affineHost.Children[0];
                        var stateCaption = (TextBlock)affineHost.Children[1];
                        Check($"missing input guidance {stage.Name}/{language}", inputCaption.Visibility == Visibility.Visible && !string.IsNullOrWhiteSpace(inputCaption.Text)
                            && stateCaption.Visibility == (stage.Name == "after" ? Visibility.Collapsed : Visibility.Visible), inputCaption.Text);
                        if (stage.Name == "after")
                        {
                            foreach (var state in new[] { "Input identity mismatch: fixture-17", "The route or current Published AffineTransform3D changed. Preview again before Publish." })
                            {
                                affineHost.DataContext = new { AffineApplyUpstreamSummary = upstream, AffineApplyExecutionSummary = state };
                                Drain(Dispatcher.CurrentDispatcher);
                                Check($"nonduplicate state remains visible {language}", stateCaption.Visibility == Visibility.Visible && !string.IsNullOrWhiteSpace(stateCaption.Text), stateCaption.Text);
                            }
                        }
                    }
                }
                Check("no desktop Window created", (Application.Current?.Windows.Count ?? 0) == windowCount, $"before={windowCount};after={Application.Current?.Windows.Count ?? 0}");
            }
            catch (Exception exception)
            {
                total++;
                lines.Add($"FAIL | unexpected exception | {exception}");
            }
            finally
            {
                OpenVisionLab.OpenVisionLanguageService.SetLanguage(previousLanguage, save: false);
            }
        });
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        summary = $"Tool Lab residual-copy verification: {(passed == total ? "PASS" : "FAIL")} ({passed}/{total})";
        lines.Insert(0, summary);
        lines.Add("Images are isolated WPF leaf renders at96DPI using source/markup snapshots, not actual desktop GUI or full Wide/Compact acceptance.");
        File.WriteAllLines(reportPath, lines);
        return passed == total;
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class FakeLocalizationProvider : IViewerLocalizationProvider
    {
        private EventHandler? languageChanged;

        public int Revision { get; private set; }

        public int SubscriberCount { get; private set; }

        public event EventHandler? LanguageChanged
        {
            add
            {
                languageChanged += value;
                SubscriberCount++;
            }
            remove
            {
                languageChanged -= value;
                SubscriberCount--;
            }
        }

        public string Resolve(string key, string korean, string english) =>
            $"custom:{key}:rev{Revision}";

        public string LocalizeRuntimeText(object? value, string? mode = null) =>
            $"custom:{value}:{mode}:rev{Revision}";

        public void RaiseLanguageChanged()
        {
            Revision++;
            languageChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
