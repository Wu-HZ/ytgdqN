using System;
using System.Collections.Generic;
using System.Drawing;

namespace WindowsFormsApplication2
{
    // Values follow the order of the color swatches in FormTheme; fonts are intentionally untouched.
    internal sealed record ThemeColorPreset(
        string Name, Color Background, Color Foreground, Color SecondaryBackground, Color SecondaryForeground,
        Color ReferenceBackground, Color ReferenceForeground, Color TypingBackground, Color TypingForeground,
        Color CorrectBackground, Color ErrorBackground, Color CorrectionMark, Color LongTimeMark,
        Color Word0, Color Word1, Color Word2, Color Word3, Color SpeedMark);

    internal static class ThemeColorPresets
    {
        internal static IReadOnlyList<ThemeColorPreset> All { get; } = new[]
        {
            Create("海盐青", new[] {
                "#DCEEEB", "#183B3C", "#A7CEC7", "#173C3D",
                "#F4FAF8", "#1D393A", "#EFF8F5", "#173C3D",
                "#B1E0CD", "#F5A8A3", "#FFE18C", "#F5CE82",
                "#175F7A", "#B33C49", "#7553A1", "#AE3F78", "#96C7CD" }),
            Create("暖纸杏", new[] {
                "#F1E5D5", "#44342D", "#DEC5A8", "#473226",
                "#FFF9EE", "#3E352D", "#FCF5E8", "#3E352D",
                "#B8D8B8", "#F0A199", "#E9CC78", "#E7C98A",
                "#205F80", "#AD4240", "#744D95", "#A2476F", "#C8D1B2" }),
            Create("夜航蓝", new[] {
                "#172437", "#E9F1FA", "#314660", "#F2F6FC",
                "#1F3045", "#E6EFFA", "#23364C", "#F1F5FB",
                "#365D57", "#82424C", "#676235", "#6B582E",
                "#8ACDE1", "#FFA9A6", "#C7AAF2", "#FFB3D9", "#465F7A" }),
            Create("雾紫", new[] {
                "#ECE6F3", "#392F4C", "#D3C4E1", "#372A49",
                "#F9F6FC", "#392F48", "#F5F0FA", "#392F48",
                "#B8DCCB", "#F2B2B5", "#F5D995", "#E9D093",
                "#355F95", "#AB3E5C", "#7650A3", "#AA4279", "#C8B6D7" }),
            Create("松林深绿", new[] {
                "#152B27", "#ECF4E7", "#31584C", "#FFFFFF",
                "#203C34", "#F1F7EB", "#284439", "#F1F7EB",
                "#406A4B", "#8A4242", "#706130", "#695523",
                "#95D6C5", "#FFA6A3", "#CFB2F0", "#F4ADCF", "#4B6756" }),
            Create("晨樱粉", new[] {
                "#F5E4EA", "#463040", "#E5BFCF", "#483040",
                "#FFF7FA", "#433541", "#FDF0F5", "#433541",
                "#B8DFC9", "#F4ACB8", "#EFD29A", "#E9C990",
                "#375C92", "#A33955", "#7951A0", "#AC4375", "#D3BEDB" }),
            Create("石墨银", new[] {
                "#34383C", "#F5F5F0", "#555C61", "#F5F5F0",
                "#292F33", "#F0F2F2", "#2F363A", "#F0F2F2",
                "#4D6B5C", "#8A4A4A", "#736229", "#68562B",
                "#9FCFE6", "#F6A6A6", "#CBB4E9", "#F0A9CB", "#505E65" }),
            Create("琥珀夜", new[] {
                "#302318", "#FFF0D9", "#624326", "#FFF4DD",
                "#3C2B1E", "#FBECDD", "#413020", "#FBECDD",
                "#56663D", "#854443", "#71612D", "#695329",
                "#A4D4E1", "#FFAC9E", "#D2B8F1", "#FFA8CB", "#63533E" })
        };

        private static ThemeColorPreset Create(string name, string[] hex)
        {
            if (hex.Length != 17) throw new ArgumentException("主题预设必须包含 17 个颜色。", nameof(hex));
            var colors = Array.ConvertAll(hex, ColorTranslator.FromHtml);
            return new ThemeColorPreset(name, colors[0], colors[1], colors[2], colors[3],
                colors[4], colors[5], colors[6], colors[7], colors[8], colors[9], colors[10],
                colors[11], colors[12], colors[13], colors[14], colors[15], colors[16]);
        }
    }
}
