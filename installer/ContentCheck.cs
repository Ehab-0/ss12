using System.Text.RegularExpressions;

namespace Ss12;

/// <summary>
///     Read-only checks of a codebase's own content against what the 3D view expects, for <c>ss12 doctor</c>. They look at text only
///     (no build), never fail the doctor, and say what to do about each finding. They exist because a fork's problems with 3D are
///     mostly names: a prototype the shipped rules mention that the fork renamed, or code that aims with the real cursor.
/// </summary>
public static class ContentCheck
{
    private const string RulesDir = "Resources/Prototypes/Render3D";

    /// <summary>
    ///     Prototype names that the Render3D rule files list under <c>parents:</c> but that no prototype of the codebase defines.
    ///     Such a rule matches nothing (harmless), but whatever it was written for is then drawn by the generic guesses.
    ///     Returns the rule file (relative) with the missing names.
    /// </summary>
    public static SortedDictionary<string, List<string>> UnknownRuleNames(string root)
    {
        var result = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        var rulesDir = Path.Combine(root, RulesDir);
        var protoDir = Path.Combine(root, "Resources", "Prototypes");
        if (!Directory.Exists(rulesDir) || !Directory.Exists(protoDir))
            return result;

        // every "id:" of every prototype file: more than the entity prototypes, so the answer errs on "known"
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(protoDir, "*.yml", SearchOption.AllDirectories))
        {
            if (Path.GetFullPath(file).StartsWith(Path.GetFullPath(rulesDir), StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var line in File.ReadLines(file))
            {
                var m = IdLine.Match(line);
                if (m.Success)
                    known.Add(m.Groups["id"].Value);
            }
        }

        foreach (var file in Directory.EnumerateFiles(rulesDir, "*.yml", SearchOption.TopDirectoryOnly))
        {
            var missing = new List<string>();
            foreach (var name in ParentNames(File.ReadAllLines(file)))
            {
                if (!known.Contains(name) && !missing.Contains(name))
                    missing.Add(name);
            }

            if (missing.Count > 0)
                result[Path.GetRelativePath(root, file).Replace('\\', '/')] = missing;
        }

        return result;
    }

    /// <summary>
    ///     Places in the client that turn the mouse position into a world position without going through the 3D pointer. In 3D the
    ///     real cursor is not where the player looks, so these aim in the wrong place. The installer rewrites the usual shapes;
    ///     what is left is code it did not recognise.
    /// </summary>
    public static List<string> CursorAimCalls(string root)
    {
        var found = new List<string>();
        var client = Path.Combine(root, "Content.Client");
        if (!Directory.Exists(client))
            return found;

        var own = Path.GetFullPath(Path.Combine(client, "Render3D"));
        var sep = Path.DirectorySeparatorChar;
        foreach (var file in Directory.EnumerateFiles(client, "*.cs", SearchOption.AllDirectories))
        {
            if (Path.GetFullPath(file).StartsWith(own, StringComparison.OrdinalIgnoreCase) || file.Contains($"{sep}obj{sep}") || file.Contains($"{sep}bin{sep}"))
                continue;

            var n = 0;
            foreach (var line in File.ReadLines(file))
            {
                n++;
                if (AimCall.IsMatch(line) && !line.Contains("Render3DPointer", StringComparison.Ordinal))
                    found.Add($"{Path.GetRelativePath(root, file).Replace('\\', '/')}:{n}");
            }
        }

        return found;
    }

    private static readonly Regex IdLine = new(@"^\s*(?:-\s+)?id:\s*(?<id>[A-Za-z0-9_.\-]+)\s*(?:#.*)?$", RegexOptions.Compiled);
    // the eye manager's PixelToMap (the 2D camera); a viewport's own PixelToMap is answered by the 3D view itself
    private static readonly Regex AimCall = new(@"\b\w*[eE]ye\w*\.PixelToMap\s*\(.*Mouse", RegexOptions.Compiled);
    private static readonly Regex Item = new(@"^\s*-\s+(?<v>[^\s#\[\]{}:]+)\s*(?:#.*)?$", RegexOptions.Compiled);
    private static readonly Regex Key = new(@"^\s*(?:-\s+)?parents:\s*(?<rest>.*)$", RegexOptions.Compiled);

    /// <summary>The names under every <c>parents:</c> key: a block list or an inline list.</summary>
    private static IEnumerable<string> ParentNames(string[] lines)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            var key = Key.Match(lines[i]);
            if (!key.Success)
                continue;

            var rest = key.Groups["rest"].Value.Trim();
            if (rest.StartsWith('['))
            {
                foreach (var part in rest.Split('#')[0].Trim('[', ']', ' ').Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var name = part.Trim('"', '\'');
                    if (name.Length > 0)
                        yield return name;
                }

                continue;
            }

            if (rest.Length > 0 && !rest.StartsWith('#'))
                continue;

            for (var j = i + 1; j < lines.Length; j++)
            {
                var t = lines[j].Trim();
                if (t.Length == 0 || t.StartsWith('#'))
                    continue;

                var item = Item.Match(lines[j]);
                if (!item.Success)
                    break;

                yield return item.Groups["v"].Value.Trim('"', '\'');
            }
        }
    }
}
