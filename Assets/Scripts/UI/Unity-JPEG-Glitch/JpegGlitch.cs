using System;
using System.Collections.Generic;
using System.IO;

// Pure managed C#: edits JPEG compressed data, never RGB pixels.
// Supported input: 8-bit baseline, one interleaved scan, YCbCr (IDs 1/2/3)
// with 4:4:4, 4:2:2 or 4:2:0 sampling, or grayscale with 1x1 sampling.
// Progressive, CMYK, RGB JPEG and multiscan sequential JPEG are rejected.
public static class JpegGlitch
{
    [Serializable]
    public sealed class Settings
    {
        public int seed = 37;
        public int bands = 6;
        public int minBandRows = 1; // MCU rows, not pixels
        public int maxBandRows = 2;
        public float gapProbability = 0.22f;
        public float colorShift = 65f; // approximate Cb/Cr sample offset
        public float brightnessShift = 18f;
        public int grayEdgeRows = 1;
        public int edgeKeepAc = 5; // retain the first N zigzag AC coefficients
        public float edgeContrast = 1.65f; // around middle gray
    }

    // Structured edits: decode Huffman symbols to quantized DCT coefficients,
    // edit selected MCUs, rebuild differential DC + RLE + Huffman coding.
    // null settings = coefficient-preserving transcode, useful for verification.
    // Original DQT is retained. Output uses legal, simple, nonoptimized DHTs;
    // the resulting file can be larger than the input.
    public static byte[] EditCoefficients(byte[] source, Settings settings)
    {
        Header h = Parse(source);
        var input = new BitReader(source, h.scanStart);
        var output = new BitWriter();
        int[] oldDc = new int[h.components.Length];
        int[] newDc = new int[h.components.Length];
        int cols = (h.width + 8 * h.maxH - 1) / (8 * h.maxH);
        int rows = (h.height + 8 * h.maxV - 1) / (8 * h.maxV);
        List<Band> bands = MakeBands(cols, rows, settings);
        int[] zz = new int[64];
        int restart = 0;
        int total = checked(cols * rows);
        for (int m = 0; m < total; m++)
        {
            int x = m % cols, y = m / cols;
            float cb, cr, light;
            int effect = EffectAt(x, y, bands, settings, out cb, out cr, out light);
            foreach (int ci in h.scanOrder)
            {
                Component c = h.components[ci];
                for (int b = 0; b < c.h * c.v; b++)
                {
                    DecodeBlock(input, h.huffman[0, c.dc], h.huffman[1, c.ac], zz, ref oldDc[ci]);
                    if (effect != 0)
                        EditBlock(zz, c.id, h.quant[c.qt][0], effect, cb, cr, light, settings);
                    EncodeBlock(output, zz, ref newDc[ci]);
                }
            }
            if (h.restartInterval > 0 && (m + 1) % h.restartInterval == 0 && m + 1 < total)
            {
                input.ExpectMarker(0xD0 + (restart & 7));
                output.Marker(0xD0 + (restart & 7));
                Array.Clear(oldDc, 0, oldDc.Length);
                Array.Clear(newDc, 0, newDc.Length);
                restart++;
            }
        }
        input.ExpectMarker(0xD9);
        output.Marker(0xD9);
        using (var file = new MemoryStream())
        {
            foreach (byte[] segment in h.preserved) file.Write(segment, 0, segment.Length);
            WriteUniversalDht(file);
            byte[] sos = (byte[])h.sos.Clone();
            for (int i = 0; i < h.scanOrder.Length; i++) sos[6 + 2 * i] = 0;
            file.Write(sos, 0, sos.Length);
            byte[] entropy = output.Bytes();
            file.Write(entropy, 0, entropy.Length);
            return file.ToArray();
        }
    }

    // Authentic byte corruption. Headers, FF00 stuffing, RST markers and EOI
    // are protected, but the entropy syntax may become invalid. Decode can fail.
    // Fractions refer to byte positions within the scan, NOT image coordinates.
    public static byte[] BendBytes(byte[] source, int seed, int edits = 3,
                                  float startFraction = 0.25f, float spanFraction = 0.1f)
    {
        Header h = Parse(source);
        int length = h.scanEnd - h.scanStart;
        int lo = h.scanStart + (int)(Clamp(startFraction, 0, 1) * length);
        int hi = Math.Min(h.scanEnd, lo + Math.Max(1, (int)(Clamp(spanFraction, 0, 1) * length)));
        var eligible = new List<int>();
        for (int i = lo; i < hi; i++)
            if (source[i] != 0xFF && source[i - 1] != 0xFF) eligible.Add(i);
        if (eligible.Count == 0) throw new InvalidDataException("No editable scan bytes in this range.");
        byte[] result = (byte[])source.Clone();
        var random = new Random(seed);
        int count = Math.Min(Math.Max(0, edits), eligible.Count);
        for (int n = 0; n < count; n++)
        {
            int pick = random.Next(eligible.Count);
            int p = eligible[pick];
            eligible[pick] = eligible[eligible.Count - 1];
            eligible.RemoveAt(eligible.Count - 1);
            int mask = 1 << random.Next(8);
            if ((result[p] ^ mask) == 0xFF) mask = mask == 128 ? 1 : mask << 1;
            result[p] ^= (byte)mask;
        }
        return result;
    }

    private sealed class Band
    {
        public int x, y, width, height;
        public bool[] active;
        public float[] cb, cr, light;
    }

    private static List<Band> MakeBands(int cols, int rows, Settings s)
    {
        var result = new List<Band>();
        if (s == null) return result;
        var rng = new Random(s.seed);
        int minH = Math.Max(1, Math.Min(rows, s.minBandRows));
        int maxH = Math.Max(minH, Math.Min(rows, s.maxBandRows));
        for (int i = 0; i < Math.Max(0, Math.Min(64, s.bands)); i++)
        {
            var b = new Band();
            b.height = rng.Next(minH, maxH + 1);
            b.y = rng.Next(rows - b.height + 1);
            b.width = rng.Next(Math.Max(1, cols / 5), Math.Max(2, cols * 4 / 5 + 1));
            b.width = Math.Min(cols, b.width);
            b.x = rng.Next(cols - b.width + 1);
            b.active = new bool[b.width];
            b.cb = new float[b.width]; b.cr = new float[b.width]; b.light = new float[b.width];
            int k = 0;
            while (k < b.width)
            {
                int span = rng.Next(2, 7);
                bool on = rng.NextDouble() >= Clamp(s.gapProbability, 0, 1);
                float cb = Signed(rng) * Clamp(s.colorShift, 0, 128);
                float cr = Signed(rng) * Clamp(s.colorShift, 0, 128);
                float light = Signed(rng) * Clamp(s.brightnessShift, 0, 128);
                for (int j = 0; j < span && k < b.width; j++, k++)
                {
                    b.active[k] = on; b.cb[k] = cb; b.cr[k] = cr; b.light[k] = light;
                }
            }
            result.Add(b);
        }
        return result;
    }

    private static int EffectAt(int x, int y, List<Band> bands, Settings s,
                                out float cb, out float cr, out float light)
    {
        cb = cr = light = 0;
        if (s == null) return 0;
        bool edge = false;
        foreach (Band b in bands)
        {
            int ix = x - b.x;
            if (ix < 0 || ix >= b.width || !b.active[ix]) continue;
            if (y >= b.y && y < b.y + b.height)
            {
                cb = b.cb[ix]; cr = b.cr[ix]; light = b.light[ix];
                return 1;
            }
            int e = Math.Max(0, Math.Min(4, s.grayEdgeRows));
            if (y >= b.y - e && y < b.y + b.height + e) edge = true;
        }
        return edge ? 2 : 0;
    }

    private static void EditBlock(int[] zz, int id, int qdc, int effect,
                                  float cb, float cr, float light, Settings s)
    {
        bool luma = id == 1;
        if (effect == 1)
        {
            float offset = luma ? light : (id == 2 ? cb : cr);
            // A DC coefficient contributes DC * Q[0] / 8 to the block mean.
            zz[0] = LimitDc(zz[0] + (int)Math.Round(8.0 * offset / qdc));
        }
        else if (!luma)
        {
            // Zero centered chroma => decoded Cb/Cr = 128 => neutral gray.
            Array.Clear(zz, 0, 64);
        }
        else
        {
            int keep = Math.Max(0, Math.Min(63, s.edgeKeepAc));
            double gain = Clamp(s.edgeContrast, 0.1f, 4f);
            zz[0] = LimitDc((int)Math.Round(zz[0] * gain));
            for (int k = 1; k < 64; k++)
                zz[k] = k > keep ? 0 : Math.Max(-1023, Math.Min(1023, (int)Math.Round(zz[k] * gain)));
        }
    }

    private static int LimitDc(int n) { return Math.Max(-1024, Math.Min(1023, n)); }
    private static float Signed(Random r) { return (float)(2 * r.NextDouble() - 1); }
    private static float Clamp(float n, float lo, float hi) { return Math.Max(lo, Math.Min(hi, n)); }

    private sealed class Component { public int id, h, v, qt, dc, ac; }
    private sealed class Header
    {
        public int width, height, maxH = 1, maxV = 1, restartInterval, scanStart, scanEnd;
        public Component[] components;
        public int[] scanOrder;
        public byte[] sos;
        public readonly int[][] quant = new int[4][];
        public readonly Huffman[,] huffman = new Huffman[2, 4];
        public readonly List<byte[]> preserved = new List<byte[]>();
    }

    private static InvalidDataException Bad(string why) { return new InvalidDataException("JPEG: " + why); }
    private static int U16(byte[] d, int p) { return (d[p] << 8) | d[p + 1]; }
    private static byte[] Slice(byte[] d, int start, int end)
    {
        byte[] a = new byte[end - start]; Buffer.BlockCopy(d, start, a, 0, a.Length); return a;
    }

    private static Header Parse(byte[] d)
    {
        if (d == null || d.Length < 4 || d[0] != 0xFF || d[1] != 0xD8) throw Bad("Missing SOI.");
        var h = new Header();
        h.preserved.Add(new byte[] { 0xFF, 0xD8 });
        int p = 2;
        while (p < d.Length)
        {
            int begin = p;
            if (d[p++] != 0xFF) throw Bad("Expected marker.");
            while (p < d.Length && d[p] == 0xFF) p++;
            if (p + 2 >= d.Length) throw Bad("Truncated marker.");
            int marker = d[p++];
            int len = U16(d, p), a = p + 2, end = p + len;
            if (len < 2 || end > d.Length) throw Bad("Invalid segment length.");
            if (marker == 0xC0)
            {
                if (h.components != null || len < 8 || d[a] != 8) throw Bad("Only 8-bit baseline is supported.");
                h.height = U16(d, a + 1); h.width = U16(d, a + 3);
                int nc = d[a + 5];
                if ((nc != 1 && nc != 3) || len != 8 + 3 * nc) throw Bad("Expected grayscale or YCbCr.");
                if (h.width < 1 || h.height < 1 || (long)h.width * h.height > 16000000)
                    throw Bad("Invalid dimensions or above this example's 16-megapixel limit.");
                h.components = new Component[nc];
                for (int i = 0; i < nc; i++)
                {
                    int q = a + 6 + 3 * i;
                    var c = new Component { id = d[q], h = d[q + 1] >> 4, v = d[q + 1] & 15, qt = d[q + 2] };
                    if (c.id != i + 1 || c.qt > 3) throw Bad("Expected component IDs 1, 2, 3 in order.");
                    if (c.h < 1 || c.h > 2 || c.v < 1 || c.v > 2) throw Bad("Unsupported sampling factors.");
                    if ((i > 0 || nc == 1) && (c.h != 1 || c.v != 1)) throw Bad("Unsupported sampling layout.");
                    if (i == 0 && c.h == 1 && c.v == 2) throw Bad("4:4:0 sampling is not supported by this example.");
                    h.components[i] = c;
                    h.maxH = Math.Max(h.maxH, c.h); h.maxV = Math.Max(h.maxV, c.v);
                }
            }
            else if (marker >= 0xC0 && marker <= 0xCF && marker != 0xC4)
                throw Bad("Use a baseline (non-progressive), Huffman-coded JPG.");
            else if (marker == 0xDB)
            {
                int q = a;
                while (q < end)
                {
                    int info = d[q++], table = info & 15;
                    if ((info >> 4) != 0 || table > 3 || q + 64 > end) throw Bad("Expected 8-bit DQT.");
                    var values = new int[64];
                    for (int k = 0; k < 64; k++)
                    {
                        values[k] = d[q++]; if (values[k] == 0) throw Bad("Zero quantizer.");
                    }
                    h.quant[table] = values;
                }
            }
            else if (marker == 0xC4)
            {
                int q = a;
                while (q < end)
                {
                    int info = d[q++], kind = info >> 4, id = info & 15;
                    if (kind > 1 || id > 3 || q + 16 > end) throw Bad("Invalid DHT.");
                    int[] counts = new int[17]; int n = 0;
                    for (int k = 1; k <= 16; k++) { counts[k] = d[q++]; n += counts[k]; }
                    if (n == 0 || n > 256 || q + n > end) throw Bad("Invalid DHT symbols.");
                    h.huffman[kind, id] = new Huffman(counts, Slice(d, q, q + n)); q += n;
                }
            }
            else if (marker == 0xDD)
            {
                if (len != 4) throw Bad("Invalid DRI."); h.restartInterval = U16(d, a);
            }
            else if (marker == 0xEE && end - a >= 12 &&
                     d[a] == 65 && d[a + 1] == 100 && d[a + 2] == 111 && d[a + 3] == 98 && d[a + 4] == 101)
            {
                if (d[a + 11] != 1) throw Bad("Adobe RGB/CMYK/YCCK is not supported.");
            }
            else if (marker == 0xDA)
            {
                if (h.components == null || len < 6) throw Bad("Missing frame.");
                int nc = d[a];
                if (nc != h.components.Length || len != 6 + 2 * nc) throw Bad("Multiscan input is not supported.");
                if (d[end - 3] != 0 || d[end - 2] != 63 || d[end - 1] != 0) throw Bad("Not a baseline scan.");
                h.scanOrder = new int[nc]; var seen = new bool[nc];
                for (int i = 0; i < nc; i++)
                {
                    int ci = d[a + 1 + 2 * i] - 1, sel = d[a + 2 + 2 * i];
                    if (ci < 0 || ci >= nc || seen[ci]) throw Bad("Invalid scan component.");
                    seen[ci] = true; h.scanOrder[i] = ci;
                    Component c = h.components[ci]; c.dc = sel >> 4; c.ac = sel & 15;
                    if (c.dc > 3 || c.ac > 3 || h.huffman[0, c.dc] == null || h.huffman[1, c.ac] == null || h.quant[c.qt] == null)
                        throw Bad("Missing coding table.");
                }
                // Normalize marker prefix so SOS field offsets are unambiguous.
                h.sos = new byte[len + 2]; h.sos[0] = 0xFF; h.sos[1] = 0xDA;
                Buffer.BlockCopy(d, p, h.sos, 2, len);
                h.scanStart = end; h.scanEnd = FindEnd(d, end);
                return h;
            }
            else if (!((marker >= 0xE0 && marker <= 0xEF) || marker == 0xFE))
                throw Bad("Unsupported marker: " + marker.ToString("X2"));
            if (marker != 0xC4) h.preserved.Add(Slice(d, begin, end));
            p = end;
        }
        throw Bad("Missing scan.");
    }

    private static int FindEnd(byte[] d, int p)
    {
        while (p < d.Length)
        {
            if (d[p++] != 0xFF) continue;
            int begin = p - 1;
            if (p >= d.Length) break;
            if (d[p] == 0) { p++; continue; }
            while (p < d.Length && d[p] == 0xFF) p++;
            if (p >= d.Length) break;
            int marker = d[p++];
            if (marker >= 0xD0 && marker <= 0xD7) continue;
            if (marker == 0xD9) return begin;
            throw Bad("Only one scan followed by EOI is supported.");
        }
        throw Bad("Missing EOI.");
    }

    private sealed class Huffman
    {
        private readonly int[] min = new int[17], max = new int[17], index = new int[17];
        private readonly byte[] symbols;
        public Huffman(int[] counts, byte[] values)
        {
            symbols = values; int code = 0, n = 0;
            for (int len = 1; len <= 16; len++)
            {
                min[len] = code; max[len] = code + counts[len] - 1; index[len] = n;
                // All-ones codes are forbidden in JPEG (used for padding).
                if (max[len] >= (1 << len) - 1) throw Bad("Invalid Huffman tree.");
                n += counts[len]; code = (code + counts[len]) << 1;
            }
        }
        public int Read(BitReader r)
        {
            int code = 0;
            for (int len = 1; len <= 16; len++)
            {
                code = (code << 1) | r.Read(1);
                if (code >= min[len] && code <= max[len]) return symbols[index[len] + code - min[len]];
            }
            throw Bad("Invalid Huffman code.");
        }
    }

    private sealed class BitReader
    {
        private readonly byte[] data;
        private int position, current, remaining;
        public BitReader(byte[] d, int start) { data = d; position = start; }
        public int Read(int count)
        {
            int value = 0;
            for (int i = 0; i < count; i++)
            {
                if (remaining == 0)
                {
                    if (position >= data.Length) throw Bad("Truncated entropy data.");
                    current = data[position++];
                    if (current == 0xFF && (position >= data.Length || data[position++] != 0))
                        throw Bad("Unexpected marker in block.");
                    remaining = 8;
                }
                value = (value << 1) | ((current >> --remaining) & 1);
            }
            return value;
        }
        public void ExpectMarker(int expected)
        {
            if (remaining > 0 && (current & ((1 << remaining) - 1)) != (1 << remaining) - 1)
                throw Bad("Invalid entropy padding.");
            remaining = 0;
            if (position >= data.Length || data[position++] != 0xFF) throw Bad("Expected end/restart marker.");
            while (position < data.Length && data[position] == 0xFF) position++;
            if (position >= data.Length || data[position++] != expected) throw Bad("Wrong end/restart marker.");
        }
    }

    private sealed class BitWriter
    {
        private readonly List<byte> data = new List<byte>();
        private int current, used;
        public void Write(int value, int count)
        {
            for (int i = count - 1; i >= 0; i--)
            {
                current = (current << 1) | ((value >> i) & 1);
                if (++used == 8)
                {
                    data.Add((byte)current); if (current == 0xFF) data.Add(0);
                    used = 0; current = 0;
                }
            }
        }
        public void Marker(int marker)
        {
            if (used != 0) { int padding = 8 - used; Write((1 << padding) - 1, padding); }
            data.Add(0xFF); data.Add((byte)marker);
        }
        public byte[] Bytes() { return data.ToArray(); }
    }

    private static int Receive(BitReader r, int size)
    {
        if (size == 0) return 0;
        int n = r.Read(size); return n < (1 << (size - 1)) ? n - ((1 << size) - 1) : n;
    }
    private static void DecodeBlock(BitReader r, Huffman dc, Huffman ac, int[] zz, ref int predictor)
    {
        Array.Clear(zz, 0, 64);
        int size = dc.Read(r); if (size > 11) throw Bad("Invalid DC category.");
        predictor += Receive(r, size); zz[0] = predictor;
        if (predictor < -1024 || predictor > 1023) throw Bad("DC outside supported baseline range.");
        int k = 1;
        while (k < 64)
        {
            int symbol = ac.Read(r), run = symbol >> 4; size = symbol & 15;
            if (size == 0)
            {
                if (run == 0) break;
                if (run != 15 || k + 16 > 64) throw Bad("Invalid AC zero run.");
                k += 16;
            }
            else
            {
                k += run;
                if (size > 10 || k >= 64) throw Bad("Invalid AC category/run.");
                zz[k++] = Receive(r, size);
            }
        }
    }
    private static int Category(int value)
    {
        int n = Math.Abs(value), size = 0; while (n != 0) { size++; n >>= 1; } return size;
    }
    private static void Amplitude(BitWriter w, int value, int size)
    {
        w.Write(value < 0 ? value + (1 << size) - 1 : value, size);
    }
    private static void AcSymbol(BitWriter w, int symbol)
    {
        // Universal AC table order: EOB, ZRL, then all (run=0..15, size=1..10).
        int code = symbol == 0 ? 0 : symbol == 0xF0 ? 1 : 2 + (symbol >> 4) * 10 + (symbol & 15) - 1;
        w.Write(code, 8);
    }
    private static void EncodeBlock(BitWriter w, int[] zz, ref int predictor)
    {
        int diff = zz[0] - predictor; predictor = zz[0];
        int size = Category(diff); if (size > 11) throw Bad("Output DC category overflow.");
        w.Write(size, 4); Amplitude(w, diff, size);
        int run = 0;
        for (int k = 1; k < 64; k++)
        {
            int value = zz[k];
            if (value == 0) { run++; continue; }
            while (run >= 16) { AcSymbol(w, 0xF0); run -= 16; }
            size = Category(value); if (size > 10) throw Bad("Output AC category overflow.");
            AcSymbol(w, (run << 4) | size); Amplitude(w, value, size); run = 0;
        }
        if (run != 0) AcSymbol(w, 0);
    }
    private static void WriteUniversalDht(Stream s)
    {
        s.WriteByte(0xFF); s.WriteByte(0xC4); s.WriteByte(0); s.WriteByte(210);
        s.WriteByte(0); // DC table 0: twelve codes of length 4
        for (int i = 1; i <= 16; i++) s.WriteByte((byte)(i == 4 ? 12 : 0));
        for (int i = 0; i < 12; i++) s.WriteByte((byte)i);
        s.WriteByte(0x10); // AC table 0: 162 codes of length 8
        for (int i = 1; i <= 16; i++) s.WriteByte((byte)(i == 8 ? 162 : 0));
        s.WriteByte(0); s.WriteByte(0xF0);
        for (int run = 0; run < 16; run++)
            for (int size = 1; size <= 10; size++) s.WriteByte((byte)((run << 4) | size));
    }
}
