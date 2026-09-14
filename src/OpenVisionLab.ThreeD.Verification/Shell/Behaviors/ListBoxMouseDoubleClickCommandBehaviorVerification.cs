using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Shell.Behaviors;

namespace OpenVisionLab.ThreeD.Verification.Shell.Behaviors;

internal static class ListBoxMouseDoubleClickCommandBehaviorVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        List<(string Name, bool Passed, string Detail)>? checks = null;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                checks = VerifyOnSta();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        checks ??= [];
        if (failure is not null)
        {
            checks.Add(("StaSetup", false, failure.GetBaseException().Message));
        }

        var passed = checks.All(check => check.Passed);
        var lines = new List<string>
        {
            "OpenVisionLab 3D ListBoxMouseDoubleClickCommandBehavior verification",
        };
        lines.AddRange(checks.Select(check =>
            $"{check.Name}={(check.Passed ? "PASS" : "FAIL")}|{check.Detail}"));
        lines.Add($"Result={(passed ? "PASS" : "FAIL")}|{checks.Count(check => check.Passed)}/{checks.Count}");

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        summary = lines[^1];
        foreach (var line in lines)
        {
            Console.WriteLine(line);
        }

        return passed;
    }

    private static List<(string Name, bool Passed, string Detail)> VerifyOnSta()
    {
        var checks = new List<(string Name, bool Passed, string Detail)>();
        var list = new ListBox();
        var selected = new object();
        list.Items.Add(selected);
        list.SelectedItem = selected;

        var forwarded = new RecordingCommand();
        ListBoxMouseDoubleClickCommandBehavior.SetCommand(list, forwarded);
        var forwardedArgs = RaiseDoubleClick(list);
        checks.Add((
            "SelectedItemIsForwarded",
            forwarded.ExecutionCount == 1
                && ReferenceEquals(forwarded.LastParameter, selected)
                && forwardedArgs.Handled,
            $"count={forwarded.ExecutionCount};parameter={ReferenceEquals(forwarded.LastParameter, selected)};handled={forwardedArgs.Handled}"));

        var noSelection = new RecordingCommand();
        var noSelectionList = new ListBox();
        ListBoxMouseDoubleClickCommandBehavior.SetCommand(noSelectionList, noSelection);
        var noSelectionArgs = RaiseDoubleClick(noSelectionList);
        checks.Add((
            "NoSelectionForwardsNull",
            noSelection.ExecutionCount == 1
                && noSelection.LastParameter is null
                && noSelectionArgs.Handled,
            $"count={noSelection.ExecutionCount};parameterNull={noSelection.LastParameter is null};handled={noSelectionArgs.Handled}"));

        var disabled = new RecordingCommand { CanExecuteValue = false };
        var disabledArgs = RaiseDoubleClick(list, disabled);
        checks.Add((
            "CanExecuteIsRespected",
            disabled.ExecutionCount == 0 && !disabledArgs.Handled,
            $"count={disabled.ExecutionCount};handled={disabledArgs.Handled}"));

        var nestedButton = new Button();
        var nestedList = new ListBox();
        nestedList.Items.Add(nestedButton);
        nestedList.SelectedItem = nestedButton;
        var nested = new RecordingCommand();
        ListBoxMouseDoubleClickCommandBehavior.SetCommand(nestedList, nested);
        var nestedArgs = RaiseDoubleClick(nestedButton);
        checks.Add((
            "NestedButtonIsIgnored",
            nested.ExecutionCount == 0 && !nestedArgs.Handled,
            $"count={nested.ExecutionCount};handled={nestedArgs.Handled}"));

        var first = new RecordingCommand();
        var second = new RecordingCommand();
        var replacementList = new ListBox { SelectedItem = selected };
        ListBoxMouseDoubleClickCommandBehavior.SetCommand(replacementList, first);
        ListBoxMouseDoubleClickCommandBehavior.SetCommand(replacementList, second);
        RaiseDoubleClick(replacementList);
        checks.Add((
            "CommandReplacementDetachesOldHandler",
            first.ExecutionCount == 0 && second.ExecutionCount == 1,
            $"first={first.ExecutionCount};second={second.ExecutionCount}"));

        var cleared = new RecordingCommand();
        var clearedList = new ListBox { SelectedItem = selected };
        ListBoxMouseDoubleClickCommandBehavior.SetCommand(clearedList, cleared);
        ListBoxMouseDoubleClickCommandBehavior.SetCommand(clearedList, null);
        var clearedArgs = RaiseDoubleClick(clearedList);
        checks.Add((
            "CommandClearingDetachesHandler",
            cleared.ExecutionCount == 0 && !clearedArgs.Handled,
            $"count={cleared.ExecutionCount};handled={clearedArgs.Handled}"));

        return checks;
    }

    private static MouseButtonEventArgs RaiseDoubleClick(DependencyObject source, ICommand? command = null)
    {
        if (source is ListBox list && command is not null)
        {
            ListBoxMouseDoubleClickCommandBehavior.SetCommand(list, command);
        }

        var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
        {
            RoutedEvent = Control.MouseDoubleClickEvent,
        };
        if (source is UIElement element)
        {
            element.RaiseEvent(args);
        }

        return args;
    }

    private sealed class RecordingCommand : ICommand
    {
        public bool CanExecuteValue { get; init; } = true;

        public int ExecutionCount { get; private set; }

        public object? LastParameter { get; private set; }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => CanExecuteValue;

        public void Execute(object? parameter)
        {
            ExecutionCount++;
            LastParameter = parameter;
        }
    }
}
