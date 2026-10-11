using System.Collections.Generic;
using System.Linq;
using Content.Shared.Render3D;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Render3D;

/// <summary>
///     The aliases of the render3dRules prototypes give the rules of a renamed base prototype to its older name, for forks
///     on older code. Every alias points at a name the rules use. Where the new name exists (current code), the old name,
///     if it still exists, must inherit it: then the alias changes nothing. Where only the old name exists (older code),
///     that is what the alias is for.
/// </summary>
[TestFixture]
public sealed class Render3DRuleAliasesTest
{
    [Test]
    public async Task EveryAliasNamesARuleParentAndChangesNothingHere()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoMan = pair.Server.ResolveDependency<IPrototypeManager>();

        var ruleSets = protoMan.EnumeratePrototypes<Render3DRulesPrototype>().ToList();
        var ruleParents = new HashSet<string>();
        foreach (var set in ruleSets)
        {
            ruleParents.UnionWith(set.Rules.SelectMany(r => r.Parents));
            ruleParents.UnionWith(set.Shapes.SelectMany(r => r.Parents));
            ruleParents.UnionWith(set.Glass.SelectMany(r => r.Parents));
            ruleParents.UnionWith(set.Surfaces.SelectMany(r => r.Parents));
        }

        var aliases = ruleSets.SelectMany(s => s.Aliases).ToList();
        Assert.That(aliases, Is.Not.Empty, "no aliases loaded");

        var problems = new List<string>();
        foreach (var (old, current) in aliases)
        {
            if (old == current)
                problems.Add($"{old}: an alias of itself");
            if (!ruleParents.Contains(current))
                problems.Add($"{old}: {current} is named by no rule");
            if (ruleParents.Contains(old))
                problems.Add($"{old}: has rules of its own, so the alias does nothing");
            if (Exists(protoMan, current) && Exists(protoMan, old) && !Inherits(protoMan, old, current))
                problems.Add($"{old}: exists here next to {current} but does not inherit it, so the alias would change how it is drawn");
        }

        Assert.That(problems, Is.Empty);
        await pair.CleanReturnAsync();
    }

    private static bool Exists(IPrototypeManager protoMan, string id) => protoMan.HasIndex<EntityPrototype>(id) || IsAncestor(protoMan, id);

    private static bool IsAncestor(IPrototypeManager protoMan, string id)
    {
        foreach (var proto in protoMan.EnumeratePrototypes<EntityPrototype>())
        {
            foreach (var (parent, _) in protoMan.EnumerateAllParents<EntityPrototype>(proto.ID))
            {
                if (parent == id)
                    return true;
            }
        }

        return false;
    }

    private static bool Inherits(IPrototypeManager protoMan, string id, string ancestor)
    {
        foreach (var proto in protoMan.EnumeratePrototypes<EntityPrototype>())
        {
            var chain = protoMan.EnumerateAllParents<EntityPrototype>(proto.ID, includeSelf: true).Select(p => p.Item1).ToList();
            var at = chain.IndexOf(id);
            if (at >= 0)
                return chain.Contains(ancestor);
        }

        return false;
    }
}
