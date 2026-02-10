using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Image;
using SixLabors.ImageSharp;
using YOLO.Data.Datasets;

namespace YOLO.MLNet.Data;

/// <summary>
/// 将 YOLO 格式数据集（图像 + txt 标签文件）转换为 ML.NET IDataView。
///
/// YOLO 标签格式 (每行): class_id cx cy w h (归一化 0~1)
/// ML.NET 需要: MLImage + uint[] Labels (1-based) + float[] BoundingBoxes (x0,y0,x1,y1 绝对像素)
/// </summary>
public static class YoloToMLNetAdapter
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".webp"
    };

    /// <summary>
    /// 从 DatasetConfig (YAML) 加载训练集/验证集/测试集为 IDataView。
    /// </summary>
    public static IDataView LoadFromDatasetConfig(MLContext mlContext, DatasetConfig config, string split = "train")
    {
        string imageDir = split.ToLowerInvariant() switch
        {
            "train" => config.Train,
            "val" => config.Val,
            "test" => config.Test ?? config.Val,
            _ => throw new ArgumentException($"未知的数据集拆分: {split}")
        };

        return LoadFromDirectory(mlContext, imageDir);
    }

    /// <summary>
    /// 从图像目录加载数据集。标签文件位于同级 labels/ 目录或图像路径中 images/ 替换为 labels/。
    /// </summary>
    public static IDataView LoadFromDirectory(MLContext mlContext, string imageDir)
    {
        if (!Directory.Exists(imageDir))
            throw new DirectoryNotFoundException($"图像目录不存在: {imageDir}");

        var items = new List<ObjectDetectionInput>();
        var imageFiles = Directory.EnumerateFiles(imageDir, "*.*", SearchOption.AllDirectories)
            .Where(f => ImageExtensions.Contains(Path.GetExtension(f)))
            .OrderBy(f => f)
            .ToList();

        foreach (var imagePath in imageFiles)
        {
            var labelPath = FindLabelFile(imagePath);
            var (labels, boxes) = ParseYoloLabel(labelPath, imagePath);

            var item = new ObjectDetectionInput
            {
                Image = MLImage.CreateFromFile(imagePath),
                Label = labels,
                BoundingBoxes = boxes
            };
            items.Add(item);
        }

        Console.WriteLine($"  ML.NET 数据加载: {items.Count} 张图像, 来自 {imageDir}");

        return mlContext.Data.LoadFromEnumerable(items);
    }

    /// <summary>
    /// 查找对应的 YOLO 标签文件。
    /// 约定: images/xxx.jpg -> labels/xxx.txt
    /// </summary>
    private static string? FindLabelFile(string imagePath)
    {
        var dir = Path.GetDirectoryName(imagePath) ?? "";
        var baseName = Path.GetFileNameWithoutExtension(imagePath);

        // 尝试 images/ -> labels/ 替换
        var labelsDir = dir.Replace("images", "labels", StringComparison.OrdinalIgnoreCase);
        var labelPath = Path.Combine(labelsDir, baseName + ".txt");
        if (File.Exists(labelPath))
            return labelPath;

        // 尝试同目录下的 txt 文件
        labelPath = Path.Combine(dir, baseName + ".txt");
        if (File.Exists(labelPath))
            return labelPath;

        // 尝试同目录下的 labels 子目录
        labelPath = Path.Combine(dir, "labels", baseName + ".txt");
        if (File.Exists(labelPath))
            return labelPath;

        return null; // 无标签（背景图像）
    }

    /// <summary>
    /// 解析 YOLO 格式标签文件，转换为 ML.NET 所需的格式。
    /// </summary>
    private static (uint[] labels, float[] boxes) ParseYoloLabel(string? labelPath, string imagePath)
    {
        if (labelPath == null || !File.Exists(labelPath))
            return ([], []);

        // 获取图像尺寸用于坐标转换
        var imageInfo = SixLabors.ImageSharp.Image.Identify(imagePath);
        int imgW = imageInfo.Width;
        int imgH = imageInfo.Height;

        var labels = new List<uint>();
        var boxes = new List<float>();

        foreach (var line in File.ReadAllLines(labelPath))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                continue;

            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5) continue;

            if (!int.TryParse(parts[0], out int classId)) continue;
            if (!float.TryParse(parts[1], out float cx)) continue;
            if (!float.TryParse(parts[2], out float cy)) continue;
            if (!float.TryParse(parts[3], out float w)) continue;
            if (!float.TryParse(parts[4], out float h)) continue;

            // YOLO (cx, cy, w, h) 归一化 -> (x0, y0, x1, y1) 绝对像素
            float x0 = (cx - w / 2f) * imgW;
            float y0 = (cy - h / 2f) * imgH;
            float x1 = (cx + w / 2f) * imgW;
            float y1 = (cy + h / 2f) * imgH;

            // Clamp to image bounds
            x0 = Math.Max(0, Math.Min(x0, imgW));
            y0 = Math.Max(0, Math.Min(y0, imgH));
            x1 = Math.Max(0, Math.Min(x1, imgW));
            y1 = Math.Max(0, Math.Min(y1, imgH));

            // ML.NET 标签是 1-based
            labels.Add((uint)(classId + 1));
            boxes.AddRange([x0, y0, x1, y1]);
        }

        return (labels.ToArray(), boxes.ToArray());
    }

    /// <summary>
    /// 获取数据集中图像文件的数量。
    /// </summary>
    public static int CountImages(string imageDir)
    {
        if (!Directory.Exists(imageDir))
            return 0;

        return Directory.EnumerateFiles(imageDir, "*.*", SearchOption.AllDirectories)
            .Count(f => ImageExtensions.Contains(Path.GetExtension(f)));
    }
}
