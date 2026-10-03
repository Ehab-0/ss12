using System.Text.RegularExpressions;

namespace Ss12;

public enum EditStatus
{
    /// <summary>The edit is already in the file.</summary>
    Applied,

    /// <summary>The anchor was found and the edit can be applied (<c>NewText</c> is set).</summary>
    Pending,

    /// <summary>Neither the edit nor its anchor is in the file.</summary>
    NoAnchor,
}

public readonly record struct EditOutcome(EditStatus Status, string? NewText = null);

/// <summary>
///     One small, anchored edit of an existing file in the target codebase. Edits are described by what the code looks
///     like (regular expressions over the file text), never by line numbers, so they survive unrelated changes and
///     most fork differences. Every edit is idempotent: it can tell that it has already been applied.
/// </summary>
public abstract record Edit(string Done)
{
    /// <summary>Marker text that is present in a file once this edit was applied.</summary>
    public string Done { get; } = Done;

    /// <summary>Examines <paramref name="text"/> (LF line endings).</summary>
    public abstract EditOutcome Evaluate(string text);

    protected bool IsDone(string text) => Done.Length > 0 && text.Contains(Done, StringComparison.Ordinal);
}

/// <summary>
///     Replaces every match of <paramref name="Find"/> (a regex) by <paramref name="Replace"/> (regex replacement
///     syntax). With <paramref name="DoneFirst"/> the marker decides: use it when the replacement still contains
///     text matching <c>Find</c> (so the edit would otherwise match again on every run).
/// </summary>
public sealed record ReplaceEdit(string Find, string Replace, string Done, bool DoneFirst = false) : Edit(Done)
{
    private readonly Regex _regex = new(Find, RegexOptions.Multiline | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));

    public override EditOutcome Evaluate(string text)
    {
        if (DoneFirst && IsDone(text))
            return new EditOutcome(EditStatus.Applied);

        if (_regex.IsMatch(text))
            return new EditOutcome(EditStatus.Pending, _regex.Replace(text, Replace));

        return new EditOutcome(IsDone(text) ? EditStatus.Applied : EditStatus.NoAnchor);
    }
}

/// <summary>
///     Inserts text directly after the first match of <paramref name="Anchor"/>. The text can depend on what the anchor
///     matched (named groups), for code that differs slightly between codebases.
/// </summary>
public sealed record InsertAfterEdit(string Anchor, Func<Match, string> Build, string Done) : Edit(Done)
{
    private readonly Regex _regex = new(Anchor, RegexOptions.Multiline | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));

    public InsertAfterEdit(string Anchor, string Text, string Done)
        : this(Anchor, _ => Text, Done)
    {
    }

    public override EditOutcome Evaluate(string text)
    {
        if (IsDone(text))
            return new EditOutcome(EditStatus.Applied);

        var match = _regex.Match(text);
        if (!match.Success)
            return new EditOutcome(EditStatus.NoAnchor);

        return new EditOutcome(EditStatus.Pending, text.Insert(match.Index + match.Length, Build(match)));
    }
}

/// <summary>Inserts <paramref name="Text"/> directly before the first match of <paramref name="Anchor"/>.</summary>
public sealed record InsertBeforeEdit(string Anchor, Func<Match, string> Build, string Done) : Edit(Done)
{
    private readonly Regex _regex = new(Anchor, RegexOptions.Multiline | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));

    public InsertBeforeEdit(string Anchor, string Text, string Done)
        : this(Anchor, _ => Text, Done)
    {
    }

    public override EditOutcome Evaluate(string text)
    {
        if (IsDone(text))
            return new EditOutcome(EditStatus.Applied);

        var match = _regex.Match(text);
        if (!match.Success)
            return new EditOutcome(EditStatus.NoAnchor);

        return new EditOutcome(EditStatus.Pending, text.Insert(match.Index, Build(match)));
    }
}

/// <summary>A named group of edits over a set of files, with a severity and instructions for doing it by hand.</summary>
public sealed record Transform(
    string Id,
    string Title,
    bool Required,
    string Impact,
    string Manual,
    string[] Include,
    string[] Exclude,
    int MinFiles,
    Edit[] Edits,
    string[]? RequiresText = null);

public enum TransformState
{
    /// <summary>Can be applied (there are pending changes).</summary>
    Pending,

    /// <summary>Already applied everywhere it applies.</summary>
    Applied,

    /// <summary>The code to change was not found (in enough files, or not all edits of a file).</summary>
    AnchorMissing,
}

public sealed class TransformResult
{
    public required Transform Transform { get; init; }
    public TransformState State { get; set; }

    /// <summary>Files that will be changed.</summary>
    public List<string> Files { get; } = new();

    public int AppliedFiles { get; set; }
    public List<string> Notes { get; } = new();

    /// <summary>New file contents to write, by repository-relative path (LF text).</summary>
    public Dictionary<string, string> Changes { get; } = new();
}

public static class TransformEngine
{
    /// <summary>
    ///     Plans a transform against the files under <paramref name="root"/> without writing anything. Per file it is
    ///     all or nothing: when one of the edits for a file cannot find its anchor the file is left alone.
    /// </summary>
    public static TransformResult Plan(string root, Transform transform, Func<string, string?> readLf)
    {
        var result = new TransformResult { Transform = transform };
        var relevant = 0;

        foreach (var rel in FileSet.Find(root, transform.Include, transform.Exclude))
        {
            var text = readLf(rel);
            if (text == null)
                continue;

            // a file that lacks code the edit text depends on is not a target at all (it is a different design)
            if (transform.RequiresText != null && transform.RequiresText.Any(t => !text.Contains(t, StringComparison.Ordinal)))
                continue;

            var current = text;
            var outcomes = new List<EditStatus>();
            foreach (var edit in transform.Edits)
            {
                var outcome = edit.Evaluate(current);
                outcomes.Add(outcome.Status);
                if (outcome.Status == EditStatus.Pending)
                    current = outcome.NewText!;
            }

            // A file is part of this transform when at least one edit found its anchor (or was already applied).
            if (outcomes.All(s => s == EditStatus.NoAnchor))
                continue;

            relevant++;

            if (outcomes.Any(s => s == EditStatus.NoAnchor))
            {
                var missing = transform.Edits.Where((_, i) => outcomes[i] == EditStatus.NoAnchor).Select(e => Short(e)).ToList();
                result.Notes.Add($"{rel}: skipped, no anchor for: {string.Join("; ", missing)}");
                relevant--; // not usable
                continue;
            }

            if (outcomes.Any(s => s == EditStatus.Pending))
            {
                result.Files.Add(rel);
                result.Changes[rel] = current;
            }
            else
            {
                result.AppliedFiles++;
            }
        }

        if (relevant < transform.MinFiles)
        {
            result.State = TransformState.AnchorMissing;
            result.Notes.Add($"usable in {relevant} file(s), expected at least {transform.MinFiles}");
        }
        else
        {
            result.State = result.Files.Count > 0 ? TransformState.Pending : TransformState.Applied;
        }

        return result;
    }

    private static string Short(Edit edit)
    {
        var text = edit switch
        {
            ReplaceEdit r => r.Find,
            InsertAfterEdit i => i.Anchor,
            InsertBeforeEdit b => b.Anchor,
            _ => edit.ToString() ?? "?",
        };

        text = text.Replace("\n", "\\n");
        return text.Length > 70 ? text[..70] + "..." : text;
    }
}

/// <summary>Tiny glob matcher (<c>*</c> within a directory, <c>**</c> across directories).</summary>
public static class FileSet
{
    private static readonly string[] SkipDirs = { ".git", "bin", "obj", "node_modules", ".ss12", ".vs", "RobustToolbox" };

    public static Regex GlobToRegex(string glob)
    {
        var s = glob.Replace('\\', '/');
        var sb = new System.Text.StringBuilder("^");
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '*')
            {
                if (i + 1 < s.Length && s[i + 1] == '*')
                {
                    i++;
                    if (i + 1 < s.Length && s[i + 1] == '/')
                    {
                        i++;
                        sb.Append("(?:.*/)?");
                    }
                    else
                    {
                        sb.Append(".*");
                    }
                }
                else
                {
                    sb.Append("[^/]*");
                }
            }
            else if (c == '?')
            {
                sb.Append("[^/]");
            }
            else
            {
                sb.Append(Regex.Escape(c.ToString()));
            }
        }

        sb.Append('$');
        return new Regex(sb.ToString(), RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    }

    /// <summary>All files under <paramref name="root"/> (relative, '/'-separated) matching any include and no exclude glob.</summary>
    public static List<string> Find(string root, IEnumerable<string> include, IEnumerable<string>? exclude = null)
    {
        var inc = include.Select(GlobToRegex).ToArray();
        var exc = (exclude ?? Array.Empty<string>()).Select(GlobToRegex).ToArray();
        var result = new List<string>();
        Walk(root, root, inc, exc, result);
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    private static void Walk(string root, string dir, Regex[] inc, Regex[] exc, List<string> result)
    {
        foreach (var file in Directory.EnumerateFiles(dir))
        {
            var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (inc.Any(r => r.IsMatch(rel)) && !exc.Any(r => r.IsMatch(rel)))
                result.Add(rel);
        }

        foreach (var sub in Directory.EnumerateDirectories(dir))
        {
            var name = Path.GetFileName(sub);
            if (SkipDirs.Contains(name, StringComparer.OrdinalIgnoreCase))
                continue;

            Walk(root, sub, inc, exc, result);
        }
    }
}
