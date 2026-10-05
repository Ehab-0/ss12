using Content.Client.Render3D;
using Content.IntegrationTests.Tests.Movement;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Render3D;

/// <summary>
///     Which wall-mounted things the 3D view hangs up at ceiling height with a glow (the lamps), and which stay at eye level
///     with the rest of the wall equipment. An APC carries a small point light too, but it is not a lamp.
/// </summary>
public sealed class Render3DFixtureTest : MovementTest
{
    protected override bool AddWalls => false;

    private async Task<bool> IsFixture(string prototype)
    {
        var spawned = await SpawnEntity(prototype, SEntMan.GetCoordinates(PlayerCoords));
        await RunTicks(5);

        var result = false;
        await Client.WaitPost(() =>
        {
            var classifier = new EntityClassifier(CEntMan, Client.ResolveDependency<IPrototypeManager>());
            var uid = CEntMan.GetEntity(SEntMan.GetNetEntity(spawned));
            Assert.That(CEntMan.HasComponent<PointLightComponent>(uid), $"{prototype} should carry a point light for this test to mean something");
            result = classifier.TryGetFixture(uid, out _);
        });

        return result;
    }

    [Test]
    public async Task AWallLampIsAFixture()
    {
        Assert.That(await IsFixture("PoweredlightLED"), Is.True);
    }

    [Test]
    public async Task AnEmergencyLightIsAFixture()
    {
        Assert.That(await IsFixture("EmergencyLight"), Is.True);
    }

    [Test]
    public async Task AnApcIsNotAFixtureEvenThoughItHasALight()
    {
        Assert.That(await IsFixture("APCBasic"), Is.False);
    }
}
