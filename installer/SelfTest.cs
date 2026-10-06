using System.Text;
using System.Text.Json;

namespace Ss12;

/// <summary>
///     The installer's own tests. They run on throw-away fixture codebases (small files that contain the original
///     upstream code the edits anchor on), so they need no git history, no engine and no build:
///     install, idempotency, update, uninstall giving back byte-identical files, CRLF files, and a required edit whose
///     anchor is missing (must change nothing).
/// </summary>
public static class SelfTest
{
    private static int _failures;

    public static bool Run(string? source)
    {
        _failures = 0;
        var report = new Report();
        var located = SourceInfo.Locate(source, report);
        if (located == null)
            return false;

        Console.WriteLine($"overlay source: {located.Root}");
        var temp = Path.Combine(Path.GetTempPath(), "ss12-selftest-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            foreach (var crlf in new[] { false, true })
            {
                Console.WriteLine($"\n-- fixture with {(crlf ? "CRLF" : "LF")} line endings");
                var dir = Path.Combine(temp, crlf ? "crlf" : "lf");
                Fixture.Create(dir, crlf);
                RoundTrip(dir, source, crlf);
            }

            Console.WriteLine("\n-- required anchor missing");
            var broken = Path.Combine(temp, "broken");
            Fixture.Create(broken, false);
            var gameplay = Path.Combine(broken, "Content.Client/Gameplay/GameplayStateBase.cs");
            File.WriteAllText(gameplay, File.ReadAllText(gameplay).Replace("if (eye == null)", "if (eye is null)"));
            var before = Snapshot(broken);
            var code = new Installer(Opts(broken, source)).Install(false);
            Check(code == 3, "install stops with exit code 3 when a required edit has no anchor");
            Check(Snapshot(broken).SequenceEqual(before), "nothing is written when a required edit cannot be applied");

            Console.WriteLine("\n-- optional anchor missing");
            var soft = Path.Combine(temp, "soft");
            Fixture.Create(soft, false);
            var popup = Path.Combine(soft, "Content.Client/Popups/PopupOverlay.cs");
            File.WriteAllText(popup, "namespace Content.Client.Popups;\npublic sealed class PopupOverlay { }\n");
            code = new Installer(Opts(soft, source)).Install(false);
            Check(code == 0, "install succeeds when only an optional edit has no anchor");
            Check(File.ReadAllText(popup).Contains("class PopupOverlay { }"), "the file of the skipped optional edit is untouched");

            Console.WriteLine("\n-- newer health bar overlay shape");
            var newer = Path.Combine(temp, "newer");
            Fixture.Create(newer, false);
            var bars = Path.Combine(newer, "Content.Client/Overlays/EntityHealthBarOverlay.cs");
            File.WriteAllText(bars, File.ReadAllText(bars).Replace(
                "CalcProgress(EntityUid uid, MobStateComponent component, DamageableComponent dmg, MobThresholdsComponent thresholds)",
                "CalcProgress(" + Environment.NewLine + "        EntityUid uid," + Environment.NewLine + "        MobStateComponent component," + Environment.NewLine + "        FixedPoint2 totalDamage," + Environment.NewLine + "        InjurableComponent injurable," + Environment.NewLine + "        MobThresholdsComponent thresholds)"));
            code = new Installer(Opts(newer, source)).Install(false);
            Check(code == 0, "install succeeds on the newer health bar shape");
            Check(File.ReadAllText(bars).Contains("_damageable.GetTotalDamage((uid, damageable))"), "the health bar method is written for the newer CalcProgress signature");

            Console.WriteLine("\n-- MCP server");
            var mcpDir = Path.Combine(temp, "mcp");
            Fixture.Create(mcpDir, false);
            McpCheck(mcpDir, source);

            Console.WriteLine("\n-- engine submodule not downloaded");
            var sub = Path.Combine(temp, "sub");
            Fixture.Create(sub, false);
            Directory.CreateDirectory(Path.Combine(sub, "RobustToolbox", "XamlX"));
            File.WriteAllText(Path.Combine(sub, "RobustToolbox", ".gitmodules"), "[submodule \"XamlX\"]\n\tpath = XamlX\n\turl = https://example.invalid/XamlX\n");
            var subInstaller = new Installer(Opts(sub, source));
            Check(subInstaller.MissingSubmodules().SequenceEqual(new[] { "XamlX" }), "an empty engine submodule folder is detected");
            File.WriteAllText(Path.Combine(sub, "RobustToolbox", "XamlX", "x.cs"), "//");
            Check(subInstaller.MissingSubmodules().Count == 0, "a populated engine submodule folder is not reported");

            Console.WriteLine("\n-- content checks of doctor");
            ContentCheckTest(Path.Combine(temp, "content"));

            Console.WriteLine("\n-- not an SS14 codebase");
            var empty = Path.Combine(temp, "empty");
            Directory.CreateDirectory(empty);
            Check(new Installer(Opts(empty, source)).Install(false) == 2, "install refuses a folder that is not an SS14 codebase");
        }
        finally
        {
            try { Directory.Delete(temp, true); } catch { /* best effort */ }
        }

        Console.WriteLine(_failures == 0 ? "\nAll installer tests passed." : $"\n{_failures} installer test(s) FAILED.");
        return _failures == 0;
    }

    private static void McpCheck(string dir, string? source)
    {
        var path = JsonSerializer.Serialize(dir);
        var calls = new[]
        {
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"selftest","version":"1"}}}""",
            """{"jsonrpc":"2.0","method":"notifications/initialized"}""",
            """{"jsonrpc":"2.0","id":2,"method":"tools/list"}""",
            """{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"inspect_codebase","arguments":{"path":""" + path + """}}}""",
            """{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"install_3d","arguments":{"path":""" + path + """}}}""",
            """{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"install_3d","arguments":{"path":""" + path + ""","dry_run":false,"commit":false,"force":true}}}""",
            """{"jsonrpc":"2.0","id":6,"method":"tools/call","params":{"name":"explain_edit","arguments":{"id":"pick-merge"}}}""",
            """{"jsonrpc":"2.0","id":7,"method":"tools/call","params":{"name":"explain_edit","arguments":{"id":"nope"}}}""",
            """{"jsonrpc":"2.0","id":8,"method":"resources/read","params":{"uri":"ss12://guide"}}""",
            "not json",
        };

        var before = Snapshot(dir);
        var dryOutput = new StringWriter { NewLine = "\n" };
        McpServer.Run(Opts(dir, source), new StringReader(calls[4] + "\n"), dryOutput);
        Check(dryOutput.ToString().Contains("Dry run only") && Snapshot(dir).SequenceEqual(before), "MCP: install is a dry run by default and changes nothing");

        var output = new StringWriter { NewLine = "\n" };
        McpServer.Run(Opts(dir, source), new StringReader(string.Join("\n", calls) + "\n"), output);

        var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Check(lines.Length == calls.Length - 1, "MCP: one response per request, none for the notification");
        var answers = new Dictionary<int, JsonElement>();
        foreach (var line in lines)
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement.Clone();
            if (root.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number)
                answers[id.GetInt32()] = root;
            else
                Check(root.TryGetProperty("error", out var e) && e.GetProperty("code").GetInt32() == -32700, "MCP: malformed JSON gets a parse error");
        }

        string Text(int id) => answers[id].GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString() ?? "";

        Check(answers.TryGetValue(1, out var init) && init.GetProperty("result").GetProperty("serverInfo").GetProperty("name").GetString() == "ss12", "MCP: initialize answers");
        Check(answers.TryGetValue(2, out var tools) && tools.GetProperty("result").GetProperty("tools").GetArrayLength() >= 8, "MCP: tools are listed");
        Check(Text(3).Contains("APPLIES AUTOMATICALLY") && Text(3).Contains("install_3d"), "MCP: inspect reports the edits");
        Check(!answers[5].GetProperty("result").GetProperty("isError").GetBoolean() && File.Exists(Path.Combine(dir, Installer.PresetPath)), "MCP: install with dry_run=false installs");
        Check(Text(6).Contains("GetClickableEntities") || Text(6).Contains("Marker"), "MCP: explain_edit describes an edit");
        Check(answers[7].GetProperty("result").GetProperty("isError").GetBoolean(), "MCP: explain_edit rejects an unknown id");
        Check(answers[8].GetProperty("result").GetProperty("contents")[0].GetProperty("text").GetString()!.Contains("Rules"), "MCP: the guide resource is served");
    }

    private static void ContentCheckTest(string dir)
    {
        void Put(string rel, string text)
        {
            var path = Path.Combine(dir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text);
        }

        Put("Resources/Prototypes/Entities/things.yml", """
            - type: entity
              id: TableBase
              abstract: true

            - type: entity
              parent: TableBase
              id: WoodTable # a table
            """);
        Put("Resources/Prototypes/Render3D/rules.yml", """
            - type: render3dRules
              id: Shipped
              rules:
              - mode: TableBox
                parents:
                - TableBase
                # a comment between the names
                - OldRack
              - mode: Billboard
                parents: [WoodTable, 'GoneChair']
              shapes:
              - parents:
                - "WoodTable"
                thickness: 0.1
            """);
        Put("Resources/Prototypes/Render3D/mine.yml", """
            - type: render3dRules
              id: Mine
              shapes:
              - parents: [TableBase]
                fixed: false
            """);
        Put("Content.Client/Aim.cs", "class A { void F() { var a = _eye.PixelToMap(_input.MouseScreenPosition); var b = _eye.PixelToMap(args.PointerLocation.Position); var c = vp.PixelToMap(_input.MouseScreenPosition.Position); } }\n");
        Put("Content.Client/Aimed.cs", "class B { void F() { var a = Content.Client.Render3D.Render3DPointer.PixelToMap(_eye, _input.MouseScreenPosition); } }\n");
        Put("Content.Client/Render3D/Own.cs", "class C { void F() { var a = _eye.PixelToMap(_input.MouseScreenPosition); } }\n");

        var unknown = ContentCheck.UnknownRuleNames(dir);
        Check(unknown.Count == 1 && unknown.ContainsKey("Resources/Prototypes/Render3D/rules.yml"), "content check: only the rule file with unknown names is reported");
        Check(unknown.Values.SelectMany(v => v).OrderBy(v => v, StringComparer.Ordinal).SequenceEqual(new[] { "GoneChair", "OldRack" }),
            "content check: block, inline, quoted and commented names are read, and known ones are not reported");

        var aims = ContentCheck.CursorAimCalls(dir);
        Check(aims.Count == 1 && aims[0] == "Content.Client/Aim.cs:1", "content check: only mouse aiming that bypasses the 3D pointer is reported");

        var nowhere = Path.Combine(dir, "nowhere");
        Check(ContentCheck.UnknownRuleNames(nowhere).Count == 0 && ContentCheck.CursorAimCalls(nowhere).Count == 0,
            "content check: a folder without prototypes or client code gives no findings");
    }

    private static Options Opts(string dir, string? source) => new()
    {
        Target = dir,
        Source = source,
        Build = false,
        Commit = false,
        Force = true, // fixtures have no engine to compare
    };

    private static void RoundTrip(string dir, string? source, bool crlf)
    {
        var original = Snapshot(dir);

        var install = new Installer(Opts(dir, source));
        Check(install.Install(false) == 0, "install succeeds");

        var afterInstall = Snapshot(dir);
        foreach (var t in BuiltIn.All)
        {
            var marker = t.Edits.First().Done;
            var applied = FileSet.Find(dir, t.Include, t.Exclude)
                .Any(f => File.ReadAllText(Path.Combine(dir, f)).Contains(marker, StringComparison.Ordinal));
            Check(applied, $"transform '{t.Id}' left its marker '{marker}'");
        }

        Check(File.Exists(Path.Combine(dir, Installer.PresetPath)), "server preset written");
        Check(File.Exists(Path.Combine(dir, "Content.Client/Render3D/Render3DController.cs")), "overlay files copied");

        // CRLF fixtures must stay CRLF (and LF ones LF)
        var gunPath = Path.Combine(dir, "Content.Client/Weapons/Ranged/Systems/GunSystem.cs");
        var gunText = File.ReadAllText(gunPath);
        Check(gunText.Contains("\r\n") == crlf, "line endings of edited files are preserved");
        Check(!gunText.Replace("\r\n", "\n").Contains("\r"), "no stray CR characters");
        Check(gunText.Contains("\r\n") == File.ReadAllText(Path.Combine(dir, "Content.Client/Popups/PopupOverlay.cs")).Contains("\r\n"), "all edited files keep the same line endings");

        Check(new Installer(Opts(dir, source)).Install(false) == 2, "a second install is refused (use update)");

        var update = new Installer(Opts(dir, source));
        Check(update.Install(true) == 0, "update succeeds");
        Check(Snapshot(dir).Where(kv => !kv.Key.StartsWith(".ss12/")).SequenceEqual(afterInstall.Where(kv => !kv.Key.StartsWith(".ss12/"))),
            "update on an unchanged install changes nothing");

        var doctor = new Installer(Opts(dir, source));
        Check(doctor.Doctor(false) == 0, "doctor reports a healthy install");

        var uninstall = new Installer(Opts(dir, source));
        Check(uninstall.Uninstall() == 0, "uninstall succeeds");
        var restored = Snapshot(dir);
        Check(restored.Count == original.Count && restored.All(kv => original.TryGetValue(kv.Key, out var o) && o == kv.Value),
            "uninstall restores every file byte for byte and removes everything it added");
    }

    private static Dictionary<string, string> Snapshot(string dir)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(dir, file).Replace('\\', '/');
            result[rel] = Hashing.Sha256(File.ReadAllBytes(file));
        }

        return result;
    }

    private static void Check(bool ok, string what)
    {
        Console.ForegroundColor = ok ? ConsoleColor.Green : ConsoleColor.Red;
        Console.WriteLine($"  {(ok ? "ok  " : "FAIL")} {what}");
        Console.ResetColor();
        if (!ok)
            _failures++;
    }
}

/// <summary>Minimal stand-ins for the upstream files the edits anchor on (the original upstream code).</summary>
internal static class Fixture
{
    public static void Create(string dir, bool crlf)
    {
        Directory.CreateDirectory(dir);
        foreach (var project in new[] { "Content.Client", "Content.Shared", "Content.Server" })
            Write(dir, $"{project}/{project}.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />\n", false);

        Write(dir, "RobustToolbox/MSBuild/Robust.Engine.Version.props", "<Project><PropertyGroup><Version>291.0.0</Version></PropertyGroup></Project>\n", false);

        Write(dir, "Content.Client/Weapons/Ranged/Systems/GunSystem.cs", """
            namespace Content.Client.Weapons.Ranged.Systems;

            public sealed partial class GunSystem
            {
                public override void Update(float frameTime)
                {
                    var mousePos = _eyeManager.PixelToMap(_inputManager.MouseScreenPosition);
                }
            }
            """, crlf);

        Write(dir, "Content.Client/Weapons/Melee/MeleeWeaponSystem.cs", """
            namespace Content.Client.Weapons.Melee;

            public sealed partial class MeleeWeaponSystem
            {
                public override void Update(float frameTime)
                {
                    var mousePos = _eyeManager.PixelToMap(_inputManager.MouseScreenPosition);
                }
            }
            """, crlf);

        Write(dir, "Content.Client/Outline/TargetOutlineSystem.cs", """
            namespace Content.Client.Outline;

            public sealed class TargetOutlineSystem
            {
                private void Update()
                {
                    var mousePos = _eyeManager.PixelToMap(_inputManager.MouseScreenPosition).Position;
                }
            }
            """, crlf);

        Write(dir, "Content.Client/Interaction/DragDropSystem.cs", """
            namespace Content.Client.Interaction;

            public sealed class DragDropSystem
            {
                private bool OnUseMouseDown(in PointerInputCmdHandler.PointerInputCmdArgs args)
                {
                    _draggedEntity = entity;
                    _state = DragState.MouseDown;
                    _mouseDownScreenPos = args.ScreenCoordinates;
                    _mouseDownTime = 0;

                    _savedMouseDown = args;
                    return true;
                }

                private void HighlightTargets()
                {
                    var mousePos = _eyeManager.PixelToMap(_inputManager.MouseScreenPosition);
                }

                public override void Update(float frameTime)
                {
                    switch (_state)
                    {
                        case DragState.MouseDown:
                        {
                            var screenPos = _inputManager.MouseScreenPosition;
                            if ((_mouseDownScreenPos!.Value.Position - screenPos.Position).Length() > Deadzone)
                            {
                                StartDrag();
                            }

                            break;
                        }
                    }
                }

                public override void FrameUpdate(float frameTime)
                {
                    if (Exists(_dragShadow))
                    {
                        var mousePos = _eyeManager.PixelToMap(_inputManager.MouseScreenPosition);
                    }
                }
            }
            """, crlf);

        Write(dir, "Content.Client/Gameplay/GameplayStateBase.cs", """
            namespace Content.Client.Gameplay;

            public partial class GameplayStateBase
            {
                public IEnumerable<EntityUid> GetClickableEntities(MapCoordinates coordinates, IEye? eye, bool excludeFaded = true)
                {
                    /*
                     * TODO:
                     * 1. Stuff like MeleeWeaponSystem need an easy way to hook into viewport specific entities
                     */

                    if (eye == null)
                        return Array.Empty<EntityUid>();

                    // Find all the entities intersecting our click
                    var spriteTree = _entityManager.EntitySysManager.GetEntitySystem<SpriteTreeSystem>();
                    return Array.Empty<EntityUid>();
                }
            }
            """, crlf);

        Write(dir, "Content.Client/Popups/PopupOverlay.cs", """
            namespace Content.Client.Popups;

            public sealed class PopupOverlay
            {
                private void DrawWorld(DrawingHandleScreen worldHandle, OverlayDrawArgs args, float scale)
                {
                    var matrix = args.ViewportControl.GetWorldToScreenMatrix();
                    foreach (var popup in _popup.WorldLabels)
                    {
                        var pos = Vector2.Transform(mapPos.Position, matrix);
                        _controller.DrawPopup(popup, worldHandle, pos, scale);
                    }
                }
            }
            """, crlf);

        Write(dir, "Content.Client/MapText/MapTextOverlay.cs", """
            namespace Content.Client.MapText;

            public sealed class MapTextOverlay
            {
                private void DrawWorld(DrawingHandleScreen handle, OverlayDrawArgs args, float scale)
                {
                    var matrix = args.ViewportControl.GetWorldToScreenMatrix();
                    while (query.MoveNext(out var uid, out var mapText))
                    {
                        if (mapText.CachedFont == null)
                            continue;

                        var pos = Vector2.Transform(mapPos.Position, matrix) + mapText.Offset;
                        var dimensions = handle.GetDimensions(mapText.CachedFont, mapText.CachedText, scale);
                        var drawPosition = (pos - dimensions / 2f).Rounded();
                        handle.DrawString(mapText.CachedFont, drawPosition, mapText.CachedText, scale, mapText.Color, TextOutline.Default);
                    }
                }
            }
            """, crlf);

        Write(dir, "Content.Client/Sprite/SpriteFadeSystem.cs", """
            namespace Content.Client.Sprite;

            public sealed partial class SpriteFadeSystem
            {
                public override void FrameUpdate(float frameTime)
                {
                    var change = ChangeRate * frameTime;

                    FadeIn(change);
                    FadeOut(change);
                }
            }
            """, crlf);

        Write(dir, "Content.Client/Overlays/EntityHealthBarOverlay.cs", """
            namespace Content.Client.Overlays;

            /// <summary>
            /// Overlay that shows a health bar on mobs.
            /// </summary>
            public sealed class EntityHealthBarOverlay : Overlay
            {
                protected override void Draw(in OverlayDrawArgs args)
                {
                }

                /// <summary>
                /// Returns a ratio between 0 and 1, and whether the entity is in crit.
                /// </summary>
                private (float ratio, bool inCrit)? CalcProgress(EntityUid uid, MobStateComponent component, DamageableComponent dmg, MobThresholdsComponent thresholds)
                {
                    // reads InjurableComponent and the damage thresholds
                    return null;
                }

                public Color GetProgressColor(float progress, bool crit)
                {
                    return Color.Red;
                }
            }
            """, crlf);
    }

    private static void Write(string dir, string rel, string content, bool crlf)
    {
        var path = Path.Combine(dir, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var text = content.Replace("\r\n", "\n");
        if (!text.EndsWith('\n'))
            text += "\n";

        if (crlf)
            text = text.Replace("\n", "\r\n");

        File.WriteAllBytes(path, new UTF8Encoding(false).GetBytes(text));
    }
}
