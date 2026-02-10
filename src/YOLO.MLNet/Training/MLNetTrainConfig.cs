namespace YOLO.MLNet.Training;

/// <summary>
/// ML.NET 目标检测训练配置。
/// </summary>
public record MLNetTrainConfig
{
    /// <summary>最大训练轮数。</summary>
    public int MaxEpoch { get; init; } = 20;

    /// <summary>初始学习率。</summary>
    public double InitLearningRate { get; init; } = 0.01;

    /// <summary>权重衰减。</summary>
    public double WeightDecay { get; init; } = 0.0005;

    /// <summary>IoU 阈值（用于 NMS 去重）。</summary>
    public double IOUThreshold { get; init; } = 0.5;

    /// <summary>置信度阈值（过滤低置信度预测框）。</summary>
    public double ScoreThreshold { get; init; } = 0.5;

    /// <summary>输入图像尺寸。</summary>
    public int ImgSize { get; init; } = 640;

    /// <summary>模型保存目录。</summary>
    public string SaveDir { get; init; } = "runs/mlnet-train";

    /// <summary>日志打印频率（每 N 步打印一次）。</summary>
    public int LogEveryNStep { get; init; } = 50;

    /// <summary>学习率调度器 step 点。</summary>
    public List<int> LrSteps { get; init; } = [6, 12];

    // ── 蒸馏设置 ─────────────────────────────────────

    /// <summary>是否启用知识蒸馏。</summary>
    public bool UseDistillation { get; init; } = false;

    /// <summary>教师模型路径（.zip 格式的 ML.NET 模型）。</summary>
    public string? TeacherModelPath { get; init; }

    /// <summary>蒸馏阶段的训练轮数。</summary>
    public int DistillEpochs { get; init; } = 10;

    /// <summary>蒸馏阶段的学习率。</summary>
    public double DistillLearningRate { get; init; } = 0.001;

    /// <summary>蒸馏温度参数。</summary>
    public double DistillTemperature { get; init; } = 4.0;

    /// <summary>蒸馏损失权重（alpha）: total = alpha * distill + (1-alpha) * hard。</summary>
    public double DistillWeight { get; init; } = 0.5;
}
