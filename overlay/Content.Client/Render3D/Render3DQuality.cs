using Content.Shared.CCVar;
using Robust.Shared.Configuration;

namespace Content.Client.Render3D;

/// <summary>
///     The visual effects of the 3D view and the quality presets that switch them together. Every effect is an
///     independent on/off setting (so older machines can turn individual ones off); a preset is just a named
///     combination. Toggling one effect by hand makes the quality "Custom" unless the result happens to equal a preset.
/// </summary>
public static class Render3DQuality
{
    public const int Low = 0;
    public const int Medium = 1;
    public const int High = 2;
    public const int Custom = 3;

    /// <summary>Supersampling factor of the Low / Medium / High presets.</summary>
    public static readonly float[] Supersample = { 1.0f, 1.5f, 2.0f };

    /// <summary>Layers of thickness (sprite stacking) of the Low / Medium / High presets.</summary>
    public static readonly int[] ThicknessLayers = { 0, 3, 4 };

    /// <param name="Shape">Part of the "shape of things" group (lean and thickness per category) in the settings window.</param>
    public sealed record Effect(string Name, CVarDef<bool> Cvar, bool OnLow, bool OnMedium, bool OnHigh, string LocId, bool Shape = false);

    /// <summary>All effects, with whether each is on in the Low / Medium / High presets.</summary>
    public static readonly Effect[] Effects =
    {
        new("bloom", CCVars.Render3DFxBloom, false, false, true, "render3d-fx-bloom"),
        new("fxaa", CCVars.Render3DFxFxaa, false, true, true, "render3d-fx-fxaa"),
        new("ao", CCVars.Render3DFxAo, false, false, true, "render3d-fx-ao"),
        new("surface", CCVars.Render3DFxSurface, false, true, true, "render3d-fx-surface"),
        new("ambient", CCVars.Render3DFxAmbient, false, true, true, "render3d-fx-ambient"),
        new("shadows", CCVars.Render3DFxShadows, false, true, true, "render3d-fx-shadows"),
        new("outline", CCVars.Render3DFxOutline, false, false, true, "render3d-fx-outline"),
        new("sharp", CCVars.Render3DFxSharp, false, true, true, "render3d-fx-sharp"),
        new("grade", CCVars.Render3DFxGrade, false, true, true, "render3d-fx-grade"),
        new("haze", CCVars.Render3DFxHaze, false, false, true, "render3d-fx-haze"),
        new("sky", CCVars.Render3DFxSky, false, true, true, "render3d-fx-sky"),
        new("fixtures", CCVars.Render3DFxFixtures, false, true, true, "render3d-fx-fixtures"),
        new("head_bob", CCVars.Render3DFxHeadBob, false, false, false, "render3d-fx-head-bob"),
        new("item_lift", CCVars.Render3DFxItemLift, false, true, true, "render3d-fx-item-lift", true),
        new("item_lean", CCVars.Render3DFxItemLean, false, true, true, "render3d-fx-item-lean", true),
        new("item_thick", CCVars.Render3DFxItemThick, false, true, true, "render3d-fx-item-thick", true),
        new("char_lean", CCVars.Render3DFxCharLean, false, true, true, "render3d-fx-char-lean", true),
        new("char_thick", CCVars.Render3DFxCharThick, false, false, true, "render3d-fx-char-thick", true),
        new("object_lean", CCVars.Render3DFxObjectLean, false, false, true, "render3d-fx-object-lean", true),
        new("object_thick", CCVars.Render3DFxObjectThick, false, false, true, "render3d-fx-object-thick", true),
    };

    public static string LevelName(int level) => level switch
    {
        Low => "low",
        Medium => "medium",
        High => "high",
        _ => "custom",
    };

    public static bool TryParseLevel(string name, out int level)
    {
        switch (name.ToLowerInvariant())
        {
            case "low": level = Low; return true;
            case "medium": level = Medium; return true;
            case "high": level = High; return true;
            default: level = Custom; return false;
        }
    }

    public static Effect? Find(string name)
    {
        foreach (var effect in Effects)
        {
            if (effect.Name == name)
                return effect;
        }

        return null;
    }

    private static bool Preset(Effect effect, int level) => level switch
    {
        Low => effect.OnLow,
        Medium => effect.OnMedium,
        _ => effect.OnHigh,
    };

    /// <summary>Switches every effect (and the cost limits) to a preset.</summary>
    public static void Apply(IConfigurationManager cfg, int level)
    {
        if (level is < Low or > High)
            return;

        foreach (var effect in Effects)
            cfg.SetCVar(effect.Cvar, Preset(effect, level));

        cfg.SetCVar(CCVars.Render3DSupersample, Supersample[level]);
        cfg.SetCVar(CCVars.Render3DThicknessLayers, ThicknessLayers[level]);

        // the cheapest preset also draws fewer things, and looks less far
        cfg.SetCVar(CCVars.Render3DBillboardCap, level == Low ? 200 : 400);
        cfg.SetCVar(CCVars.Render3DGroundRadius, level == Low ? 16 : 24);
        cfg.SetCVar(CCVars.Render3DQuality, level);
    }

    /// <summary>The preset the current effect settings equal, or <see cref="Custom"/>.</summary>
    public static int Detect(IConfigurationManager cfg)
    {
        for (var level = Low; level <= High; level++)
        {
            var match = Math.Abs(cfg.GetCVar(CCVars.Render3DSupersample) - Supersample[level]) < 0.01f
                && cfg.GetCVar(CCVars.Render3DThicknessLayers) == ThicknessLayers[level];
            foreach (var effect in Effects)
            {
                if (cfg.GetCVar(effect.Cvar) != Preset(effect, level))
                {
                    match = false;
                    break;
                }
            }

            if (match)
                return level;
        }

        return Custom;
    }

    /// <summary>Turns one effect on or off by hand and updates the quality label (a preset name or Custom).</summary>
    public static void SetEffect(IConfigurationManager cfg, Effect effect, bool value)
    {
        cfg.SetCVar(effect.Cvar, value);
        cfg.SetCVar(CCVars.Render3DQuality, Detect(cfg));
    }
}
