using TorchSharp;
using static TorchSharp.torch;
using YOLOv8.Core.Utils;
using YOLOv8.Inference.Metrics;

namespace YOLOv8.Tests;

/// <summary>
/// Tests for loss computation and metrics.
/// </summary>
public class LossTests
{
    [Fact]
    public void CIoU_Returns_Between_Minus1_And_1()
    {
        var pred = torch.tensor(new float[,]
        {
            { 10, 10, 50, 50 },
            { 20, 20, 60, 60 },
            { 0, 0, 100, 100 }
        });

        var target = torch.tensor(new float[,]
        {
            { 15, 15, 55, 55 },
            { 25, 25, 65, 65 },
            { 50, 50, 150, 150 }
        });

        var ciou = IoUUtils.CIoU(pred, target);

        Assert.Equal(3, ciou.shape[0]);

        for (int i = 0; i < 3; i++)
        {
            float val = ciou[i].item<float>();
            Assert.True(val >= -1.5 && val <= 1.0, $"CIoU[{i}] = {val} out of range");
        }

        pred.Dispose();
        target.Dispose();
        ciou.Dispose();
    }

    [Fact]
    public void BatchBoxIoU_Matrix_Shape()
    {
        var box1 = torch.tensor(new float[,] { { 0, 0, 10, 10 }, { 5, 5, 15, 15 } }); // (2, 4)
        var box2 = torch.tensor(new float[,] { { 0, 0, 10, 10 }, { 20, 20, 30, 30 }, { 5, 5, 15, 15 } }); // (3, 4)

        var iou = IoUUtils.BatchBoxIoU(box1, box2);

        Assert.Equal(new long[] { 2, 3 }, iou.shape);

        // box1[0] vs box2[0] should be ~1.0 (identical)
        Assert.True(iou[0, 0].item<float>() > 0.999f);

        // box1[0] vs box2[1] should be ~0.0 (no overlap)
        Assert.True(iou[0, 1].item<float>() < 0.001f);

        box1.Dispose();
        box2.Dispose();
        iou.Dispose();
    }

    [Fact]
    public void MAPMetric_PerfectPrediction()
    {
        var metric = new MAPMetric(numClasses: 2);

        var gtBoxes = new float[,] { { 10, 10, 50, 50 }, { 60, 60, 100, 100 } };
        var gtClasses = new int[] { 0, 1 };

        // Perfect predictions
        var predBoxes = new float[,] { { 10, 10, 50, 50 }, { 60, 60, 100, 100 } };
        var predScores = new float[] { 0.9f, 0.8f };
        var predClasses = new int[] { 0, 1 };

        metric.Update(predBoxes, predScores, predClasses, gtBoxes, gtClasses);

        var (map50, map5095, perClassAP) = metric.Compute();

        Assert.True(map50 > 0.9, $"mAP@0.5 should be > 0.9 for perfect predictions, got {map50}");
    }

    [Fact]
    public void MAPMetric_NoPredictions_ZeroMap()
    {
        var metric = new MAPMetric(numClasses: 2);

        var gtBoxes = new float[,] { { 10, 10, 50, 50 } };
        var gtClasses = new int[] { 0 };

        // No predictions
        var predBoxes = new float[0, 4];
        var predScores = Array.Empty<float>();
        var predClasses = Array.Empty<int>();

        metric.Update(predBoxes, predScores, predClasses, gtBoxes, gtClasses);

        var (map50, map5095, _) = metric.Compute();
        Assert.Equal(0.0, map50);
    }

    [Fact]
    public void MAPMetric_Reset_ClearsState()
    {
        var metric = new MAPMetric(numClasses: 2);

        var gtBoxes = new float[,] { { 10, 10, 50, 50 } };
        var gtClasses = new int[] { 0 };
        var predBoxes = new float[,] { { 10, 10, 50, 50 } };
        var predScores = new float[] { 0.9f };
        var predClasses = new int[] { 0 };

        metric.Update(predBoxes, predScores, predClasses, gtBoxes, gtClasses);
        metric.Reset();

        var (map50, _, _) = metric.Compute();
        Assert.Equal(0.0, map50);
    }
}
