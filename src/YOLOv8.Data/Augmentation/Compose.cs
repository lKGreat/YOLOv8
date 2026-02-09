using YOLOv8.Data.Utils;

namespace YOLOv8.Data.Augmentation;

/// <summary>
/// Composes multiple augmentation transforms into a pipeline.
/// Applies transforms sequentially in order.
/// </summary>
public class AugmentationPipeline
{
    private readonly LetterBox letterBox;
    private readonly Mosaic? mosaic;
    private readonly RandomPerspective? perspective;
    private readonly RandomHSV? hsv;
    private readonly RandomFlip? flip;
    private readonly MixUp? mixUp;
    private readonly bool isTrain;

    /// <summary>
    /// Create the training augmentation pipeline matching YOLOv8 defaults.
    /// </summary>
    public static AugmentationPipeline CreateTrainPipeline(
        int imgSize = 640,
        float mosaicProb = 1.0f,
        float mixUpProb = 0.0f,
        float hsvH = 0.015f, float hsvS = 0.7f, float hsvV = 0.4f,
        float flipLR = 0.5f, float flipUD = 0.0f,
        float scale = 0.5f, float translate = 0.1f)
    {
        return new AugmentationPipeline(
            letterBox: new LetterBox(imgSize, scaleUp: true, center: true),
            mosaic: mosaicProb > 0 ? new Mosaic(imgSize, mosaicProb) : null,
            perspective: new RandomPerspective(scale: scale, translate: translate),
            hsv: new RandomHSV(hsvH, hsvS, hsvV),
            flip: new RandomFlip(flipLR, flipUD),
            mixUp: mixUpProb > 0 ? new MixUp(mixUpProb) : null,
            isTrain: true);
    }

    /// <summary>
    /// Create the validation pipeline (letterbox only, no augmentation).
    /// </summary>
    public static AugmentationPipeline CreateValPipeline(int imgSize = 640)
    {
        return new AugmentationPipeline(
            letterBox: new LetterBox(imgSize, scaleUp: false, center: true),
            isTrain: false);
    }

    private AugmentationPipeline(LetterBox letterBox, Mosaic? mosaic = null,
        RandomPerspective? perspective = null, RandomHSV? hsv = null,
        RandomFlip? flip = null, MixUp? mixUp = null, bool isTrain = false)
    {
        this.letterBox = letterBox;
        this.mosaic = mosaic;
        this.perspective = perspective;
        this.hsv = hsv;
        this.flip = flip;
        this.mixUp = mixUp;
        this.isTrain = isTrain;
    }

    /// <summary>
    /// Apply the augmentation pipeline to a single image.
    /// For mosaic, call ApplyMosaic instead.
    /// </summary>
    public (byte[] data, int w, int h, List<BboxInstance> labels) Apply(
        byte[] imgData, int w, int h, List<BboxInstance> labels, int targetSize)
    {
        byte[] data = imgData;
        int curW = w, curH = h;
        var curLabels = labels;

        if (!isTrain)
        {
            // Validation: only letterbox
            float padX, padY, ratio;
            (data, curW, curH, padX, padY, ratio) = letterBox.Apply(data, curW, curH, curLabels);
            return (data, curW, curH, curLabels);
        }

        // Training pipeline
        // 1. Random perspective (scale + translate)
        if (perspective != null)
        {
            (data, curW, curH, curLabels) = perspective.Apply(data, curW, curH, curLabels, targetSize);
        }
        else
        {
            // Just letterbox
            (data, curW, curH, _, _, _) = letterBox.Apply(data, curW, curH, curLabels);
        }

        // 2. HSV augmentation
        hsv?.Apply(data, curW, curH);

        // 3. Random flip
        flip?.Apply(data, curW, curH, curLabels);

        return (data, curW, curH, curLabels);
    }

    /// <summary>
    /// Apply mosaic augmentation (call with 4 images).
    /// </summary>
    public (byte[] data, int w, int h, List<BboxInstance> labels) ApplyMosaic(
        (byte[] data, int w, int h)[] images,
        List<BboxInstance>[] labels,
        int targetSize)
    {
        if (mosaic == null)
        {
            return Apply(images[0].data, images[0].w, images[0].h, labels[0], targetSize);
        }

        // Apply mosaic
        var (mosaicData, mW, mH, mosaicLabels) = mosaic.Apply(images, labels);

        // Apply perspective to mosaic result
        if (perspective != null)
        {
            return perspective.Apply(mosaicData, mW, mH, mosaicLabels, targetSize);
        }

        return (mosaicData, mW, mH, mosaicLabels);
    }

    /// <summary>
    /// Disable mosaic (for close_mosaic epochs at end of training).
    /// </summary>
    public AugmentationPipeline WithoutMosaic()
    {
        return new AugmentationPipeline(letterBox, null, perspective, hsv, flip, null, isTrain);
    }
}
