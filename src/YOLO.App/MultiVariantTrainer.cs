using TorchSharp;
using YOLO.Data.Datasets;
using YOLO.Training;
using static TorchSharp.torch;

namespace YOLO.App;

/// <summary>
/// 批量训练多个 YOLO 模型变体的简单脚本
/// 使用同一份数据训练多个型号（s, m等），每个训练20轮
/// </summary>
public static class MultiVariantTrainer
{
    /// <summary>
    /// 批量训练多个模型变体
    /// </summary>
    /// <param name="dataYamlPath">数据集配置文件路径（YAML格式）</param>
    /// <param name="variants">要训练的模型变体列表，例如：["s", "m"]</param>
    /// <param name="epochs">训练轮数（默认20）</param>
    /// <param name="batchSize">批次大小（默认16）</param>
    /// <param name="imgSize">图像大小（默认640）</param>
    /// <param name="saveDir">保存目录（默认runs/multi_train）</param>
    /// <param name="device">设备（默认自动选择）</param>
    public static void TrainMultipleVariants(
        string dataYamlPath,
        string[] variants,
        int epochs = 20,
        int batchSize = 16,
        int imgSize = 640,
        string saveDir = "runs/multi_train",
        Device? device = null)
    {
        Console.WriteLine("=== YOLO 多模型批量训练 ===");
        Console.WriteLine($"数据集配置: {dataYamlPath}");
        Console.WriteLine($"模型变体: {string.Join(", ", variants.Select(v => $"YOLO{v}"))}");
        Console.WriteLine($"训练轮数: {epochs}");
        Console.WriteLine($"批次大小: {batchSize}");
        Console.WriteLine($"图像大小: {imgSize}");
        Console.WriteLine($"保存目录: {saveDir}");
        Console.WriteLine();

        // 加载数据集配置
        if (!File.Exists(dataYamlPath))
        {
            Console.Error.WriteLine($"错误: 数据集配置文件不存在: {dataYamlPath}");
            return;
        }

        Console.WriteLine($"加载数据集配置: {dataYamlPath}");
        var dataConfig = DatasetConfig.Load(dataYamlPath);
        Console.WriteLine($"  类别数: {dataConfig.Nc}");
        if (dataConfig.Names.Count > 0)
        {
            Console.WriteLine($"  类别名称: {string.Join(", ", dataConfig.Names.Take(5))}" +
                (dataConfig.Names.Count > 5 ? $" ... +{dataConfig.Names.Count - 5} 更多" : ""));
        }
        Console.WriteLine($"  训练数据: {dataConfig.Train}");
        Console.WriteLine($"  验证数据: {dataConfig.Val ?? "(无)"}");
        Console.WriteLine();

        // 设置设备
        device ??= torch.cuda.is_available() ? torch.CUDA : torch.CPU;
        Console.WriteLine($"使用设备: {device}");
        Console.WriteLine();

        // 创建基础训练配置
        var baseConfig = new TrainConfig
        {
            Epochs = epochs,
            BatchSize = batchSize,
            ImgSize = imgSize,
            NumClasses = dataConfig.Nc,
            SaveDir = saveDir,
            Seed = 0
        };

        // 使用 BenchmarkRunner 进行批量训练
        var runner = new BenchmarkRunner(
            baseConfig,
            dataConfig.Train,
            dataConfig.Val,
            dataConfig.Names.ToArray(),
            device);

        // 运行训练
        runner.Run(variants);

        Console.WriteLine();
        Console.WriteLine("=== 批量训练完成 ===");
        Console.WriteLine($"所有模型已保存到: {saveDir}");
    }

}
