using TorchSharp;
using YOLO.Core.Models;
using YOLO.Core.Heads;
using YOLO.Training.Loss;
using static TorchSharp.torch;

namespace YOLO.Tests;

/// <summary>
/// Regression test suite covering all five task types (Detect, Segment, Pose, Classify, OBB).
/// Each test verifies:
///   1. Model construction and forward pass shape correctness
///   2. Loss computation does not NaN/Inf
///   3. Weight count matches expected parameter count range
///
/// These tests serve as the statistical consistency acceptance gate.
/// Run multiple times with different seeds to verify reproducibility.
/// </summary>
public class RegressionSuiteTests
{
    private const int ImgSize = 64; // small for speed

    [Theory]
    [InlineData("n")]
    [InlineData("s")]
    public void Detect_AllVariants_ForwardAndLoss(string variant)
    {
        using var _ = NewDisposeScope();
        int nc = 80;
        var model = new YOLOv8Model("det", nc: nc, variant: variant);
        model.train();

        var input = torch.randn(2, 3, ImgSize, ImgSize);
        var (rawBox, rawCls, featureSizes) = model.ForwardTrain(input);

        Assert.Equal(2, rawBox.shape[0]);
        Assert.Equal(nc, rawCls.shape[1]);

        // Loss smoke test
        var loss = new DetectionLoss(nc);
        long totalAnchors = 0;
        foreach (var (h, w) in featureSizes) totalAnchors += h * w;

        var gtLabels = torch.zeros(2, 1, 1);
        var gtBboxes = torch.zeros(2, 1, 4);
        gtBboxes[0, 0] = torch.tensor(new float[] { 0.1f, 0.1f, 0.5f, 0.5f });
        gtBboxes[1, 0] = torch.tensor(new float[] { 0.2f, 0.2f, 0.6f, 0.6f });
        var maskGT = torch.ones(2, 1, 1);

        var (totalLoss, lossItems) = loss.Compute(rawBox, rawCls, featureSizes,
            gtLabels, gtBboxes, maskGT, ImgSize);

        Assert.False(totalLoss.isnan().item<bool>(), "Loss should not be NaN");
        Assert.False(totalLoss.isinf().item<bool>(), "Loss should not be Inf");
    }

    [Fact]
    public void Segment_ForwardAndLoss()
    {
        using var _ = NewDisposeScope();
        int nc = 80;
        var model = new YOLOv8SegModel("seg", nc: nc, variant: "n", nm: 32);
        model.train();

        var input = torch.randn(2, 3, ImgSize, ImgSize);
        var (rawBox, rawCls, featureSizes, mc, proto) = model.ForwardTrain(input);

        Assert.Equal(32, mc.shape[1]);
        Assert.Equal(32, proto.shape[1]);

        // Segmentation loss smoke
        var loss = new SegmentationLoss(nc, nm: 32);
        var gtLabels = torch.zeros(2, 1, 1);
        var gtBboxes = torch.zeros(2, 1, 4);
        gtBboxes[0, 0] = torch.tensor(new float[] { 0.1f, 0.1f, 0.5f, 0.5f });
        gtBboxes[1, 0] = torch.tensor(new float[] { 0.2f, 0.2f, 0.6f, 0.6f });
        var maskGT = torch.ones(2, 1, 1);
        var gtMasks = torch.zeros(2, 1, proto.shape[2], proto.shape[3]);

        var (totalLoss, lossItems) = loss.Compute(rawBox, rawCls, featureSizes,
            mc, proto, gtLabels, gtBboxes, maskGT, gtMasks, ImgSize);

        Assert.False(totalLoss.isnan().item<bool>(), "Seg loss should not be NaN");
    }

    [Fact]
    public void Pose_ForwardAndLoss()
    {
        using var _ = NewDisposeScope();
        int nc = 1;
        var model = new YOLOv8PoseModel("pose", nc: nc, variant: "n", nkpt: 17, ndim: 3);
        model.train();

        var input = torch.randn(2, 3, ImgSize, ImgSize);
        var (rawBox, rawCls, featureSizes, rawKpt) = model.ForwardTrain(input);

        Assert.Equal(51, rawKpt.shape[1]); // 17*3

        var loss = new PoseLoss(nc, nkpt: 17, ndim: 3);
        var gtLabels = torch.zeros(2, 1, 1);
        var gtBboxes = torch.zeros(2, 1, 4);
        gtBboxes[0, 0] = torch.tensor(new float[] { 0.1f, 0.1f, 0.5f, 0.5f });
        gtBboxes[1, 0] = torch.tensor(new float[] { 0.2f, 0.2f, 0.6f, 0.6f });
        var maskGT = torch.ones(2, 1, 1);
        var gtKpts = torch.zeros(2, 1, 17, 3);

        var (totalLoss, lossItems) = loss.Compute(rawBox, rawCls, featureSizes,
            rawKpt, gtLabels, gtBboxes, maskGT, gtKpts, ImgSize);

        Assert.False(totalLoss.isnan().item<bool>(), "Pose loss should not be NaN");
    }

    [Fact]
    public void Classify_ForwardAndLoss()
    {
        using var _ = NewDisposeScope();
        int nc = 10;
        var model = new YOLOv8ClsModel("cls", nc: nc, variant: "n");
        model.train();

        var input = torch.randn(4, 3, ImgSize, ImgSize);
        var logits = model.forward(input);

        Assert.Equal(new long[] { 4, nc }, logits.shape);

        var loss = new ClassificationLoss();
        var targets = torch.zeros(4, dtype: ScalarType.Int64);
        var (totalLoss, lossItems) = loss.Compute(logits, targets);

        Assert.False(totalLoss.isnan().item<bool>(), "Cls loss should not be NaN");
    }

    [Fact]
    public void OBB_ForwardAndLoss()
    {
        using var _ = NewDisposeScope();
        int nc = 15;
        var model = new YOLOv8OBBModel("obb", nc: nc, variant: "n");
        model.train();

        var input = torch.randn(2, 3, ImgSize, ImgSize);
        var (rawBox, rawCls, featureSizes, rawAngle) = model.ForwardTrain(input);

        Assert.Equal(1, rawAngle.shape[1]); // ne=1

        var loss = new OBBLoss(nc);
        var gtLabels = torch.zeros(2, 1, 1);
        var gtBboxes = torch.zeros(2, 1, 4);
        gtBboxes[0, 0] = torch.tensor(new float[] { 0.1f, 0.1f, 0.5f, 0.5f });
        gtBboxes[1, 0] = torch.tensor(new float[] { 0.2f, 0.2f, 0.6f, 0.6f });
        var maskGT = torch.ones(2, 1, 1);

        var (totalLoss, lossItems) = loss.Compute(rawBox, rawCls, featureSizes,
            rawAngle, gtLabels, gtBboxes, maskGT, imgSize: ImgSize);

        Assert.False(totalLoss.isnan().item<bool>(), "OBB loss should not be NaN");
    }

    [Theory]
    [InlineData("n", 3_000_000, 4_000_000)]   // ~3.2M
    [InlineData("s", 10_000_000, 13_000_000)]  // ~11.2M
    public void Detect_ParameterCount_InRange(string variant, long min, long max)
    {
        using var _ = NewDisposeScope();
        var model = new YOLOv8Model("det_param", nc: 80, variant: variant);
        long count = model.parameters().Sum(p => p.numel());
        Assert.InRange(count, min, max);
    }

    /// <summary>
    /// Deterministic reproducibility test: same seed → same output.
    /// </summary>
    [Fact]
    public void Detect_DeterministicOutput()
    {
        using var _ = NewDisposeScope();
        torch.manual_seed(42);
        var model1 = new YOLOv8Model("det_seed1", nc: 80, variant: "n");
        model1.eval();
        var input1 = torch.randn(1, 3, ImgSize, ImgSize);
        var (boxes1, scores1, _) = model1.forward(input1);
        var b1 = boxes1.clone();

        torch.manual_seed(42);
        var model2 = new YOLOv8Model("det_seed2", nc: 80, variant: "n");
        model2.eval();
        var input2 = torch.randn(1, 3, ImgSize, ImgSize);
        var (boxes2, scores2, _) = model2.forward(input2);

        Assert.True(torch.allclose(b1, boxes2, atol: 1e-5),
            "Same seed should produce identical outputs");
    }
}
