using Content.Client.Render3D;
using Content.IntegrationTests.Tests.Movement;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Render3D;

/// <summary>
///     Which windows, window doors and grilles the 3D view draws as glass (a thin frame and a see-through pane) and which keep
///     their sprite, resolved by the client's classifier on real entities.
/// </summary>
public sealed class Render3DGlassLookTest : MovementTest
{
    protected override bool AddWalls => false;

    private async Task<GlassLook> LookOf(string prototype)
    {
        var spawned = await SpawnEntity(prototype, SEntMan.GetCoordinates(PlayerCoords));
        await RunTicks(5);

        GlassLook look = null;
        await Client.WaitPost(() =>
        {
            var classifier = new EntityClassifier(CEntMan, Client.ResolveDependency<IPrototypeManager>());
            look = classifier.GetGlass(CEntMan.GetEntity(SEntMan.GetNetEntity(spawned)));
        });

        return look;
    }

    [TestCase("Window")]
    [TestCase("ReinforcedWindow")]
    [TestCase("PlasmaWindow")]
    [TestCase("ReinforcedPlasmaWindow")]
    [TestCase("UraniumWindow")]
    [TestCase("TintedWindow")]
    [TestCase("WindowReinforcedDirectional")]
    [TestCase("WindowFrostedDirectional")]
    [TestCase("Windoor")]
    [TestCase("WindoorPlasma")]
    [TestCase("Grille")]
    public async Task GlassIsDrawnAsGlass(string prototype)
    {
        Assert.That(await LookOf(prototype), Is.Not.Null, $"{prototype} should be drawn as glass");
    }

    [TestCase("WindowDiagonal")]
    [TestCase("ReinforcedWindowDiagonal")]
    [TestCase("WindowReinforcedDirectionalCorner")]
    [TestCase("GrilleDiagonal")]
    public async Task CornerAndDiagonalGlassKeepsItsSprite(string prototype)
    {
        Assert.That(await LookOf(prototype), Is.Null, $"{prototype} is not a plain pane and should keep its sprite");
    }

    [Test]
    public async Task AReinforcedWindowHasAThickerFrameAndLessClarityThanAPlainOne()
    {
        var plain = await LookOf("Window");
        var reinforced = await LookOf("ReinforcedWindow");
        Assert.That(reinforced.FrameWidth, Is.GreaterThan(plain.FrameWidth));
        Assert.That(reinforced.Alpha, Is.GreaterThan(plain.Alpha));
    }

    [Test]
    public async Task AGrilleIsAMeshWithNoPane()
    {
        var grille = await LookOf("Grille");
        Assert.That(grille.Mesh, Is.GreaterThan(0));
        Assert.That(grille.Alpha, Is.EqualTo(0f));
    }
}
