using YOLO.Data.Utils;

namespace YOLO.Data.Augmentation;

/// <summary>
/// MixUp augmentation: blends two images and concatenates their labels.
/// Mix ratio is drawn from Beta(32, 32) distribution.
/// Default probability: 0.0 (disabled by default).
/// </summary>
public class MixUp
{
    private readonly double probability;
    private readonly Random rng;

    public MixUp(double probability = 0.0, int? seed = null)
    {
        this.probability = probability;
        rng = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <summary>
    /// Apply mixup to two images.
    /// </summary>
    public (byte[] data, List<BboxInstance> labels) Apply(
        byte[] img1, byte[] img2, int w, int h,
        List<BboxInstance> labels1, List<BboxInstance> labels2)
    {
        if (rng.NextDouble() > probability)
            return (img1, labels1);

        // Beta(32, 32) distribution approximation
        // Simple approximation: ratio close to 0.5
        double r = SampleBeta(32.0, 32.0);

        var result = new byte[img1.Length];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = (byte)Math.Clamp((int)(img1[i] * r + img2[i] * (1 - r)), 0, 255);
        }

        // Concatenate labels
        var mergedLabels = new List<BboxInstance>(labels1);
        mergedLabels.AddRange(labels2);

        return (result, mergedLabels);
    }

    /// <summary>
    /// Sample from Beta distribution using the Jöhnk algorithm (approximation).
    /// </summary>
    private double SampleBeta(double alpha, double beta)
    {
        // For large alpha=beta, result is close to 0.5
        // Use gamma function approximation
        double x = SampleGamma(alpha);
        double y = SampleGamma(beta);
        return x / (x + y);
    }

    private double SampleGamma(double shape)
    {
        // Marsaglia and Tsang's method for shape >= 1
        if (shape < 1)
            return SampleGamma(shape + 1) * Math.Pow(rng.NextDouble(), 1.0 / shape);

        double d = shape - 1.0 / 3.0;
        double c = 1.0 / Math.Sqrt(9.0 * d);

        while (true)
        {
            double x, v;
            do
            {
                x = NextGaussian();
                v = 1.0 + c * x;
            } while (v <= 0);

            v = v * v * v;
            double u = rng.NextDouble();

            if (u < 1 - 0.0331 * (x * x) * (x * x))
                return d * v;

            if (Math.Log(u) < 0.5 * x * x + d * (1 - v + Math.Log(v)))
                return d * v;
        }
    }

    private double NextGaussian()
    {
        double u1 = 1.0 - rng.NextDouble();
        double u2 = 1.0 - rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }
}
