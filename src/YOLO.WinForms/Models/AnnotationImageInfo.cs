using System.Text.Json.Serialization;

namespace YOLO.WinForms.Models;

/// <summary>
/// Per-image annotation metadata.
/// </summary>
public class AnnotationImageInfo
{
    /// <summary>Relative path within the project images folder.</summary>
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    /// <summary>Whether this image has been marked as annotation-complete.</summary>
    [JsonPropertyName("isCompleted")]
    public bool IsCompleted { get; set; }

    /// <summary>All rectangle annotations on this image.</summary>
    [JsonPropertyName("annotations")]
    public List<RectAnnotation> Annotations { get; set; } = [];

    /// <summary>
    /// Create a deep copy.
    /// </summary>
    public AnnotationImageInfo Clone() => new()
    {
        FileName = FileName,
        IsCompleted = IsCompleted,
        Annotations = Annotations.Select(a => a.Clone()).ToList()
    };
}
