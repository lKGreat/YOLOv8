using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace YOLO.App.Parity;

/// <summary>
/// Freeze and verify a Python baseline snapshot for parity checks.
/// This service is C#-only and does not execute Python code.
/// </summary>
public static class ParityBaselineService
{
    private static readonly string[] KeyPythonFiles =
    [
        @"ultralytics\nn\tasks.py",
        @"ultralytics\nn\modules\head.py",
        @"ultralytics\utils\loss.py",
        @"ultralytics\utils\tal.py",
        @"ultralytics\data\augment.py",
        @"ultralytics\models\yolo\detect\train.py",
        @"ultralytics\models\yolo\segment\train.py",
        @"ultralytics\models\yolo\pose\train.py",
        @"ultralytics\models\yolo\classify\train.py"
    ];

    public static string Freeze(string workspaceRoot, string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        var ultralyticsRoot = Path.Combine(workspaceRoot, "ultralytics");
        if (!Directory.Exists(ultralyticsRoot))
            throw new DirectoryNotFoundException($"未找到 Python 基线目录: {ultralyticsRoot}");

        var manifest = new BaselineManifest
        {
            CreatedUtc = DateTime.UtcNow,
            WorkspaceRoot = workspaceRoot,
            BaselineTag = "ultralytics-latest-local-snapshot",
            Tasks = new TaskSupport
            {
                Detect = true,
                Segment = true,
                Pose = true,
                Classify = true,
                OBB = DetectObbPresence(ultralyticsRoot)
            }
        };

        foreach (var rel in KeyPythonFiles)
        {
            var abs = Path.Combine(workspaceRoot, rel);
            manifest.KeyFiles.Add(new FileHashRecord
            {
                RelativePath = rel.Replace('\\', '/'),
                Exists = File.Exists(abs),
                Sha256 = File.Exists(abs) ? Sha256File(abs) : null
            });
        }

        // Collect a compact hash over all Python sources used for parity.
        var pyFiles = Directory
            .GetFiles(ultralyticsRoot, "*.py", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        using var sha = SHA256.Create();
        foreach (var file in pyFiles)
        {
            var rel = Path.GetRelativePath(workspaceRoot, file).Replace('\\', '/');
            var bytes = Encoding.UTF8.GetBytes(rel + "\n" + Sha256File(file) + "\n");
            _ = sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }
        _ = sha.TransformFinalBlock([], 0, 0);
        manifest.GlobalPythonSha256 = Convert.ToHexString(sha.Hash!);
        manifest.PythonFileCount = pyFiles.Length;

        var outputPath = Path.Combine(outputDir, "baseline.lock.json");
        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        File.WriteAllText(outputPath, json);
        return outputPath;
    }

    public static BaselineVerificationReport Verify(string workspaceRoot, string baselineLockPath)
    {
        if (!File.Exists(baselineLockPath))
            throw new FileNotFoundException("未找到 baseline lock 文件", baselineLockPath);

        var manifest = JsonSerializer.Deserialize<BaselineManifest>(
            File.ReadAllText(baselineLockPath), JsonOptions)
            ?? throw new InvalidDataException("baseline.lock.json 解析失败");

        var report = new BaselineVerificationReport
        {
            BaselineLockPath = baselineLockPath,
            WorkspaceRoot = workspaceRoot,
            BaselineTag = manifest.BaselineTag
        };

        foreach (var rec in manifest.KeyFiles)
        {
            var abs = Path.Combine(workspaceRoot, rec.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            bool existsNow = File.Exists(abs);
            var nowHash = existsNow ? Sha256File(abs) : null;
            bool match = rec.Exists == existsNow &&
                         string.Equals(rec.Sha256, nowHash, StringComparison.OrdinalIgnoreCase);

            report.KeyFileChecks.Add(new FileCheckResult
            {
                RelativePath = rec.RelativePath,
                ExpectedExists = rec.Exists,
                CurrentExists = existsNow,
                ExpectedSha256 = rec.Sha256,
                CurrentSha256 = nowHash,
                IsMatch = match
            });
        }

        report.AllKeyFilesMatched = report.KeyFileChecks.All(x => x.IsMatch);
        return report;
    }

    private static bool DetectObbPresence(string ultralyticsRoot)
    {
        var hasObbCfg = Directory.GetFiles(Path.Combine(ultralyticsRoot, "cfg"), "*obb*.yaml", SearchOption.AllDirectories).Length > 0;
        var hasObbLogic = Directory.GetFiles(ultralyticsRoot, "*.py", SearchOption.AllDirectories)
            .Any(f =>
            {
                var name = Path.GetFileName(f);
                return name.Contains("obb", StringComparison.OrdinalIgnoreCase);
            });
        return hasObbCfg || hasObbLogic;
    }

    private static string Sha256File(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

public sealed class BaselineManifest
{
    public DateTime CreatedUtc { get; set; }
    public string WorkspaceRoot { get; set; } = string.Empty;
    public string BaselineTag { get; set; } = string.Empty;
    public string GlobalPythonSha256 { get; set; } = string.Empty;
    public int PythonFileCount { get; set; }
    public TaskSupport Tasks { get; set; } = new();
    public List<FileHashRecord> KeyFiles { get; set; } = [];
}

public sealed class TaskSupport
{
    public bool Detect { get; set; }
    public bool Segment { get; set; }
    public bool Pose { get; set; }
    public bool Classify { get; set; }
    public bool OBB { get; set; }
}

public sealed class FileHashRecord
{
    public string RelativePath { get; set; } = string.Empty;
    public bool Exists { get; set; }
    public string? Sha256 { get; set; }
}

public sealed class BaselineVerificationReport
{
    public string WorkspaceRoot { get; set; } = string.Empty;
    public string BaselineLockPath { get; set; } = string.Empty;
    public string BaselineTag { get; set; } = string.Empty;
    public bool AllKeyFilesMatched { get; set; }
    public List<FileCheckResult> KeyFileChecks { get; set; } = [];
}

public sealed class FileCheckResult
{
    public string RelativePath { get; set; } = string.Empty;
    public bool ExpectedExists { get; set; }
    public bool CurrentExists { get; set; }
    public string? ExpectedSha256 { get; set; }
    public string? CurrentSha256 { get; set; }
    public bool IsMatch { get; set; }
}
