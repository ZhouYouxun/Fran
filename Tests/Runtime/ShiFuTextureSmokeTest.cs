using System;
using System.Buffers.Binary;
using FranMod.Core;
using Terraria;
using Terraria.ModLoader;

namespace FranMod.Tests;

// Validate the actual packed assets in an isolated dedicated-server load, without a GPU.
public sealed class ShiFuTextureSmokeTest : ModSystem
{
    public override bool IsLoadingEnabled(Mod mod) => Main.dedServ &&
        Program.LaunchParameters.ContainsKey("-frantexturetest");

    public override void PostSetupContent()
    {
        try
        {
            foreach (string path in new[] { FranShiFuVisuals.TexturePath,
                FranShiFuVisuals.GhostTexturePath, FranShiFuVisuals.GlowTexturePath })
            {
                byte[] data = ModContent.GetFileBytes(path + ".rawimg");
                if (data is null || data.Length < 12) throw new Exception("Missing packed texture: " + path);
                int width = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(4, 4));
                int height = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(8, 4));
                if (width != 200 || height != 200 || data.Length != 12 + width * height * 4)
                    throw new Exception("Unexpected packed dimensions: " + path);
                Console.WriteLine($"TEXTURE_ASSET_PASS: {path} {width}x{height}");
            }
            if (Math.Abs(FranShiFuVisuals.TextureDiagonal - MathF.Sqrt(80000f)) > .001f ||
                !float.IsFinite(FranShiFuVisuals.TextureScale) || FranShiFuVisuals.TextureScale <= 0f ||
                !float.IsFinite(FranShiFuVisuals.GripInset) || FranShiFuVisuals.BladeLengthFromGripPixels <= 0f)
                throw new Exception("Sword sizing failed on packaged resources");
            Console.WriteLine("SHIFU_TEXTURE_RUNTIME_PASS: all three packed assets and server-side sword geometry");
            Environment.Exit(0);
        }
        catch (Exception error)
        {
            Console.WriteLine("SHIFU_TEXTURE_RUNTIME_FAIL: " + error);
            Environment.Exit(9);
        }
    }
}
