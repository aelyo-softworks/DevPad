using System;
using System.Windows.Media;

namespace DevPad.Utilities
{
    public struct Hsl : IEquatable<Hsl>
    {
        public float Hue;
        public float Saturation;
        public float Brightness;

        public Hsl(float hue, float saturation, float brightness)
        {
            Hue = hue;
            Saturation = saturation;
            Brightness = brightness;
        }

        public Hsl Complementary
        {
            get
            {
                var hue = Hue;
                hue += 0.5f;
                if (hue > 1f)
                {
                    hue -= 1f;
                }
                return new Hsl(hue, Saturation, Brightness);
            }
        }

        public Tuple<Hsl, Hsl> Triadic
        {
            get
            {
                var first = Hue;
                first += 1f / 3f;
                if (first > 1f)
                {
                    first -= 1f;
                }

                var second = Hue;
                second += 2f / 3f;
                if (second > 1f)
                {
                    second -= 1f;
                }
                return new Tuple<Hsl, Hsl>(new Hsl(first, Saturation, Brightness), new Hsl(second, Saturation, Brightness));
            }
        }

        public Color ToColor(float a = 1f)
        {
            float r;
            float g;
            float b;

            if (Brightness == 0f)
            {
                r = 0f;
                g = 0f;
                b = 0f;
            }
            else
            {
                if (Saturation == 0f)
                {
                    r = Brightness;
                    g = Brightness;
                    b = Brightness;
                }
                else
                {
                    float max;
                    if (Brightness < 0.5f)
                    {
                        max = Brightness * (1f + Saturation);
                    }
                    else
                    {
                        max = Brightness + Saturation - (Saturation * Brightness);
                    }

                    var min = 2f * Brightness - max;
                    var hue = Hue / 360f;
                    r = RgbFromHue(hue + 1f / 3f, min, max);
                    g = RgbFromHue(hue, min, max);
                    b = RgbFromHue(hue - 1f / 3f, min, max);
                }
            }
            return Color.FromScRgb(a, r, g, b);
        }

        private static float RgbFromHue(float hue, float min, float max)
        {
            if (hue < 0f)
            {
                hue += 1f;
            }
            else if (hue > 1f)
            {
                hue -= 1f;
            }

            float color;
            if (6f * hue < 1f)
            {
                color = min + (max - min) * hue * 6f;
            }
            else if (2f * hue < 1f)
            {
                color = max;
            }
            else if (3f * hue < 2f)
            {
                color = min + (max - min) * ((2f / 3f) - hue) * 6f;
            }
            else
            {
                color = min;
            }
            return color;
        }

        public override int GetHashCode() => Hue.GetHashCode() ^ Saturation.GetHashCode() ^ Brightness.GetHashCode();
        public override bool Equals(object other) => other is Hsl hsb && Equals(hsb);
        public bool Equals(Hsl other) => Brightness == other.Brightness && Hue == other.Hue && Saturation == other.Saturation;
        public override string ToString() => "H:" + Hue + " S:" + Saturation + " B:" + Brightness;

        public static Hsl From(System.Drawing.Color color) => new Hsl(color.GetHue() / 360, color.GetSaturation(), color.GetBrightness());
        public static Hsl From(Color color) => new Hsl(Hsv.GetHue(color.ScR, color.ScG, color.ScB), GetSaturation(color.ScR, color.ScG, color.ScB), GetBrightness(color.ScR, color.ScG, color.ScB));

        public static float GetBrightness(float r, float g, float b)
        {
            var max = r;
            var min = r;
            if (g > max) max = g;
            if (b > max) max = b;
            if (g < min) min = g;
            if (b < min) min = b;
            return (max + min) / 2;
        }

        public static float GetSaturation(float r, float g, float b)
        {
            var max = r;
            var min = r;
            var s = 0f;

            if (g > max) max = g;
            if (b > max) max = b;
            if (g < min) min = g;
            if (b < min) min = b;

            if (max != min)
            {
                var l = (max + min) / 2;
                if (l <= .5f)
                {
                    s = (max - min) / (max + min);
                }
                else
                {
                    s = (max - min) / (2 - max - min);
                }
            }
            return s;
        }

        public static bool operator ==(Hsl x, Hsl y) => x.Equals(y);
        public static bool operator !=(Hsl x, Hsl y) => !x.Equals(y);
    }
}
