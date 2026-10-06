using System.Collections.Generic;
using Content.Client.Render3D;
using NUnit.Framework;
using Robust.Shared.GameObjects;

namespace Content.Tests.Client.Render3D;

[TestFixture]
public sealed class PointListStateTest
{
    private static EntityUid U(int id) => new(id);

    private static PointListState With(params int[] ids)
    {
        var state = new PointListState();
        var list = new List<(EntityUid, string, bool)>();
        foreach (var id in ids)
            list.Add((U(id), $"thing {id}", true));

        state.SetEntries(list);
        return state;
    }

    [Test]
    public void NothingIsHighlightedUntilTheKeysAreUsed()
    {
        Assert.That(With(1, 2, 3).Highlight, Is.EqualTo(-1));
    }

    [Test]
    public void DownFromNothingIsTheFirstRowAndUpIsTheLast()
    {
        var a = With(1, 2, 3);
        a.Move(1);
        Assert.That(a.Highlight, Is.EqualTo(0));

        var b = With(1, 2, 3);
        b.Move(-1);
        Assert.That(b.Highlight, Is.EqualTo(2));
    }

    [Test]
    public void TheHighlightWrapsRound()
    {
        var s = With(1, 2, 3);
        s.Move(1);
        s.Move(-1);
        Assert.That(s.Highlight, Is.EqualTo(2));
        s.Move(1);
        Assert.That(s.Highlight, Is.EqualTo(0));
    }

    [Test]
    public void TheHighlightFollowsTheEntityWhenTheListChanges()
    {
        var s = With(1, 2, 3);
        s.Move(1);
        s.Move(1); // thing 2
        s.SetEntries(new List<(EntityUid, string, bool)> { (U(9), "new", true), (U(2), "thing 2", true), (U(3), "thing 3", true) });
        Assert.That(s.Highlight, Is.EqualTo(1));
    }

    [Test]
    public void TheHighlightIsLostWhenItsEntityLeavesTheList()
    {
        var s = With(1, 2, 3);
        s.Move(1);
        s.SetEntries(new List<(EntityUid, string, bool)> { (U(2), "thing 2", true) });
        Assert.That(s.Highlight, Is.EqualTo(-1));
    }

    [Test]
    public void SelectingChoosesTheTargetAndSelectingItAgainClearsIt()
    {
        var s = With(1, 2, 3);
        Assert.That(s.SelectHighlighted(), Is.False);
        s.Move(1);
        s.Move(1);
        Assert.That(s.SelectHighlighted(), Is.True);
        Assert.That(s.Target, Is.EqualTo(U(2)));
        Assert.That(s.SelectHighlighted(), Is.True);
        Assert.That(s.Target, Is.Null);
    }

    [Test]
    public void ClickingARowTogglesItsEntity()
    {
        var s = With(1, 2);
        s.Toggle(U(1));
        Assert.That(s.Target, Is.EqualTo(U(1)));
        s.Toggle(U(2));
        Assert.That(s.Target, Is.EqualTo(U(2)));
        s.Toggle(U(2));
        Assert.That(s.Target, Is.Null);
    }

    [Test]
    public void AnEmptyListHasNothingToMoveOrSelect()
    {
        var s = With();
        s.Move(1);
        Assert.That(s.Highlight, Is.EqualTo(-1));
        Assert.That(s.SelectHighlighted(), Is.False);
    }
}
