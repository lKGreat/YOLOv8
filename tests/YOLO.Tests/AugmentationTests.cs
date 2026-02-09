using YOLO.Data.Augmentation;
using YOLO.Data.Utils;

namespace YOLO.Tests;

/// <summary>
/// Tests for data augmentation pipeline.
/// </summary>
public class AugmentationTests
{
    [Fact]
    public void LetterBox_OutputSize_Correct()
    {
        var letterBox = new LetterBox(targetSize: 640, scaleUp: true, center: true);

        // Create a dummy 800x600 image
        int w = 800, h = 600;
        var data = new byte[w * h * 3];
        Array.Fill<byte>(data, 128);

        var (padded, newW, newH, padX, padY, ratio) = letterBox.Apply(data, w, h);

        Assert.Equal(640, newW);
        Assert.Equal(640, newH);
        Assert.True(ratio <= 1.0f);
        Assert.Equal(640 * 640 * 3, padded.Length);
    }

    [Fact]
    public void LetterBox_ScaleDown_Only()
    {
        var letterBox = new LetterBox(targetSize: 640, scaleUp: false, center: true);

        // Small image should not be scaled up
        int w = 320, h = 240;
        var data = new byte[w * h * 3];

        var (_, newW, newH, _, _, ratio) = letterBox.Apply(data, w, h);

        Assert.Equal(640, newW);
        Assert.Equal(640, newH);
        Assert.True(ratio <= 1.0f);
    }

    [Fact]
    public void RandomFlip_Horizontal_FlipsLabels()
    {
        var flip = new RandomFlip(horizontalProb: 1.0, verticalProb: 0.0, seed: 42);

        int w = 100, h = 100;
        var data = new byte[w * h * 3];

        var labels = new List<BboxInstance>
        {
            new() { ClassId = 0, Bbox = [0.25f, 0.5f, 0.2f, 0.3f] } // center at (0.25, 0.5)
        };

        flip.Apply(data, w, h, labels);

        // After horizontal flip: cx should be 1.0 - 0.25 = 0.75
        Assert.InRange(labels[0].Bbox[0], 0.74f, 0.76f);
        Assert.Equal(0.5f, labels[0].Bbox[1]); // cy unchanged
    }

    [Fact]
    public void RandomHSV_DoesNotCrash()
    {
        var hsv = new RandomHSV(0.015f, 0.7f, 0.4f, seed: 42);

        int w = 64, h = 64;
        var data = new byte[w * h * 3];
        new Random(42).NextBytes(data);

        // Should not throw
        hsv.Apply(data, w, h);

        // All values should still be valid bytes
        foreach (var b in data)
        {
            Assert.InRange(b, (byte)0, (byte)255);
        }
    }

    [Fact]
    public void Mosaic_OutputSize_DoubleImgSize()
    {
        var mosaic = new Mosaic(imgSize: 320, probability: 1.0, seed: 42);

        // Create 4 dummy images of varying sizes
        var images = new (byte[] data, int w, int h)[4];
        var labels = new List<BboxInstance>[4];

        for (int i = 0; i < 4; i++)
        {
            int iw = 200 + i * 50;
            int ih = 150 + i * 30;
            images[i] = (new byte[iw * ih * 3], iw, ih);
            Array.Fill<byte>(images[i].data, (byte)(i * 60 + 50));
            labels[i] = new List<BboxInstance>
            {
                new() { ClassId = i, Bbox = [0.5f, 0.5f, 0.3f, 0.3f] }
            };
        }

        var (data, mW, mH, mergedLabels) = mosaic.Apply(images, labels);

        // Mosaic output should be 2x the img size
        Assert.Equal(640, mW);
        Assert.Equal(640, mH);
        Assert.True(mergedLabels.Count >= 1); // At least some labels survived
    }

    [Fact]
    public void AugmentationPipeline_Val_OnlyLetterbox()
    {
        var pipeline = AugmentationPipeline.CreateValPipeline(640);

        int w = 800, h = 600;
        var data = new byte[w * h * 3];
        var labels = new List<BboxInstance>
        {
            new() { ClassId = 0, Bbox = [0.5f, 0.5f, 0.2f, 0.3f] }
        };

        var (result, rW, rH, rLabels) = pipeline.Apply(data, w, h, labels, 640);

        Assert.Equal(640, rW);
        Assert.Equal(640, rH);
        Assert.Single(rLabels);
    }

    [Fact]
    public void BboxInstance_Clip_ClampsToImage()
    {
        var inst = new BboxInstance
        {
            ClassId = 0,
            Bbox = [-10, -5, 50, 30] // Partially outside image
        };

        inst.Clip(100, 100);

        // After clipping, bbox should be inside [0, 100]
        var xyxy = inst.BboxXyxy;
        Assert.True(xyxy[0] >= 0);
        Assert.True(xyxy[1] >= 0);
        Assert.True(xyxy[2] <= 100);
        Assert.True(xyxy[3] <= 100);
    }

    [Fact]
    public void LabelParser_ImageToLabelPath_Correct()
    {
        string imgPath = @"C:\data\images\train\img001.jpg";
        string expected = @"C:\data\labels\train\img001.txt";
        Assert.Equal(expected, LabelParser.ImageToLabelPath(imgPath));

        string imgPath2 = "data/images/val/test.png";
        string expected2 = "data/labels/val/test.txt";
        Assert.Equal(expected2, LabelParser.ImageToLabelPath(imgPath2));
    }
}
