using YOLO.MLNet.Training;

namespace YOLO.WinForms.Services;

/// <summary>
/// ML.NET 目标检测后台训练服务。
///
/// 封装 MLNetDetectionTrainer 和 MLNetDistillationTrainer，
/// 提供异步执行、取消、进度报告和实时 per-epoch 指标事件。
/// </summary>
public class MLNetTrainingService
{
    private CancellationTokenSource? _cts;

    /// <summary>每轮/阶段完成时触发。</summary>
    public event EventHandler<MLNetEpochMetrics>? EpochCompleted;

    /// <summary>训练成功完成时触发。</summary>
    public event EventHandler<MLNetTrainResult>? TrainingCompleted;

    /// <summary>训练失败时触发。</summary>
    public event EventHandler<string>? TrainingFailed;

    /// <summary>验证/测试完成时触发。</summary>
    public event EventHandler<YOLO.MLNet.Evaluation.MLNetEvalResult>? EvaluationCompleted;

    /// <summary>通用日志消息。</summary>
    public event EventHandler<string>? LogMessage;

    public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;

    /// <summary>
    /// 异步执行 ML.NET 目标检测训练。
    /// </summary>
    public Task<MLNetTrainResult?> TrainAsync(
        MLNetTrainConfig config,
        string trainDataDir,
        string? valDataDir,
        string[]? classNames = null)
    {
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        return Task.Run(() =>
        {
            try
            {
                LogMessage?.Invoke(this, $"[ML.NET] 开始训练: AutoFormerV2");
                LogMessage?.Invoke(this, $"[ML.NET] 轮数: {config.MaxEpoch}, 学习率: {config.InitLearningRate}");
                LogMessage?.Invoke(this, $"[ML.NET] 蒸馏: {(config.UseDistillation ? "启用" : "禁用")}");

                MLNetTrainResult result;

                if (config.UseDistillation && !string.IsNullOrEmpty(config.TeacherModelPath))
                {
                    LogMessage?.Invoke(this, $"[ML.NET] 教师模型: {config.TeacherModelPath}");
                    LogMessage?.Invoke(this, $"[ML.NET] 蒸馏温度: {config.DistillTemperature}, 权重: {config.DistillWeight}");

                    var distillTrainer = new MLNetDistillationTrainer(config);
                    result = distillTrainer.Train(
                        trainDataDir, valDataDir, classNames,
                        metrics =>
                        {
                            EpochCompleted?.Invoke(this, metrics);
                            ct.ThrowIfCancellationRequested();
                        },
                        ct);
                }
                else
                {
                    var trainer = new MLNetDetectionTrainer(config);
                    result = trainer.Train(
                        trainDataDir, valDataDir, classNames,
                        metrics =>
                        {
                            EpochCompleted?.Invoke(this, metrics);
                            ct.ThrowIfCancellationRequested();
                        },
                        ct);
                }

                TrainingCompleted?.Invoke(this, result);
                return result;
            }
            catch (OperationCanceledException)
            {
                LogMessage?.Invoke(this, "[ML.NET] 训练已被用户取消。");
                return null;
            }
            catch (Exception ex)
            {
                var msg = $"[ML.NET] 训练失败: {ex.Message}";
                LogMessage?.Invoke(this, msg);
                TrainingFailed?.Invoke(this, msg);
                return null;
            }
        }, ct);
    }

    /// <summary>
    /// 异步执行验证/测试评估。
    /// </summary>
    public Task<YOLO.MLNet.Evaluation.MLNetEvalResult?> EvaluateAsync(
        string modelPath,
        string evalDataDir,
        string[]? classNames = null)
    {
        _cts = new CancellationTokenSource();

        return Task.Run(() =>
        {
            try
            {
                LogMessage?.Invoke(this, $"[ML.NET] 评估模型: {modelPath}");
                LogMessage?.Invoke(this, $"[ML.NET] 评估数据: {evalDataDir}");

                var mlContext = new Microsoft.ML.MLContext();
                var model = mlContext.Model.Load(modelPath, out _);
                var evalData = YOLO.MLNet.Data.YoloToMLNetAdapter.LoadFromDirectory(mlContext, evalDataDir);

                var result = YOLO.MLNet.Evaluation.MLNetEvaluator.Evaluate(
                    mlContext, model, evalData, classNames);

                EvaluationCompleted?.Invoke(this, result);
                return result;
            }
            catch (Exception ex)
            {
                var msg = $"[ML.NET] 评估失败: {ex.Message}";
                LogMessage?.Invoke(this, msg);
                TrainingFailed?.Invoke(this, msg);
                return (YOLO.MLNet.Evaluation.MLNetEvalResult?)null;
            }
        });
    }

    /// <summary>
    /// 取消当前操作。
    /// </summary>
    public void Cancel()
    {
        _cts?.Cancel();
        LogMessage?.Invoke(this, "[ML.NET] 正在取消...");
    }

    /// <summary>
    /// 重置取消令牌。
    /// </summary>
    public void Reset()
    {
        _cts?.Dispose();
        _cts = null;
    }
}
