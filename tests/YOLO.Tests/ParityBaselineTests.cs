using YOLO.App.Parity;

namespace YOLO.Tests;

/// <summary>
/// Tests for the parity-freeze / parity-report baseline service.
/// </summary>
public class ParityBaselineTests
{
    /// <summary>
    /// Find the workspace root (d:\Code\YOLOv8 or equivalent) by walking up from test dir.
    /// Returns null if not found (CI without full workspace).
    /// </summary>
    private static string? FindRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10; i++)
        {
            if (Directory.Exists(Path.Combine(dir, "ultralytics")) &&
                Directory.Exists(Path.Combine(dir, "csharp")))
                return dir;
            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }
        return null;
    }

    [Fact]
    public void Freeze_CreatesLockFile()
    {
        var root = FindRoot();
        if (root is null) return; // skip when workspace is not available

        var tmpDir = Path.Combine(Path.GetTempPath(), $"parity_test_{Guid.NewGuid():N}");
        try
        {
            var lockPath = ParityBaselineService.Freeze(root, tmpDir);
            Assert.True(File.Exists(lockPath), "baseline.lock.json should be created");

            var json = File.ReadAllText(lockPath);
            Assert.Contains("ultralytics-latest-local-snapshot", json);
            Assert.Contains("GlobalPythonSha256", json);
        }
        finally
        {
            if (Directory.Exists(tmpDir))
                Directory.Delete(tmpDir, true);
        }
    }

    [Fact]
    public void FreezeAndVerify_RoundTrip_AllMatch()
    {
        var root = FindRoot();
        if (root is null) return;

        var tmpDir = Path.Combine(Path.GetTempPath(), $"parity_rt_{Guid.NewGuid():N}");
        try
        {
            var lockPath = ParityBaselineService.Freeze(root, tmpDir);
            var report = ParityBaselineService.Verify(root, lockPath);

            Assert.True(report.AllKeyFilesMatched,
                "Immediately after freeze, all key files should match");
            Assert.True(report.KeyFileChecks.Count > 0,
                "Should have checked at least one key file");
        }
        finally
        {
            if (Directory.Exists(tmpDir))
                Directory.Delete(tmpDir, true);
        }
    }

    [Fact]
    public void Verify_DetectsDrift_WhenLockTampered()
    {
        var root = FindRoot();
        if (root is null) return;

        var tmpDir = Path.Combine(Path.GetTempPath(), $"parity_drift_{Guid.NewGuid():N}");
        try
        {
            var lockPath = ParityBaselineService.Freeze(root, tmpDir);

            // Tamper: replace a hash with an obviously wrong value
            var content = File.ReadAllText(lockPath);
            content = content.Replace(
                "\"Exists\": true",
                "\"Exists\": false",
                StringComparison.Ordinal);
            // Only replace the first occurrence to create a mismatch
            File.WriteAllText(lockPath, content);

            var report = ParityBaselineService.Verify(root, lockPath);
            // At least one check should now fail
            Assert.False(report.AllKeyFilesMatched,
                "After tampering, verification should report a mismatch");
        }
        finally
        {
            if (Directory.Exists(tmpDir))
                Directory.Delete(tmpDir, true);
        }
    }
}
