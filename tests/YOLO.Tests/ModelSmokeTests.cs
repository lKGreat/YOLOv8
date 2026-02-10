using TorchSharp;
using YOLO.Core.Models;
using YOLO.Core.Heads;
using static TorchSharp.torch;

namespace YOLO.Tests;

/// <summary>
/// Smoke tests: construct each model variant, run a dummy forward pass,
/// and verify output tensor shapes match expectations.
/// </summary>
public class ModelSmokeTests
{
    private const int Nc = 80;
    private const int ImgSize = 64; // small for speed

    [Fact]
    public void YOLOv8SegModel_Forward_ShapesCorrect()
    {
        using var _ = NewDisposeScope();
        var model = new YOLOv8SegModel("test_seg", nc: Nc, variant: "n", nm: 32);
        model.eval();

        var input = torch.randn(1, 3, ImgSize, ImgSize);
        var output = model.forward(input);

        // Boxes: (1, 4, totalAnchors)
        Assert.Equal(1, output.Boxes.shape[0]);
        Assert.Equal(4, output.Boxes.shape[1]);

        // Scores: (1, nc, totalAnchors)
        Assert.Equal(Nc, output.Scores.shape[1]);

        // MaskCoeffs: (1, nm, totalAnchors)
        Assert.Equal(1, output.MaskCoeffs.shape[0]);
        Assert.Equal(32, output.MaskCoeffs.shape[1]);

        // Protos: (1, nm, H_proto, W_proto)
        Assert.Equal(1, output.Protos.shape[0]);
        Assert.Equal(32, output.Protos.shape[1]);
    }

    [Fact]
    public void YOLOv8PoseModel_Forward_ShapesCorrect()
    {
        using var _ = NewDisposeScope();
        var model = new YOLOv8PoseModel("test_pose", nc: 1, variant: "n", nkpt: 17, ndim: 3);
        model.eval();

        var input = torch.randn(1, 3, ImgSize, ImgSize);
        var output = model.forward(input);

        // Boxes: (1, 4, totalAnchors)
        Assert.Equal(1, output.Boxes.shape[0]);

        // Keypoints: (1, 51, totalAnchors) = 17*3
        Assert.Equal(1, output.Keypoints.shape[0]);
        Assert.Equal(51, output.Keypoints.shape[1]);
    }

    [Fact]
    public void YOLOv8ClsModel_Forward_ShapesCorrect()
    {
        using var _ = NewDisposeScope();
        int ncCls = 10;
        var model = new YOLOv8ClsModel("test_cls", nc: ncCls, variant: "n");
        model.eval();

        var input = torch.randn(1, 3, ImgSize, ImgSize);
        var output = model.forward(input);

        // Output: (1, nc) softmax probabilities
        Assert.Equal(new long[] { 1, ncCls }, output.shape);

        // Should sum to ~1.0 (softmax)
        var sum = output.sum().item<float>();
        Assert.InRange(sum, 0.99f, 1.01f);
    }

    [Fact]
    public void YOLOv8SegModel_ForwardTrain_ShapesCorrect()
    {
        using var _ = NewDisposeScope();
        var model = new YOLOv8SegModel("test_seg_train", nc: Nc, variant: "n", nm: 32);
        model.train();

        var input = torch.randn(2, 3, ImgSize, ImgSize);
        var (rawBox, rawCls, featureSizes, maskCoeffs, protos) = model.ForwardTrain(input);

        long totalAnchors = 0;
        foreach (var (h, w) in featureSizes)
            totalAnchors += h * w;

        // rawBox: (2, 64, totalAnchors)
        Assert.Equal(2, rawBox.shape[0]);
        Assert.Equal(64, rawBox.shape[1]); // 4 * 16
        Assert.Equal(totalAnchors, rawBox.shape[2]);

        // rawCls: (2, nc, totalAnchors)
        Assert.Equal(Nc, rawCls.shape[1]);

        // maskCoeffs: (2, nm, totalAnchors)
        Assert.Equal(32, maskCoeffs.shape[1]);
        Assert.Equal(totalAnchors, maskCoeffs.shape[2]);

        // protos: (2, nm, H, W)
        Assert.Equal(2, protos.shape[0]);
        Assert.Equal(32, protos.shape[1]);
    }

    [Fact]
    public void YOLOv8ClsModel_ForwardTrain_ReturnsLogits()
    {
        using var _ = NewDisposeScope();
        int ncCls = 10;
        var model = new YOLOv8ClsModel("test_cls_train", nc: ncCls, variant: "n");
        model.train();

        var input = torch.randn(2, 3, ImgSize, ImgSize);
        var output = model.forward(input);

        // In training mode, output is raw logits (NOT softmaxed)
        Assert.Equal(new long[] { 2, ncCls }, output.shape);
    }

    [Fact]
    public void YOLOv8OBBModel_Forward_ShapesCorrect()
    {
        using var _ = NewDisposeScope();
        int ncObb = 15; // DOTA dataset has 15 classes
        var model = new YOLOv8OBBModel("test_obb", nc: ncObb, variant: "n");
        model.eval();

        var input = torch.randn(1, 3, ImgSize, ImgSize);
        var output = model.forward(input);

        // Boxes: (1, 4, totalAnchors)
        Assert.Equal(1, output.Boxes.shape[0]);
        Assert.Equal(4, output.Boxes.shape[1]);

        // Scores: (1, nc, totalAnchors)
        Assert.Equal(ncObb, output.Scores.shape[1]);

        // Angles: (1, 1, totalAnchors)
        Assert.Equal(1, output.Angles.shape[0]);
        Assert.Equal(1, output.Angles.shape[1]);
    }

    [Fact]
    public void YOLOv8OBBModel_ForwardTrain_ShapesCorrect()
    {
        using var _ = NewDisposeScope();
        int ncObb = 15;
        var model = new YOLOv8OBBModel("test_obb_train", nc: ncObb, variant: "n");
        model.train();

        var input = torch.randn(2, 3, ImgSize, ImgSize);
        var (rawBox, rawCls, featureSizes, rawAngle) = model.ForwardTrain(input);

        long totalAnchors = 0;
        foreach (var (h, w) in featureSizes)
            totalAnchors += h * w;

        Assert.Equal(2, rawBox.shape[0]);
        Assert.Equal(64, rawBox.shape[1]); // 4 * 16
        Assert.Equal(totalAnchors, rawBox.shape[2]);

        Assert.Equal(ncObb, rawCls.shape[1]);

        Assert.Equal(1, rawAngle.shape[1]); // ne=1
        Assert.Equal(totalAnchors, rawAngle.shape[2]);
    }
}
