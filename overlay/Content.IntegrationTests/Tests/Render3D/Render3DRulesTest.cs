using System.Collections.Generic;
using System.Linq;
using Content.Shared.Render3D;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Render3D;

/// <summary>
///     The render3dRules prototypes name entity prototypes as plain strings so that a codebase which renamed or
///     removed one of them does not fail validation. In this codebase every name must still be real: a rule that
///     matches nothing means a base prototype was renamed and 3D would quietly draw those entities with the generic
///     heuristics.
/// </summary>
[TestFixture]
public sealed class Render3DRulesTest
{
    [Test]
    public async Task EveryRuleParentIsAPrototypeOrAnAncestorOfOne()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoMan = pair.Server.ResolveDependency<IPrototypeManager>();

        // abstract prototypes are not indexed, but they show up as ancestors of concrete ones
        var known = new HashSet<string>();
        foreach (var proto in protoMan.EnumeratePrototypes<EntityPrototype>())
        {
            foreach (var (id, _) in protoMan.EnumerateAllParents<EntityPrototype>(proto.ID, includeSelf: true))
                known.Add(id);
        }

        var ruleSets = protoMan.EnumeratePrototypes<Render3DRulesPrototype>().ToList();
        Assert.That(ruleSets, Is.Not.Empty, "no render3dRules prototypes loaded");

        var missing = new List<string>();
        foreach (var set in ruleSets)
        {
            foreach (var rule in set.Rules)
            {
                foreach (var parent in rule.Parents)
                {
                    if (!known.Contains(parent))
                        missing.Add($"{set.ID}: {parent} ({rule.Mode})");
                }
            }
        }

        Assert.That(missing, Is.Empty, "rules naming prototypes that do not exist");
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DoorsWindowsAndTablesInheritTheirRuleParents()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoMan = pair.Server.ResolveDependency<IPrototypeManager>();

        Assert.That(Inherits(protoMan, "AirlockGlass", "BaseAirlockIndestructible"));
        Assert.That(Inherits(protoMan, "Table", "TableBase"));
        Assert.That(Inherits(protoMan, "WindowDirectional", "BaseWindowStructureDirectional"));
        await pair.CleanReturnAsync();
    }

    private static bool Inherits(IPrototypeManager protoMan, string id, string ancestor)
    {
        foreach (var (parent, _) in protoMan.EnumerateAllParents<EntityPrototype>(id, includeSelf: true))
        {
            if (parent == ancestor)
                return true;
        }

        return false;
    }
}
