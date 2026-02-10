using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Image;

namespace YOLO.MLNet.Data;

/// <summary>
/// ML.NET 目标检测输入数据模型。
/// 与 ML.NET ObjectDetectionTrainer 所需的 IDataView schema 对齐。
/// </summary>
public sealed class ObjectDetectionInput
{
    /// <summary>原始图像（从文件加载）。</summary>
    [ImageType(640, 640)]
    public MLImage Image { get; set; } = null!;

    /// <summary>
    /// 每个目标的类别标签（1-based key，与 ML.NET 约定一致）。
    /// 可变长度向量，元素个数 = 图中目标数量。
    /// </summary>
    [VectorType]
    public uint[] Label { get; set; } = [];

    /// <summary>
    /// 边界框坐标，格式为 [x0,y0,x1,y1, x0,y0,x1,y1, ...]，绝对像素坐标。
    /// 长度 = Label.Length * 4。
    /// </summary>
    [VectorType]
    public float[] BoundingBoxes { get; set; } = [];
}

/// <summary>
/// ML.NET 目标检测输出（推理结果）。
/// </summary>
public sealed class ObjectDetectionOutput
{
    /// <summary>预测的类别标签。</summary>
    [VectorType]
    [ColumnName("PredictedLabel")]
    public uint[] PredictedLabel { get; set; } = [];

    /// <summary>预测的边界框坐标 [x0,y0,x1,y1, ...]。</summary>
    [VectorType]
    [ColumnName("PredictedBoundingBoxes")]
    public float[] PredictedBoundingBoxes { get; set; } = [];

    /// <summary>每个预测框的置信度分数。</summary>
    [VectorType]
    [ColumnName("Score")]
    public float[] Score { get; set; } = [];
}
