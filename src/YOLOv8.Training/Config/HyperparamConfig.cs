using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace YOLOv8.Training.Config;

/// <summary>
/// YOLOv8 hyperparameter configuration matching ultralytics default.yaml.
/// All fields use snake_case YAML aliases to match the Python original.
/// </summary>
public class HyperparamConfig
{
    // --- Training ---
    [YamlMember(Alias = "epochs")]
    public int Epochs { get; set; } = 100;

    [YamlMember(Alias = "patience")]
    public int Patience { get; set; } = 100;

    [YamlMember(Alias = "batch")]
    public int Batch { get; set; } = 16;

    [YamlMember(Alias = "imgsz")]
    public int ImgSz { get; set; } = 640;

    [YamlMember(Alias = "save")]
    public bool Save { get; set; } = true;

    [YamlMember(Alias = "save_period")]
    public int SavePeriod { get; set; } = -1;

    [YamlMember(Alias = "workers")]
    public int Workers { get; set; } = 8;

    [YamlMember(Alias = "device")]
    public string? Device { get; set; }

    [YamlMember(Alias = "exist_ok")]
    public bool ExistOk { get; set; } = false;

    [YamlMember(Alias = "pretrained")]
    public bool Pretrained { get; set; } = true;

    // --- Optimizer ---
    [YamlMember(Alias = "optimizer")]
    public string Optimizer { get; set; } = "auto";

    [YamlMember(Alias = "lr0")]
    public double Lr0 { get; set; } = 0.01;

    [YamlMember(Alias = "lrf")]
    public double Lrf { get; set; } = 0.01;

    [YamlMember(Alias = "momentum")]
    public double Momentum { get; set; } = 0.937;

    [YamlMember(Alias = "weight_decay")]
    public double WeightDecay { get; set; } = 0.0005;

    [YamlMember(Alias = "warmup_epochs")]
    public double WarmupEpochs { get; set; } = 3.0;

    [YamlMember(Alias = "warmup_momentum")]
    public double WarmupMomentum { get; set; } = 0.8;

    [YamlMember(Alias = "warmup_bias_lr")]
    public double WarmupBiasLr { get; set; } = 0.1;

    [YamlMember(Alias = "cos_lr")]
    public bool CosLr { get; set; } = false;

    [YamlMember(Alias = "close_mosaic")]
    public int CloseMosaic { get; set; } = 10;

    [YamlMember(Alias = "nbs")]
    public int Nbs { get; set; } = 64;

    // --- Loss gains ---
    [YamlMember(Alias = "box")]
    public double Box { get; set; } = 7.5;

    [YamlMember(Alias = "cls")]
    public double Cls { get; set; } = 0.5;

    [YamlMember(Alias = "dfl")]
    public double Dfl { get; set; } = 1.5;

    // --- Augmentation ---
    [YamlMember(Alias = "hsv_h")]
    public float HsvH { get; set; } = 0.015f;

    [YamlMember(Alias = "hsv_s")]
    public float HsvS { get; set; } = 0.7f;

    [YamlMember(Alias = "hsv_v")]
    public float HsvV { get; set; } = 0.4f;

    [YamlMember(Alias = "degrees")]
    public float Degrees { get; set; } = 0.0f;

    [YamlMember(Alias = "translate")]
    public float Translate { get; set; } = 0.1f;

    [YamlMember(Alias = "scale")]
    public float Scale { get; set; } = 0.5f;

    [YamlMember(Alias = "shear")]
    public float Shear { get; set; } = 0.0f;

    [YamlMember(Alias = "perspective")]
    public float Perspective { get; set; } = 0.0f;

    [YamlMember(Alias = "flipud")]
    public float FlipUD { get; set; } = 0.0f;

    [YamlMember(Alias = "fliplr")]
    public float FlipLR { get; set; } = 0.5f;

    [YamlMember(Alias = "bgr")]
    public float Bgr { get; set; } = 0.0f;

    [YamlMember(Alias = "mosaic")]
    public float Mosaic { get; set; } = 1.0f;

    [YamlMember(Alias = "mixup")]
    public float MixUp { get; set; } = 0.0f;

    [YamlMember(Alias = "copy_paste")]
    public float CopyPaste { get; set; } = 0.0f;

    [YamlMember(Alias = "copy_paste_mode")]
    public string CopyPasteMode { get; set; } = "flip";

    [YamlMember(Alias = "auto_augment")]
    public string AutoAugment { get; set; } = "randaugment";

    [YamlMember(Alias = "erasing")]
    public float Erasing { get; set; } = 0.4f;

    [YamlMember(Alias = "crop_fraction")]
    public float CropFraction { get; set; } = 1.0f;

    // --- Inference ---
    [YamlMember(Alias = "conf")]
    public double Conf { get; set; } = 0.25;

    [YamlMember(Alias = "iou")]
    public double Iou { get; set; } = 0.7;

    [YamlMember(Alias = "max_det")]
    public int MaxDet { get; set; } = 300;

    [YamlMember(Alias = "agnostic_nms")]
    public bool AgnosticNms { get; set; } = false;

    // --- Reproducibility ---
    [YamlMember(Alias = "seed")]
    public int Seed { get; set; } = 0;

    // --- Distillation ---
    [YamlMember(Alias = "distill_weight")]
    public double DistillWeight { get; set; } = 1.0;

    [YamlMember(Alias = "distill_temperature")]
    public double DistillTemperature { get; set; } = 20.0;

    [YamlMember(Alias = "distill_mode")]
    public string DistillMode { get; set; } = "logit";

    /// <summary>
    /// Load hyperparameters from a YAML file.
    /// </summary>
    public static HyperparamConfig Load(string yamlPath)
    {
        var yaml = File.ReadAllText(yamlPath);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        return deserializer.Deserialize<HyperparamConfig>(yaml);
    }

    /// <summary>
    /// Convert to TrainConfig record used by the Trainer.
    /// </summary>
    public TrainConfig ToTrainConfig(int numClasses, string modelVariant = "n", string saveDir = "runs/train")
    {
        return new TrainConfig
        {
            Epochs = Epochs,
            BatchSize = Batch,
            ImgSize = ImgSz,
            NumClasses = numClasses,
            ModelVariant = modelVariant,
            Optimizer = Optimizer,
            Lr0 = Lr0,
            Lrf = Lrf,
            Momentum = Momentum,
            WeightDecay = WeightDecay,
            WarmupEpochs = WarmupEpochs,
            WarmupBiasLr = WarmupBiasLr,
            WarmupMomentum = WarmupMomentum,
            CosLR = CosLr,
            CloseMosaic = CloseMosaic,
            Patience = Patience,
            Nbs = Nbs,
            BoxGain = Box,
            ClsGain = Cls,
            DflGain = Dfl,
            MosaicProb = Mosaic,
            MixUpProb = MixUp,
            HsvH = HsvH,
            HsvS = HsvS,
            HsvV = HsvV,
            FlipLR = FlipLR,
            FlipUD = FlipUD,
            Scale = Scale,
            Translate = Translate,
            SaveDir = saveDir,
            Seed = Seed,
            DistillWeight = DistillWeight,
            DistillTemperature = DistillTemperature,
            DistillMode = DistillMode
        };
    }
}
