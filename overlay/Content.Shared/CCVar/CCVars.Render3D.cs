using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    ///     Whether the client renders the game in 3D (when the server allows it). Players can switch this off
    ///     unless <see cref="Render3DEnforced"/> is set (ghosts, admins and the lobby can always switch).
    /// </summary>
    public static readonly CVarDef<bool> Render3DEnabled =
        CVarDef.Create("render3d.enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     If true, living player-controlled mobs are forced into the 3D view and cannot toggle back to 2D.
    ///     Our server config preset turns this on; the upstream default is off.
    /// </summary>
    public static readonly CVarDef<bool> Render3DEnforced =
        CVarDef.Create("render3d.enforced", false, CVar.SERVER | CVar.REPLICATED);

    /// <summary>Height of the ceiling (and of wall blocks) in tile units.</summary>
    public static readonly CVarDef<float> Render3DWallHeight =
        CVarDef.Create("render3d.wall_height", 2.3f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>First-person eye height in tile units.</summary>
    public static readonly CVarDef<float> Render3DEyeHeight =
        CVarDef.Create("render3d.eye_height", 0.9f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Height of table tops (and of the surface that items on tables stand on).</summary>
    public static readonly CVarDef<float> Render3DTableHeight =
        CVarDef.Create("render3d.table_height", 0.45f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Height at which "air" effects (tracers, beams, swing effects) are drawn.</summary>
    public static readonly CVarDef<float> Render3DEffectHeight =
        CVarDef.Create("render3d.effect_height", 0.6f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Height above the floor at which world-space UI (speech bubbles, icons) anchors by default.</summary>
    public static readonly CVarDef<float> Render3DUiAnchorHeight =
        CVarDef.Create("render3d.ui_anchor_height", 0.9f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Vertical field of view in degrees.</summary>
    public static readonly CVarDef<int> Render3DFov =
        CVarDef.Create("render3d.fov", 80, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Fraction of the on-screen size the 3D image is rendered at (nearest-neighbour upscaled).</summary>
    public static readonly CVarDef<float> Render3DRenderScale =
        CVarDef.Create("render3d.render_scale", 1.0f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Supersampling: the 3D image is rendered at this many times the window size in each direction and averaged
    ///     down, which removes shimmer and jagged edges on every surface at once. 1 = off.
    /// </summary>
    public static readonly CVarDef<float> Render3DSupersample =
        CVarDef.Create("render3d.supersample", 2.0f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>How many tiles around the camera are rendered (also the size of the ground capture).</summary>
    public static readonly CVarDef<int> Render3DGroundRadius =
        CVarDef.Create("render3d.ground_radius", 24, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Size in pixels of the per-frame billboard atlas.</summary>
    public static readonly CVarDef<int> Render3DAtlasSize =
        CVarDef.Create("render3d.atlas_size", 2048, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Maximum number of entities drawn as 3D quads per frame (nearest first).</summary>
    public static readonly CVarDef<int> Render3DBillboardCap =
        CVarDef.Create("render3d.billboard_cap", 400, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Third-person camera distance behind the character.</summary>
    public static readonly CVarDef<float> Render3DThirdPersonDistance =
        CVarDef.Create("render3d.tp_distance", 1.6f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Mouse-look sensitivity multiplier (1 = about 0.15 degrees per pixel).</summary>
    public static readonly CVarDef<float> Render3DMouseSensitivity =
        CVarDef.Create("render3d.mouse_sensitivity", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> Render3DInvertY =
        CVarDef.Create("render3d.invert_y", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Show the name of the entity under the crosshair.</summary>
    public static readonly CVarDef<bool> Render3DCrosshairNames =
        CVarDef.Create("render3d.crosshair_names", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Start in first person instead of third person.</summary>
    public static readonly CVarDef<bool> Render3DFirstPerson =
        CVarDef.Create("render3d.first_person", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Sub-rectangle (u0,v0,u1,v1; v measured from the top of the icon) of a wall icon that is used as the
    ///     front-face texture of a wall block. SS14 wall art is 3/4 view, so the front face is the lower part.
    /// </summary>
    public static readonly CVarDef<string> Render3DWallUvRect =
        CVarDef.Create("render3d.wall_uv_rect", "0,0.34,1,1", CVar.CLIENTONLY | CVar.ARCHIVE);

    // ---- visual effects: every one can be switched off (older PCs), see Content.Client.Render3D.Render3DQuality ----

    /// <summary>Graphics quality preset: 0 low, 1 medium, 2 high, 3 custom (set when an individual effect is toggled).</summary>
    public static readonly CVarDef<int> Render3DQuality =
        CVarDef.Create("render3d.quality", 2, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Opens the 3D settings window by itself when a player enters a round, so they can pick the graphics quality:
    ///     0 never (the default), 1 the first time only (see <see cref="Render3DSettingsShown"/>), 2 every round.
    ///     Set by the server.
    /// </summary>
    public static readonly CVarDef<int> Render3DSettingsOnJoin =
        CVarDef.Create("render3d.settings_on_join", 0, CVar.SERVER | CVar.REPLICATED);

    /// <summary>The 3D settings window has already been opened automatically on this computer (for mode 1 of <see cref="Render3DSettingsOnJoin"/>).</summary>
    public static readonly CVarDef<bool> Render3DSettingsShown =
        CVarDef.Create("render3d.settings_shown", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     How much closer (on the squared distance) an entity that was drawn last frame counts when the nearest ones are
    ///     picked under <see cref="Render3DBillboardCap"/>; 1 turns the head start off. A developer aid.
    /// </summary>
    public static readonly CVarDef<float> Render3DCapHysteresis =
        CVarDef.Create("render3d.cap_hysteresis", 0.6f, CVar.CLIENTONLY);

    /// <summary>
    ///     How many camera yaw updates per second the client may send to the server while the mouse turns the view (4 to
    ///     30). Each update is a reliable message, so on a lossy connection a high rate adds packets that can each hold up
    ///     the movement keys behind them; a lower rate sends fewer. WASD follows the new direction at this rate.
    /// </summary>
    public static readonly CVarDef<float> Render3DYawSendRate =
        CVarDef.Create("render3d.yaw_send_rate", 12f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     A developer aid: whether the billboard atlas slot of a sprite covers the offset of the sprite itself (wall lamps have
    ///     one). Off gives the old behaviour, where the art of such a sprite lands outside its slot, on top of its neighbours;
    ///     it exists to compare the two (<c>r3d_atlas</c>) and should stay on.
    /// </summary>
    public static readonly CVarDef<bool> Render3DDevAtlasOffset =
        CVarDef.Create("render3d.dev_atlas_offset", true, CVar.CLIENTONLY);

    /// <summary>Step the quality down automatically when the frame rate is low (never steps up).</summary>
    public static readonly CVarDef<bool> Render3DAutoQuality =
        CVarDef.Create("render3d.auto_quality", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Average FPS below which auto quality steps down.</summary>
    public static readonly CVarDef<int> Render3DAutoQualityFps =
        CVarDef.Create("render3d.auto_quality_fps", 45, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     The last time the quality or the render scale was lowered, it was automatic (not the player's choice). The next time the
    ///     player joins a round the game starts on the highest graphics again and measures the frame rate again. Any change the
    ///     player makes themselves clears it.
    /// </summary>
    public static readonly CVarDef<bool> Render3DAutoLowered =
        CVarDef.Create("render3d.auto_lowered", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>The minimap in a corner of the 3D view: 0 hidden, 1 small, 2 large (with the names of the areas).</summary>
    public static readonly CVarDef<int> Render3DMinimapMode =
        CVarDef.Create("render3d.minimap_mode", 1, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Side of the small minimap in pixels.</summary>
    public static readonly CVarDef<int> Render3DMinimapSize =
        CVarDef.Create("render3d.minimap_size", 200, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Tiles from the player to the edge of the small minimap (the large one shows about two and a half times as many).</summary>
    public static readonly CVarDef<float> Render3DMinimapRange =
        CVarDef.Create("render3d.minimap_range", 20f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>The minimap turns with the camera, so that up on the map is the way the player looks (otherwise north is up).</summary>
    public static readonly CVarDef<bool> Render3DMinimapRotate =
        CVarDef.Create("render3d.minimap_rotate", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Glow around bright lights and screens.</summary>
    public static readonly CVarDef<bool> Render3DFxBloom =
        CVarDef.Create("render3d.fx.bloom", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Smooths the edges of walls and quads.</summary>
    public static readonly CVarDef<bool> Render3DFxFxaa =
        CVarDef.Create("render3d.fx.fxaa", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Screen-space ambient occlusion (soft shadows in corners and under things).</summary>
    public static readonly CVarDef<bool> Render3DFxAo =
        CVarDef.Create("render3d.fx.ao", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Wall, floor and ceiling shading: edge darkening, floor sheen, ceiling panels.</summary>
    public static readonly CVarDef<bool> Render3DFxSurface =
        CVarDef.Create("render3d.fx.surface", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     A faint ambient light on walls, floors and ceilings the player cannot see (and so get no light from the 2D
    ///     line-of-sight rule), so a third-person camera does not show pure black holes. Entities stay hidden.
    /// </summary>
    public static readonly CVarDef<bool> Render3DFxAmbient =
        CVarDef.Create("render3d.fx.ambient", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Soft contact shadows under characters and items.</summary>
    public static readonly CVarDef<bool> Render3DFxShadows =
        CVarDef.Create("render3d.fx.shadows", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Thin dark outline around characters and items.</summary>
    public static readonly CVarDef<bool> Render3DFxOutline =
        CVarDef.Create("render3d.fx.outline", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Sharper, shimmer-free sprite sampling.</summary>
    public static readonly CVarDef<bool> Render3DFxSharp =
        CVarDef.Create("render3d.fx.sharp", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Tone mapping, colour grading, vignette and dithering.</summary>
    public static readonly CVarDef<bool> Render3DFxGrade =
        CVarDef.Create("render3d.fx.grade", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Distance haze tinted by the local light.</summary>
    public static readonly CVarDef<bool> Render3DFxHaze =
        CVarDef.Create("render3d.fx.haze", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Layered stars and a faint nebula in space.</summary>
    public static readonly CVarDef<bool> Render3DFxSky =
        CVarDef.Create("render3d.fx.sky", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Wall lamps get a soft glow halo on the wall. (Lamps hang near the ceiling whether this is on or not.)</summary>
    public static readonly CVarDef<bool> Render3DFxFixtures =
        CVarDef.Create("render3d.fx.fixtures", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Windows, window doors and grilles are drawn as see-through glass (a thin frame and a mostly transparent pane) instead
    ///     of the top-down picture of the sprite, which looked like a solid wall.
    /// </summary>
    public static readonly CVarDef<bool> Render3DFxGlass =
        CVarDef.Create("render3d.fx.glass", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    // ---- shape of things: lean and thickness, per category (items on the ground, characters, other objects) ----

    /// <summary>Items on the ground are lifted a little, get a contact shadow and a thin outline, so they read as objects and not as stains.</summary>
    public static readonly CVarDef<bool> Render3DFxItemLift =
        CVarDef.Create("render3d.fx.item_lift", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Items on the ground tilt towards the camera (more the lower the camera is), so their art is readable.</summary>
    public static readonly CVarDef<bool> Render3DFxItemLean =
        CVarDef.Create("render3d.fx.item_lean", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Items on the ground get thickness (stacked layers), so they look like small solid objects.</summary>
    public static readonly CVarDef<bool> Render3DFxItemThick =
        CVarDef.Create("render3d.fx.item_thick", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Items rest on the real top of what they lie on (a table, a rack, a bed, a locker; see the <c>surfaces</c> rules), are drawn
    ///     after it, so they are never hidden inside it, and cast their contact shadow on it.
    /// </summary>
    public static readonly CVarDef<bool> Render3DFxItemSurface =
        CVarDef.Create("render3d.fx.item_surface", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Items that lie on top of each other (the same spot) are spread apart a little, inside their tile, so each can be seen.</summary>
    public static readonly CVarDef<bool> Render3DFxItemSpread =
        CVarDef.Create("render3d.fx.item_spread", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     How far (tiles) the far edge of an item lying on a surface may rise when it tilts towards the camera. A big sprite (a
    ///     bedsheet, a pile of clothes) tilts less, so it does not hover over the table. 0 = no limit.
    /// </summary>
    public static readonly CVarDef<float> Render3DItemMaxRise =
        CVarDef.Create("render3d.item_max_rise", 0.10f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Show a small list, to the right, of what the crosshair points at.</summary>
    public static readonly CVarDef<bool> Render3DPointList =
        CVarDef.Create("render3d.pointlist", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Furniture and machines (anchored standing objects) stay where they are, facing the way they point, instead of turning
    ///     to face the camera as you walk round them. Characters and animals still turn.
    /// </summary>
    public static readonly CVarDef<bool> Render3DFxObjectFixed =
        CVarDef.Create("render3d.fx.object_fixed", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Items on the floor and on tables keep a fixed tilt (towards the south) instead of tilting towards the camera.</summary>
    public static readonly CVarDef<bool> Render3DFxItemFixed =
        CVarDef.Create("render3d.fx.item_fixed", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Characters lean back slightly so they are less squashed when seen from above.</summary>
    public static readonly CVarDef<bool> Render3DFxCharLean =
        CVarDef.Create("render3d.fx.char_lean", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Characters get thickness (a thin slab), so they are not paper thin.</summary>
    public static readonly CVarDef<bool> Render3DFxCharThick =
        CVarDef.Create("render3d.fx.char_thick", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Machines, furniture and other standing objects lean back slightly.</summary>
    public static readonly CVarDef<bool> Render3DFxObjectLean =
        CVarDef.Create("render3d.fx.object_lean", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Machines, furniture and other standing objects get thickness.</summary>
    public static readonly CVarDef<bool> Render3DFxObjectThick =
        CVarDef.Create("render3d.fx.object_thick", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>How many stacked layers thick things are drawn with (the Low / Medium / High presets set 0 / 3 / 4). 0 = flat.</summary>
    public static readonly CVarDef<int> Render3DThicknessLayers =
        CVarDef.Create("render3d.thickness_layers", 4, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Gentle walking head bob in first person.</summary>
    public static readonly CVarDef<bool> Render3DFxHeadBob =
        CVarDef.Create("render3d.fx.head_bob", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Developer view selector for the 3D renderer (0 = normal; see Render3DViewportControl.DebugView).</summary>
    public static readonly CVarDef<int> Render3DDebugView =
        CVarDef.Create("render3d.debug_view", 0, CVar.CLIENTONLY);

    /// <summary>
    ///     Developer aid: poll /render3d_dev.txt in the client's user data directory and run each line as a console
    ///     command (used to script screenshots and performance captures without OS-level input).
    /// </summary>
    public static readonly CVarDef<bool> Render3DDevChannel =
        CVarDef.Create("render3d.dev_channel", false, CVar.CLIENTONLY);
}
