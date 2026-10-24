using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using FranMod.Core.Graphics;
using FranMod.Content.Projectiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

class Render
{
    const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl)] static extern int SDL_Init(uint flags);
    [DllImport("FNA3D.dll", CallingConvention = CallingConvention.Cdecl)] static extern uint FNA3D_PrepareWindowAttributes();
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl)] static extern IntPtr SDL_CreateWindow(string name, int x, int y, int w, int h, uint flags);
    sealed class Services : IServiceProvider, IGraphicsDeviceService
    {
        public GraphicsDevice GraphicsDevice { get; set; }
        public event EventHandler<EventArgs> DeviceCreated { add {} remove {} }
        public event EventHandler<EventArgs> DeviceDisposing { add {} remove {} }
        public event EventHandler<EventArgs> DeviceReset { add {} remove {} }
        public event EventHandler<EventArgs> DeviceResetting { add {} remove {} }
        public object GetService(Type t) => t == typeof(IGraphicsDeviceService) ? this : null;
    }
    static void Main()
    {
        AssemblyLoadContext.Default.Resolving += (_, n) => {
            string path = Directory.EnumerateFiles(@"F:\SteamLibrary\steamapps\common\tModLoader\Libraries", n.Name + ".dll", SearchOption.AllDirectories).FirstOrDefault();
            if (path == null && File.Exists("lib/" + n.Name + ".dll")) path = Path.GetFullPath("lib/" + n.Name + ".dll");
            return path == null ? null : AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
        };
        Run();
    }
    [MethodImpl(MethodImplOptions.NoInlining)] static void Run()
    {
        typeof(Terraria.Program).GetField("SavePath", Static).SetValue(null, Path.GetTempPath());
        typeof(Terraria.Program).GetProperty("SavePathShared", Static).SetValue(null, Path.GetTempPath());
        SDL_Init(0x20);
        IntPtr window = SDL_CreateWindow("ShiFu hidden material validation", 0, 0, 720, 640, 8 | FNA3D_PrepareWindowAttributes());
        if (window == IntPtr.Zero) throw new Exception("No hidden graphics window");
        using var device = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.Reach,
            new PresentationParameters { BackBufferWidth = 720, BackBufferHeight = 640, DeviceWindowHandle = window });
        using var content = new ContentManager(new Services { GraphicsDevice = device }, Directory.GetCurrentDirectory());
        using var tml = new ContentManager(new Services { GraphicsDevice = device }, @"F:\SteamLibrary\steamapps\common\tModLoader\Content");
        using var batch = new SpriteBatch(device);
        Terraria.Main.spriteBatch = batch;
        Terraria.Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(device);
        Terraria.Main.screenWidth = 720; Terraria.Main.screenHeight = 640;
        Terraria.Main.dedServ = false;
        typeof(Terraria.Main).GetField("PixelShaderRef", Static).SetValue(null, new Ref<Effect>(tml.Load<Effect>("PixelShader")));
        typeof(FranShiFuSlashRenderer).GetField("material", Static).SetValue(null, content.Load<Effect>("Effects/FranShiFuSlash"));
        foreach (var name in new[] { "Flow", "Erosion" })
        {
            using var file = File.OpenRead("Assets/OriginalVFX/ShiFu/" + name + ".png");
            typeof(FranShiFuSlashRenderer).GetField(name.ToLowerInvariant(), Static).SetValue(null, Texture2D.FromStream(device, file));
        }
        using var pixel = new Texture2D(device, 1, 1); pixel.SetData(new[] { Color.White });
        using var target = new RenderTarget2D(device, 720, 640);
        Directory.CreateDirectory("bin/ShiFuSlashValidation/frames");
        var swing = new ShiFuSwingRebuild();
        typeof(Terraria.ModLoader.ModType<Projectile, Terraria.ModLoader.ModProjectile>)
            .GetProperty("Entity", Instance).SetValue(swing, new Projectile());
        var projectile = swing.Projectile;
        var compute = typeof(ShiFuSwingRebuild).GetMethod("ComputePose", Instance);
        float[] angles = new float[FranShiFuSlashRenderer.Samples];
        int checks = 0;
        foreach (int facing in new[] { -1, 1 })
        foreach (int style in new[] { 0, 1, 2 })
        foreach (float progress in new[] { .19f, .28f, .36f, .48f, .60f, .70f, .82f, .94f })
        foreach (bool day in new[] { false, true })
        {
            projectile.ai[0] = style; projectile.velocity = Vector2.UnitX;
            Terraria.Main.GlobalTimeWrappedHourly = progress * 1.37f;
            typeof(ShiFuSwingRebuild).GetField("facing", Instance).SetValue(swing, facing);
            float start = style == 0 ? .16f : style == 1 ? .11f : .23f;
            float end = style == 0 ? .60f : style == 1 ? .84f : .56f;
            float head = Math.Clamp(progress, start, end);
            float tail = Math.Max(start, head - (style == 1 ? .24f : .20f));
            tail = MathHelper.Lerp(tail, head, Math.Clamp((progress - end) / .19f, 0, 1));
            for (int i = 0; i < angles.Length; i++)
            {
                object[] args = { MathHelper.Lerp(head, tail, i / (float)(angles.Length - 1)), 0f, 0f, 0f };
                compute.Invoke(swing, args); angles[i] = (float)args[1];
            }
            object[] pose = { progress, 0f, 0f, 0f }; compute.Invoke(swing, pose);
            float strength = (float)pose[3];
            var background = day ? new Color(110, 152, 157) : new Color(17, 23, 38);
            device.SetRenderTarget(target); device.Clear(background);
            batch.Begin();
            FranShiFuSlashRenderer.Draw(new Vector2(360, 320), angles, 240, new Color(78, 210, 239), strength, style);
            // A white marker drawn after the material verifies SpriteBatch/shader restoration.
            batch.Draw(pixel, new Rectangle(5, 5, 6, 6), Color.White); batch.End();
            Color[] pixels = new Color[720 * 640]; target.GetData(pixels);
            if (pixels[7 * 720 + 7] != Color.White) throw new Exception("SpriteBatch state leaked");
            if (pixels[320 * 720 + 360] != background) throw new Exception("Arc filled player center");
            if (strength > .25f && Math.Abs(angles[0] - angles[^1]) > .15f &&
                pixels.Count(p => p != background && p != Color.White) < 150)
                throw new Exception("Visible swing has an empty material");
            for (int y = 0; y < 640; y++)
            for (int x = 0; x < 720; x++)
                if ((x > 20 || y > 20) && Vector2.DistanceSquared(new(x, y), new(360, 320)) > 243 * 243 && pixels[y * 720 + x] != background)
                    throw new Exception("Unbounded triangle");
            string name = $"bin/ShiFuSlashValidation/frames/{style}-{facing}-{progress:0.00}-{(day ? "day" : "night")}.png";
            using var output = File.Create(name); target.SaveAsPng(output, 720, 640);
            checks++;
        }
        device.SetRenderTarget(null);
        Console.WriteLine($"PASS: {checks} hidden GPU frames; production renderer and ComputePose, 3 swings, both facings, day/night, state restoration and bounded geometry. Not an in-game playtest.");
        // Freeze three real poses while advancing material time to show the surface flow clearly.
        Directory.CreateDirectory("bin/ShiFuSlashValidation/animation");
        using var animated = new RenderTarget2D(device, 1260, 480);
        using var swordStream = File.OpenRead("Textures/大剑/师父.png");
        using var sword = Texture2D.FromStream(device, swordStream);
        Terraria.Main.screenWidth = 1260; Terraria.Main.screenHeight = 480;
        for (int frame = 0; frame < 36; frame++)
        {
            Terraria.Main.GlobalTimeWrappedHourly = frame / 24f;
            device.SetRenderTarget(animated); device.Clear(new Color(17, 23, 38));
            batch.Begin();
            for (int style = 0; style < 3; style++)
            {
                projectile.ai[0] = style;
                typeof(ShiFuSwingRebuild).GetField("facing", Instance).SetValue(swing, 1);
                float head = style == 0 ? .28f : .36f;
                float start = style == 0 ? .16f : style == 1 ? .11f : .23f;
                float tail = Math.Max(start, head - (style == 1 ? .24f : .20f));
                for (int i = 0; i < angles.Length; i++)
                {
                    object[] args = { MathHelper.Lerp(head, tail, i / (float)(angles.Length - 1)), 0f, 0f, 0f };
                    compute.Invoke(swing, args); angles[i] = (float)args[1];
                }
                Vector2 pivot = new(210 + style * 420, 260);
                FranShiFuSlashRenderer.Draw(pivot, angles, 165, new Color(115, 222, 255), 1, style);
                float diagonal = MathF.Sqrt(sword.Width * sword.Width + sword.Height * sword.Height);
                float scale = 165 / (diagonal * (1 - .14142136f));
                Vector2 grip = angles[0].ToRotationVector2() * (diagonal * .14142136f * scale);
                batch.Draw(sword, pivot - grip, null, Color.White, angles[0] + MathHelper.PiOver4,
                    new Vector2(0, sword.Height), scale, SpriteEffects.None, 0);
            }
            batch.End();
            using var output = File.Create($"bin/ShiFuSlashValidation/animation/{frame:00}.png");
            animated.SaveAsPng(output, 1260, 480);
        }
        device.SetRenderTarget(null);
        if (File.ReadAllBytes("bin/ShiFuSlashValidation/animation/00.png").SequenceEqual(File.ReadAllBytes("bin/ShiFuSlashValidation/animation/18.png")))
            throw new Exception("Material time does not affect production GPU output");
        Console.WriteLine("PASS: 36 animated material frames; time changes the GPU output at fixed production poses.");
        var package = (Terraria.ModLoader.Core.TmodFile)Activator.CreateInstance(typeof(Terraria.ModLoader.Core.TmodFile),
            Instance, null, new object[] { Path.GetFullPath("bin/ShiFuSlashValidation/Sandbox/Mods/Fran.tmod"), null, null }, null);
        using (package.Open())
        {
            foreach (string asset in new[] { "Fran.dll", "Effects/FranShiFuSlash.xnb" })
            {
                string disk = asset == "Fran.dll" ? "bin/Debug/net8.0/Fran.dll" : asset;
                if (!package.GetBytes(asset).SequenceEqual(File.ReadAllBytes(disk)))
                    throw new Exception("Package differs from current source build: " + asset);
            }
            foreach (string name in new[] { "Flow", "Erosion" })
                if (!package.HasFile("Assets/OriginalVFX/ShiFu/" + name + ".rawimg"))
                    throw new Exception("Noise map missing in package: " + name);
        }
        Console.WriteLine("PASS: current assembly, compiled material and both runtime noise maps packaged.");
    }
}
