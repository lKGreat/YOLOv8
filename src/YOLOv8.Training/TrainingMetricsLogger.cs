using System.Globalization;
using System.Text;

namespace YOLOv8.Training;

/// <summary>
/// Logs per-epoch training metrics to a CSV file and provides formatted summary display.
/// </summary>
public class TrainingMetricsLogger : IDisposable
{
    private readonly string saveDir;
    private readonly StreamWriter csvWriter;
    private readonly List<EpochMetrics> history = new();

    public IReadOnlyList<EpochMetrics> History => history;

    public record EpochMetrics(
        int Epoch,
        double BoxLoss,
        double ClsLoss,
        double DflLoss,
        double Map50,
        double Map5095,
        double Fitness,
        double Lr);

    public TrainingMetricsLogger(string saveDir)
    {
        this.saveDir = saveDir;
        Directory.CreateDirectory(saveDir);

        var csvPath = Path.Combine(saveDir, "results.csv");
        csvWriter = new StreamWriter(csvPath, append: false, Encoding.UTF8);
        csvWriter.WriteLine("epoch,box_loss,cls_loss,dfl_loss,mAP50,mAP50-95,fitness,lr");
        csvWriter.Flush();
    }

    /// <summary>
    /// Log one epoch's metrics to CSV and memory.
    /// </summary>
    public void LogEpoch(int epoch, double boxLoss, double clsLoss, double dflLoss,
        double map50, double map5095, double fitness, double lr)
    {
        var m = new EpochMetrics(epoch, boxLoss, clsLoss, dflLoss, map50, map5095, fitness, lr);
        history.Add(m);

        csvWriter.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "{0},{1:F6},{2:F6},{3:F6},{4:F6},{5:F6},{6:F6},{7:F8}",
            epoch, boxLoss, clsLoss, dflLoss, map50, map5095, fitness, lr));
        csvWriter.Flush();
    }

    /// <summary>
    /// Save model/training args to args.yaml in the save directory.
    /// </summary>
    public void SaveArgs(string variant, long paramCount, TrainConfig config)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"model: yolov8{variant}");
        sb.AppendLine($"parameters: {paramCount}");
        sb.AppendLine($"epochs: {config.Epochs}");
        sb.AppendLine($"batch_size: {config.BatchSize}");
        sb.AppendLine($"img_size: {config.ImgSize}");
        sb.AppendLine($"num_classes: {config.NumClasses}");
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "lr0: {0}", config.Lr0));
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "lrf: {0}", config.Lrf));
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "momentum: {0}", config.Momentum));
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "weight_decay: {0}", config.WeightDecay));
        sb.AppendLine($"optimizer: {config.Optimizer}");
        sb.AppendLine($"cos_lr: {config.CosLR}");
        sb.AppendLine($"close_mosaic: {config.CloseMosaic}");
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "box_gain: {0}", config.BoxGain));
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "cls_gain: {0}", config.ClsGain));
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "dfl_gain: {0}", config.DflGain));
        sb.AppendLine($"seed: {config.Seed}");

        File.WriteAllText(Path.Combine(saveDir, "args.yaml"), sb.ToString());
    }

    /// <summary>
    /// Print a formatted summary table of the training history.
    /// </summary>
    public void PrintSummary()
    {
        if (history.Count == 0) return;

        Console.WriteLine();
        Console.WriteLine("=== Training History ===");
        Console.WriteLine(string.Format("{0,6} {1,10} {2,10} {3,10} {4,10} {5,10} {6,10}",
            "Epoch", "box", "cls", "dfl", "mAP50", "mAP50-95", "fitness"));
        Console.WriteLine(new string('-', 72));

        // Show first 5, last 5, and best epoch
        var bestEpoch = history.MaxBy(m => m.Fitness);
        var toShow = new HashSet<int>();

        for (int i = 0; i < Math.Min(5, history.Count); i++)
            toShow.Add(i);
        for (int i = Math.Max(0, history.Count - 5); i < history.Count; i++)
            toShow.Add(i);
        if (bestEpoch != null)
            toShow.Add(history.IndexOf(bestEpoch));

        bool skipped = false;
        for (int i = 0; i < history.Count; i++)
        {
            if (!toShow.Contains(i))
            {
                if (!skipped)
                {
                    Console.WriteLine($"{"...",6}");
                    skipped = true;
                }
                continue;
            }
            skipped = false;

            var m = history[i];
            string marker = m == bestEpoch ? " *" : "";
            Console.WriteLine($"{m.Epoch,6} {m.BoxLoss,10:F4} {m.ClsLoss,10:F4} {m.DflLoss,10:F4} " +
                $"{m.Map50,10:F4} {m.Map5095,10:F4} {m.Fitness,10:F4}{marker}");
        }

        Console.WriteLine(new string('-', 72));
        if (bestEpoch != null)
        {
            Console.WriteLine($"Best epoch: {bestEpoch.Epoch} " +
                $"(fitness={bestEpoch.Fitness:F4}, mAP50={bestEpoch.Map50:F4}, mAP50-95={bestEpoch.Map5095:F4})");
        }
    }

    public void Dispose()
    {
        csvWriter?.Dispose();
    }
}
