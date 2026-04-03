namespace DisplaySwitcher.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Display topology mode. Null in a profile means "don't change topology".
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TopologyMode
{
    Extend,
    Clone,
    InternalOnly,
    ExternalOnly,
}
