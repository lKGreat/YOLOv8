using System;

namespace YOLO.MLNet.Training;

/// <summary>
/// ML.NET 训练期间的实时进度快照（用于 UI 状态栏/心跳输出）。
/// </summary>
public record MLNetLiveProgress
{
    /// <summary>已完成的 epoch（尽力从日志/阶段推断）。</summary>
    public int CompletedEpochs { get; init; }

    /// <summary>总 epoch。</summary>
    public int TotalEpochs { get; init; }

    /// <summary>当前 step（若可从日志解析到）。</summary>
    public int? Step { get; init; }

    /// <summary>总 step（若可从日志解析到）。</summary>
    public int? TotalSteps { get; init; }

    /// <summary>训练已运行时间。</summary>
    public TimeSpan Elapsed { get; init; }

    /// <summary>当前阶段（如 "Fit" / "Validate"）。</summary>
    public string Stage { get; init; } = "Fit";

    /// <summary>最近一条训练相关日志（已过滤）。</summary>
    public string? LastLog { get; init; }

    /// <summary>最后一次收到训练日志的时间（UTC）。</summary>
    public DateTime LastLogAtUtc { get; init; } = DateTime.UtcNow;
}

