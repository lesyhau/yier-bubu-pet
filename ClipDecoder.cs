using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace YierBubuPet;

/// <summary>Decoded animation: premultiplied BGRA frames, all the same size.</summary>
internal sealed record DecodedClip(int Width, int Height, byte[][] Frames, int[] DelaysMs);

/// <summary>
/// Turns a GIF / animated WebP / PNG into frames ready for the screen:
/// removes a solid background if there is one, crops away empty space
/// (the same crop for every frame, so the character doesn't jitter),
/// and scales to the requested height.
/// </summary>
internal static class ClipDecoder
{
    public static DecodedClip Decode(string path, int targetHeightPx)
    {
        using var codec = SKCodec.Create(path) ?? throw new InvalidOperationException("Unsupported image: " + path);
        int w = codec.Info.Width, h = codec.Info.Height;
        var info = new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var frameInfo = codec.FrameInfo;
        int n = Math.Max(1, codec.FrameCount);

        // Decode every frame to straight-alpha RGBA. Skia composites each frame on top of
        // the frame it depends on (and applies GIF disposal), so these are complete pictures.
        var frames = new byte[n][];
        var delays = new int[n];
        for (int i = 0; i < n; i++)
        {
            int prior = i < frameInfo.Length ? frameInfo[i].RequiredFrame : -1;
            var buffer = prior >= 0 ? (byte[])frames[prior].Clone() : new byte[w * h * 4];
            var pin = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                var result = codec.GetPixels(info, pin.AddrOfPinnedObject(), new SKCodecOptions(i, prior));
                if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
                    throw new InvalidOperationException($"Couldn't decode frame {i} of {path}: {result}");
            }
            finally
            {
                pin.Free();
            }
            frames[i] = buffer;
            int ms = i < frameInfo.Length ? frameInfo[i].Duration : 0;
            // Browsers treat tiny delays as 100 ms; do the same so clips play at the intended speed.
            delays[i] = ms < 20 ? 100 : ms;
        }

        if (HasSolidBackground(frames[0], w, h))
            foreach (var px in frames) RemoveBackground(px, w, h);

        var box = OpaqueBounds(frames, w, h);
        if (box.IsEmpty) throw new InvalidOperationException("Animation is fully transparent: " + path);

        int outH = Math.Max(1, targetHeightPx);
        int outW = Math.Max(1, (int)Math.Round(box.Width * (double)outH / box.Height));
        var outInfo = new SKImageInfo(outW, outH, SKColorType.Bgra8888, SKAlphaType.Premul);
        var sampling = outH < box.Height / 2
            ? new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)
            : new SKSamplingOptions(SKCubicResampler.Mitchell);

        var output = new byte[n][];
        for (int i = 0; i < n; i++)
        {
            using var src = SKImage.FromPixelCopy(info, frames[i]);
            using var dst = new SKBitmap(outInfo);
            using (var canvas = new SKCanvas(dst))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.DrawImage(src, SKRect.Create(box.Left, box.Top, box.Width, box.Height),
                    SKRect.Create(0, 0, outW, outH), sampling, null);
            }
            output[i] = dst.Bytes;
        }
        return new DecodedClip(outW, outH, output, delays);
    }

    /// <summary>True when the image edge is mostly one opaque colour (a sticker on a plain background, not a scene).</summary>
    static bool HasSolidBackground(byte[] px, int w, int h)
    {
        int matching = 0, total = 0;
        foreach (int i in BorderIndices(w, h))
        {
            total++;
            int o = i * 4;
            if (px[o + 3] >= 250 && Similar(px, o, px[0], px[1], px[2])) matching++;
        }
        return px[3] >= 250 && matching > total * 0.85;
    }

    static bool Similar(byte[] px, int o, byte r, byte g, byte b) =>
        Math.Abs(px[o] - r) <= 40 && Math.Abs(px[o + 1] - g) <= 40 && Math.Abs(px[o + 2] - b) <= 40;

    /// <summary>
    /// Flood-fills from the edges, clearing pixels that match the corner colour.
    /// Cartoon outlines stop the fill, so a white character on a white background survives.
    /// </summary>
    static void RemoveBackground(byte[] px, int w, int h)
    {
        byte bgR = px[0], bgG = px[1], bgB = px[2];
        var seen = new bool[w * h];
        var stack = new Stack<int>(BorderIndices(w, h));
        while (stack.Count > 0)
        {
            int i = stack.Pop();
            if (seen[i]) continue;
            seen[i] = true;
            int o = i * 4;
            if (px[o + 3] >= 16)
            {
                if (!Similar(px, o, bgR, bgG, bgB)) continue;
                px[o] = px[o + 1] = px[o + 2] = px[o + 3] = 0;
            }
            int x = i % w, y = i / w;
            if (x > 0) stack.Push(i - 1);
            if (x < w - 1) stack.Push(i + 1);
            if (y > 0) stack.Push(i - w);
            if (y < h - 1) stack.Push(i + w);
        }
    }

    static SKRectI OpaqueBounds(byte[][] frames, int w, int h)
    {
        int minX = w, minY = h, maxX = -1, maxY = -1;
        foreach (var px in frames)
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (px[(y * w + x) * 4 + 3] > 16)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
        return maxX < 0 ? SKRectI.Empty : new SKRectI(minX, minY, maxX + 1, maxY + 1);
    }

    static IEnumerable<int> BorderIndices(int w, int h)
    {
        for (int x = 0; x < w; x++) { yield return x; yield return (h - 1) * w + x; }
        for (int y = 1; y < h - 1; y++) { yield return y * w; yield return y * w + w - 1; }
    }
}
