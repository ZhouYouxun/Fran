using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

class Render
{
    const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const string Tml = @"F:\SteamLibrary\steamapps\common\tModLoader";
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl)] static extern int SDL_Init(uint flags);
    [DllImport("FNA3D.dll", CallingConvention = CallingConvention.Cdecl)] static extern uint FNA3D_PrepareWindowAttributes();
    [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl)] static extern IntPtr SDL_CreateWindow(string name, int x, int y, int w, int h, uint flags);
    sealed class Services : IGraphicsDeviceService
    {
        public GraphicsDevice GraphicsDevice { get; set; }
        public event EventHandler<EventArgs> DeviceCreated { add {} remove {} }
        public event EventHandler<EventArgs> DeviceDisposing { add {} remove {} }
        public event EventHandler<EventArgs> DeviceReset { add {} remove {} }
        public event EventHandler<EventArgs> DeviceResetting { add {} remove {} }
    }
    static void Main()
    {
        AssemblyLoadContext.Default.Resolving += (_, n) => {
            string file = Directory.EnumerateFiles(Tml + "/Libraries", n.Name + ".dll", SearchOption.AllDirectories).FirstOrDefault();
            if (file == null && File.Exists("lib/" + n.Name + ".dll")) file = Path.GetFullPath("lib/" + n.Name + ".dll");
            return file == null ? null : AssemblyLoadContext.Default.LoadFromAssemblyPath(file);
        };
        Run();
    }
    [MethodImpl(MethodImplOptions.NoInlining)] static void Run()
    {
        typeof(Terraria.Program).GetField("SavePath", Static).SetValue(null, Path.GetTempPath());
        typeof(Terraria.Program).GetProperty("SavePathShared", Static).SetValue(null, Path.GetTempPath());
        SDL_Init(0x20);
        IntPtr window = SDL_CreateWindow("Mind core hidden offline rendering", 0, 0, 1100, 630, 8 | FNA3D_PrepareWindowAttributes());
        if (window == IntPtr.Zero) throw new Exception("No hidden GPU window");
        using var device = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.Reach,
            new PresentationParameters { BackBufferWidth = 1100, BackBufferHeight = 630, DeviceWindowHandle = window });
        using var batch = new SpriteBatch(device);
        Terraria.Main.spriteBatch = batch;
        Terraria.Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(device);
        Terraria.Main.screenWidth = 1100; Terraria.Main.screenHeight = 630;
        Terraria.Main.dedServ = false;
        Terraria.Main.myPlayer = 0;
        Terraria.Main.player[0] = new Player { gravDir = 1f };
        Terraria.Main.instance = (Terraria.Main)RuntimeHelpers.GetUninitializedObject(typeof(Terraria.Main));
        var service = typeof(Game).GetFields(Instance).Single(f => f.FieldType == typeof(IGraphicsDeviceService));
        service.SetValue(Terraria.Main.instance, new Services { GraphicsDevice = device });
        SetPrimitive("MainPositions", new Vector2[2000]);
        SetPrimitive("MainVertices", new VertexPosition2DColorTexture[6144]);
        SetPrimitive("MainIndices", new short[16384]);
        using var vertices = new DynamicVertexBuffer(device, VertexPosition2DColorTexture.VertexDeclaration2D, 6144, BufferUsage.WriteOnly);
        using var indices = new DynamicIndexBuffer(device, IndexElementSize.SixteenBits, 16384, BufferUsage.WriteOnly);
        SetPrimitive("VertexBuffer", vertices); SetPrimitive("IndexBuffer", indices);
        using var effect = new Effect(device, File.ReadAllBytes("Assets/AutoloadedEffects/Shaders/Primitives/FranTelekineticCoreShader.fxc"));
        var shader = (ManagedShader)Activator.CreateInstance(typeof(ManagedShader), Instance, null,
            new object[] { "Fran.FranTelekineticCoreShader", new Ref<Effect>(effect) }, null);
        using var bloomFile = File.OpenRead("Particles/CL_BloomCircle.png");
        using var bloom = Texture2D.FromStream(device, bloomFile);
        using var noiseFile = File.OpenRead("Assets/TrailNoiseReference/FranTrailBlobNoise.png");
        using var noise = Texture2D.FromStream(device, noiseFile);
        using var pixel = new Texture2D(device, 1, 1); pixel.SetData(new[] { Color.White });
        using var target = new RenderTarget2D(device, 1100, 630);
        var fran = Assembly.Load("Fran");
        var bloomType = fran.GetType("FranMod.Particles.FranMindCoreBloomParticle", true);
        var create = bloomType.GetMethod("CreatePair", Static);
        var draw = bloomType.GetMethod("DrawBloom", Instance);
        var step = bloomType.BaseType.GetMethod("Step", Instance);
        var pos = bloomType.BaseType.GetField("Position", Instance);
        var time = bloomType.BaseType.GetField("Time", Instance);
        var geometry = fran.GetType("FranMod.Core.Graphics.FranProjectileTrailRenderer").GetMethod("BuildTelekineticCorePoints", Static);
        var particles = new List<object>();
        Directory.CreateDirectory("bin/MindCoreBloomValidation/frames");
        int checks = 0;
        // Warm the particle queue first; the final two seconds loop without a spawn discontinuity.
        for (int frame = 0; frame < 108; frame++)
        {
            Terraria.Main.GlobalTimeWrappedHourly = frame / 60f;
            for (int i = particles.Count - 1; i >= 0; i--)
                if (!(bool)step.Invoke(particles[i], null)) particles.RemoveAt(i);
            float cycle = (frame % 72) / 72f;
            Vector2 center = new(250 + cycle * 600, 0);
            Vector2 velocity = new(12, 0);
            for (int sample = 0; sample < 3; sample++)
            {
                object[] args = { center - velocity * sample, velocity, 20f, 1f, null, null };
                create.Invoke(null, args); particles.Add(args[4]); particles.Add(args[5]);
            }
            if (particles.Count > 36) throw new Exception("Unexpected particle budget");
            // Deterministic lifetime and position checks use the actual production Step method.
            foreach (object p in particles)
            {
                Vector2 pPosition = (Vector2)pos.GetValue(p);
                if (!float.IsFinite(pPosition.X) || MathF.Abs(pPosition.Y) > 0.001f)
                    throw new Exception("Light samples deviated from the straight path");
                if ((int)time.GetValue(p) >= 7) throw new Exception("Particle outlived its bloom");
                checks++;
            }
            device.SetRenderTarget(target); device.Clear(new Color(15, 23, 39));
            batch.Begin();
            batch.Draw(pixel, new Rectangle(0, 450, 1100, 180), new Color(116, 153, 166));
            batch.Draw(pixel, new Rectangle(0, 240, 1100, 2), new Color(35, 53, 74));
            batch.End();
            for (int row = 0; row < 3; row++)
            {
                float zoom = row == 1 ? 2.5f : 1f;
                float y = row == 0 ? 170 : row == 1 ? 355 : 555;
                // The unchanged trail uses the production distance sampler, compiled shader,
                // Luminance geometry, width and colors. No separate illustration of the effect.
                var projectile = new Projectile { width = 12, height = 12, scale = 1,
                    velocity = velocity, oldPos = new Vector2[24] };
                projectile.Center = center + new Vector2(0, y);
                for (int i = 0; i < 24; i++)
                    projectile.oldPos[i] = projectile.position - velocity * (i + 1);
                if (row == 1)
                {
                    projectile.Center = new Vector2(730, y);
                    for (int i = 0; i < 24; i++)
                        projectile.oldPos[i] = projectile.position - velocity * (i + 1) * zoom;
                }
                object[] geoArgs = { projectile, null };
                var points = (List<Vector2>)geometry.Invoke(null, geoArgs);
                shader.TrySetParameter("uBodyColor", new Color(28, 145, 235));
                shader.TrySetParameter("uEdgeColor", new Color(55, 178, 235));
                shader.TrySetParameter("uCoreColor", Color.White);
                shader.TrySetParameter("uOpacity", 1f);
                shader.TrySetParameter("uDistanceProgress", (float[])geoArgs[1]);
                shader.TrySetParameter("uSeed", 0f);
                shader.TrySetParameter("globalTime", frame / 60f);
                shader.SetTexture(noise, 1, SamplerState.LinearWrap);
                device.BlendState = BlendState.AlphaBlend;
                PrimitiveRenderer.RenderTrail(points, new PrimitiveSettings(t => MathHelper.Lerp(10f, 2f, t) * zoom,
                    _ => Color.White, Smoothen: false, Pixelate: false, Shader: shader), points.Count);
                batch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
                foreach (object p in particles)
                {
                    Vector2 original = (Vector2)pos.GetValue(p);
                    if (row == 1)
                    {
                        // Scale via SpriteBatch transform, preserving the production particle dimensions.
                        continue;
                    }
                    pos.SetValue(p, original + new Vector2(0, y));
                    draw.Invoke(p, new object[] { batch, bloom });
                    pos.SetValue(p, original);
                }
                batch.End();
                if (row == 1)
                {
                    Matrix transform = Matrix.CreateTranslation(-center.X, 0, 0) * Matrix.CreateScale(zoom) *
                        Matrix.CreateTranslation(730, y, 0);
                    batch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                        DepthStencilState.None, RasterizerState.CullNone, null, transform);
                    foreach (object p in particles) draw.Invoke(p, new object[] { batch, bloom });
                    batch.End();
                }
            }
            if (frame >= 36)
            {
                using var output = File.Create($"bin/MindCoreBloomValidation/frames/{frame - 36:000}.png");
                target.SaveAsPng(output, 1100, 630);
            }
        }
        device.SetRenderTarget(null);
        Console.WriteLine($"PASS: 72 GPU frames, {checks} particle lifetime/path checks, actual Fran.dll CreatePair/Step/DrawBloom; unchanged compiled trail material and production path sampler. No in-game playtest.");
        string packagePath = Path.GetFullPath("../../Mods/fran.tmod");
        var package = (Terraria.ModLoader.Core.TmodFile)Activator.CreateInstance(typeof(Terraria.ModLoader.Core.TmodFile),
            Instance, null, new object[] { packagePath, null, null }, null);
        using (package.Open())
        {
            if (!package.GetBytes("Fran.dll").SequenceEqual(File.ReadAllBytes("bin/Debug/net8.0/Fran.dll")))
                throw new Exception("Installed assembly differs from the current source build");
            if (!package.HasFile("Particles/CL_BloomCircle.rawimg"))
                throw new Exception("Installed package has no soft bloom texture");
        }
        Console.WriteLine("PASS: installed fran.tmod contains the current assembly and soft bloom texture.");
    }
    static void SetPrimitive(string name, object value) => typeof(PrimitiveRenderer).GetField(name, Static).SetValue(null, value);
}
