using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YOLO.WinForms.Models;

namespace YOLO.WinForms.Services;

/// <summary>
/// Service for splitting annotated images into train/val sets
/// and generating YOLO-format dataset.yaml config files.
/// </summary>
public class DatasetSplitService
{
    /// <summary>
    /// Split completed images into train/val folders with YOLO directory structure.
    /// Returns (trainCount, valCount).
    /// </summary>
    public (int TrainCount, int ValCount) SplitDataset(
        AnnotationProject project,
        double trainRatio = 0.8,
        IProgress<(int current, int total, string message)>? progress = null)
    {
        var datasetRoot = project.DatasetFolder;

        // Create YOLO directory structure
        var trainImgDir = Path.Combine(datasetRoot, "images", "train");
        var valImgDir = Path.Combine(datasetRoot, "images", "val");
        var trainLblDir = Path.Combine(datasetRoot, "labels", "train");
        var valLblDir = Path.Combine(datasetRoot, "labels", "val");

        // Clean and recreate
        if (Directory.Exists(datasetRoot))
            Directory.Delete(datasetRoot, recursive: true);

        Directory.CreateDirectory(trainImgDir);
        Directory.CreateDirectory(valImgDir);
        Directory.CreateDirectory(trainLblDir);
        Directory.CreateDirectory(valLblDir);

        // Get completed images only
        var completed = project.Images
            .Where(img => img.IsCompleted && img.Annotations.Count > 0)
            .ToList();

        if (completed.Count == 0)
            return (0, 0);

        // Shuffle deterministically
        var rng = new Random(42);
        var shuffled = completed.OrderBy(_ => rng.Next()).ToList();

        // Ensure val set is not empty; for 1 image, duplicate into both train and val
        List<AnnotationImageInfo> trainImages;
        List<AnnotationImageInfo> valImages;

        if (shuffled.Count == 1)
        {
            trainImages = new List<AnnotationImageInfo> { shuffled[0] };
            valImages = new List<AnnotationImageInfo> { shuffled[0] };
        }
        else
        {
            int trainCount = (int)Math.Round(shuffled.Count * trainRatio);
            trainCount = Math.Clamp(trainCount, 1, shuffled.Count - 1);
            trainImages = shuffled.Take(trainCount).ToList();
            valImages = shuffled.Skip(trainCount).ToList();
        }

        int total = shuffled.Count;
        int current = 0;

        // Copy train
        foreach (var img in trainImages)
        {
            CopyImageAndLabel(project, img, trainImgDir, trainLblDir);
            current++;
            progress?.Report((current, total, $"Train: {img.FileName}"));
        }

        // Copy val
        foreach (var img in valImages)
        {
            CopyImageAndLabel(project, img, valImgDir, valLblDir);
            current++;
            progress?.Report((current, total, $"Val: {img.FileName}"));
        }

        return (trainImages.Count, valImages.Count);
    }

    /// <summary>
    /// Generate a YOLO-format dataset.yaml configuration file.
    /// </summary>
    public string GenerateYamlConfig(AnnotationProject project, string? outputPath = null)
    {
        outputPath ??= Path.Combine(project.DatasetFolder, "dataset.yaml");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var config = new Dictionary<string, object>
        {
            ["path"] = project.DatasetFolder.Replace('\\', '/'),
            ["train"] = "images/train",
            ["val"] = "images/val",
            ["nc"] = project.Classes.Count,
            ["names"] = project.Classes
        };

        var serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        var yaml = serializer.Serialize(config);

        // Add a header comment
        var header = $"# YOLO Dataset Configuration\n# Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n# Project: {project.Name}\n\n";
        File.WriteAllText(outputPath, header + yaml);

        return outputPath;
    }

    /// <summary>
    /// Load YAML config as text for editing.
    /// </summary>
    public string LoadConfigText(string yamlPath)
    {
        return File.Exists(yamlPath) ? File.ReadAllText(yamlPath) : string.Empty;
    }

    /// <summary>
    /// Save YAML config text.
    /// </summary>
    public void SaveConfigText(string yamlPath, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(yamlPath)!);
        File.WriteAllText(yamlPath, text);
    }

    // ── Private helpers ────────────────────────────────────────────

    private static void CopyImageAndLabel(
        AnnotationProject project, AnnotationImageInfo img,
        string imgDestDir, string lblDestDir)
    {
        // Copy image
        var srcImage = project.GetImageAbsolutePath(img);
        var destImage = Path.Combine(imgDestDir, img.FileName);
        if (File.Exists(srcImage))
            File.Copy(srcImage, destImage, overwrite: true);

        // Write label
        if (img.Annotations.Count > 0)
        {
            var labelName = Path.ChangeExtension(img.FileName, ".txt");
            var destLabel = Path.Combine(lblDestDir, labelName);
            var lines = img.Annotations.Select(a => a.ToYoloLine());
            File.WriteAllLines(destLabel, lines);
        }
    }
}
