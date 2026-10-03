using System.Collections.Generic;
using System.Linq;
using Content.Shared.Render3D;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Render3D;

/// <summary>
///     The <c>shapes</c> part of the render3dRules prototypes (thickness and lean overrides per prototype / component)
///     names prototypes and components as plain strings. In this codebase they must all exist, and the numbers must be
///     sensible, so a rename upstream shows up here instead of silently turning a rule off.
/// </summary>
[TestFixture]
public sealed class Render3DShapeRulesTest
{
    [Test]
    public async Task EveryShapeRuleNamesRealPrototypesAndComponents()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoMan = pair.Server.ResolveDependency<IPrototypeManager>();
        var factory = pair.Server.ResolveDependency<IComponentFactory>();

        var known = new HashSet<string>();
        foreach (var proto in protoMan.EnumeratePrototypes<EntityPrototype>())
        {
            foreach (var (id, _) in protoMan.EnumerateAllParents<EntityPrototype>(proto.ID, includeSelf: true))
                known.Add(id);
        }

        var problems = new List<string>();
        var shapes = 0;
        foreach (var set in protoMan.EnumeratePrototypes<Render3DRulesPrototype>())
        {
            foreach (var shape in set.Shapes)
            {
                shapes++;
                if (shape.Parents.Count == 0 && shape.Components.Count == 0)
                    problems.Add($"{set.ID}: a shape rule with neither parents nor components");

                foreach (var parent in shape.Parents.Where(p => !known.Contains(p)))
                    problems.Add($"{set.ID}: unknown prototype {parent}");

                foreach (var name in shape.Components.Where(n => !factory.TryGetRegistration(n, out _)))
                    problems.Add($"{set.ID}: unknown component {name}");

                if (shape.Thickness > 0.5f)
                    problems.Add($"{set.ID}: thickness {shape.Thickness} is above half a tile");
            }
        }

        Assert.That(shapes, Is.GreaterThan(0), "no shape rules loaded");
        Assert.That(problems, Is.Empty);
        await pair.CleanReturnAsync();
    }
}
