using TorchSharp;
using static TorchSharp.torch;
using YOLOv8.Core.Models;
using YOLOv8.Core.Utils;
using YOLOv8.Training.Loss;

namespace YOLOv8.Tests;

/// <summary>
/// Tests for PyTorch checkpoint reading, weight loading, key remapping, and distillation pipeline.
/// </summary>
public class WeightLoaderTests
{
    // === Key Remapping Tests ===

    [Theory]
    [InlineData("model.0.conv.weight", "backbone0.conv.weight")]
    [InlineData("model.0.bn.weight", "backbone0.bn.weight")]
    [InlineData("model.0.bn.bias", "backbone0.bn.bias")]
    [InlineData("model.0.bn.running_mean", "backbone0.bn.running_mean")]
    [InlineData("model.0.bn.running_var", "backbone0.bn.running_var")]
    [InlineData("model.0.bn.num_batches_tracked", "backbone0.bn.num_batches_tracked")]
    [InlineData("model.1.conv.weight", "backbone1.conv.weight")]
    [InlineData("model.9.cv1.conv.weight", "backbone9.cv1.conv.weight")]
    [InlineData("model.9.cv2.conv.weight", "backbone9.cv2.conv.weight")]
    public void RemapKey_Backbone_ConvAndSPPF(string pyKey, string expected)
    {
        var result = WeightLoader.RemapKey(pyKey);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("model.2.cv1.conv.weight", "backbone2.cv1.conv.weight")]
    [InlineData("model.2.cv2.conv.weight", "backbone2.cv2.conv.weight")]
    [InlineData("model.2.m.0.cv1.conv.weight", "backbone2.m.0.cv1.conv.weight")]
    [InlineData("model.2.m.0.cv2.bn.weight", "backbone2.m.0.cv2.bn.weight")]
    [InlineData("model.4.m.0.cv1.conv.weight", "backbone4.m.0.cv1.conv.weight")]
    [InlineData("model.6.cv1.bn.bias", "backbone6.cv1.bn.bias")]
    [InlineData("model.8.m.0.cv2.conv.weight", "backbone8.m.0.cv2.conv.weight")]
    public void RemapKey_Backbone_C2f(string pyKey, string expected)
    {
        var result = WeightLoader.RemapKey(pyKey);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("model.12.cv1.conv.weight", "neck_c2f1.cv1.conv.weight")]
    [InlineData("model.15.cv2.bn.weight", "neck_c2f2.cv2.bn.weight")]
    [InlineData("model.16.conv.weight", "neck_down1.conv.weight")]
    [InlineData("model.18.m.0.cv1.bn.bias", "neck_c2f3.m.0.cv1.bn.bias")]
    [InlineData("model.19.conv.weight", "neck_down2.conv.weight")]
    [InlineData("model.21.cv2.conv.weight", "neck_c2f4.cv2.conv.weight")]
    public void RemapKey_Neck(string pyKey, string expected)
    {
        var result = WeightLoader.RemapKey(pyKey);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("model.22.cv2.0.0.conv.weight", "detect.cv2.0.cv2_0_0.conv.weight")]
    [InlineData("model.22.cv2.0.0.bn.weight", "detect.cv2.0.cv2_0_0.bn.weight")]
    [InlineData("model.22.cv2.0.1.conv.weight", "detect.cv2.0.cv2_0_1.conv.weight")]
    [InlineData("model.22.cv2.0.2.weight", "detect.cv2.0.cv2_0_2.weight")]
    [InlineData("model.22.cv2.0.2.bias", "detect.cv2.0.cv2_0_2.bias")]
    [InlineData("model.22.cv2.1.0.conv.weight", "detect.cv2.1.cv2_1_0.conv.weight")]
    [InlineData("model.22.cv2.2.1.bn.bias", "detect.cv2.2.cv2_2_1.bn.bias")]
    [InlineData("model.22.cv3.0.0.conv.weight", "detect.cv3.0.cv3_0_0.conv.weight")]
    [InlineData("model.22.cv3.1.2.weight", "detect.cv3.1.cv3_1_2.weight")]
    [InlineData("model.22.cv3.2.0.bn.running_mean", "detect.cv3.2.cv3_2_0.bn.running_mean")]
    public void RemapKey_DetectHead_Cv2Cv3(string pyKey, string expected)
    {
        var result = WeightLoader.RemapKey(pyKey);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void RemapKey_DetectHead_DFL()
    {
        var result = WeightLoader.RemapKey("model.22.dfl.conv.weight");
        Assert.Equal("detect.dfl.conv.weight", result);
    }

    [Theory]
    [InlineData("model.10.weight")]  // Upsample (no params, but key would be null)
    [InlineData("model.11.weight")]  // Concat (no params)
    [InlineData("model.13.weight")]  // Upsample
    [InlineData("model.14.weight")]  // Concat
    public void RemapKey_NonParamLayers_ReturnsNull(string pyKey)
    {
        var result = WeightLoader.RemapKey(pyKey);
        Assert.Null(result);
    }

    [Fact]
    public void RemapKey_CSharpNaming_PassThrough()
    {
        // Keys already in C# naming should pass through
        Assert.Equal("backbone0.conv.weight", WeightLoader.RemapKey("backbone0.conv.weight"));
        Assert.Equal("detect.cv2.0.cv2_0_0.conv.weight",
            WeightLoader.RemapKey("detect.cv2.0.cv2_0_0.conv.weight"));
    }

    // === Pickle Reader Tests ===

    [Fact]
    public void PickleReader_SimpleDict()
    {
        // Test basic pickle parsing with a hand-crafted pickle byte sequence
        // Protocol 2 pickle for: {"hello": 42}
        var pickle = new byte[]
        {
            0x80, 0x02, // PROTO 2
            0x7D,       // EMPTY_DICT
            0x8C, 0x05, // SHORT_BINUNICODE "hello" (5 bytes)
            (byte)'h', (byte)'e', (byte)'l', (byte)'l', (byte)'o',
            0x4B, 42,   // BININT1 42
            0x73,       // SETITEM
            0x2E        // STOP
        };

        var reader = new PickleReader();
        var result = reader.Load(pickle);

        Assert.IsType<Dictionary<string, object?>>(result);
        var dict = (Dictionary<string, object?>)result!;
        Assert.Single(dict);
        Assert.Equal(42L, dict["hello"]);
    }

    [Fact]
    public void PickleReader_NestedDict()
    {
        // Protocol 2 pickle for: {"a": {"b": 1}}
        var pickle = new byte[]
        {
            0x80, 0x02,                                    // PROTO 2
            0x7D,                                          // EMPTY_DICT
            0x8C, 0x01, (byte)'a',                         // SHORT_BINUNICODE "a"
            0x7D,                                          // EMPTY_DICT (inner)
            0x8C, 0x01, (byte)'b',                         // SHORT_BINUNICODE "b"
            0x4B, 1,                                       // BININT1 1
            0x73,                                          // SETITEM (inner)
            0x73,                                          // SETITEM (outer)
            0x2E                                           // STOP
        };

        var reader = new PickleReader();
        var result = reader.Load(pickle);

        var dict = (Dictionary<string, object?>)result!;
        var inner = (Dictionary<string, object?>)dict["a"]!;
        Assert.Equal(1L, inner["b"]);
    }

    [Fact]
    public void PickleReader_Tuple()
    {
        // Protocol 2 pickle for tuple: (1, 2, 3)
        var pickle = new byte[]
        {
            0x80, 0x02,     // PROTO 2
            0x4B, 1,        // BININT1 1
            0x4B, 2,        // BININT1 2
            0x4B, 3,        // BININT1 3
            0x87,           // TUPLE3
            0x2E            // STOP
        };

        var reader = new PickleReader();
        var result = reader.Load(pickle);

        var tuple = (List<object?>)result!;
        Assert.Equal(3, tuple.Count);
        Assert.Equal(1L, tuple[0]);
        Assert.Equal(2L, tuple[1]);
        Assert.Equal(3L, tuple[2]);
    }

    [Fact]
    public void PickleReader_GlobalAndReduce()
    {
        // Pickle for: collections.OrderedDict() via GLOBAL + EMPTY_TUPLE + REDUCE
        var pickle = new byte[]
        {
            0x80, 0x02,
            0x63, // GLOBAL
        };
        var globalBytes = System.Text.Encoding.ASCII.GetBytes("collections\nOrderedDict\n");
        var rest = new byte[]
        {
            0x29, // EMPTY_TUPLE
            0x52, // REDUCE
            0x2E  // STOP
        };

        var full = pickle.Concat(globalBytes).Concat(rest).ToArray();
        var reader = new PickleReader();
        var result = reader.Load(full);

        Assert.IsType<PythonObject>(result);
        var obj = (PythonObject)result!;
        Assert.Equal("OrderedDict", obj.Type.Name);
        Assert.Equal("collections", obj.Type.Module);
    }

    // === Checkpoint Format Detection ===

    [Fact]
    public void DetectFormat_NonExistentFile_ReturnsUnknown()
    {
        var format = WeightLoader.DetectFormat("nonexistent_file.pt");
        Assert.Equal(CheckpointFormat.Unknown, format);
    }

    // === TorchSharp Save/Load Round Trip ===

    [Fact]
    public void SaveAndLoad_TorchSharpFormat_RoundTrip()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"yolov8_test_{Guid.NewGuid()}.pt");

        try
        {
            // Create and save a model
            using var model1 = new YOLOv8Model("yolov8_save", nc: 3, variant: "n");
            model1.save(tempPath);

            // Load into a new model
            using var model2 = new YOLOv8Model("yolov8_load", nc: 3, variant: "n");
            model2.load(tempPath);

            // Verify parameter shapes match
            var params1 = model1.named_parameters().ToList();
            var params2 = model2.named_parameters().ToList();

            Assert.Equal(params1.Count, params2.Count);

            for (int i = 0; i < params1.Count; i++)
            {
                Assert.Equal(params1[i].name, params2[i].name);
                Assert.Equal(params1[i].parameter.shape, params2[i].parameter.shape);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public void SmartLoad_TorchSharpFormat_Succeeds()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"yolov8_smart_{Guid.NewGuid()}.pt");

        try
        {
            // Create and save model
            using var model1 = new YOLOv8Model("yolov8_smart_save", nc: 3, variant: "n");
            model1.save(tempPath);

            // SmartLoad should detect TorchSharp format
            using var model2 = new YOLOv8Model("yolov8_smart_load", nc: 3, variant: "n");
            var result = WeightLoader.SmartLoad(model2, tempPath);

            Assert.True(result.LoadedCount > 0);
            Assert.Equal(0, result.MissingCount);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    // === Distillation Pipeline Tests ===

    [Fact]
    public void DistillationLoss_Logit_ComputesWithoutError()
    {
        int nc = 3;
        int regMax = 16;
        int totalAnchors = 100;
        int batch = 2;

        using var studentRawBox = torch.randn(batch, 4 * regMax, totalAnchors);
        using var studentRawCls = torch.randn(batch, nc, totalAnchors);
        using var teacherRawBox = torch.randn(batch, 4 * regMax, totalAnchors);
        using var teacherRawCls = torch.randn(batch, nc, totalAnchors);

        using var distillLoss = new DistillationLoss(
            "test_distill",
            temperature: 20.0,
            mode: "logit");

        var (loss, lossItem) = distillLoss.Compute(
            studentRawBox, studentRawCls,
            teacherRawBox, teacherRawCls);

        Assert.False(loss.isnan().any().item<bool>());
        Assert.False(loss.isinf().any().item<bool>());
        Assert.True(loss.item<float>() >= 0);

        loss.Dispose();
        lossItem.Dispose();
    }

    [Fact]
    public void DistillationLoss_Feature_ComputesWithoutError()
    {
        long[] studentCh = [32, 64, 128];
        long[] teacherCh = [64, 128, 256];

        using var distillLoss = new DistillationLoss(
            "test_distill_feat",
            temperature: 20.0,
            mode: "feature",
            studentChannels: studentCh,
            teacherChannels: teacherCh);

        int nc = 3;
        int regMax = 16;
        int totalAnchors = 100;
        int batch = 2;

        using var studentRawBox = torch.randn(batch, 4 * regMax, totalAnchors);
        using var studentRawCls = torch.randn(batch, nc, totalAnchors);
        using var teacherRawBox = torch.randn(batch, 4 * regMax, totalAnchors);
        using var teacherRawCls = torch.randn(batch, nc, totalAnchors);

        var studentFeats = new Tensor[]
        {
            torch.randn(batch, 32, 80, 80),
            torch.randn(batch, 64, 40, 40),
            torch.randn(batch, 128, 20, 20)
        };

        var teacherFeats = new Tensor[]
        {
            torch.randn(batch, 64, 80, 80),
            torch.randn(batch, 128, 40, 40),
            torch.randn(batch, 256, 20, 20)
        };

        var (loss, lossItem) = distillLoss.Compute(
            studentRawBox, studentRawCls,
            teacherRawBox, teacherRawCls,
            studentFeats, teacherFeats);

        Assert.False(loss.isnan().any().item<bool>());
        Assert.False(loss.isinf().any().item<bool>());
        Assert.True(loss.item<float>() >= 0);

        loss.Dispose();
        lossItem.Dispose();
        foreach (var f in studentFeats) f.Dispose();
        foreach (var f in teacherFeats) f.Dispose();
    }

    [Fact]
    public void DistillationLoss_Both_ComputesWithoutError()
    {
        long[] studentCh = [64, 128, 256];
        long[] teacherCh = [64, 128, 256]; // same size

        using var distillLoss = new DistillationLoss(
            "test_distill_both",
            temperature: 20.0,
            mode: "both",
            studentChannels: studentCh,
            teacherChannels: teacherCh);

        int nc = 3;
        int regMax = 16;
        int totalAnchors = 100;
        int batch = 2;

        using var studentRawBox = torch.randn(batch, 4 * regMax, totalAnchors);
        using var studentRawCls = torch.randn(batch, nc, totalAnchors);
        using var teacherRawBox = torch.randn(batch, 4 * regMax, totalAnchors);
        using var teacherRawCls = torch.randn(batch, nc, totalAnchors);

        var studentFeats = new Tensor[]
        {
            torch.randn(batch, 64, 80, 80),
            torch.randn(batch, 128, 40, 40),
            torch.randn(batch, 256, 20, 20)
        };

        var teacherFeats = new Tensor[]
        {
            torch.randn(batch, 64, 80, 80),
            torch.randn(batch, 128, 40, 40),
            torch.randn(batch, 256, 20, 20)
        };

        var (loss, lossItem) = distillLoss.Compute(
            studentRawBox, studentRawCls,
            teacherRawBox, teacherRawCls,
            studentFeats, teacherFeats);

        Assert.False(loss.isnan().any().item<bool>());
        Assert.False(loss.isinf().any().item<bool>());
        Assert.True(loss.item<float>() >= 0);

        loss.Dispose();
        lossItem.Dispose();
        foreach (var f in studentFeats) f.Dispose();
        foreach (var f in teacherFeats) f.Dispose();
    }

    // === Teacher-Student Forward Pipeline Test ===

    [Fact]
    public void TeacherStudentForward_DistillationPipeline()
    {
        // Simulates the full distillation pipeline:
        // 1. Create teacher (YOLOv8s) and student (YOLOv8n) models
        // 2. Freeze teacher
        // 3. Forward pass both on same input
        // 4. Compute distillation loss

        int nc = 3;

        using var teacher = new YOLOv8Model("teacher", nc, "s");
        using var student = new YOLOv8Model("student", nc, "n");

        // Freeze teacher
        teacher.eval();
        foreach (var p in teacher.parameters())
            p.requires_grad = false;

        // Student is in train mode
        student.train();

        using var input = torch.randn(1, 3, 320, 320); // smaller size for test speed

        // Teacher forward (no gradient)
        Tensor tRawBox, tRawCls;
        Tensor[] teacherFeats;
        using (torch.no_grad())
        {
            var tResult = teacher.ForwardTrainWithFeatures(input);
            tRawBox = tResult.rawBox;
            tRawCls = tResult.rawCls;
            teacherFeats = tResult.neckFeatures;
        }

        // Student forward
        var sResult = student.ForwardTrainWithFeatures(input);
        var sRawBox = sResult.rawBox;
        var sRawCls = sResult.rawCls;
        var studentFeats = sResult.neckFeatures;

        // Verify shapes match between teacher and student
        Assert.Equal(tRawBox.shape[0], sRawBox.shape[0]); // batch
        Assert.Equal(tRawBox.shape[1], sRawBox.shape[1]); // 4*reg_max
        Assert.Equal(tRawBox.shape[2], sRawBox.shape[2]); // total anchors
        Assert.Equal(tRawCls.shape[0], sRawCls.shape[0]); // batch
        Assert.Equal(tRawCls.shape[1], sRawCls.shape[1]); // nc
        Assert.Equal(tRawCls.shape[2], sRawCls.shape[2]); // total anchors

        // Neck features: same spatial size, different channels
        Assert.Equal(teacherFeats.Length, studentFeats.Length); // 3 levels
        for (int i = 0; i < teacherFeats.Length; i++)
        {
            Assert.Equal(teacherFeats[i].shape[0], studentFeats[i].shape[0]); // batch
            Assert.Equal(teacherFeats[i].shape[2], studentFeats[i].shape[2]); // H
            Assert.Equal(teacherFeats[i].shape[3], studentFeats[i].shape[3]); // W
            // Channels may differ between teacher (s) and student (n)
        }

        // Compute distillation loss
        long[] sCh = [student.Ch2, student.Ch3, student.Ch4];
        long[] tCh = [teacher.Ch2, teacher.Ch3, teacher.Ch4];

        using var distillLoss = new DistillationLoss(
            "test_distill_pipeline",
            temperature: 20.0,
            mode: "both",
            studentChannels: sCh,
            teacherChannels: tCh);

        var (loss, lossItem) = distillLoss.Compute(
            sRawBox, sRawCls, tRawBox, tRawCls,
            studentFeats, teacherFeats);

        Assert.False(loss.isnan().any().item<bool>());
        Assert.True(loss.item<float>() >= 0);

        // Verify gradient can flow back through student
        loss.backward();

        // Check that student parameters have gradients
        bool hasGrad = student.parameters().Any(p => p.grad is not null && !p.grad.IsInvalid);
        Assert.True(hasGrad, "Student should have gradients after backward pass");

        // Clean up
        loss.Dispose();
        lossItem.Dispose();
        tRawBox.Dispose();
        tRawCls.Dispose();
        sRawBox.Dispose();
        sRawCls.Dispose();
        foreach (var f in teacherFeats) f.Dispose();
        foreach (var f in studentFeats) f.Dispose();
    }

    [Fact]
    public void TeacherModel_SaveLoad_ThenDistill()
    {
        // Test the full workflow:
        // 1. Create teacher model, save in TorchSharp format
        // 2. Load into new model using SmartLoad
        // 3. Use as teacher for distillation

        string tempPath = Path.Combine(Path.GetTempPath(), $"teacher_{Guid.NewGuid()}.pt");

        try
        {
            int nc = 3;

            // Create and save teacher
            using (var teacherOrig = new YOLOv8Model("teacher_orig", nc, "s"))
            {
                teacherOrig.save(tempPath);
            }

            // Load teacher using SmartLoad
            using var teacher = new YOLOv8Model("teacher_loaded", nc, "s");
            var loadResult = WeightLoader.SmartLoad(teacher, tempPath);
            Assert.True(loadResult.LoadedCount > 0);

            teacher.eval();
            foreach (var p in teacher.parameters())
                p.requires_grad = false;

            // Create student
            using var student = new YOLOv8Model("student", nc, "n");
            student.train();

            // Forward pass
            using var input = torch.randn(1, 3, 320, 320);

            Tensor tRawBox, tRawCls;
            using (torch.no_grad())
            {
                (tRawBox, tRawCls, _) = teacher.ForwardTrain(input);
            }

            var (sRawBox, sRawCls, _) = student.ForwardTrain(input);

            using var distillLoss = new DistillationLoss(
                "test_distill_reload",
                temperature: 20.0,
                mode: "logit");

            var (loss, lossItem) = distillLoss.Compute(
                sRawBox, sRawCls, tRawBox, tRawCls);

            Assert.False(loss.isnan().any().item<bool>());

            loss.Dispose();
            lossItem.Dispose();
            tRawBox.Dispose();
            tRawCls.Dispose();
            sRawBox.Dispose();
            sRawCls.Dispose();
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    // === Load from Python Checkpoint (if file exists) ===

    [Fact]
    public void LoadPythonCheckpoint_IfAvailable()
    {
        // This test only runs if the Python checkpoint exists
        string ptPath = @"D:\Code\YOLOv8\yolov8s.pt";

        if (!File.Exists(ptPath))
        {
            // Skip test if file doesn't exist
            return;
        }

        // Detect format
        var format = WeightLoader.DetectFormat(ptPath);
        Assert.Equal(CheckpointFormat.PyTorch, format);

        // Read checkpoint
        using var reader = new PyTorchCheckpointReader(ptPath);
        var stateDict = reader.ReadStateDict();

        Assert.True(stateDict.Count > 0, "Should have extracted tensors from checkpoint");

        // Verify some expected Python keys exist
        bool hasBackboneKeys = stateDict.Keys.Any(k => k.StartsWith("model.0."));
        bool hasDetectKeys = stateDict.Keys.Any(k => k.StartsWith("model.22."));
        Assert.True(hasBackboneKeys, "Should have backbone keys (model.0.*)");
        Assert.True(hasDetectKeys, "Should have detect head keys (model.22.*)");

        // Load into model
        using var model = new YOLOv8Model("yolov8s_py", nc: 80, variant: "s");
        var result = WeightLoader.LoadFromCheckpoint(model, ptPath);

        Assert.True(result.LoadedCount > 0, $"Should have loaded parameters, got {result.LoadedCount}");

        // Verify forward pass works after loading
        using var input = torch.randn(1, 3, 640, 640);
        var (boxes, scores, rawFeats) = model.forward(input);

        long totalAnchors = 8400; // 80*80 + 40*40 + 20*20
        Assert.Equal(new long[] { 1, 4, totalAnchors }, boxes.shape);
        Assert.Equal(new long[] { 1, 80, totalAnchors }, scores.shape);

        boxes.Dispose();
        scores.Dispose();
        foreach (var f in rawFeats) f.Dispose();
        foreach (var t in stateDict.Values) t.Dispose();
    }

    // === Model Parameter Name Consistency ===

    [Fact]
    public void ModelParameterNames_DumpAll()
    {
        // Diagnostic test: dump all parameter and buffer names to understand TorchSharp naming
        using var model = new YOLOv8Model("test_params", nc: 80, variant: "s");

        var allNames = new List<string>();
        foreach (var (name, _) in model.named_parameters())
            allNames.Add(name);
        foreach (var (name, _) in model.named_buffers())
            allNames.Add(name);

        // Check if ANY parameter contains bottleneck indicators
        var bottleneckNames = allNames.Where(n =>
            n.Contains("m.0") || n.Contains("m-0") || n.Contains("_m0") ||
            n.Contains("m[0]") || n.Contains("bottleneck")).ToList();

        // Also show total count vs expected
        long totalParams = model.parameters().Sum(p => p.numel());

        string allParamsStr = string.Join("\n", allNames);
        Assert.True(bottleneckNames.Count > 0,
            $"Should find bottleneck parameters (total params: {totalParams}, total names: {allNames.Count}).\n" +
            $"All params:\n{allParamsStr}");
    }

    [Fact]
    public void ModelParameterNames_MatchRemapTargets()
    {
        // Verify that the key remapping produces names that exist in the actual model
        using var model = new YOLOv8Model("test_params", nc: 80, variant: "s");

        var paramNames = new HashSet<string>();
        foreach (var (name, _) in model.named_parameters())
            paramNames.Add(name);
        foreach (var (name, _) in model.named_buffers())
            paramNames.Add(name);

        // Test basic keys first (no bottleneck arrays)
        string[] basicPythonKeys =
        [
            "model.0.conv.weight",
            "model.0.bn.weight",
            "model.0.bn.bias",
            "model.2.cv1.conv.weight",
            "model.9.cv1.conv.weight",
            "model.12.cv1.conv.weight",
            "model.22.dfl.conv.weight",
        ];

        foreach (var pyKey in basicPythonKeys)
        {
            var csKey = WeightLoader.RemapKey(pyKey);
            Assert.NotNull(csKey);
            Assert.True(paramNames.Contains(csKey!),
                $"Remapped key '{csKey}' (from '{pyKey}') not found in model parameters. " +
                $"Available params with same prefix: " +
                $"{string.Join(", ", paramNames.Where(p => p.StartsWith(csKey!.Split('.')[0])).Take(10))}");
        }
    }
}
