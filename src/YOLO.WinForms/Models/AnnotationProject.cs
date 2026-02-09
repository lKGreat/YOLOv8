using System.Text.Json;
using System.Text.Json.Serialization;

namespace YOLO.WinForms.Models;

/// <summary>
/// Root model for an annotation project. Serialized to JSON (.yolo-anno).
/// </summary>
public class AnnotationProject
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>Project display name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "Untitled";

    /// <summary>Absolute path to the project folder.</summary>
    [JsonPropertyName("projectPath")]
    public string ProjectPath { get; set; } = string.Empty;

    /// <summary>Creation timestamp.</summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>Class names for annotation (e.g. "person", "car").</summary>
    [JsonPropertyName("classes")]
    public List<string> Classes { get; set; } = [];

    /// <summary>All images in the project.</summary>
    [JsonPropertyName("images")]
    public List<AnnotationImageInfo> Images { get; set; } = [];

    /// <summary>Train / Val split ratio (0-1, fraction used for training).</summary>
    [JsonPropertyName("splitRatio")]
    public double SplitRatio { get; set; } = 0.8;

    /// <summary>
    /// Index of the last opened image, for resume annotation (断点标注).
    /// </summary>
    [JsonPropertyName("lastOpenedImageIndex")]
    public int LastOpenedImageIndex { get; set; }

    /// <summary>Training device: "cpu" or "cuda".</summary>
    [JsonPropertyName("trainDevice")]
    public string TrainDevice { get; set; } = "cpu";

    // ── Derived paths ──────────────────────────────────────────────

    /// <summary>Path to the project JSON file.</summary>
    [JsonIgnore]
    public string ProjectFilePath => Path.Combine(ProjectPath, "project.yolo-anno");

    /// <summary>Folder where source images are stored.</summary>
    [JsonIgnore]
    public string ImagesFolder => Path.Combine(ProjectPath, "images");

    /// <summary>Folder where the generated dataset lives.</summary>
    [JsonIgnore]
    public string DatasetFolder => Path.Combine(ProjectPath, "dataset");

    // ── Persistence ────────────────────────────────────────────────

    /// <summary>
    /// Save the project to its JSON file.
    /// </summary>
    public void Save()
    {
        Directory.CreateDirectory(ProjectPath);
        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(ProjectFilePath, json);
    }

    /// <summary>
    /// Load a project from a JSON file.
    /// </summary>
    public static AnnotationProject Load(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var project = JsonSerializer.Deserialize<AnnotationProject>(json, JsonOptions)
            ?? throw new InvalidDataException($"Failed to deserialize project: {filePath}");

        // Ensure project path is set correctly
        project.ProjectPath = Path.GetDirectoryName(filePath)
            ?? throw new InvalidDataException($"Invalid project path: {filePath}");

        return project;
    }

    // ── Helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Get the absolute path for an image by its relative filename.
    /// </summary>
    public string GetImageAbsolutePath(AnnotationImageInfo img) =>
        Path.Combine(ImagesFolder, img.FileName);

    /// <summary>
    /// Count of completed images.
    /// </summary>
    [JsonIgnore]
    public int CompletedCount => Images.Count(i => i.IsCompleted);

    /// <summary>
    /// Get the index of the first incomplete image (for resume).
    /// Returns 0 if all are complete or the list is empty.
    /// </summary>
    public int GetResumeIndex()
    {
        for (int i = 0; i < Images.Count; i++)
        {
            if (!Images[i].IsCompleted)
                return i;
        }
        return 0;
    }
}
