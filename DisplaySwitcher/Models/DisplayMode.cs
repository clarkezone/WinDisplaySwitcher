namespace DisplaySwitcher.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Represents a display mode: resolution, refresh rate, and DPI scale percentage.
/// Two profiles may share the same resolution but differ in scale.
/// </summary>
public sealed class DisplayMode : IEquatable<DisplayMode>
{
    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("refreshRate")]
    public int RefreshRate { get; set; }

    [JsonPropertyName("bitsPerPixel")]
    public int BitsPerPixel { get; set; } = 32;

    /// <summary>DPI scaling percentage (100, 125, 150, 175, 200, …). 0 means "don't change".</summary>
    [JsonPropertyName("scalePercent")]
    public int ScalePercent { get; set; } = 100;

    public DisplayMode() { }

    public DisplayMode(int width, int height, int refreshRate, int scalePercent = 100, int bitsPerPixel = 32)
    {
        Width = width;
        Height = height;
        RefreshRate = refreshRate;
        ScalePercent = scalePercent;
        BitsPerPixel = bitsPerPixel;
    }

    /// <summary>True if resolution + refresh match (ignoring scale).</summary>
    public bool ResolutionEquals(DisplayMode? other)
    {
        if (other is null) return false;
        return Width == other.Width && Height == other.Height && RefreshRate == other.RefreshRate;
    }

    public override string ToString() =>
        ScalePercent > 0
            ? $"{Width}×{Height} @ {RefreshRate}Hz · {ScalePercent}%"
            : $"{Width}×{Height} @ {RefreshRate}Hz";

    public bool Equals(DisplayMode? other)
    {
        if (other is null) return false;
        return Width == other.Width && Height == other.Height
            && RefreshRate == other.RefreshRate && BitsPerPixel == other.BitsPerPixel
            && ScalePercent == other.ScalePercent;
    }

    public override bool Equals(object? obj) => Equals(obj as DisplayMode);
    public override int GetHashCode() => HashCode.Combine(Width, Height, RefreshRate, BitsPerPixel, ScalePercent);
}
