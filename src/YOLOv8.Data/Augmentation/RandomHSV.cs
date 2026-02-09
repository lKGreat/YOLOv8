namespace YOLOv8.Data.Augmentation;

/// <summary>
/// Random HSV augmentation.
/// Randomly adjusts hue, saturation, and value of the image.
/// Default gains: hue=0.015, saturation=0.7, value=0.4
/// </summary>
public class RandomHSV
{
    private readonly float hGain;
    private readonly float sGain;
    private readonly float vGain;
    private readonly Random rng;

    public RandomHSV(float hGain = 0.015f, float sGain = 0.7f, float vGain = 0.4f, int? seed = null)
    {
        this.hGain = hGain;
        this.sGain = sGain;
        this.vGain = vGain;
        rng = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <summary>
    /// Apply random HSV augmentation in-place.
    /// </summary>
    /// <param name="data">Image byte array (HWC RGB)</param>
    /// <param name="w">Image width</param>
    /// <param name="h">Image height</param>
    public void Apply(byte[] data, int w, int h)
    {
        float rH = (float)(rng.NextDouble() * 2 - 1) * hGain;
        float rS = (float)(rng.NextDouble() * 2 - 1) * sGain + 1.0f;
        float rV = (float)(rng.NextDouble() * 2 - 1) * vGain + 1.0f;

        // Build lookup tables for S and V
        var lutS = new byte[256];
        var lutV = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            lutS[i] = (byte)Math.Clamp((int)(i * rS), 0, 255);
            lutV[i] = (byte)Math.Clamp((int)(i * rV), 0, 255);
        }

        for (int i = 0; i < w * h; i++)
        {
            int idx = i * 3;
            byte r = data[idx], g = data[idx + 1], b = data[idx + 2];

            // RGB to HSV (simplified)
            RgbToHsv(r, g, b, out float hue, out float sat, out float val);

            // Apply gains
            hue = (hue + rH * 360.0f) % 360.0f;
            if (hue < 0) hue += 360.0f;
            sat = Math.Clamp(sat * rS, 0, 1);
            val = Math.Clamp(val * rV, 0, 1);

            // HSV to RGB
            HsvToRgb(hue, sat, val, out byte rOut, out byte gOut, out byte bOut);

            data[idx] = rOut;
            data[idx + 1] = gOut;
            data[idx + 2] = bOut;
        }
    }

    private static void RgbToHsv(byte r, byte g, byte b, out float h, out float s, out float v)
    {
        float rf = r / 255f, gf = g / 255f, bf = b / 255f;
        float max = Math.Max(rf, Math.Max(gf, bf));
        float min = Math.Min(rf, Math.Min(gf, bf));
        float delta = max - min;

        v = max;
        s = max == 0 ? 0 : delta / max;

        if (delta == 0)
            h = 0;
        else if (max == rf)
            h = 60 * (((gf - bf) / delta) % 6);
        else if (max == gf)
            h = 60 * ((bf - rf) / delta + 2);
        else
            h = 60 * ((rf - gf) / delta + 4);

        if (h < 0) h += 360;
    }

    private static void HsvToRgb(float h, float s, float v, out byte r, out byte g, out byte b)
    {
        float c = v * s;
        float x = c * (1 - Math.Abs((h / 60) % 2 - 1));
        float m = v - c;

        float rf, gf, bf;
        if (h < 60) { rf = c; gf = x; bf = 0; }
        else if (h < 120) { rf = x; gf = c; bf = 0; }
        else if (h < 180) { rf = 0; gf = c; bf = x; }
        else if (h < 240) { rf = 0; gf = x; bf = c; }
        else if (h < 300) { rf = x; gf = 0; bf = c; }
        else { rf = c; gf = 0; bf = x; }

        r = (byte)Math.Clamp((int)((rf + m) * 255), 0, 255);
        g = (byte)Math.Clamp((int)((gf + m) * 255), 0, 255);
        b = (byte)Math.Clamp((int)((bf + m) * 255), 0, 255);
    }
}
