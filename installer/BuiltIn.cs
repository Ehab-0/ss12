namespace Ss12;

/// <summary>
///     The edits to existing files that the 3D view needs, and nothing else: everything else is copied in as new files
///     (see overlay.manifest). Each one is a one-line call into code that lives in Content.Client/Render3D
///     (Render3DPointer.cs), written with fully qualified names so no <c>using</c> has to be added.
///     <para>
///         <b>Required</b> transforms make the game play correctly in 3D; install stops if one cannot be applied.
///         <b>Optional</b> ones polish a feature and fail soft (the Impact text says what is lost).
///     </para>
/// </summary>
public static class BuiltIn
{
    private const string Ns = "Content.Client.Render3D";
    private static readonly string Lf = ((char) 10).ToString();

    public static IReadOnlyList<Transform> All { get; } = new Transform[]
    {
        new(
            Id: "pointer-aim",
            Title: "Aim guns, melee, drag-and-drop and target outlines with the crosshair",
            Required: true,
            Impact: "While the 3D mouse is captured the OS cursor position is stale, so guns, melee swings and drag-and-drop would silently do nothing.",
            Manual: "Replace every `_eyeManager.PixelToMap(_inputManager.MouseScreenPosition)` in Content.Client (GunSystem, MeleeWeaponSystem, DragDropSystem, TargetOutlineSystem and any fork system that aims with the mouse) by `Content.Client.Render3D.Render3DPointer.PixelToMap(_eyeManager, _inputManager.MouseScreenPosition)`.",
            Include: new[] { "Content.Client/**/*.cs" },
            Exclude: new[] { "Content.Client/Render3D/**" },
            MinFiles: 2,
            Edits: new Edit[]
            {
                new ReplaceEdit(
                    Find: @"(?<![\w.])(?<eye>_?\w*[eE]ye\w*)\.PixelToMap\((?<inp>_?\w*[iI]nput\w*)\.MouseScreenPosition\)",
                    Replace: Ns + ".Render3DPointer.PixelToMap(${eye}, ${inp}.MouseScreenPosition)",
                    Done: "Render3DPointer.PixelToMap("),
            }),

        new(
            Id: "pick-merge",
            Title: "Let the 3D crosshair pick decide what is under the cursor",
            Required: true,
            Impact: "Clicking, examining, the context menu and interaction would not find anything in the 3D view.",
            Manual: "In GameplayStateBase.GetClickableEntities, right after `if (eye == null) return Array.Empty<EntityUid>();` add the block shown in Tools/ss12/BuiltIn.cs (pick-merge): ask Render3DPicking.TryBegin(_eyeManager, coordinates, out var picked3D); if it succeeds append the 2D results with Render3DPicking.AddMissing and return picked3D.",
            Include: new[] { "Content.Client/Gameplay/GameplayStateBase.cs" },
            Exclude: Array.Empty<string>(),
            MinFiles: 1,
            Edits: new Edit[]
            {
                new InsertAfterEdit(
                    // GetClickableEntities(MapCoordinates coordinates, IEye? eye[, bool excludeFaded = true]) { ... if (eye == null) return ...; }
                    Anchor: @"GetClickableEntities\(MapCoordinates coordinates, IEye\? eye(?:, bool (?<ex>\w+)[^)]*)?\)\s*\{[\s\S]*?if \(eye == null\)\s*return Array\.Empty<EntityUid>\(\);",
                    Build: m => PickMergeBlock(m.Groups["ex"].Success ? ", " + m.Groups["ex"].Value : ""),
                    Done: "Render3DPicking.TryBegin"),
            }),

        new(
            Id: "drag-drop",
            Title: "Start drag-and-drop with a captured mouse",
            Required: false,
            Impact: "Drag-and-drop (dragging items, pulling mobs onto things) would not start in the 3D view while the mouse is captured; hold Alt (free cursor) to drag.",
            Manual: "In DragDropSystem: after `_mouseDownTime = 0;` in OnUseMouseDown call Render3DPointer.ResetDrag(); and wrap the `Length() > Deadzone` test in Update with Render3DPointer.DragMoved(<test>, Deadzone).",
            Include: new[] { "Content.Client/Interaction/DragDropSystem.cs" },
            Exclude: Array.Empty<string>(),
            MinFiles: 1,
            Edits: new Edit[]
            {
                new ReplaceEdit(
                    Find: @"if \(\(_mouseDownScreenPos!\.Value\.Position - screenPos\.Position\)\.Length\(\) > (?<dz>\w+)\)",
                    Replace: "if (" + Ns + ".Render3DPointer.DragMoved((_mouseDownScreenPos!.Value.Position - screenPos.Position).Length() > ${dz}, ${dz}))",
                    Done: "Render3DPointer.DragMoved"),
                new InsertAfterEdit(
                    Anchor: @"_mouseDownScreenPos = [^;]+;\s*_mouseDownTime = 0;",
                    Text: "\n        " + Ns + ".Render3DPointer.ResetDrag();",
                    Done: "Render3DPointer.ResetDrag"),
            }),

        new(
            Id: "popups",
            Title: "Place popup text over the right spot in 3D",
            Required: false,
            Impact: "Floating popups (\"gasps\", interaction messages) would appear at the wrong screen position in 3D.",
            Manual: "In PopupOverlay.DrawWorld replace `Vector2.Transform(mapPos.Position, matrix)` by `Content.Client.Render3D.Render3DOverlays.WorldToScreen(args.ViewportControl, mapPos.Position, matrix)`.",
            Include: new[] { "Content.Client/Popups/PopupOverlay.cs" },
            Exclude: Array.Empty<string>(),
            MinFiles: 1,
            Edits: new Edit[]
            {
                new ReplaceEdit(
                    Find: @"Vector2\.Transform\(mapPos\.Position, matrix\)",
                    Replace: Ns + ".Render3DOverlays.WorldToScreen(args.ViewportControl, mapPos.Position, matrix)",
                    Done: "Render3DOverlays.WorldToScreen"),
            }),

        new(
            Id: "map-text",
            Title: "Place, cull and declutter map text labels in 3D",
            Required: false,
            Impact: "Map text labels (mapper-placed area titles) would be misplaced and pile up at the horizon in 3D.",
            Manual: "In MapTextOverlay.DrawWorld: skip labels where Render3DOverlays.ShowMapText(args.ViewportControl, mapPos.Position) is false, compute `pos` with Render3DOverlays.WorldToScreen(...), and draw only if Render3DOverlays.AllowLabel(args.ViewportControl, drawPosition, dimensions).",
            Include: new[] { "Content.Client/MapText/MapTextOverlay.cs" },
            Exclude: Array.Empty<string>(),
            MinFiles: 1,
            Edits: new Edit[]
            {
                new ReplaceEdit(
                    Find: @"var pos = Vector2\.Transform\(mapPos\.Position, matrix\) \+ mapText\.Offset;",
                    Replace: "if (!" + Ns + ".Render3DOverlays.ShowMapText(args.ViewportControl, mapPos.Position))\n"
                             + "                continue;\n\n"
                             + "            var pos = " + Ns + ".Render3DOverlays.WorldToScreen(args.ViewportControl, mapPos.Position, matrix) + mapText.Offset;",
                    Done: "Render3DOverlays.ShowMapText"),
                new ReplaceEdit(
                    // newer code draws at `drawPosition` with an outline, older code at `pos - dimensions / 2f`
                    Find: @"handle\.DrawString\(mapText\.CachedFont, (?<at>[^,]+), mapText\.CachedText, scale, mapText\.Color(?<tail>[^;]*)\);",
                    Replace: "if (" + Ns + ".Render3DOverlays.AllowLabel(args.ViewportControl, ${at}, dimensions))\n"
                             + "                handle.DrawString(mapText.CachedFont, ${at}, mapText.CachedText, scale, mapText.Color${tail});",
                    Done: "Render3DOverlays.AllowLabel",
                    DoneFirst: true),
            }),

        new(
            Id: "sprite-fade",
            Title: "Do not fade sprites towards the cursor in 3D",
            Required: false,
            Impact: "In 3D, big things between the camera and the cursor would fade out like they do in 2D (they are solid in 3D).",
            Manual: "In SpriteFadeSystem.FrameUpdate wrap `FadeIn(change);` in `if (!Content.Client.Render3D.Render3DPointer.Shown)`.",
            Include: new[] { "Content.Client/Sprite/SpriteFadeSystem.cs" },
            Exclude: Array.Empty<string>(),
            MinFiles: 1,
            Edits: new Edit[]
            {
                new ReplaceEdit(
                    Find: @"^([ \t]*)FadeIn\(change\);",
                    Replace: "${1}if (!" + Ns + ".Render3DPointer.Shown)\n${1}    FadeIn(change);",
                    Done: "Render3DPointer.Shown",
                    DoneFirst: true),
            }),

        new(
            Id: "health-bars",
            Title: "Draw health bars over mobs in 3D",
            Required: false,
            Impact: "No health bars (showhealthbars, medical HUDs) over mobs in the 3D view.",
            Manual: "Make EntityHealthBarOverlay implement Content.Client.Render3D.IRender3DHealthBar by adding a `bool TryGetBar(EntityUid uid, out float ratio, out Color color)` that applies the same selection rules as its Draw method for one entity.",
            Include: new[] { "Content.Client/Overlays/EntityHealthBarOverlay.cs" },
            Exclude: Array.Empty<string>(),
            MinFiles: 1,
            Edits: new Edit[]
            {
                new ReplaceEdit(
                    Find: @"public sealed class EntityHealthBarOverlay : Overlay(?![\w,])",
                    Replace: "public sealed class EntityHealthBarOverlay : Overlay, " + Ns + ".IRender3DHealthBar",
                    Done: "IRender3DHealthBar"),
                // Two shapes of CalcProgress exist: (uid, mobState, DamageableComponent dmg, thresholds) and the newer
                // (uid, mobState, FixedPoint2 totalDamage, injurable, thresholds). The method is built for whichever is found.
                new InsertBeforeEdit(
                    Anchor: @"^[ \t]*/// <summary>\s*\n[ \t]*/// Returns a ratio between 0 and 1, and whether the entity is in crit\.\s*\n[ \t]*/// </summary>\s*\n[ \t]*private \(float ratio, bool inCrit\)\? CalcProgress\(\s*EntityUid uid,\s*MobStateComponent component,\s*(?:(?<new>FixedPoint2 totalDamage,)|(?<old>DamageableComponent dmg,))",
                    Build: m => m.Groups["new"].Success
                        ? HealthBarMethod.Replace(
                            "CalcProgress(uid, mobState, damageable, thresholds)",
                            "CalcProgress(uid, mobState, _damageable.GetTotalDamage((uid, damageable)), injurable, thresholds)")
                        : HealthBarMethod,
                    Done: "public bool TryGetBar("),
            },
            // the inserted method is written against this overlay's current designs; other designs are not targets
            RequiresText: new[] { "InjurableComponent", "CalcProgress(", "GetProgressColor(" }),
    };

    /// <summary>The block inserted into GetClickableEntities; <paramref name="extraArgs"/> forwards an optional trailing parameter (excludeFaded).</summary>
    private static string PickMergeBlock(string extraArgs) => string.Join(Lf, new[]
    {
        "",
        "",
        "            // 3D view: what is under a position is decided by the crosshair/cursor pick (Content.Client.Render3D),",
        "            // followed by whatever the 2D lookup finds at the same spot.",
        "            if (" + Ns + ".Render3DPicking.TryBegin(_eyeManager, coordinates, out var picked3D))",
        "            {",
        "                try",
        "                {",
        "                    " + Ns + ".Render3DPicking.AddMissing(picked3D, GetClickableEntities(coordinates, eye" + extraArgs + "));",
        "                }",
        "                finally",
        "                {",
        "                    " + Ns + ".Render3DPicking.End();",
        "                }",
        "",
        "                return picked3D;",
        "            }",
    });

    private const string HealthBarMethod =
        "    /// <summary>\n"
        + "    /// Same selection rules as <see cref=\"Draw\"/> for a single entity, for views (the 3D renderer) that draw the bar\n"
        + "    /// themselves in screen space. Returns false when the entity shows no bar.\n"
        + "    /// </summary>\n"
        + "    public bool TryGetBar(EntityUid uid, out float ratio, out Color color)\n"
        + "    {\n"
        + "        ratio = 0;\n"
        + "        color = Color.White;\n"
        + "\n"
        + "        if (!_entManager.TryGetComponent(uid, out MobThresholdsComponent? thresholds)\n"
        + "            || !_entManager.TryGetComponent(uid, out MobStateComponent? mobState)\n"
        + "            || !_entManager.TryGetComponent(uid, out DamageableComponent? damageable)\n"
        + "            || !_entManager.TryGetComponent(uid, out InjurableComponent? injurable))\n"
        + "        {\n"
        + "            return false;\n"
        + "        }\n"
        + "\n"
        + "        _prototype.Resolve(StatusIcon, out var statusIcon);\n"
        + "        if (statusIcon != null && !_statusIconSystem.IsVisible((uid, _entManager.GetComponent<MetaDataComponent>(uid)), statusIcon))\n"
        + "            return false;\n"
        + "\n"
        + "        if (injurable.DamageContainer == null || !DamageContainers.Contains(injurable.DamageContainer))\n"
        + "            return false;\n"
        + "\n"
        + "        if (CalcProgress(uid, mobState, damageable, thresholds) is not { } progress)\n"
        + "            return false;\n"
        + "\n"
        + "        ratio = progress.ratio;\n"
        + "        color = GetProgressColor(progress.ratio, progress.inCrit);\n"
        + "        return true;\n"
        + "    }\n"
        + "\n";
}
