using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace YOLOv8.Data.Datasets;

/// <summary>
/// YAML dataset configuration matching YOLO format.
/// Example:
///   train: path/to/train/images
///   val: path/to/val/images
///   nc: 80
///   names: ['person', 'bicycle', ...]
/// </summary>
public class DatasetConfig
{
    public string? Path { get; set; }
    public string Train { get; set; } = string.Empty;
    public string Val { get; set; } = string.Empty;
    public string? Test { get; set; }
    public int Nc { get; set; }
    public List<string> Names { get; set; } = new();

    /// <summary>
    /// Load dataset config from a YAML file.
    /// </summary>
    public static DatasetConfig Load(string yamlPath)
    {
        var yaml = File.ReadAllText(yamlPath);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var config = deserializer.Deserialize<DatasetConfig>(yaml);

        // Resolve relative paths
        var basePath = config.Path ?? System.IO.Path.GetDirectoryName(yamlPath) ?? "";
        if (!System.IO.Path.IsPathRooted(config.Train))
            config.Train = System.IO.Path.Combine(basePath, config.Train);
        if (!System.IO.Path.IsPathRooted(config.Val))
            config.Val = System.IO.Path.Combine(basePath, config.Val);
        if (config.Test != null && !System.IO.Path.IsPathRooted(config.Test))
            config.Test = System.IO.Path.Combine(basePath, config.Test);

        return config;
    }
}
