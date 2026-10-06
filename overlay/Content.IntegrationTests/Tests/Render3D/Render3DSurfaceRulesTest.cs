using System.Collections.Generic;
using System.Linq;
using Content.Shared.Render3D;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Render3D;

/// <summary>
///     The <c>surfaces</c> part of the render3dRules prototypes (how high the top of a bed, a sink and so on is, for the items that
///     lie on it) names prototypes as plain strings. In this codebase they must exist and the heights must be sensible, so a
///     rename upstream shows up here instead of silently turning a rule off.
/// </summary>
[TestFixture]
public sealed class Render3DSurfaceRulesTest
{
    [Test]
    public async Task EverySurfaceRuleNamesRealPrototypesAndSensibleHeights()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoMan = pair.Server.ResolveDependency<IPrototypeManager>();

        var known = new HashSet<string>();
        foreach (var proto in protoMan.EnumeratePrototypes<EntityPrototype>())
        {
            foreach (var (id, _) in protoMan.EnumerateAllParents<EntityPrototype>(proto.ID, includeSelf: true))
                known.Add(id);
        }

        var problems = new List<string>();
        var rules = 0;
        foreach (var set in protoMan.EnumeratePrototypes<Render3DRulesPrototype>())
        {
            foreach (var rule in set.Surfaces)
            {
                rules++;
                if (rule.Parents.Count == 0)
                    problems.Add($"{set.ID}: a surface rule without parents");

                foreach (var parent in rule.Parents.Where(p => !known.Contains(p)))
                    problems.Add($"{set.ID}: unknown prototype {parent}");

                if (rule.Height is < 0.05f or > 1.5f)
                    problems.Add($"{set.ID}: surface height {rule.Height} is not between 0.05 and 1.5");
            }
        }

        Assert.That(rules, Is.GreaterThan(0), "no surface rules loaded");
        Assert.That(problems, Is.Empty);
        await pair.CleanReturnAsync();
    }
}
