using LiteMCLauncher.Models;
using System.Runtime.InteropServices;
using SkiaSharp;
using Windows.UI;
using SeedMap = RZeroF1.Minecraft.SeedMap;

namespace LiteMCLauncher.Services;

public static class BiomeMapService
{
    private const int MaxSamples = 1024;
    private const int MinSamples = 256;

    public static async Task<BiomeMapResult> GenerateAsync(
        long seed,
        SeedVersionGroup version,
        int radiusChunks,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var radiusBlocks = Math.Clamp(radiusChunks, 1, 30000) * 16;
            var step = radiusBlocks <= 512 ? 1
                : radiusBlocks <= 2048 ? 4
                : radiusBlocks <= 8192 ? 16
                : radiusBlocks <= 32768 ? 64
                : 256;
            var samples = Math.Clamp((int)Math.Ceiling(radiusBlocks / (double)step), MinSamples, MaxSamples);
            var extentRadius = samples * step / 2;
            return GenerateCore(
                seed,
                version,
                -extentRadius,
                -extentRadius,
                samples,
                samples,
                samples,
                samples,
                step,
                cancellationToken);
        }, cancellationToken);
    }

    public static async Task<BiomeMapResult> GenerateViewportAsync(
        long seed,
        SeedVersionGroup version,
        double centerX,
        double centerZ,
        double widthBlocks,
        double heightBlocks,
        int screenWidth,
        int screenHeight,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var blocksPerPixel = Math.Max(widthBlocks / Math.Max(1, screenWidth),
                heightBlocks / Math.Max(1, screenHeight));
            var step = blocksPerPixel <= 1 ? 1
                : blocksPerPixel <= 4 ? 4
                : blocksPerPixel <= 16 ? 16
                : blocksPerPixel <= 64 ? 64
                : 256;
            var samplesX = Math.Clamp((int)Math.Ceiling(widthBlocks / step), MinSamples, MaxSamples);
            var samplesZ = Math.Clamp((int)Math.Ceiling(heightBlocks / step), MinSamples, MaxSamples);
            var originX = (long)Math.Floor(centerX - (samplesX * step / 2.0));
            var originZ = (long)Math.Floor(centerZ - (samplesZ * step / 2.0));
            return GenerateCore(
                seed,
                version,
                originX,
                originZ,
                samplesX,
                samplesZ,
                screenWidth,
                screenHeight,
                step,
                cancellationToken);
        }, cancellationToken);
    }

    public static async Task<(string Name, Color Color)?> GetBiomeAtAsync(
        long seed,
        SeedVersionGroup version,
        int x,
        int z,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var versionInfo = SeedMap.Api.Versions.FirstOrDefault(candidate => candidate.Key == version.RepresentativeKey)
                              ?? throw new InvalidOperationException($"Unsupported version: {version.RepresentativeKey}");
            var biomes = SeedMap.Api.Biomes(versionInfo.Id, 0).ToDictionary(biome => biome.Id);
            var world = new SeedMap.World(seed, versionInfo.Id, 0, 64);
            using var generator = SeedMap.Api.Create(world);
            var ids = SeedMap.Api.Generate(generator, x, z, 1, 1, 1, 64);
            if (ids.Length == 0 || !biomes.TryGetValue(ids[0], out var biome))
            {
                return ((string Name, Color Color)?)null;
            }

            return (BiomeName(biome), FromArgb(biome.Color));
        }, cancellationToken);
    }

    private static BiomeMapResult GenerateCore(
        long seed,
        SeedVersionGroup version,
        long originX,
        long originZ,
        int samplesX,
        int samplesZ,
        int outputWidth,
        int outputHeight,
        int step,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var versionInfo = SeedMap.Api.Versions.FirstOrDefault(candidate => candidate.Key == version.RepresentativeKey)
                          ?? throw new InvalidOperationException($"Unsupported version: {version.RepresentativeKey}");
        var biomes = SeedMap.Api.Biomes(versionInfo.Id, 0).ToDictionary(biome => biome.Id);
        var world = new SeedMap.World(seed, versionInfo.Id, 0, 64);
        using var generator = SeedMap.Api.Create(world);
        var ids = SeedMap.Api.Generate(
            generator,
            originX,
            originZ,
            samplesX,
            samplesZ,
            step,
            64);

        var pixels = SeedMap.Palette.ToBgra(ids, biomes, null, false, false);
        FlipVertical(pixels, samplesX, samplesZ);
        var output = ResizeNearest(pixels, samplesX, samplesZ, outputWidth, outputHeight);
        var png = EncodePng(output, outputWidth, outputHeight);
        var legend = ids.Distinct()
            .Where(biomes.ContainsKey)
            .Select(id => biomes[id])
            .Select(biome => (Name: BiomeName(biome), Color: FromArgb(biome.Color)))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new BiomeMapResult(
            png,
            outputWidth,
            outputHeight,
            originX,
            originZ,
            originX + ((long)samplesX * step),
            originZ + ((long)samplesZ * step),
            legend,
            version.IsApproximate);
    }

    private static void FlipVertical(byte[] pixels, int width, int height)
    {
        var rowBytes = width * 4;
        var temp = new byte[rowBytes];
        for (var y = 0; y < height / 2; y++)
        {
            var top = y * rowBytes;
            var bottom = (height - 1 - y) * rowBytes;
            Buffer.BlockCopy(pixels, top, temp, 0, rowBytes);
            Buffer.BlockCopy(pixels, bottom, pixels, top, rowBytes);
            Buffer.BlockCopy(temp, 0, pixels, bottom, rowBytes);
        }
    }

    private static byte[] ResizeNearest(byte[] source, int sourceWidth, int sourceHeight, int width, int height)
    {
        if (sourceWidth == width && sourceHeight == height)
        {
            return source;
        }

        var output = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            var sourceY = Math.Min(sourceHeight - 1, y * sourceHeight / Math.Max(1, height));
            for (var x = 0; x < width; x++)
            {
                var sourceX = Math.Min(sourceWidth - 1, x * sourceWidth / Math.Max(1, width));
                var sourceIndex = (sourceY * sourceWidth + sourceX) * 4;
                var targetIndex = (y * width + x) * 4;
                output[targetIndex] = source[sourceIndex];
                output[targetIndex + 1] = source[sourceIndex + 1];
                output[targetIndex + 2] = source[sourceIndex + 2];
                output[targetIndex + 3] = source[sourceIndex + 3];
            }
        }

        return output;
    }

    private static byte[] EncodePng(byte[] pixels, int width, int height)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(
            width,
            height,
            SKColorType.Bgra8888,
            SKAlphaType.Unpremul));
        Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static Color FromArgb(uint color)
    {
        var alpha = (byte)(color >> 24);
        return Color.FromArgb(
            alpha == 0 ? (byte)255 : alpha,
            (byte)(color >> 16),
            (byte)(color >> 8),
            (byte)color);
    }

    private static string BiomeName(SeedMap.BiomeInfo biome)
    {
        return LauncherLanguageService.IsEnglish ? PrettifyKey(biome.Key) : biome.Name;
    }

    private static string PrettifyKey(string key)
    {
        return string.Join(' ', key.Split('_')
            .Where(part => part.Length > 0)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }
}
