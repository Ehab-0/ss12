using System.Collections.Generic;
using System.Linq;
using Content.Shared.Render3D;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Render3D;

/// <summary>
///     The <c>glass</c> part of the render3dRules prototypes (how windows, window doors and grilles are drawn) names prototypes
///     as plain strings. In this codebase they must all exist and the numbers must be sensible, so a rename upstream shows up
///     here instead of silently turning a rule off.
/// </summary>
[TestFixture]
public sealed class Render3DGlassRulesTest
{
    [Test]
    public async Task EveryGlassRuleNamesRealPrototypesAndSensibleValues()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoMan = pair.Server.ResolveDependency<IPrototypeManager>();

        // abstract prototypes (the base prototypes the rules mostly name) are not indexed at runtime: find them as ancestors
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
            foreach (var rule in set.Glass)
            {
                rules++;
                if (rule.Parents.Count == 0)
                    problems.Add($"{set.ID}: a glass rule without parents");

                foreach (var parent in rule.Parents.Where(p => !known.Contains(p)))
                    problems.Add($"{set.ID}: unknown prototype {parent}");

                if (rule.Alpha is < 0f or > 1f)
                    problems.Add($"{set.ID}: alpha {rule.Alpha} is not between 0 and 1");
                if (rule.FrameWidth is < 0 or > 6)
                    problems.Add($"{set.ID}: frame width {rule.FrameWidth} is not between 0 and 6");
                if (rule.Mesh is < 0 or > 16)
                    problems.Add($"{set.ID}: mesh {rule.Mesh} is not between 0 and 16");
            }
        }

        Assert.That(rules, Is.GreaterThan(0), "no glass rules loaded");
        Assert.That(problems, Is.Empty);
        await pair.CleanReturnAsync();
    }
}
