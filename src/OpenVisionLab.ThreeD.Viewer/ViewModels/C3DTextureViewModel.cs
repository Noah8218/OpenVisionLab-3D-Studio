using System.ComponentModel;
using System.IO;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.Loading;

namespace OpenVisionLab.ThreeD.Viewer.ViewModels;

/// <summary>Owns source-bound texture preferences and decoded image lifetime; never inspection input.</summary>
public sealed class C3DTextureViewModel : INotifyPropertyChanged
{
    private readonly Dictionary<string, HeightTextureImageSettings> settings = new(StringComparer.OrdinalIgnoreCase);
    private string sourceSha256 = string.Empty;
    private int sourceWidth;
    private int sourceHeight;
    private bool visible;
    private string error = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    internal C3DTextureImage? Image { get; private set; }
    public bool CanLoad => sourceSha256.Length > 0;
    public bool HasImage => Image is not null;
    public bool IsVisible => visible && HasImage;
    public string ImageName => Image is { } image ? Path.GetFileName(image.Path) : string.Empty;
    public string Error => error;

    internal void SetSource(C3DHeightGrid? source)
    {
        var sha256 = source is { IsSignedDelta: false } ? source.ContentSha256 : string.Empty;
        if (sourceSha256 == sha256 && sourceWidth == source?.Width && sourceHeight == source?.Height) return;
        sourceSha256 = sha256;
        sourceWidth = source?.Width ?? 0;
        sourceHeight = source?.Height ?? 0;
        Image = null;
        visible = false;
        error = string.Empty;
        RestoreCurrent();
        Notify();
    }

    internal bool TryLoad(string path)
    {
        if (!CanLoad) return false;
        try
        {
            var image = C3DTextureImage.Load(path);
            Image = image;
            visible = true;
            error = string.Empty;
            settings[sourceSha256] = new(sourceWidth, sourceHeight, image.Path, image.Sha256, true);
            Notify();
            return true;
        }
        catch (Exception exception) when (IsImageFailure(exception))
        {
            error = exception.GetBaseException().Message;
            Notify();
            return false;
        }
    }

    internal bool SetVisible(bool value)
    {
        if (!HasImage) return false;
        visible = value;
        settings[sourceSha256] = settings[sourceSha256] with { IsVisible = value };
        Notify();
        return true;
    }

    internal void Clear()
    {
        settings.Remove(sourceSha256);
        Image = null;
        visible = false;
        error = string.Empty;
        Notify();
    }

    internal IReadOnlyDictionary<string, HeightTextureImageSettings> CaptureSettings() => new Dictionary<string, HeightTextureImageSettings>(settings);

    internal void RestoreSettings(IReadOnlyDictionary<string, HeightTextureImageSettings>? restored)
    {
        settings.Clear();
        if (restored is not null)
            foreach (var entry in restored.Where(entry => entry.Key.Length == 64 && entry.Key.All(Uri.IsHexDigit) && entry.Value?.IsValid == true))
                settings[entry.Key] = entry.Value;
        Image = null;
        visible = false;
        error = string.Empty;
        RestoreCurrent();
        Notify();
    }

    internal void ReportRenderFailure(string message)
    {
        visible = false;
        error = message;
        if (settings.TryGetValue(sourceSha256, out var saved)) settings[sourceSha256] = saved with { IsVisible = false };
        Notify();
    }

    internal void ReleaseImage()
    {
        Image = null;
        visible = false;
    }

    private void RestoreCurrent()
    {
        if (!CanLoad || !settings.TryGetValue(sourceSha256, out var saved)) return;
        try
        {
            if (saved.GridWidth != sourceWidth || saved.GridHeight != sourceHeight)
                throw new InvalidDataException("Saved texture dimensions do not match this height map. Select the image again.");
            Image = C3DTextureImage.Load(saved.ImagePath, saved.ImageSha256);
            visible = saved.IsVisible;
        }
        catch (Exception exception) when (IsImageFailure(exception))
        {
            error = exception.GetBaseException().Message;
        }
    }

    private static bool IsImageFailure(Exception exception) => exception is IOException or InvalidDataException or UnauthorizedAccessException
        or ArgumentException or NotSupportedException or InvalidOperationException or System.Runtime.InteropServices.COMException
        or System.IO.FileFormatException or System.Security.SecurityException;

    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
}
