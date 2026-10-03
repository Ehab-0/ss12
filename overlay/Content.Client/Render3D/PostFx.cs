using System.Numerics;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client.Render3D;

/// <summary>
///     The final image processing of the 3D view: bloom (bright parts blurred at a quarter of the resolution and added
///     back), depth-based ambient occlusion, depth-edge smoothing, tone mapping, vignette and dithering. Everything is
///     switched by settings; when nothing is on the caller skips this class and presents the image directly.
/// </summary>
public sealed class PostFx : IDisposable
{
    private static readonly ProtoId<ShaderPrototype> ExtractShader = "Render3DBloomExtract";
    private static readonly ProtoId<ShaderPrototype> BlurShader = "Render3DBlur";
    private static readonly ProtoId<ShaderPrototype> PostShader = "Render3DPost";

    /// <summary>Bloom is computed at 1 / this of the scene resolution (times the supersampling, so it looks the same).</summary>
    private const int BloomDivisor = 4;

    private readonly IClyde _clyde;
    private readonly IPrototypeManager _protos;

    private IRenderTexture? _bloomA;
    private IRenderTexture? _bloomB;

    // Two blur instances: a shader instance's parameters are read when the draw runs, not when it is recorded, so each
    // direction needs its own instance.
    private ShaderInstance? _extract;
    private ShaderInstance? _blurH;
    private ShaderInstance? _blurV;
    private ShaderInstance? _post;
    private Vector2i _blurFor;

    public readonly record struct Settings(float Bloom, float Ao, float Edge, float Grade, bool Dither)
    {
        public bool AnyActive => Bloom > 0f || Ao > 0f || Edge > 0f || Grade > 0f;
    }

    public PostFx(IClyde clyde, IPrototypeManager protos)
    {
        _clyde = clyde;
        _protos = protos;
    }

    public static Settings Read(IConfigurationManager cfg)
    {
        var grade = cfg.GetCVar(CCVars.Render3DFxGrade);
        return new Settings(
            cfg.GetCVar(CCVars.Render3DFxBloom) ? 0.6f : 0f,
            cfg.GetCVar(CCVars.Render3DFxAo) ? 1f : 0f,
            cfg.GetCVar(CCVars.Render3DFxFxaa) ? 1f : 0f,
            grade ? 1f : 0f,
            grade);
    }

    /// <summary>
    ///     Processes <paramref name="composite"/> (colour) using <paramref name="scene"/> (raw raymarch output whose alpha
    ///     is the view depth) and draws the result into <paramref name="destination"/> on the screen.
    /// </summary>
    public void Draw(DrawingHandleScreen screen, Texture composite, Texture scene, Vector2i sceneSize, UIBox2 destination, in Settings s, float supersample)
    {
        _post ??= _protos.Index(PostShader).InstanceUnique();

        // (when the caller already drew into another target it has called PrepareBloom before binding it)
        if (s.Bloom > 0f && !_bloomReady)
            RenderBloom(screen, composite, sceneSize, supersample);

        _bloomReady = false;

        var texel = new Vector2(1f / sceneSize.X, 1f / sceneSize.Y);
        _post.SetParameter("depthTex", scene);
        if (s.Bloom > 0f && _bloomA != null)
            _post.SetParameter("bloomTex", _bloomA.Texture);
        _post.SetParameter("texel", texel);
        _post.SetParameter("fx1", new Vector4(s.Bloom, s.Ao, s.Edge, s.Grade));
        _post.SetParameter("fx2", new Vector4(s.Dither ? 1f : 0f, MathF.Max(1.5f, sceneSize.Y / 540f), 0f, 0f));

        screen.UseShader(_post);
        screen.DrawTextureRect(composite, destination);
        screen.UseShader(null);
    }

    private bool _bloomReady;

    /// <summary>Renders the bloom now, so a following <see cref="Draw"/> inside another render target does not have to.</summary>
    public void PrepareBloom(DrawingHandleScreen screen, Texture composite, Vector2i sceneSize, in Settings s, float supersample)
    {
        if (s.Bloom <= 0f)
            return;

        RenderBloom(screen, composite, sceneSize, supersample);
        _bloomReady = true;
    }

    private void RenderBloom(DrawingHandleScreen screen, Texture composite, Vector2i sceneSize, float supersample)
    {
        var divisor = Math.Max(1, (int) MathF.Round(BloomDivisor * Math.Max(1f, supersample)));
        var small = new Vector2i(Math.Max(1, sceneSize.X / divisor), Math.Max(1, sceneSize.Y / divisor));
        if (_bloomA == null || _bloomB == null || _blurFor != small)
        {
            _bloomA?.Dispose();
            _bloomB?.Dispose();
            var format = new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba16F);
            var sample = new TextureSampleParameters { Filter = true };
            _bloomA = _clyde.CreateRenderTarget(small, format, sample, "render3d-bloom-a");
            _bloomB = _clyde.CreateRenderTarget(small, format, sample, "render3d-bloom-b");
            _blurFor = small;

            _blurH = _protos.Index(BlurShader).InstanceUnique();
            _blurH.SetParameter("dir", new Vector2(1f / small.X, 0f));
            _blurV = _protos.Index(BlurShader).InstanceUnique();
            _blurV.SetParameter("dir", new Vector2(0f, 1f / small.Y));
        }

        _extract ??= _protos.Index(ExtractShader).InstanceUnique();
        _extract.SetParameter("texel", new Vector2(1f / sceneSize.X, 1f / sceneSize.Y));
        _extract.SetParameter("threshold", 0.8f);

        var rect = new UIBox2(Vector2.Zero, small);
        var a = _bloomA;
        var b = _bloomB;

        screen.RenderInRenderTarget(a, () =>
        {
            screen.UseShader(_extract);
            screen.DrawTextureRect(composite, rect);
            screen.UseShader(null);
        }, Color.Black);

        screen.RenderInRenderTarget(b, () =>
        {
            screen.UseShader(_blurH);
            screen.DrawTextureRect(a.Texture, rect);
            screen.UseShader(null);
        }, Color.Black);

        screen.RenderInRenderTarget(a, () =>
        {
            screen.UseShader(_blurV);
            screen.DrawTextureRect(b.Texture, rect);
            screen.UseShader(null);
        }, Color.Black);
    }

    public void Dispose()
    {
        _bloomA?.Dispose();
        _bloomB?.Dispose();
        _bloomA = null;
        _bloomB = null;
        _blurFor = default;
    }
}
