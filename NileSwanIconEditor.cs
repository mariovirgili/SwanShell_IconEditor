// NileSwan Icon Editor - file icon sets (.ICN) for swanshell (nileswan menu)
// C# / WinForms, .NET Framework 4.x (built into Windows 10/11), no dependencies.
//
// .ICN (4832 bytes): "WSIC", version 1, 3 reserved bytes; 12 x 16-bit colors
// (0x0RGB) for the color indexes 4..15; tile data: 16color, 16color_rot
// (10 x 128 bytes), 8color, 8color_rot (10 x 32), 16mono, 16mono_rot (10 x 64),
// 8mono, 8mono_rot (10 x 16). 16x16 icons as tiles TL, BL, TR, BR.
// Color indexes: 2 = row background, 3 = ink (white on the selected row),
// 4..15 = the set's palette. Mono: 2 = background, 0 = light, 1 = dark, 3 = ink.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;

// ---------------------------------------------------------------- model

class IconSet
{
    public const int ICONS = 10, COLORS = 12, FILE_SIZE = 4832;
    public static readonly string[] Names = { "folder", "file", "rom", "image", "music", "bfb", "com", "fr-il", "zip", "fx" };
    public static readonly string[] Labels = { "0 folder", "1 file", "2 rom (.ws .wsc .pc2)", "3 image (.bmp)", "4 music (.wav .vgm .vgz)",
        "5 .bfb", "6 .com", "7 .fr .il", "8 .zip", "9 .fx" };
    // variants: 0 = 16 color, 1 = 16 mono, 2 = 8 color, 3 = 8 mono
    public static readonly string[] VariantNames = { "16color", "16mono", "8color", "8mono" };
    public static int Size(int v) { return v < 2 ? 16 : 8; }
    public static bool Mono(int v) { return (v & 1) == 1; }

    // pixel values. color: 0 = background, 1 = ink, 2..13 = palette 0..11
    //               mono:  0 = background, 1 = light, 2 = dark, 3 = ink
    public byte[][,] Pix = new byte[ICONS * 4][,];
    public int[] Palette = new int[COLORS]; // 0x0RGB

    public byte[,] Icon(int i, int v) { return Pix[i * 4 + v]; }

    public IconSet()
    {
        for (int i = 0; i < ICONS; i++)
            for (int v = 0; v < 4; v++)
                Pix[i * 4 + v] = new byte[Size(v), Size(v)];
    }

    public IconSet Clone()
    {
        var c = new IconSet();
        Array.Copy(Palette, c.Palette, COLORS);
        for (int k = 0; k < Pix.Length; k++) c.Pix[k] = (byte[,]) Pix[k].Clone();
        return c;
    }

    public static Color Rgb(int c) { return Color.FromArgb(((c >> 8) & 15) * 17, ((c >> 4) & 15) * 17, (c & 15) * 17); }
    public static readonly Color[] MonoColors = { Color.White, Color.FromArgb(170, 170, 170), Color.FromArgb(85, 85, 85), Color.Black };

    // screen color of a pixel value; selected = as on the selected list row
    public Color ColorOf(int v, int value, bool selected)
    {
        if (Mono(v))
        {
            if (selected && (value == 0 || value == 3)) return value == 0 ? Color.Black : Color.White;
            return MonoColors[value & 3];
        }
        if (value == 0) return selected ? Color.Black : Color.White;
        if (value == 1) return selected ? Color.White : Color.Black;
        return Rgb(Palette[(value - 2) % COLORS]);
    }

    // ---- .ICN encoding

    static readonly int[,] Order16 = { { 0, 0 }, { 0, 8 }, { 8, 0 }, { 8, 8 } };

    static byte[,] Rotate(byte[,] p)
    {
        int n = p.GetLength(0);
        var r = new byte[n, n];
        // same as tools/icon_rotate.lua: (x, y) -> (n-1-y, x)
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                r[x, n - 1 - y] = p[y, x];
        return r;
    }

    static int HwIndex(int v, int value)
    {
        if (Mono(v)) { int[] m = { 2, 0, 1, 3 }; return m[value & 3]; }
        return value == 0 ? 2 : value == 1 ? 3 : value + 2;
    }

    static int FromHw(int v, int hw)
    {
        if (Mono(v)) { int[] m = { 1, 2, 0, 3 }; return m[hw & 3]; }
        return hw == 2 || hw == 0 ? 0 : hw <= 3 ? 1 : hw - 2;
    }

    void WriteStrip(List<byte> o, int v, bool rot)
    {
        int n = Size(v), bpp = Mono(v) ? 2 : 4;
        for (int i = 0; i < ICONS; i++)
        {
            var p = Icon(i, v);
            if (rot) p = Rotate(p);
            int tiles = n == 16 ? 4 : 1;
            for (int t = 0; t < tiles; t++)
            {
                int tx = n == 16 ? Order16[t, 0] : 0, ty = n == 16 ? Order16[t, 1] : 0;
                for (int y = 0; y < 8; y++)
                    for (int plane = 0; plane < bpp; plane++)
                    {
                        int b = 0;
                        for (int x = 0; x < 8; x++)
                            b |= ((HwIndex(v, p[ty + y, tx + x]) >> plane) & 1) << (7 - x);
                        o.Add((byte) b);
                    }
            }
        }
    }

    public byte[] ToIcn()
    {
        var o = new List<byte> { (byte) 'W', (byte) 'S', (byte) 'I', (byte) 'C', 1, 0, 0, 0 };
        foreach (int c in Palette) { o.Add((byte) (c & 0xFF)); o.Add((byte) ((c >> 8) & 0x0F)); }
        foreach (int v in new[] { 0, 2, 1, 3 }) { WriteStrip(o, v, false); WriteStrip(o, v, true); }
        if (o.Count != FILE_SIZE) throw new Exception("internal error: size " + o.Count);
        return o.ToArray();
    }

    public static IconSet FromIcn(byte[] d)
    {
        if (d.Length != FILE_SIZE || d[0] != 'W' || d[1] != 'S' || d[2] != 'I' || d[3] != 'C')
            throw new Exception("not a valid .ICN icon set (4832 bytes, WSIC)");
        var s = new IconSet();
        for (int i = 0; i < COLORS; i++) s.Palette[i] = d[8 + i * 2] | ((d[9 + i * 2] & 15) << 8);
        int off = 32;
        foreach (int v in new[] { 0, 2, 1, 3 })
        {
            int n = Size(v), bpp = Mono(v) ? 2 : 4, tiles = n == 16 ? 4 : 1;
            for (int i = 0; i < ICONS; i++)
            {
                var p = s.Icon(i, v);
                for (int t = 0; t < tiles; t++)
                {
                    int tx = n == 16 ? Order16[t, 0] : 0, ty = n == 16 ? Order16[t, 1] : 0;
                    for (int y = 0; y < 8; y++)
                        for (int x = 0; x < 8; x++)
                        {
                            int hw = 0;
                            for (int plane = 0; plane < bpp; plane++)
                                hw |= ((d[off + y * bpp + plane] >> (7 - x)) & 1) << plane;
                            p[ty + y, tx + x] = (byte) FromHw(v, hw);
                        }
                    off += 8 * bpp;
                }
            }
            off += ICONS * (n * n * bpp / 8); // skip the rotated strip
        }
        return s;
    }

    public static IconSet BuiltIn()
    {
        return FromIcn(Convert.FromBase64String(BUILTIN));
    }

    const string BUILTIN =
        "V1NJQwEAAADzD6oKLwLWAd8FkQ8AAAAAAAAAAAAAAAAA/wAAAP8AAAD/AAAA/wAAH/8AAB/wAA8f/wAAEPAPABDwDwAQ8A8AEPAP" +
        "ABDwDwAf/wAAAP8AAAD/AAAA/wAAAP8AAAD/AAAA/wAAAP8AAAD/AAD4/wAACA/wAAgP8AAID/AACA/wAAgP8AAID/AA+P8AAAD/" +
        "AAAA/wAAAP8AAAD/AAAA/wAAAP8AAAD/AAAP8A8ACPcIAA/wDwAJ9gkAD/APAAr1CgAP8A8ACPcIAA/wDwAA/wAAAP8AAAD/AAAA" +
        "/wAAAP8AAAD/AAAA/wAA4B/gADDPMADwD/AAEO8QAPAP8AAQ7xAA8A/wAJBvkADwD/AAAP8AAAD/AAAA/wAAAP8AAAD/AAAA/wAA" +
        "AP8AAAD/AAAA/wAAAP8AAAP/AAAP/wAAH/8AAAzuEQAA6hUAAPsEAAD/AAAA/wAAAP8AAAD/AAAA/wAAAP8AAAD/AAAA/wAAMP8A" +
        "APj/AADw9wgAwOcYAACvUAAAv0AAAP8AAAD/AAAA/wAAAP8AAAD/AAAA/wAAAP8AAAD/AAAA/wAAAOAaBQDgEA8A4AQbAOAQDwDg" +
        "AB8H5wcYH/8fAB//HwAf/x8AAP8AAAD/AAAA/wAAAP8AAAD/AAAA/wAAAP8AAAAHAPgABwD4AAcA+AAHAPh4f3iA+P/4APj/+AD4" +
        "//gA+P/4AAD/AAAA/wAAAP8AAAD/AAAA/wAAAP8AAAD/AAAA/wAAAP8AAAH/AAAP/wAAD/8AAA//AAAB/wAAAP8AAAD/AAAA/wAA" +
        "AP8AAAD/AAAA/wAAAP8AAAD/AAAA/wAAAP8AAJD/AACg/wAAgP8AALD/AACA/wAAoP8AAJD/AAAA/wAAAP8AAAD/AAAA/wAAAP8A" +
        "AAD/AAAA/wAAAP8AAAP/AAAD/x0AD/McAAD/EAAA/xAAAP8QAAD/EAAA/xAAAP8fAAD/AAAA/wAAAP8AAAD/AAAA/wAAAP8AAGD/" +
        "AADw/yAA+P/wAPD/IABg/wAAAP8gAAD/IAAA/yAAAP8gAAD/4AAA/wAAAP8AAAD/AAAA/wAAAP8AAAD/AAAA/wAAAP8PAAf4DwAA" +
        "/wgAAP8IAAD/CAAA/wgAAP8IAAD/DwAA/wAAAP8AAAD/AAAA/wAAAP8AAAD/AAAA/wAAAP8AAAD/8ADgP9AAAP8QAAD/EAAA/xAA" +
        "AP8QAAD/EAAA//AAAP8AAAD/AAAA/wAAAP8AAAD/AAAA/wAAAP8AAAD/AAAA/wAAAP8AAAL9AgAB/gEAAf4BAAL9AgAA/wAAAP8A" +
        "AAD/AAAA/wAAAP8AAAD/AAAA/wAAAP8AAAD/AAAA/wAAAP8AAIB/gACgX6AAwD/AAMA/wACgX6AAgH+AAAD/AAAA/wAAAP8AAAD/" +
        "AAAA/wAAAP8AAAD/AAAA/wAAAP8AAB//AAAf8AAPH/8AABDwDwAQ8A8AEPAPABDwDwAQ8A8AH/8AAAD/AAAA/wAAAP8AAAD/AAAA" +
        "/wAAAP8AAAD/AAAA/wAA+P8AACgv0ABIT7AAKC/QAEhPsAAoL9AASE+wAPj/AAAA/wAAAP8AAAD/AAAA/wAAAP8AAAD/AAAA/wAA" +
        "AP8AAA7/AAAH/wAAA/8AAAP/AAAD/wAAD/8AAAD/AAAA/wAAAP8AAAD/AAAA/wAAAP8AAAD/AAAA/wAAAP8AAAD/AAAA/wAAAP8A" +
        "AID/AADA/wAAwN8gAPD/AAAA/wAAAP8AAAD/AAAA/wAAAP8AAAD/AAAA/wAAAP8AAB//AAAQ8A8AEPAPABDwDwAQ8A8AEPAPABDw" +
        "DwAQ8A8AEPAPAB//AAAA/wAAAP8AAAD/AAAA/wAAAP8AAAD/AADw/wAAcF+AIHBfgCBwX4AgcF+AICA/wAAgP8AAID/AACA/wADg" +
        "/wAAAP8AAAD/AAAA/wAAAP8AAAD/AAAA/wAAAP8AAB/gHwAV6hUAF+gXABXqFQAd4h0AFeoVABXqFQAf4B8AAP8AAAD/AAAA/wAA" +
        "AP8AAAD/AAAA/wAAAP8AAAD/AADwD/AAUK9QAFCvUADQL9AAUK9QAFCvUABwj3AA4B/gAAD/AAAA/wAAAP8AAAD/AAAA/wAAAP8A" +
        "AAD/AAAC8wwAB/8AAAfnGAAD/wAAA/MMAAH/AAAB+QYAAP8AAAD8AwAA/gEAAP8AAAD/AAAA/wAAAP8AAAD/AAAA/wAAAP8AAAD/" +
        "AAAA/wAAgP8AAID/AADA/wAAwP8AAOD/AADg/wAAQH+AAAD/AAAA/wAAAP8AAAD/AAAA/wAAAP8AABz8HAMc/BwDHv4eAR7+HgEe" +
        "/h4BHv4eAR//HwAf/x8AH/8fAB//HwAA/wAAAP8AAAD/AAAA/wAAAP8AAAD/AAAAD7BAAA8Q4AAPQLAADxDgAA8A8AAPAPAADwDw" +
        "AA8A8AAPAPAADwDwAP8AAAD/AAAA/wAAAP8AAAD/AAAA/wAAAP8AAAP/AAAD/wAAA/8AAAf/AAAP/wAAAP8AAAX/AAAJ/wAAAP8A" +
        "AAD/AAAA/wAAAP8AAAD/AAAA/wAAAP8AAAD/AACA/wAAgP8AAID/AADA/wAA4P8AAAD/AABA/wAAIP8AAAD/AAAA/wAAAP8AAAD/" +
        "AAAA/wAAAP8AAAD/AAAA/x8AAP8QAAD/EAAA/xAAAP8QAAD/EAAA/xAAAP8fAAD/AAAA/wAAAP8AAAD/AAAA/wAAAP8AAAD/AAAA" +
        "/wAAAP/gAEC/YABAv2AAcP8AAHD/IABw/yAA+P8gAPj/cABw/yAAIP8AAAD/AAAA/wAAAP8AAAD/AAAA/wAAAP8AAAD/AAAA/w8A" +
        "AP8IAAD/CAAA/wgAAP8IAAD/CAAA/wgAAP8PAAD/AAAA/wAAAP8AAAD/AAAA/wAAAP8AAAD/AAAA/wAAAP/wACDfMAAg3zAAIN8w" +
        "ACDfMAAg3zAAIP8QAAD/8AAA/wAAAP8AAAD/AAAA/wAAAP8AAAD/AAAA/wAAAP8AAAD/AAAA/wAAAv0CAAH+AQAH+AcAAf4BAAL9" +
        "AgAA/wAAAP8AAAD/AAAA/wAAAP8AAAD/AAAA/wAAAP8AAAD/AAAA/wAAAP8AAEC/QACAf4AA4B/gAIB/gABAv0AAAP8AAAD/AAAA" +
        "/wAAAP8AAAD/AAAA/wAAAP8AAAD/AAAf/wAAEPAPABDwDwAQ8A8AEPAPABDwDwAa+gUAFfUKABDwDwAf/wAAAP8AAAD/AAAA/wAA" +
        "AP8AAAD/AAAA/wAA8P8AAHBfgCBwX4AgcF+AIHBfgCAgP8AAoL9AAGB/gAAgP8AA4P8AAAD/AAAA/wAAAP8AAAD/AAAA/wAAAP8A" +
        "AAD/AAAE/wAABP8AAAf/AAAH/wAAB/8AAAf/AAAE/QIABP8AAAD/AAAA/wAAAP8AAAD/AAAA/wAAAP8AAAD/AAAA/wAAIP8AAGD/" +
        "AADg/wAAwP8AAID/AAAA/wAAAP8AAAD/AAAA/wAAAP8AAAD/AAAA/wAA8P8AAP+PAHDx8Q4AgYF+AIGBfgCBgX4A//8AAAD/AAB8" +
        "g3wARrlGAH6BfgBKtUoAfoF+AFKtUgB+gX4AAP8AAA7/AAA//wAA/v8AAPj8AwBg8g0AAEu0AACvUAAA/wAAAADQLwAAgH8AACDf" +
        "Dw+PcH9/f4D///8A////AAD/AAAJ/wAAGv8AAPj/AAD7/wAA+P8AABr/AAAJ/wAAAP8AAAz/AAA+/wQAP//eAH6/xAAM/4AAAP+E" +
        "AAD/hAAA//wAAP9/AD7DfQAA/0EAAP9BAAD/QQAA/0EAAP9/AAD/AAAA/wAACPcIACrVKgAc4xwAHOMcACrVKgAI9wgAAP8AAPD/" +
        "AAD/jwBw9fUKAImJdgCFhXoAiYl2AP//AAAA/wAAAP8AAOD/AABw/wAAOP8AADz/AAA8/QIA//8AAAD/AAB//wAAR8U4AkfFOAJH" +
        "xTgCQsM8AELDPABCwzwAfv8AAAD/AAB/gH8AVapVAHWKdQBdol0AV6hXAH6BfgAA/wAADN8gABy/QAAe3yAADp9gAA/vEAAHzzAA" +
        "B/cIAALnGABg4GsUcPBxDnDwdAtw8HEOePh4B3j4eAd4+HgHePh4Bxz/AAAc/wAAHP8AAD7/AAB//wAAAP8AACr/AABJ/wAAAP/8" +
        "AAj3jAAO/4AADv+EAB//hAAf/+4ADv8EAAT/AAAA/wAAAP9/AAL9QwAC/UMAAv1DAAL9QwAC/0EAAP9/AAD/AAAA/wAAJNskABjn" +
        "GAB+gX4AGOcYACTbJAAA/wAAf/8AAEfFOAJHxTgCR8U4AmrrFABW1ygAQsM8AH7/AABC/wAARv8AAH7/AAB8/wAAeP8AAHD/AABA" +
        "3yAAQP8AAAD/AP8A/wD/H/8Q8B//EP8Q/xD/EP8Q/x//AP8A/wD/AP8A/wD/AP8A//j/CP8I/wj/CP8I/wj/+P8A/wD/AP8A/wD/" +
        "AP8A/wDwAPcA8AD2APAA9QDwAPcA8AD/AP8A/wD/AP8A/wD/AB8AzwAPAO8ADwDvAA8AbwAPAP8A/wD/AP8A/wD/AP8A/wD/AP8D" +
        "/w//H/8M7gDqAPsA/wD/AP8A/wD/AP8A/wD/MP/4//D3wOcArwC/AP8A/wD/AP8A/wD/AP8A/wD/AOUA7wD7AO8A/wf4H+Af4B/g" +
        "AP8A/wD/AP8A/wD/AP8A/wD/AP8A/3iH+Af4B/gH+AcA/wD/AP8A/wD/AP8A/wD/AP8B/w//D/8P/wH/AP8A/wD/AP8A/wD/AP8A" +
        "/wD/AP+Q/6D/gP+w/4D/oP+Q/wD/AP8A/wD/AP8A/wD/AP8D/x/iE+MQ7xDvEO8Q7xDvH+AA/wD/AP8A/wD/AP9g//Df+A/w32D/" +
        "IN8g3yDfIN/gHwD/AP8A/wD/AP8A/wD/D/AI8Aj3CPcI9wj3CPcP8AD/AP8A/wD/AP8A/wD/AP/wDzAvEO8Q7xDvEO8Q7/APAP8A" +
        "/wD/AP8A/wD/AP8A/wD/AP8C/wH/Af8C/wD/AP8A/wD/AP8A/wD/AP8A/wD/AP+A/6D/wP/A/6D/gP8A/wD/AP8A/wD/AP8A/wD/" +
        "AP8f/xDwH/8Q/xD/EP8Q/xD/H/8A/wD/AP8A/wD/AP8A/wD/+P8o/0j/KP9I/yj/SP/4/wD/AP8A/wD/AP8A/wD/AP8O/wf/A/8D" +
        "/wP/D/8A/wD/AP8A/wD/AP8A/wD/AP8A/wD/AP+A/8D/wN/w/wD/AP8A/wD/AP8A/wD/AP8f/xD/EP8Q/xD/EP8Q/xD/EP8f/wD/" +
        "AP8A/wD/AP8A//D/UN9Q31DfUN8g/yD/IP8g/+D/AP8A/wD/AP8A/wD/AP8A4ADqAOgA6gDiAOoA6gDgAP8A/wD/AP8A/wD/AP8A" +
        "/wAPAK8ArwAvAK8ArwCPAB8A/wD/AP8A/wD/AP8A/wLzB/8H5wP/A/MB/wH5AP8A/AD+AP8A/wD/AP8A/wD/AP8A/wD/gP+A/8D/" +
        "wP/g/+D/QH8A/wD/AP8A/wD/AP8c4xzjHuEe4R7hHuEf4B/gH+Af4AD/AP8A/wD/AP8A/wBPAO8AvwDvAP8A/wD/AP8A/wD/AP8A" +
        "/wD/AP8A/wD/AP8D/wP/A/8H/w//AP8F/wn/AP8A/wD/AP8A/wD/AP8A/4D/gP+A/8D/4P8A/0D/IP8A/wD/AP8A/wD/AP8A/x/g" +
        "EO8Q7xDvEO8Q7xDvH+AA/wD/AP8A/wD/AP8A/wD/4B8gnyCfcP9w33Df+N/4j3DfIP8A/wD/AP8A/wD/AP8A/w/wCPcI9wj3CPcI" +
        "9wj3D/AA/wD/AP8A/wD/AP8A/wD/8A8QzxDPEM8QzxDPMO/wDwD/AP8A/wD/AP8A/wD/AP8A/wD/Av8B/wf/Af8C/wD/AP8A/wD/" +
        "AP8A/wD/AP8A/wD/AP9A/4D/4P+A/0D/AP8A/wD/AP8A/wD/AP8A/x//EP8Q/xD/EP8Q/xr/Ff8Q/x//AP8A/wD/AP8A/wD/8P9Q" +
        "31DfUN9Q3yD/oP9g/yD/4P8A/wD/AP8A/wD/AP8A/wT/BP8H/wf/B/8H/wT9BP8A/wD/AP8A/wD/AP8A/wD/IP9g/+D/wP+A/wD/" +
        "AP8A/wD/AP8A/wD/8P+Pj/H/gf+B/4H///8A/wCDALkAgQC1AIEArQCBAP8O/z///v/4/GDyAEsArwD/0C+AfyDfgHAAgAAAAAAA" +
        "/wn/Gv/4//v/+P8a/wn/AP8M/zr74SG6O4x/hHuEe/wDf4BDgkG+Qb5BvkG+f4AA/wD/APcA1QDjAOMA1QD3AP/w/4+P9f+J/4X/" +
        "if///wD/AP/g/3D/OP88/zz9//8A/3//Rf1F/UX9Qv9C/0L/fv8A/wCAAKoAigCiAKgAgQD/DN8cvx7fDp8P7wfPB/cC5wuUAY4E" +
        "iwGOAIcAhwCHAIcc/xz/HP8+/3//AP8q/0n//AOEc45/inube/ERCvsE/wD/f4BBvEG8QbxBvEO+f4AA/wD/ANsA5wCBAOcA2wD/" +
        "f/9F/UX9Rf1q/1b/Qv9+/0L/Rv9+/3z/eP9w/0DfQP8=";

    // ---- checks

    public List<string> Problems()
    {
        var r = new List<string>();
        for (int i = 0; i < ICONS; i++)
            for (int v = 0; v < 2; v++)
            {
                var p = Icon(i, v);
                for (int y = 0; y < 16; y++)
                    if (p[y, 15] != 0) { r.Add(Labels[i] + " " + VariantNames[v] + ": last column (x=15) not empty"); break; }
            }
        return r;
    }

    public void ClearLastColumns()
    {
        for (int i = 0; i < ICONS; i++)
            for (int v = 0; v < 2; v++)
                for (int y = 0; y < 16; y++) Icon(i, v)[y, 15] = 0;
    }

    // ---- PNG folders (16color/00-folder.png ...), the format of icon_set.py

    public void ExportPng(string dir)
    {
        for (int v = 0; v < 4; v++)
        {
            string d = Path.Combine(dir, VariantNames[v]);
            Directory.CreateDirectory(d);
            for (int i = 0; i < ICONS; i++)
            {
                int n = Size(v);
                using (var b = new Bitmap(n, n, PixelFormat.Format24bppRgb))
                {
                    var p = Icon(i, v);
                    for (int y = 0; y < n; y++)
                        for (int x = 0; x < n; x++) b.SetPixel(x, y, ColorOf(v, p[y, x], false));
                    b.Save(Path.Combine(d, i.ToString("00") + "-" + Names[i] + ".png"), ImageFormat.Png);
                }
            }
        }
    }

    static int C4(Color c) { return ((c.R + 8) / 17 << 8) | ((c.G + 8) / 17 << 4) | ((c.B + 8) / 17); }

    public static IconSet ImportPng(string dir, List<string> warnings)
    {
        var s = new IconSet();
        var raw = new Color[ICONS * 4][,];
        var count = new Dictionary<int, int>();
        for (int v = 0; v < 4; v++)
            for (int i = 0; i < ICONS; i++)
            {
                string f = Path.Combine(dir, VariantNames[v], i.ToString("00") + "-" + Names[i] + ".png");
                if (!File.Exists(f)) throw new Exception("missing " + f);
                int n = Size(v);
                using (var img = new Bitmap(f))
                {
                    if (img.Width != n || img.Height != n) throw new Exception(f + ": must be " + n + "x" + n);
                    var a = new Color[n, n];
                    for (int y = 0; y < n; y++)
                        for (int x = 0; x < n; x++)
                        {
                            a[y, x] = img.GetPixel(x, y);
                            if (!Mono(v))
                            {
                                int c = C4(a[y, x]);
                                if (a[y, x].R == 15 && a[y, x].G == 15 && a[y, x].B == 15) c = 0;
                                if (c != 0xFFF && c != 0)
                                {
                                    int k; count.TryGetValue(c, out k); count[c] = k + 1;
                                }
                            }
                        }
                    raw[i * 4 + v] = a;
                }
            }
        var pal = count.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).ToList();
        if (pal.Count > COLORS)
            warnings.Add(pal.Count + " colors besides white and black: the " + (pal.Count - COLORS) + " least used were replaced by the nearest");
        for (int k = 0; k < COLORS; k++) s.Palette[k] = k < pal.Count ? pal[k] : 0;
        for (int v = 0; v < 4; v++)
            for (int i = 0; i < ICONS; i++)
            {
                int n = Size(v);
                var a = raw[i * 4 + v];
                var p = s.Icon(i, v);
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                        p[y, x] = (byte) (Mono(v) ? NearestMono(a[y, x]) : s.NearestColor(a[y, x]));
            }
        if (s.Problems().Count > 0) { warnings.Add("the last column of some 16x16 icons was not empty: cleared"); s.ClearLastColumns(); }
        return s;
    }

    public static int NearestMono(Color c)
    {
        int g = (int) (c.R * 0.299 + c.G * 0.587 + c.B * 0.114);
        return g >= 213 ? 0 : g >= 128 ? 1 : g >= 43 ? 2 : 3;
    }

    public int NearestColor(Color c)
    {
        int c4 = C4(c);
        if (c.R == 15 && c.G == 15 && c.B == 15) return 1;
        if (c4 == 0xFFF) return 0;
        if (c4 == 0) return 1;
        int best = 1, bd = int.MaxValue;
        for (int k = -1; k < COLORS; k++)
        {
            int p = k < 0 ? 0 : Palette[k];
            int dr = ((p >> 8) & 15) - ((c4 >> 8) & 15), dg = ((p >> 4) & 15) - ((c4 >> 4) & 15), db = (p & 15) - (c4 & 15);
            int d = dr * dr + dg * dg + db * db;
            if (d < bd) { bd = d; best = k < 0 ? 1 : k + 2; }
        }
        return best;
    }

    // ---- helpers: mono from color, 8x8 from 16x16

    public void MonoFromColor(int i, int size16or8)
    {
        int vc = size16or8 == 16 ? 0 : 2, vm = vc + 1, n = Size(vc);
        var src = Icon(i, vc); var dst = Icon(i, vm);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int val = src[y, x];
                dst[y, x] = (byte) (val == 0 ? 0 : val == 1 ? 3 : NearestMono(ColorOf(vc, val, false)));
            }
    }

    public void SmallFromLarge(int i, bool mono)
    {
        var src = Icon(i, mono ? 1 : 0); var dst = Icon(i, mono ? 3 : 2);
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                // the most frequent non-background value of the 2x2 block
                var cnt = new int[16];
                for (int dy = 0; dy < 2; dy++)
                    for (int dx = 0; dx < 2; dx++) cnt[src[y * 2 + dy, x * 2 + dx]]++;
                int best = 0;
                for (int k = 1; k < 16; k++) if (cnt[k] > 0 && cnt[k] >= cnt[best == 0 ? 0 : best] && (best == 0 || cnt[k] > cnt[best])) best = k;
                if (cnt[0] >= 3) best = 0;
                dst[y, x] = (byte) best;
            }
    }
}

// ---------------------------------------------------------------- settings

class Settings
{
    public string Folder = "";
    public Rectangle Bounds;
    public bool Maximized;

    static string PortablePath() { return Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "NileSwanIconEditor.ini"); }
    static string AppDataPath() { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NileSwanIconEditor", "NileSwanIconEditor.ini"); }

    public static Settings Load()
    {
        var s = new Settings();
        foreach (var p in new[] { PortablePath(), AppDataPath() })
        {
            try
            {
                if (!File.Exists(p)) continue;
                foreach (var line in File.ReadAllLines(p))
                {
                    int e = line.IndexOf('=');
                    if (e < 0) continue;
                    string k = line.Substring(0, e).Trim(), v = line.Substring(e + 1).Trim();
                    if (k == "Folder") s.Folder = v;
                    else if (k == "Maximized") s.Maximized = v == "1";
                    else if (k == "Window")
                    {
                        var a = v.Split(',').Select(t => { int n; int.TryParse(t, out n); return n; }).ToArray();
                        if (a.Length == 4) s.Bounds = new Rectangle(a[0], a[1], a[2], a[3]);
                    }
                }
                return s;
            }
            catch { }
        }
        return s;
    }

    public void Save()
    {
        var lines = new[] { "[NileSwanIconEditor]", "Folder=" + Folder,
            "Window=" + Bounds.X + "," + Bounds.Y + "," + Bounds.Width + "," + Bounds.Height, "Maximized=" + (Maximized ? "1" : "0") };
        foreach (var p in new[] { PortablePath(), AppDataPath() })
        {
            try { Directory.CreateDirectory(Path.GetDirectoryName(p)); File.WriteAllLines(p, lines); return; }
            catch { }
        }
    }
}

// ---------------------------------------------------------------- UI

class Canvas : Panel
{
    public Canvas() { DoubleBuffered = true; ResizeRedraw = true; }
}

class MainForm : Form
{
    IconSet set = IconSet.BuiltIn();
    string fileName = null;
    bool dirty = false;
    int icon = 0, variant = 0, current = 2; // current pixel value
    readonly Settings settings = Settings.Load();
    readonly List<IconSet> undo = new List<IconSet>(), redo = new List<IconSet>();
    byte[,] clipboard;
    int clipVariant = -1;

    enum Tool { Pencil, Line, Rect, Fill, Picker }
    Tool tool = Tool.Pencil;

    readonly ListBox list = new ListBox { IntegralHeight = false };
    readonly RadioButton[] variantRadios = new RadioButton[4];
    readonly Canvas canvas = new Canvas { BackColor = Color.FromArgb(200, 200, 200) };
    readonly Canvas paletteBox = new Canvas { BackColor = SystemColors.Control };
    readonly Canvas preview = new Canvas { BackColor = Color.FromArgb(200, 200, 200) };
    readonly Label status = new Label { Dock = DockStyle.Bottom, Height = 22, TextAlign = ContentAlignment.MiddleLeft, BorderStyle = BorderStyle.Fixed3D };
    readonly Dictionary<Tool, RadioButton> toolButtons = new Dictionary<Tool, RadioButton>();

    Point dragStart = new Point(-1, -1), dragNow;
    bool dragging;
    byte dragValue;

    public MainForm()
    {
        Text = "NileSwan Icon Editor";
        ClientSize = new Size(1000, 680);
        MinimumSize = new Size(860, 600);
        Font = new Font("Segoe UI", 9f);
        KeyPreview = true;

        // menus
        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("&File");
        file.DropDownItems.Add(Item("&New (built-in icons)", Keys.Control | Keys.N, (s, e) => NewSet(false)));
        file.DropDownItems.Add(Item("New &blank", Keys.None, (s, e) => NewSet(true)));
        file.DropDownItems.Add(Item("&Open .ICN...", Keys.Control | Keys.O, (s, e) => OpenIcn()));
        file.DropDownItems.Add(Item("&Save .ICN", Keys.Control | Keys.S, (s, e) => SaveIcn(false)));
        file.DropDownItems.Add(Item("Save .ICN &as...", Keys.Control | Keys.Shift | Keys.S, (s, e) => SaveIcn(true)));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(Item("&Import PNG folder...", Keys.None, (s, e) => ImportPng()));
        file.DropDownItems.Add(Item("&Export PNG folder...", Keys.None, (s, e) => ExportPng()));
        file.DropDownItems.Add(Item("Export &preview picture...", Keys.None, (s, e) => ExportPreview()));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(Item("E&xit", Keys.None, (s, e) => Close()));
        var edit = new ToolStripMenuItem("&Edit");
        edit.DropDownItems.Add(Item("&Undo", Keys.Control | Keys.Z, (s, e) => Undo()));
        edit.DropDownItems.Add(Item("&Redo", Keys.Control | Keys.Y, (s, e) => Redo()));
        edit.DropDownItems.Add(new ToolStripSeparator());
        edit.DropDownItems.Add(Item("&Copy icon", Keys.Control | Keys.C, (s, e) => { clipboard = (byte[,]) Cur().Clone(); clipVariant = variant; }));
        edit.DropDownItems.Add(Item("&Paste icon", Keys.Control | Keys.V, (s, e) => Paste()));
        edit.DropDownItems.Add(Item("C&lear icon", Keys.Delete, (s, e) => Modify(p => Array.Clear(p, 0, p.Length))));
        edit.DropDownItems.Add(new ToolStripSeparator());
        edit.DropDownItems.Add(Item("Flip &horizontally", Keys.None, (s, e) => Transform((p, n, x, y) => p[y, n - 1 - x])));
        edit.DropDownItems.Add(Item("Flip &vertically", Keys.None, (s, e) => Transform((p, n, x, y) => p[n - 1 - y, x])));
        edit.DropDownItems.Add(Item("Move left", Keys.Control | Keys.Left, (s, e) => Transform((p, n, x, y) => x + 1 < n ? p[y, x + 1] : (byte) 0)));
        edit.DropDownItems.Add(Item("Move right", Keys.Control | Keys.Right, (s, e) => Transform((p, n, x, y) => x > 0 ? p[y, x - 1] : (byte) 0)));
        edit.DropDownItems.Add(Item("Move up", Keys.Control | Keys.Up, (s, e) => Transform((p, n, x, y) => y + 1 < n ? p[y + 1, x] : (byte) 0)));
        edit.DropDownItems.Add(Item("Move down", Keys.Control | Keys.Down, (s, e) => Transform((p, n, x, y) => y > 0 ? p[y - 1, x] : (byte) 0)));
        var gen = new ToolStripMenuItem("&Generate");
        gen.DropDownItems.Add(Item("Mono from color (this icon)", Keys.Control | Keys.M, (s, e) => Generate(false, false)));
        gen.DropDownItems.Add(Item("8x8 from 16x16 (this icon)", Keys.Control | Keys.H, (s, e) => Generate(true, false)));
        gen.DropDownItems.Add(new ToolStripSeparator());
        gen.DropDownItems.Add(Item("Mono from color (all icons)", Keys.None, (s, e) => Generate(false, true)));
        gen.DropDownItems.Add(Item("8x8 from 16x16 (all icons)", Keys.None, (s, e) => Generate(true, true)));
        var help = new ToolStripMenuItem("&Help");
        help.DropDownItems.Add(Item("&Rules and keys", Keys.F1, (s, e) => ShowHelp()));
        menu.Items.AddRange(new ToolStripItem[] { file, edit, gen, help });
        MainMenuStrip = menu;

        // left: icons and variant
        var left = new Panel { Dock = DockStyle.Left, Width = 200, Padding = new Padding(6) };
        list.Dock = DockStyle.Fill;
        list.Items.AddRange(IconSet.Labels);
        list.SelectedIndex = 0;
        list.SelectedIndexChanged += (s, e) => { icon = Math.Max(0, list.SelectedIndex); RefreshAll(); };
        var vbox = new GroupBox { Text = "Variant", Dock = DockStyle.Bottom, Height = 128 };
        string[] vlabels = { "16x16 color", "16x16 mono", "8x8 color", "8x8 mono" };
        for (int v = 0; v < 4; v++)
        {
            int vv = v;
            variantRadios[v] = new RadioButton { Text = vlabels[v], Left = 10, Top = 20 + v * 25, AutoSize = true, Checked = v == 0 };
            variantRadios[v].CheckedChanged += (s, e) => { if (variantRadios[vv].Checked) { SelectVariant(vv); } };
            vbox.Controls.Add(variantRadios[v]);
        }
        left.Controls.Add(list);
        left.Controls.Add(vbox);

        // right: tools, palette
        var right = new Panel { Dock = DockStyle.Right, Width = 230, Padding = new Padding(6) };
        var tools = new GroupBox { Text = "Tool", Dock = DockStyle.Top, Height = 92 };
        var tl = new[] { Tool.Pencil, Tool.Line, Tool.Rect, Tool.Fill, Tool.Picker };
        string[] tn = { "Pencil (P)", "Line (L)", "Rectangle (R)", "Fill (F)", "Picker (K)" };
        for (int k = 0; k < tl.Length; k++)
        {
            var t = tl[k];
            var rb = new RadioButton { Text = tn[k], Left = 10 + (k % 2) * 105, Top = 18 + (k / 2) * 23, AutoSize = true, Checked = k == 0 };
            rb.CheckedChanged += (s, e) => { if (((RadioButton) s).Checked) tool = t; };
            toolButtons[t] = rb;
            tools.Controls.Add(rb);
        }
        var palGroup = new GroupBox { Text = "Colors", Dock = DockStyle.Fill };
        paletteBox.Dock = DockStyle.Fill;
        paletteBox.Paint += PaintPalette;
        paletteBox.MouseDown += PaletteClick;
        paletteBox.MouseDoubleClick += PaletteDoubleClick;
        palGroup.Controls.Add(paletteBox);
        right.Controls.Add(palGroup);
        right.Controls.Add(tools);

        // bottom: preview
        var prevGroup = new GroupBox { Text = "Preview (as on the console)", Dock = DockStyle.Bottom, Height = 200 };
        preview.Dock = DockStyle.Fill;
        preview.Paint += PaintPreview;
        preview.MouseDown += PreviewClick;
        prevGroup.Controls.Add(preview);

        canvas.Dock = DockStyle.Fill;
        canvas.Paint += PaintCanvas;
        canvas.MouseDown += CanvasDown;
        canvas.MouseMove += CanvasMove;
        canvas.MouseUp += CanvasUp;

        Controls.Add(canvas);
        Controls.Add(prevGroup);
        Controls.Add(right);
        Controls.Add(left);
        Controls.Add(status);
        Controls.Add(menu);

        KeyDown += OnKey;
        FormClosing += (s, e) =>
        {
            if (!ConfirmDiscard()) { e.Cancel = true; return; }
            settings.Maximized = WindowState == FormWindowState.Maximized;
            settings.Bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            settings.Save();
        };
        if (!settings.Bounds.IsEmpty && settings.Bounds.Width >= MinimumSize.Width
            && Screen.AllScreens.Any(sc => sc.WorkingArea.IntersectsWith(settings.Bounds)))
        {
            StartPosition = FormStartPosition.Manual;
            Bounds = settings.Bounds;
        }
        if (settings.Maximized) WindowState = FormWindowState.Maximized;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        RefreshAll();
    }

    static ToolStripMenuItem Item(string text, Keys keys, EventHandler h)
    {
        var i = new ToolStripMenuItem(text, null, h);
        if (keys != Keys.None) i.ShortcutKeys = keys;
        return i;
    }

    byte[,] Cur() { return set.Icon(icon, variant); }
    int N() { return IconSet.Size(variant); }
    bool MonoV() { return IconSet.Mono(variant); }
    int ValueCount() { return MonoV() ? 4 : 2 + IconSet.COLORS; }

    void SelectVariant(int v)
    {
        variant = v;
        if (current >= ValueCount()) current = MonoV() ? 3 : 1;
        RefreshAll();
    }

    void RefreshAll()
    {
        Text = "NileSwan Icon Editor - " + (fileName == null ? "(new set)" : Path.GetFileName(fileName)) + (dirty ? " *" : "");
        canvas.Invalidate(); paletteBox.Invalidate(); preview.Invalidate();
    }

    void PushUndo()
    {
        undo.Add(set.Clone());
        if (undo.Count > 100) undo.RemoveAt(0);
        redo.Clear();
        dirty = true;
    }

    void Undo() { if (undo.Count == 0) return; redo.Add(set); set = undo[undo.Count - 1]; undo.RemoveAt(undo.Count - 1); dirty = true; RefreshAll(); }
    void Redo() { if (redo.Count == 0) return; undo.Add(set); set = redo[redo.Count - 1]; redo.RemoveAt(redo.Count - 1); dirty = true; RefreshAll(); }

    void Modify(Action<byte[,]> a) { PushUndo(); a(Cur()); Fix(); RefreshAll(); }

    void Transform(Func<byte[,], int, int, int, byte> f)
    {
        PushUndo();
        var p = Cur(); var src = (byte[,]) p.Clone(); int n = N();
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) p[y, x] = f(src, n, x, y);
        Fix(); RefreshAll();
    }

    // the last column of 16x16 icons is always empty
    void Fix()
    {
        if (N() == 16) for (int y = 0; y < 16; y++) Cur()[y, 15] = 0;
    }

    void Paste()
    {
        if (clipboard == null) return;
        if (clipboard.GetLength(0) != N() || IconSet.Mono(clipVariant) != MonoV())
        {
            MessageBox.Show(this, "The copied icon is a different variant (size or color/mono).", Text);
            return;
        }
        PushUndo();
        Array.Copy(clipboard, Cur(), clipboard.Length);
        Fix(); RefreshAll();
    }

    void Generate(bool small, bool all)
    {
        PushUndo();
        for (int i = 0; i < IconSet.ICONS; i++)
        {
            if (!all && i != icon) continue;
            if (small) { set.SmallFromLarge(i, false); set.SmallFromLarge(i, true); }
            else { set.MonoFromColor(i, 16); set.MonoFromColor(i, 8); }
        }
        RefreshAll();
    }

    // ---- canvas

    int Cell() { int n = N(); return Math.Max(4, Math.Min((canvas.Width - 20) / n, (canvas.Height - 20) / n)); }
    Point Origin() { int c = Cell(), n = N(); return new Point((canvas.Width - c * n) / 2, (canvas.Height - c * n) / 2); }

    bool CellAt(Point m, out int x, out int y)
    {
        var o = Origin(); int c = Cell();
        x = (m.X - o.X) / c; y = (m.Y - o.Y) / c;
        if (m.X < o.X || m.Y < o.Y) { x = -1; y = -1; }
        return x >= 0 && y >= 0 && x < N() && y < N();
    }

    void PaintCanvas(object sender, PaintEventArgs e)
    {
        var g = e.Graphics; var o = Origin(); int c = Cell(), n = N();
        var p = Cur();
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int val = p[y, x];
                if (dragging && (tool == Tool.Line || tool == Tool.Rect) && ShapeContains(x, y)) val = dragValue;
                using (var b = new SolidBrush(set.ColorOf(variant, val, false)))
                    g.FillRectangle(b, o.X + x * c, o.Y + y * c, c, c);
            }
        using (var pen = new Pen(Color.FromArgb(70, 128, 128, 128)))
            for (int k = 0; k <= n; k++)
            {
                g.DrawLine(pen, o.X + k * c, o.Y, o.X + k * c, o.Y + n * c);
                g.DrawLine(pen, o.X, o.Y + k * c, o.X + n * c, o.Y + k * c);
            }
        using (var pen = new Pen(Color.FromArgb(110, 0, 0, 0)))
            if (n == 16) { g.DrawLine(pen, o.X + 8 * c, o.Y, o.X + 8 * c, o.Y + n * c); g.DrawLine(pen, o.X, o.Y + 8 * c, o.X + n * c, o.Y + 8 * c); }
        if (n == 16)
            using (var hatch = new HatchBrush(HatchStyle.BackwardDiagonal, Color.FromArgb(160, 230, 0, 0), Color.Transparent))
            using (var pen = new Pen(Color.FromArgb(230, 0, 0), 2))
            {
                g.FillRectangle(hatch, o.X + 15 * c, o.Y, c, n * c);
                g.DrawRectangle(pen, o.X + 15 * c, o.Y, c, n * c);
            }
    }

    bool ShapeContains(int x, int y)
    {
        int x0 = dragStart.X, y0 = dragStart.Y, x1 = dragNow.X, y1 = dragNow.Y;
        if (tool == Tool.Rect)
        {
            int l = Math.Min(x0, x1), r = Math.Max(x0, x1), t = Math.Min(y0, y1), b = Math.Max(y0, y1);
            return x >= l && x <= r && y >= t && y <= b && (x == l || x == r || y == t || y == b);
        }
        foreach (var pt in LinePoints(x0, y0, x1, y1)) if (pt.X == x && pt.Y == y) return true;
        return false;
    }

    static IEnumerable<Point> LinePoints(int x0, int y0, int x1, int y1)
    {
        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1, dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1, err = dx + dy;
        while (true)
        {
            yield return new Point(x0, y0);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    void Plot(int x, int y, byte v)
    {
        if (x < 0 || y < 0 || x >= N() || y >= N()) return;
        if (N() == 16 && x == 15) return;
        Cur()[y, x] = v;
    }

    void CanvasDown(object sender, MouseEventArgs e)
    {
        int x, y;
        if (!CellAt(e.Location, out x, out y)) return;
        byte v = (byte) (e.Button == MouseButtons.Right ? 0 : current);
        var t = (Control.ModifierKeys & Keys.Alt) != 0 ? Tool.Picker : tool;
        if (t == Tool.Picker)
        {
            current = Cur()[y, x];
            RefreshAll(); return;
        }
        PushUndo();
        if (t == Tool.Fill)
        {
            FloodFill(x, y, v); Fix(); RefreshAll(); return;
        }
        dragging = true; dragValue = v; dragStart = new Point(x, y); dragNow = dragStart;
        if (t == Tool.Pencil) Plot(x, y, v);
        canvas.Invalidate(); preview.Invalidate();
    }

    void CanvasMove(object sender, MouseEventArgs e)
    {
        int x, y;
        bool inside = CellAt(e.Location, out x, out y);
        if (inside)
        {
            int val = Cur()[y, x];
            status.Text = "x " + x + "  y " + y + "   " + ValueName(val) + (N() == 16 && x == 15 ? "   (column 15: always empty, gap before the name)" : "");
        }
        if (!dragging) return;
        x = Math.Max(0, Math.Min(N() - 1, x)); y = Math.Max(0, Math.Min(N() - 1, y));
        if (tool == Tool.Pencil)
        {
            foreach (var pt in LinePoints(dragNow.X, dragNow.Y, x, y)) Plot(pt.X, pt.Y, dragValue);
            preview.Invalidate();
        }
        dragNow = new Point(x, y);
        canvas.Invalidate();
    }

    void CanvasUp(object sender, MouseEventArgs e)
    {
        if (!dragging) return;
        dragging = false;
        if (tool == Tool.Line || tool == Tool.Rect)
            for (int y = 0; y < N(); y++) for (int x = 0; x < N(); x++) if (ShapeContains(x, y)) Plot(x, y, dragValue);
        Fix();
        RefreshAll();
    }

    void FloodFill(int x, int y, byte v)
    {
        var p = Cur(); int n = N(); byte target = p[y, x];
        if (target == v) return;
        var st = new Stack<Point>(); st.Push(new Point(x, y));
        while (st.Count > 0)
        {
            var q = st.Pop();
            if (q.X < 0 || q.Y < 0 || q.X >= n || q.Y >= n || p[q.Y, q.X] != target) continue;
            if (n == 16 && q.X == 15) continue;
            p[q.Y, q.X] = v;
            st.Push(new Point(q.X + 1, q.Y)); st.Push(new Point(q.X - 1, q.Y));
            st.Push(new Point(q.X, q.Y + 1)); st.Push(new Point(q.X, q.Y - 1));
        }
    }

    string ValueName(int v)
    {
        if (MonoV()) return new[] { "background (white)", "light grey", "dark grey", "ink (black)" }[v & 3];
        if (v == 0) return "background (white; black on the selected row)";
        if (v == 1) return "ink (black; white on the selected row)";
        int c = set.Palette[v - 2];
        return "color " + (v - 1) + "  #" + ((c >> 8) & 15).ToString("X") + ((c >> 4) & 15).ToString("X") + (c & 15).ToString("X");
    }

    // ---- palette

    const int SW = 40, SH = 30;

    Rectangle SwatchRect(int k)
    {
        // background and ink first, then the 12 colors (2 per row)
        return new Rectangle(8 + (k % 2) * (SW + 60), 8 + (k / 2) * (SH + 8), SW, SH);
    }

    void PaintPalette(object sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        for (int k = 0; k < ValueCount(); k++)
        {
            var r = SwatchRect(k);
            using (var b = new SolidBrush(set.ColorOf(variant, k, false))) g.FillRectangle(b, r);
            g.DrawRectangle(Pens.Gray, r);
            if (k == current) { using (var p = new Pen(Color.Red, 3)) g.DrawRectangle(p, Rectangle.Inflate(r, 2, 2)); }
            string label;
            if (MonoV()) label = new[] { "back", "light", "dark", "ink" }[k];
            else if (k < 2) label = k == 0 ? "back" : "ink";
            else
            {
                int c = set.Palette[k - 2];
                label = "#" + ((c >> 8) & 15).ToString("X") + ((c >> 4) & 15).ToString("X") + (c & 15).ToString("X");
            }
            g.DrawString(label, Font, Brushes.Black, r.Right + 4, r.Top + 7);
        }
        if (!MonoV())
            g.DrawString("Click: select the color\nDouble click: edit it\n(12-bit, 16 levels each)\nRight click on the icon:\nbackground",
                Font, Brushes.DimGray, 8, SwatchRect(14).Top + 4);
    }

    int SwatchAt(Point p)
    {
        for (int k = 0; k < ValueCount(); k++) if (Rectangle.Inflate(SwatchRect(k), 2, 2).Contains(p)) return k;
        return -1;
    }

    void PaletteClick(object sender, MouseEventArgs e)
    {
        int k = SwatchAt(e.Location);
        if (k < 0) return;
        current = k;
        if (tool == Tool.Picker) toolButtons[Tool.Pencil].Checked = true;
        RefreshAll();
    }

    void PaletteDoubleClick(object sender, MouseEventArgs e)
    {
        int k = SwatchAt(e.Location);
        if (MonoV() || k < 2) return;
        int c = set.Palette[k - 2];
        int? nc = EditColor(c);
        if (nc == null) return;
        PushUndo();
        set.Palette[k - 2] = nc.Value;
        RefreshAll();
    }

    int? EditColor(int c)
    {
        using (var f = new Form { Text = "Edit color (12-bit)", FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false,
            StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(360, 190), Font = Font })
        {
            var bars = new TrackBar[3];
            var sample = new Panel { Left = 260, Top = 15, Width = 85, Height = 85, BorderStyle = BorderStyle.FixedSingle };
            var hex = new Label { Left = 260, Top = 105, AutoSize = true };
            string[] names = { "R", "G", "B" };
            EventHandler upd = (s, e) =>
            {
                int v = (bars[0].Value << 8) | (bars[1].Value << 4) | bars[2].Value;
                sample.BackColor = IconSet.Rgb(v);
                hex.Text = "#" + v.ToString("X3") + "  (" + IconSet.Rgb(v).R + "," + IconSet.Rgb(v).G + "," + IconSet.Rgb(v).B + ")";
            };
            for (int i = 0; i < 3; i++)
            {
                f.Controls.Add(new Label { Text = names[i], Left = 10, Top = 22 + i * 40, AutoSize = true });
                bars[i] = new TrackBar { Left = 30, Top = 15 + i * 40, Width = 220, Minimum = 0, Maximum = 15, TickFrequency = 1, Value = (c >> (8 - i * 4)) & 15 };
                bars[i].ValueChanged += upd;
                f.Controls.Add(bars[i]);
            }
            var pick = new Button { Text = "Windows color...", Left = 10, Top = 150, Width = 120 };
            pick.Click += (s, e) =>
            {
                using (var cd = new ColorDialog { Color = sample.BackColor, FullOpen = true })
                    if (cd.ShowDialog(f) == DialogResult.OK)
                    {
                        bars[0].Value = (cd.Color.R + 8) / 17; bars[1].Value = (cd.Color.G + 8) / 17; bars[2].Value = (cd.Color.B + 8) / 17;
                    }
            };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 190, Top = 150, Width = 75 };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Left = 272, Top = 150, Width = 75 };
            f.Controls.AddRange(new Control[] { sample, hex, pick, ok, cancel });
            f.AcceptButton = ok; f.CancelButton = cancel;
            upd(null, null);
            if (f.ShowDialog(this) != DialogResult.OK) return null;
            return (bars[0].Value << 8) | (bars[1].Value << 4) | bars[2].Value;
        }
    }

    // ---- preview: rows as in the menu (normal, selected), 8x8, mono

    const int PS = 2; // preview scale

    void DrawIcon(Graphics g, int i, int v, bool selected, int x0, int y0, int scale)
    {
        var p = set.Icon(i, v); int n = IconSet.Size(v);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                using (var b = new SolidBrush(set.ColorOf(v, p[y, x], selected)))
                    g.FillRectangle(b, x0 + x * scale, y0 + y * scale, scale, scale);
    }

    static readonly string[] PreviewRows = { "16 color", "16 color, selected row", "8 color", "16 mono", "8 mono" };

    void PaintPreview(object sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        int step = 16 * PS + 10;
        for (int r = 0; r < PreviewRows.Length; r++)
        {
            int y0 = 4 + r * 34;
            int v = r == 0 || r == 1 ? 0 : r == 2 ? 2 : r == 3 ? 1 : 3;
            bool sel = r == 1;
            g.DrawString(PreviewRows[r], Font, Brushes.Black, 4, y0 + 8);
            int x0 = 150;
            if (sel) using (var b = new SolidBrush(Color.Black)) g.FillRectangle(b, x0 - 4, y0 - 1, step * IconSet.ICONS, 16 * PS + 2);
            for (int i = 0; i < IconSet.ICONS; i++)
            {
                int x = x0 + i * step;
                DrawIcon(g, i, v, sel, x, y0, PS);
                if (i == icon && (v == variant || (sel && variant == 0)))
                    using (var pen = new Pen(Color.Red, 2)) g.DrawRectangle(pen, x - 2, y0 - 2, IconSet.Size(v) * PS + 3, IconSet.Size(v) * PS + 3);
            }
        }
    }

    void PreviewClick(object sender, MouseEventArgs e)
    {
        int step = 16 * PS + 10;
        int r = (e.Y - 4) / 34, i = (e.X - 150) / step;
        if (r < 0 || r >= PreviewRows.Length || i < 0 || i >= IconSet.ICONS || e.X < 150) return;
        int v = r == 0 || r == 1 ? 0 : r == 2 ? 2 : r == 3 ? 1 : 3;
        list.SelectedIndex = i;
        variantRadios[v].Checked = true;
    }

    // ---- keys

    void OnKey(object sender, KeyEventArgs e)
    {
        if (e.Control || e.Alt || ActiveControl is ListBox && (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)) return;
        Tool? t = null;
        switch (e.KeyCode)
        {
            case Keys.P: case Keys.B: t = Tool.Pencil; break;
            case Keys.L: t = Tool.Line; break;
            case Keys.R: t = Tool.Rect; break;
            case Keys.F: case Keys.G: t = Tool.Fill; break;
            case Keys.K: case Keys.I: t = Tool.Picker; break;
            case Keys.D1: case Keys.D2: case Keys.D3: case Keys.D4:
                variantRadios[e.KeyCode - Keys.D1].Checked = true; e.Handled = true; return;
            case Keys.PageUp: if (icon > 0) list.SelectedIndex = icon - 1; e.Handled = true; return;
            case Keys.PageDown: if (icon < IconSet.ICONS - 1) list.SelectedIndex = icon + 1; e.Handled = true; return;
        }
        if (t != null) { toolButtons[t.Value].Checked = true; e.Handled = true; }
    }

    // ---- files

    bool ConfirmDiscard()
    {
        if (!dirty) return true;
        var r = MessageBox.Show(this, "Save the changes to the icon set?", Text, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        if (r == DialogResult.Cancel) return false;
        if (r == DialogResult.Yes) return SaveIcn(false);
        return true;
    }

    void Loaded(IconSet s, string name)
    {
        set = s; fileName = name; dirty = false; undo.Clear(); redo.Clear(); RefreshAll();
    }

    void NewSet(bool blank)
    {
        if (!ConfirmDiscard()) return;
        var s = IconSet.BuiltIn();
        if (blank) { s = new IconSet(); Array.Copy(IconSet.BuiltIn().Palette, s.Palette, IconSet.COLORS); }
        Loaded(s, null);
    }

    string StartFolder() { return settings.Folder.Length > 0 && Directory.Exists(settings.Folder) ? settings.Folder : ""; }

    void OpenIcn()
    {
        if (!ConfirmDiscard()) return;
        using (var d = new OpenFileDialog { Filter = "swanshell icon sets (*.icn)|*.icn|All files|*.*", InitialDirectory = StartFolder() })
        {
            if (d.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                Loaded(IconSet.FromIcn(File.ReadAllBytes(d.FileName)), d.FileName);
                settings.Folder = Path.GetDirectoryName(d.FileName);
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }

    bool SaveIcn(bool askName)
    {
        string f = fileName;
        if (askName || f == null)
        {
            using (var d = new SaveFileDialog { Filter = "swanshell icon sets (*.icn)|*.icn", FileName = f == null ? "MYICONS.ICN" : Path.GetFileName(f),
                InitialDirectory = StartFolder() })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return false;
                f = d.FileName;
            }
        }
        try
        {
            set.ClearLastColumns();
            File.WriteAllBytes(f, set.ToIcn());
            fileName = f; dirty = false;
            settings.Folder = Path.GetDirectoryName(f);
            status.Text = "Saved " + f + "  -  copy it to NILESWAN\\ICONS on the microSD card, then Settings > Theme > Icons.";
            RefreshAll();
            return true;
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); return false; }
    }

    string PickFolder(string title)
    {
        using (var d = new FolderBrowserDialog { Description = title, SelectedPath = StartFolder() })
            return d.ShowDialog(this) == DialogResult.OK ? d.SelectedPath : null;
    }

    void ImportPng()
    {
        if (!ConfirmDiscard()) return;
        string dir = PickFolder("Folder with 16color, 16mono, 8color, 8mono (NN-name.png)");
        if (dir == null) return;
        try
        {
            var w = new List<string>();
            Loaded(IconSet.ImportPng(dir, w), null);
            dirty = true; RefreshAll();
            settings.Folder = dir;
            if (w.Count > 0) MessageBox.Show(this, string.Join("\n", w), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    void ExportPng()
    {
        string dir = PickFolder("Folder where 16color, 16mono, 8color, 8mono will be written");
        if (dir == null) return;
        try { set.ExportPng(dir); settings.Folder = dir; status.Text = "Exported to " + dir; }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    void ExportPreview()
    {
        using (var d = new SaveFileDialog { Filter = "PNG|*.png", FileName = "preview.png", InitialDirectory = StartFolder() })
        {
            if (d.ShowDialog(this) != DialogResult.OK) return;
            SavePreview(set, d.FileName, 4);
        }
    }

    public static void SavePreview(IconSet s, string path, int scale)
    {
        int step = 20 * scale;
        using (var b = new Bitmap(10 * step + 10, 5 * step + 10))
        using (var g = Graphics.FromImage(b))
        {
            g.Clear(Color.FromArgb(200, 200, 200));
            for (int r = 0; r < 5; r++)
            {
                int v = r == 0 || r == 1 ? 0 : r == 2 ? 2 : r == 3 ? 1 : 3;
                for (int i = 0; i < IconSet.ICONS; i++)
                {
                    int n = IconSet.Size(v);
                    var p = s.Icon(i, v);
                    for (int y = 0; y < n; y++)
                        for (int x = 0; x < n; x++)
                            using (var br = new SolidBrush(s.ColorOf(v, p[y, x], r == 1)))
                                g.FillRectangle(br, 5 + i * step + x * scale, 5 + r * step + y * scale, scale, scale);
                }
            }
            b.Save(path, ImageFormat.Png);
        }
    }

    void ShowHelp()
    {
        MessageBox.Show(this,
            "NileSwan Icon Editor - file icon sets for the swanshell menu\n\n" +
            "10 icons, each in 4 variants: 16x16 color, 16x16 mono, 8x8 color, 8x8 mono.\n\n" +
            "Rules (enforced by the editor):\n" +
            "- 16x16: the last column (red) stays empty: it separates the icon from the file name.\n" +
            "- 'back' is the row background: white, black on the selected row. There is no opaque white.\n" +
            "- 'ink' is black, white on the selected row: use it for outlines.\n" +
            "- 12 free colors per set (12-bit), shared by all color icons. Mono: 4 tones.\n\n" +
            "Mouse: left = draw with the selected color, right = background, Alt+click = pick a color.\n" +
            "Keys: P pencil, L line, R rectangle, F fill, K picker, 1-4 variant, PgUp/PgDn icon,\n" +
            "Ctrl+Z/Y undo/redo, Ctrl+C/V copy/paste icon, Ctrl+arrows move the icon,\n" +
            "Ctrl+M mono from color, Ctrl+H 8x8 from 16x16.\n\n" +
            "Save .ICN, copy the file to NILESWAN\\ICONS on the microSD card and choose it in\n" +
            "Settings > Interface > Theme > Icons.", "Help");
    }
}

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0].StartsWith("--")) return Cli(args);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
        return 0;
    }

    // command line (testing): --pack <png folder> <out.icn> | --unpack <in.icn> <png folder>
    //   | --preview <in.icn> <out.png> | --builtin <out.icn>
    static int Cli(string[] a)
    {
        try
        {
            if (a[0] == "--pack" && a.Length == 3)
            {
                var w = new List<string>();
                var s = IconSet.ImportPng(a[1], w);
                foreach (var x in w) Console.WriteLine("warning: " + x);
                File.WriteAllBytes(a[2], s.ToIcn());
            }
            else if (a[0] == "--unpack" && a.Length == 3) IconSet.FromIcn(File.ReadAllBytes(a[1])).ExportPng(a[2]);
            else if (a[0] == "--roundtrip" && a.Length == 3) File.WriteAllBytes(a[2], IconSet.FromIcn(File.ReadAllBytes(a[1])).ToIcn());
            else if (a[0] == "--preview" && a.Length == 3) MainForm.SavePreview(IconSet.FromIcn(File.ReadAllBytes(a[1])), a[2], 4);
            else if (a[0] == "--builtin" && a.Length == 2) File.WriteAllBytes(a[1], IconSet.BuiltIn().ToIcn());
            else { Console.Error.WriteLine("usage: --pack dir out.icn | --unpack in.icn dir | --roundtrip in.icn out.icn | --preview in.icn out.png | --builtin out.icn"); return 1; }
            Console.WriteLine("ok");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 2; }
    }
}
