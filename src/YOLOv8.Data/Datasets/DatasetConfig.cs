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
    [YamlMember(Alias = "path")]
    public string? Path { get; set; }

    [YamlMember(Alias = "train")]
    public string Train { get; set; } = string.Empty;

    [YamlMember(Alias = "val")]
    public string Val { get; set; } = string.Empty;

    [YamlMember(Alias = "test")]
    public string? Test { get; set; }

    [YamlMember(Alias = "nc")]
    public int Nc { get; set; }

    [YamlMember(Alias = "names")]
    public List<string> Names { get; set; } = new();

    /// <summary>
    /// Load dataset config from a YAML file.
    /// Uses underscore naming convention to match Python YOLO YAML format.
    /// </summary>
    public static DatasetConfig Load(string yamlPath)
    {
        var yaml = File.ReadAllText(yamlPath);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var config = deserializer.Deserialize<DatasetConfig>(yaml);

        // Resolve relative paths
        var basePath = config.Path ?? System.IO.Path.GetDirectoryName(yamlPath) ?? "";

        // If 'path' is a relative path, resolve it relative to the YAML file location
        if (config.Path != null && !System.IO.Path.IsPathRooted(config.Path))
        {
            var yamlDir = System.IO.Path.GetDirectoryName(yamlPath) ?? "";
            basePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(yamlDir, config.Path));
        }

        if (!System.IO.Path.IsPathRooted(config.Train))
            config.Train = System.IO.Path.Combine(basePath, config.Train);
        if (!System.IO.Path.IsPathRooted(config.Val))
            config.Val = System.IO.Path.Combine(basePath, config.Val);
        if (config.Test != null && !System.IO.Path.IsPathRooted(config.Test))
            config.Test = System.IO.Path.Combine(basePath, config.Test);

        return config;
    }
}
