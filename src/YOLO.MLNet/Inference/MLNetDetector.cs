using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Image;
using YOLO.MLNet.Data;
using YOLO.Runtime.Results;

namespace YOLO.MLNet.Inference;

/// <summary>
/// ML.NET 目标检测推理封装。
///
/// 加载训练好的 ML.NET 模型 (.zip), 提供与 YOLO.Runtime 兼容的
/// DetectionResult[] 返回格式, 便于在 ModelTestPanel 中复用展示逻辑。
/// </summary>
public class MLNetDetector : IDisposable
{
    private readonly MLContext _mlContext;
    private readonly ITransformer _model;
    private readonly PredictionEngine<ObjectDetectionInput, ObjectDetectionOutput> _engine;
    private readonly string[]? _classNames;
    private bool _disposed;

    /// <summary>
    /// 从 .zip 模型文件创建推理器。
    /// </summary>
    /// <param name="modelPath">.zip 格式的 ML.NET 模型路径</param>
    /// <param name="classNames">可选的类别名称</param>
    public MLNetDetector(string modelPath, string[]? classNames = null)
    {
        if (!File.Exists(modelPath))
            throw new FileNotFoundException($"模型文件不存在: {modelPath}");

        _classNames = classNames;
        _mlContext = new MLContext();
        _model = _mlContext.Model.Load(modelPath, out _);
        _engine = _mlContext.Model.CreatePredictionEngine<ObjectDetectionInput, ObjectDetectionOutput>(_model);
    }

    /// <summary>
    /// 对单张图像执行目标检测。
    /// </summary>
    /// <param name="imagePath">图像文件路径</param>
    /// <param name="scoreThreshold">置信度阈值</param>
    /// <returns>检测结果数组（与 YOLO.Runtime 兼容的格式）</returns>
    public DetectionResult[] Detect(string imagePath, float scoreThreshold = 0.5f)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException($"图像文件不存在: {imagePath}");

        var input = new ObjectDetectionInput
        {
            Image = MLImage.CreateFromFile(imagePath),
            Label = [],
            BoundingBoxes = []
        };

        var output = _engine.Predict(input);
        return ConvertToDetectionResults(output, scoreThreshold);
    }

    /// <summary>
    /// 对批量图像执行目标检测。
    /// </summary>
    public DetectionResult[][] DetectBatch(string[] imagePaths, float scoreThreshold = 0.5f)
    {
        var results = new DetectionResult[imagePaths.Length][];
        for (int i = 0; i < imagePaths.Length; i++)
        {
            results[i] = Detect(imagePaths[i], scoreThreshold);
        }
        return results;
    }

    /// <summary>
    /// 将 ML.NET 输出转换为 YOLO.Runtime.DetectionResult[]。
    /// </summary>
    private DetectionResult[] ConvertToDetectionResults(ObjectDetectionOutput output, float scoreThreshold)
    {
        if (output.PredictedLabel == null || output.PredictedLabel.Length == 0)
            return [];

        var results = new List<DetectionResult>();

        for (int i = 0; i < output.PredictedLabel.Length; i++)
        {
            float score = i < output.Score.Length ? output.Score[i] : 0;
            if (score < scoreThreshold)
                continue;

            int boxIdx = i * 4;
            if (boxIdx + 3 >= output.PredictedBoundingBoxes.Length)
                continue;

            float x0 = output.PredictedBoundingBoxes[boxIdx];
            float y0 = output.PredictedBoundingBoxes[boxIdx + 1];
            float x1 = output.PredictedBoundingBoxes[boxIdx + 2];
            float y1 = output.PredictedBoundingBoxes[boxIdx + 3];

            // ML.NET 标签是 1-based, 转为 0-based
            int classId = (int)output.PredictedLabel[i] - 1;
            classId = Math.Max(0, classId);

            string? className = _classNames != null && classId < _classNames.Length
                ? _classNames[classId]
                : null;

            results.Add(new DetectionResult(x0, y0, x1, y1, score, classId, className));
        }

        // 按置信度降序排序
        return results.OrderByDescending(r => r.Confidence).ToArray();
    }

    /// <summary>
    /// 尝试从模型目录加载类别名称。
    /// 搜索 args.yaml 或 names.txt。
    /// </summary>
    public static string[]? TryLoadClassNames(string modelPath)
    {
        var dir = Path.GetDirectoryName(modelPath);
        if (dir == null) return null;

        // 尝试 args.yaml (在 weights/ 的父目录)
        var parentDir = Path.GetDirectoryName(dir);
        if (parentDir != null)
        {
            var argsPath = Path.Combine(parentDir, "args.yaml");
            if (File.Exists(argsPath))
            {
                return ParseClassNamesFromYaml(argsPath);
            }
        }

        // 尝试同目录 args.yaml
        var sameDirArgs = Path.Combine(dir, "args.yaml");
        if (File.Exists(sameDirArgs))
            return ParseClassNamesFromYaml(sameDirArgs);

        // 尝试 names.txt
        var namesPath = Path.Combine(dir, "names.txt");
        if (File.Exists(namesPath))
        {
            return File.ReadAllLines(namesPath)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToArray();
        }

        return null;
    }

    /// <summary>
    /// 从 args.yaml 中解析类别名称。
    /// </summary>
    private static string[]? ParseClassNamesFromYaml(string yamlPath)
    {
        var lines = File.ReadAllLines(yamlPath);
        var names = new List<string>();
        bool inNames = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("names:"))
            {
                inNames = true;
                continue;
            }
            if (inNames)
            {
                if (trimmed.Contains(':') && char.IsDigit(trimmed[0]))
                {
                    var val = trimmed[(trimmed.IndexOf(':') + 1)..].Trim().Trim('\'', '"');
                    names.Add(val);
                }
                else if (!string.IsNullOrWhiteSpace(trimmed) && !trimmed.StartsWith('-'))
                {
                    break;
                }
            }
        }

        return names.Count > 0 ? names.ToArray() : null;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            (_engine as IDisposable)?.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
