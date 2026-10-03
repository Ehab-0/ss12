using System.Linq;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Console;
using Robust.Shared.Timing;

namespace Content.Client.Render3D;

/// <summary>Quality presets as console commands, and the automatic step-down for machines that cannot keep up.</summary>
public sealed partial class Render3DController
{
    [Dependency] private IGameTiming _timing = default!;

    // auto quality: after a warm-up, five one-second frame rate samples must all be below the limit
    private const float AutoWarmupSeconds = 10f;
    private const int AutoSamples = 5;
    private float _autoSeconds;
    private float _autoNextSample;
    private readonly List<double> _autoRecent = new();

    private void InitializeQuality()
    {
        _console.RegisterCommand("render3d_quality", "Show or set the 3D graphics quality preset", "render3d_quality [low|medium|high]",
            (shell, _, args) =>
            {
                if (args.Length == 0)
                {
                    shell.WriteLine($"quality: {Render3DQuality.LevelName(_cfg.GetCVar(CCVars.Render3DQuality))}");
                    return;
                }

                if (!Render3DQuality.TryParseLevel(args[0], out var level))
                {
                    shell.WriteError("usage: render3d_quality [low|medium|high]");
                    return;
                }

                Render3DQuality.Apply(_cfg, level);
                shell.WriteLine($"quality: {Render3DQuality.LevelName(level)}");
            });

        _console.RegisterCommand("render3d_fx", "List, or switch, the 3D visual effects", "render3d_fx [<effect> on|off]",
            (shell, _, args) =>
            {
                if (args.Length == 0)
                {
                    foreach (var effect in Render3DQuality.Effects)
                        shell.WriteLine($"{effect.Name,-10} {(_cfg.GetCVar(effect.Cvar) ? "on" : "off")}");
                    shell.WriteLine($"quality: {Render3DQuality.LevelName(_cfg.GetCVar(CCVars.Render3DQuality))}");
                    return;
                }

                if (args.Length != 2 || Render3DQuality.Find(args[0]) is not { } found || args[1] is not ("on" or "off"))
                {
                    shell.WriteError("usage: render3d_fx [<effect> on|off]  (effect names: " + string.Join(", ", Render3DQuality.Effects.Select(e => e.Name)) + ")");
                    return;
                }

                Render3DQuality.SetEffect(_cfg, found, args[1] == "on");
            });
    }

    private void UpdateAutoQuality(float frameTime)
    {
        var focused = _clyde.MainWindow.IsFocused || DevForceCapture;
        if (!_cfg.GetCVar(CCVars.Render3DAutoQuality) || !Active || !focused || _control == null)
        {
            _autoSeconds = 0;
            _autoNextSample = 0;
            _autoRecent.Clear();
            return;
        }

        _autoSeconds += frameTime;
        if (_autoSeconds < AutoWarmupSeconds || _autoSeconds < _autoNextSample)
            return;

        _autoNextSample = _autoSeconds + 1f;
        _autoRecent.Add(_timing.FramesPerSecondAvg);
        if (_autoRecent.Count < AutoSamples)
            return;

        var limit = _cfg.GetCVar(CCVars.Render3DAutoQualityFps);
        var slow = _autoRecent.All(fps => fps < limit);
        _autoRecent.RemoveAt(0);
        if (!slow)
            return;

        StepQualityDown();

        // give the lower setting time to settle before judging it
        _autoSeconds = 0;
        _autoNextSample = 0;
        _autoRecent.Clear();
    }

    private void StepQualityDown()
    {
        var level = _cfg.GetCVar(CCVars.Render3DQuality);
        string message;

        if (level is > Render3DQuality.Low and <= Render3DQuality.High)
        {
            Render3DQuality.Apply(_cfg, level - 1);
            message = Loc.GetString("render3d-quality-lowered", ("level", Render3DQuality.LevelName(level - 1)));
        }
        else if (level == Render3DQuality.Low && _cfg.GetCVar(CCVars.Render3DRenderScale) > 0.55f)
        {
            // last resort once everything is off: draw fewer pixels
            var scale = _cfg.GetCVar(CCVars.Render3DRenderScale) > 0.8f ? 0.75f : 0.5f;
            _cfg.SetCVar(CCVars.Render3DRenderScale, scale);
            message = Loc.GetString("render3d-quality-scale", ("scale", scale.ToString("0.00")));
        }
        else
        {
            return; // custom settings are the player's choice; nothing left to lower
        }

        _control?.ShowNotice(message);
    }
}
