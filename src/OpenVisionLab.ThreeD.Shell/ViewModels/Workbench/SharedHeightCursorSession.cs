using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns one view-only native-grid cursor shared by the Height Image and 3D
/// Viewer. The session contains no WPF, OpenGL, recipe, or execution state.
/// </summary>
public sealed class SharedHeightCursorSession : INotifyPropertyChanged
{
    private SharedHeightCursorSnapshot? cursor;
    private long revision;

    public event PropertyChangedEventHandler? PropertyChanged;

    public SharedHeightCursorSnapshot? Cursor => cursor;
    public bool HasCursor => cursor is not null;
    public long Revision => revision;

    public void Update(
        SharedHeightCursorOrigin origin,
        string sourceContentSha256,
        int row,
        int column,
        double rawHeight,
        bool isValid)
    {
        if (origin == SharedHeightCursorOrigin.None)
        {
            throw new ArgumentOutOfRangeException(nameof(origin));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(sourceContentSha256);
        if (row < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(row));
        }

        if (column < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(column));
        }

        if (isValid && !double.IsFinite(rawHeight))
        {
            throw new ArgumentOutOfRangeException(nameof(rawHeight));
        }

        var next = new SharedHeightCursorSnapshot(
            origin,
            sourceContentSha256,
            row,
            column,
            isValid ? rawHeight : double.NaN,
            isValid);
        if (cursor == next)
        {
            return;
        }

        cursor = next;
        revision++;
        RaiseStateChanged();
    }

    public void Clear(SharedHeightCursorOrigin origin)
    {
        if (cursor?.Origin != origin)
        {
            return;
        }

        Clear();
    }

    public void Clear()
    {
        if (cursor is null)
        {
            return;
        }

        cursor = null;
        revision++;
        RaiseStateChanged();
    }

    private void RaiseStateChanged()
    {
        OnPropertyChanged(nameof(Cursor));
        OnPropertyChanged(nameof(HasCursor));
        OnPropertyChanged(nameof(Revision));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public enum SharedHeightCursorOrigin
{
    None,
    HeightImage,
    ThreeDViewer
}

public readonly record struct SharedHeightCursorSnapshot(
    SharedHeightCursorOrigin Origin,
    string SourceContentSha256,
    int Row,
    int Column,
    double RawHeight,
    bool IsValid);
