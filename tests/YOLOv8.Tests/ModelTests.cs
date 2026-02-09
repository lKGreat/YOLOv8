using TorchSharp;
using static TorchSharp.torch;
using YOLOv8.Core.Models;
using YOLOv8.Core.Modules;
using YOLOv8.Core.Heads;
using YOLOv8.Core.Utils;

namespace YOLOv8.Tests;

/// <summary>
/// Tests verifying the model architecture, module shapes, and forward pass correctness.
/// </summary>
public class ModelTests
{
    [Fact]
    public void ConvBlock_OutputShape_Correct()
    {
        using var conv = new ConvBlock("test_conv", 3, 64, k: 3, s: 2);
        using var input = torch.randn(1, 3, 640, 640);
        using var output = conv.forward(input);

        Assert.Equal(new long[] { 1, 64, 320, 320 }, output.shape);
    }

    [Fact]
    public void ConvBlock_NoAct_OutputShape_Correct()
    {
        using var conv = new ConvBlock("test_conv_noact", 64, 128, k: 1, s: 1, useAct: false);
        using var input = torch.randn(1, 64, 80, 80);
        using var output = conv.forward(input);

        Assert.Equal(new long[] { 1, 128, 80, 80 }, output.shape);
    }

    [Fact]
    public void Bottleneck_WithShortcut_OutputShape_Correct()
    {
        using var bn = new Bottleneck("test_bn", 64, 64, shortcut: true);
        using var input = torch.randn(1, 64, 80, 80);
        using var output = bn.forward(input);

        Assert.Equal(new long[] { 1, 64, 80, 80 }, output.shape);
    }

    [Fact]
    public void C2f_OutputShape_Correct()
    {
        using var c2f = new C2f("test_c2f", 128, 128, n: 3, shortcut: true);
        using var input = torch.randn(1, 128, 80, 80);
        using var output = c2f.forward(input);

        Assert.Equal(new long[] { 1, 128, 80, 80 }, output.shape);
    }

    [Fact]
    public void C2f_DifferentChannels_OutputShape_Correct()
    {
        using var c2f = new C2f("test_c2f2", 256, 128, n: 2, shortcut: false);
        using var input = torch.randn(1, 256, 40, 40);
        using var output = c2f.forward(input);

        Assert.Equal(new long[] { 1, 128, 40, 40 }, output.shape);
    }

    [Fact]
    public void SPPF_OutputShape_Correct()
    {
        using var sppf = new SPPF("test_sppf", 512, 512, k: 5);
        using var input = torch.randn(1, 512, 20, 20);
        using var output = sppf.forward(input);

        Assert.Equal(new long[] { 1, 512, 20, 20 }, output.shape);
    }

    [Fact]
    public void DFL_OutputShape_Correct()
    {
        using var dfl = new DFL("test_dfl", 16);
        // Input: (B, 4*reg_max, N)
        using var input = torch.randn(2, 64, 100);
        using var output = dfl.forward(input);

        Assert.Equal(new long[] { 2, 4, 100 }, output.shape);
    }

    [Fact]
    public void DetectHead_OutputShapes_Correct()
    {
        int nc = 80;
        var channelsPerLevel = new long[] { 64, 128, 256 };
        var strides = new long[] { 8, 16, 32 };

        using var head = new DetectHead("test_detect", nc, channelsPerLevel, strides, regMax: 16);

        // Create feature maps matching P3, P4, P5 sizes for 640x640 input
        var feats = new Tensor[]
        {
            torch.randn(1, 64, 80, 80),   // P3/8
            torch.randn(1, 128, 40, 40),   // P4/16
            torch.randn(1, 256, 20, 20)    // P5/32
        };

        var (boxes, scores, rawFeats) = head.forward(feats);

        long totalAnchors = 80 * 80 + 40 * 40 + 20 * 20; // 8400

        Assert.Equal(new long[] { 1, 4, totalAnchors }, boxes.shape);
        Assert.Equal(new long[] { 1, nc, totalAnchors }, scores.shape);

        foreach (var f in feats) f.Dispose();
        boxes.Dispose();
        scores.Dispose();
        foreach (var f in rawFeats) f.Dispose();
    }

    [Fact]
    public void YOLOv8Model_Nano_ForwardShape()
    {
        using var model = new YOLOv8Model("yolov8n_test", nc: 80, variant: "n");
        using var input = torch.randn(1, 3, 640, 640);

        var (boxes, scores, rawFeats) = model.forward(input);

        // YOLOv8n at 640: P3=80x80=6400, P4=40x40=1600, P5=20x20=400 -> 8400 anchors
        long totalAnchors = 8400;
        Assert.Equal(new long[] { 1, 4, totalAnchors }, boxes.shape);
        Assert.Equal(new long[] { 1, 80, totalAnchors }, scores.shape);

        boxes.Dispose();
        scores.Dispose();
        foreach (var f in rawFeats) f.Dispose();
    }

    [Fact]
    public void YOLOv8Model_Small_ForwardShape()
    {
        using var model = new YOLOv8Model("yolov8s_test", nc: 80, variant: "s");
        using var input = torch.randn(1, 3, 640, 640);

        var (boxes, scores, rawFeats) = model.forward(input);

        long totalAnchors = 8400;
        Assert.Equal(new long[] { 1, 4, totalAnchors }, boxes.shape);
        Assert.Equal(new long[] { 1, 80, totalAnchors }, scores.shape);

        boxes.Dispose();
        scores.Dispose();
        foreach (var f in rawFeats) f.Dispose();
    }

    [Fact]
    public void YOLOv8Model_BatchForward()
    {
        using var model = new YOLOv8Model("yolov8n_batch", nc: 20, variant: "n");
        using var input = torch.randn(4, 3, 640, 640);

        var (boxes, scores, rawFeats) = model.forward(input);

        Assert.Equal(4, boxes.shape[0]);
        Assert.Equal(4, scores.shape[0]);
        Assert.Equal(20, scores.shape[1]); // nc = 20

        boxes.Dispose();
        scores.Dispose();
        foreach (var f in rawFeats) f.Dispose();
    }

    [Fact]
    public void ModelConfig_ScaleWidth_DivisibleBy8()
    {
        // Width scaling should always produce multiples of 8
        long result = ModelConfig.ScaleWidth(64, 0.25, 1024);
        Assert.Equal(0, result % 8);

        result = ModelConfig.ScaleWidth(128, 0.75, 768);
        Assert.Equal(0, result % 8);

        result = ModelConfig.ScaleWidth(1024, 1.25, 512);
        Assert.Equal(0, result % 8);
    }

    [Fact]
    public void ModelConfig_ScaleDepth_MinimumOne()
    {
        int result = ModelConfig.ScaleDepth(3, 0.33);
        Assert.True(result >= 1);

        result = ModelConfig.ScaleDepth(1, 0.1);
        Assert.Equal(1, result);
    }

    [Fact]
    public void BboxUtils_Dist2Bbox_RoundTrip()
    {
        // Test that dist2bbox produces reasonable outputs
        var anchors = torch.tensor(new float[,] { { 5, 5 }, { 10, 10 } }); // 2 anchors
        var dist = torch.tensor(new float[,,] { { { 2, 3 }, { 2, 3 }, { 2, 3 }, { 2, 3 } } }); // (1, 4, 2)

        var boxes = BboxUtils.Dist2Bbox(dist, anchors, xywh: true);
        Assert.Equal(new long[] { 1, 4, 2 }, boxes.shape);

        anchors.Dispose();
        dist.Dispose();
        boxes.Dispose();
    }

    [Fact]
    public void BboxUtils_Xywh2Xyxy_Conversion()
    {
        var xywh = torch.tensor(new float[] { 50, 50, 20, 30 }).unsqueeze(0); // (1, 4)
        var xyxy = BboxUtils.Xywh2Xyxy(xywh);

        Assert.Equal(40, xyxy[0, 0].item<float>(), 1e-5);  // x1 = 50 - 10
        Assert.Equal(35, xyxy[0, 1].item<float>(), 1e-5);  // y1 = 50 - 15
        Assert.Equal(60, xyxy[0, 2].item<float>(), 1e-5);  // x2 = 50 + 10
        Assert.Equal(65, xyxy[0, 3].item<float>(), 1e-5);  // y2 = 50 + 15

        xywh.Dispose();
        xyxy.Dispose();
    }

    [Fact]
    public void IoUUtils_BoxIoU_IdenticalBoxes()
    {
        var box = torch.tensor(new float[] { 10, 10, 50, 50 }).unsqueeze(0);
        var iou = IoUUtils.BoxIoU(box, box);

        Assert.True(iou[0].item<float>() > 0.999f);

        box.Dispose();
        iou.Dispose();
    }

    [Fact]
    public void IoUUtils_BoxIoU_NoOverlap()
    {
        var box1 = torch.tensor(new float[] { 0, 0, 10, 10 }).unsqueeze(0);
        var box2 = torch.tensor(new float[] { 20, 20, 30, 30 }).unsqueeze(0);
        var iou = IoUUtils.BoxIoU(box1, box2);

        Assert.True(iou[0].item<float>() < 1e-5f);

        box1.Dispose();
        box2.Dispose();
        iou.Dispose();
    }

    [Fact]
    public void IoUUtils_CIoU_IdenticalBoxes()
    {
        var box = torch.tensor(new float[] { 10, 10, 50, 50 }).unsqueeze(0);
        var ciou = IoUUtils.CIoU(box, box);

        Assert.True(ciou[0].item<float>() > 0.999f);

        box.Dispose();
        ciou.Dispose();
    }

    [Fact]
    public void MakeAnchors_TotalCount()
    {
        var featureSizes = new (long h, long w)[] { (80, 80), (40, 40), (20, 20) };
        var strides = new long[] { 8, 16, 32 };

        var (anchors, stride) = BboxUtils.MakeAnchors(featureSizes, strides);

        long expected = 80 * 80 + 40 * 40 + 20 * 20; // 8400
        Assert.Equal(expected, anchors.shape[0]);
        Assert.Equal(expected, stride.shape[0]);
        Assert.Equal(2, anchors.shape[1]);
        Assert.Equal(1, stride.shape[1]);

        anchors.Dispose();
        stride.Dispose();
    }

    [Fact]
    public void YOLOv8Model_ParameterCount_Reasonable()
    {
        using var model = new YOLOv8Model("yolov8n_params", nc: 80, variant: "n");
        long totalParams = model.parameters().Sum(p => p.numel());

        // YOLOv8n should have ~3M parameters
        Assert.True(totalParams > 1_000_000, $"Too few params: {totalParams}");
        Assert.True(totalParams < 10_000_000, $"Too many params: {totalParams}");
    }
}
