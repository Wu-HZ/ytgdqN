using System;
using System.Drawing;
using System.Linq;
using WindowsFormsApplication2;

var presets = ThemeColorPresets.All;
Assert(presets.Count == 8 && presets.Select(p => p.Name).Distinct().Count() == presets.Count,
    "八套不同名称的配色");
Assert(presets.Select(p => p.Background.ToArgb()).Distinct().Count() == presets.Count,
    "背景色互不相同");
foreach (var preset in presets)
{
    Assert(Contrast(preset.Background, preset.Foreground) >= 4.5, $"{preset.Name} 主界面文字对比度不足");
    Assert(Contrast(preset.SecondaryBackground, preset.SecondaryForeground) >= 4.5,
        $"{preset.Name} 次级控件文字对比度不足");
    Assert(Contrast(preset.ReferenceBackground, preset.ReferenceForeground) >= 4.5,
        $"{preset.Name} 对照区文字对比度不足");
    Assert(Contrast(preset.TypingBackground, preset.TypingForeground) >= 4.5,
        $"{preset.Name} 跟打区文字对比度不足");
    Assert(Contrast(preset.CorrectBackground, preset.ReferenceForeground) >= 4.5 &&
        Contrast(preset.ErrorBackground, preset.ReferenceForeground) >= 4.5 &&
        Contrast(preset.SpeedMark, preset.ReferenceForeground) >= 4.5,
        $"{preset.Name} 对照区标记与文字对比度不足");
    Assert(preset.Background.A == 255 && preset.ReferenceBackground.A == 255 &&
        preset.TypingBackground.A == 255, $"{preset.Name} 底色不应透明");
}
Console.WriteLine($"Theme palette checks passed ({presets.Count} palettes)");

static void Assert(bool condition, string description)
{
    if (!condition) throw new Exception(description);
}
static double Contrast(Color a, Color b)
{
    static double Brightness(Color c)
    {
        static double Linear(byte x)
        {
            double v = x / 255.0;
            return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Linear(c.R) + 0.7152 * Linear(c.G) + 0.0722 * Linear(c.B);
    }
    double l1 = Brightness(a), l2 = Brightness(b);
    return (Math.Max(l1, l2) + 0.05) / (Math.Min(l1, l2) + 0.05);
}
