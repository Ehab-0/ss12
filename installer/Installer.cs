using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ss12;

public sealed class Options
{
    public string Target { get; set; } = ".";
    public string? Source { get; set; }
    public bool Enforce { get; set; }
    public bool DryRun { get; set; }
    public bool Build { get; set; } = true;
    public bool Commit { get; set; } = true;
    public bool Force { get; set; }
    public string Branch { get; set; } = "ss12";
    public bool Report { get; set; }
    public string Configuration { get; set; } = "DebugOpt";
}

public sealed record FileRecord(string Path, string Sha256);

public sealed record ModifiedRecord(string Path, string OriginalSha256, string NewSha256, string[] Transforms);

public sealed record InstallManifest(
    string InstallerVersion,
    DateTime InstalledUtc,
    string? EngineVersion,
    bool Enforce,
    List<FileRecord> Overlay,
    List<ModifiedRecord> Modified,
    List<FileRecord> Generated,
    string? BaseCommit = null);

/// <summary>The install, update, uninstall and doctor operations.</summary>
public sealed class Installer
{
    public const string StateDir = ".ss12";
    public const string ManifestName = "manifest.json";
    public const string PresetPath = "Resources/ConfigPresets/Build/render3d.toml";

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private readonly Options _opt;
    private readonly string _root;
    private readonly Report _report;

    /// <param name="report">Where messages go; the MCP server passes a quiet one because stdout is its protocol channel.</param>
    public Installer(Options options, Report? report = null)
    {
        _opt = options;
        _root = Path.GetFullPath(options.Target);
        _report = report ?? new Report();
    }

    public string Root => _root;

    /// <summary>Plans every built-in edit against the codebase without writing anything.</summary>
    public List<TransformResult> PlanAll()
    {
        var cache = new Dictionary<string, string>();
        string? Read(string rel)
        {
            if (cache.TryGetValue(rel, out var text))
                return text;

            var path = Path.Combine(_root, rel);
            if (!File.Exists(path))
                return null;

            return cache[rel] = TextFile.Read(path).Lf;
        }

        var results = new List<TransformResult>();
        foreach (var transform in BuiltIn.All)
        {
            var planned = TransformEngine.Plan(_root, transform, Read);
            results.Add(planned);
            foreach (var (rel, newText) in planned.Changes)
                cache[rel] = newText;
        }

        return results;
    }

    public Report Report => _report;

    private string StatePath(string rel) => Path.Combine(_root, StateDir, rel);

    private string ManifestFile => StatePath(ManifestName);

    public static string InstallerVersion
    {
        get
        {
            var file = Path.Combine(AppContext.BaseDirectory, "version.txt");
            return File.Exists(file) ? File.ReadAllText(file).Trim() : "0.0.0";
        }
    }

    // ------------------------------------------------------------------ detection

    /// <summary>Checks that the target looks like an SS14 content repository.</summary>
    public bool ValidateRepo()
    {
        if (!Directory.Exists(_root))
        {
            _report.Error($"{_root} is not a directory.");
            return false;
        }

        var missing = new[] { "Content.Client/Content.Client.csproj", "Content.Shared/Content.Shared.csproj", "Content.Server/Content.Server.csproj" }
            .Where(f => !File.Exists(Path.Combine(_root, f))).ToList();
        if (missing.Count > 0)
        {
            _report.Error($"{_root} does not look like a Space Station 14 codebase (missing {string.Join(", ", missing)}). Point this at the folder that contains Content.Client, Content.Shared and Content.Server.");
            return false;
        }

        if (!Directory.Exists(Path.Combine(_root, "RobustToolbox")))
            _report.Warn("RobustToolbox/ not found (git submodule not initialised?). Builds will fail until it is.");
        else if (MissingSubmodules() is { Count: > 0 } missingParts)
            _report.Warn(SubmoduleAdvice(missingParts));

        return true;
    }

    /// <summary>
    ///     Whether git shows uncommitted changes that are not this tool's own (the files it added or edited, listed in
    ///     the manifest). An install whose build failed leaves its own changes uncommitted; that must not block
    ///     `uninstall` or `update`.
    /// </summary>
    private bool HasForeignChanges(Git git, InstallManifest? manifest)
    {
        var own = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (manifest != null)
        {
            foreach (var f in manifest.Overlay)
                own.Add(f.Path);
            foreach (var f in manifest.Modified)
                own.Add(f.Path);
            foreach (var f in manifest.Generated)
                own.Add(f.Path);
        }

        return git.DirtyFiles().Any(f => !own.Contains(f) && !f.StartsWith(StateDir + "/", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Parts of RobustToolbox (its own submodules, such as XamlX) that are listed but empty on disk.</summary>
    public List<string> MissingSubmodules()
    {
        var result = new List<string>();
        var file = Path.Combine(_root, "RobustToolbox", ".gitmodules");
        if (!File.Exists(file))
            return result;

        foreach (Match m in Regex.Matches(File.ReadAllText(file), @"^\s*path\s*=\s*(\S+)\s*$", RegexOptions.Multiline))
        {
            var dir = Path.Combine(_root, "RobustToolbox", m.Groups[1].Value.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(dir) || !Directory.EnumerateFileSystemEntries(dir).Any())
                result.Add(m.Groups[1].Value);
        }

        return result;
    }

    public static string SubmoduleAdvice(IEnumerable<string> missing) =>
        $"Parts of the game engine were not downloaded ({string.Join(", ", missing)}), so the code cannot be built. Run this once in your server's folder: git submodule update --init --recursive";

    public string? EngineVersion()
    {
        var props = Path.Combine(_root, "RobustToolbox", "MSBuild", "Robust.Engine.Version.props");
        if (!File.Exists(props))
            return null;

        var match = Regex.Match(File.ReadAllText(props), @"<Version>\s*([\d.]+)\s*</Version>");
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>Compares the codebase's engine version with the versions this overlay was tested on.</summary>
    public bool CheckEngine(SourceInfo source)
    {
        var version = EngineVersion();
        if (version == null)
        {
            _report.Warn("Could not read the engine version (RobustToolbox/MSBuild/Robust.Engine.Version.props); continuing.");
            return true;
        }

        var compat = source.Compat;
        var major = int.TryParse(version.Split('.')[0], out var m) ? m : -1;

        var minMajor = int.TryParse(compat.MinEngineVersion.Split('.')[0], out var mm) ? mm : 0;
        if (major >= 0 && major < minMajor)
        {
            var tooOld = $"Engine {version} is older than {compat.MinEngineVersion}, the first version with relative mouse mode for windows (needed for mouse-look) and the viewport FOV render target. The 3D view cannot compile against it. Update your codebase's RobustToolbox (and the content that goes with it) first.";
            if (!_opt.Force)
            {
                _report.Error(tooOld);
                return false;
            }

            _report.Warn(tooOld + " Continuing because of --force; expect compile errors.");
            return true;
        }

        var testedMajors = compat.TestedEngineVersions.Select(v => int.Parse(v.Split('.')[0])).ToList();

        if (testedMajors.Contains(major))
        {
            _report.Info($"Engine {version}: tested.");
            return true;
        }

        var nearest = testedMajors.Count == 0 ? int.MaxValue : testedMajors.Min(t => Math.Abs(t - major));
        if (nearest <= compat.WarnWithin)
        {
            _report.Warn($"Engine {version} is close to the tested versions ({string.Join(", ", compat.TestedEngineVersions)}). It will probably work; run `ss12 doctor --build` to find out.");
            return true;
        }

        var message = $"Engine {version} is far from the tested versions ({string.Join(", ", compat.TestedEngineVersions)}); engine APIs used by the 3D view may differ.";
        if (_opt.Force)
        {
            _report.Warn(message + " Continuing because of --force.");
            return true;
        }

        _report.Error(message + " Use --force to try anyway.");
        return false;
    }

    // ------------------------------------------------------------------ install / update

    public int Install(bool update)
    {
        if (!ValidateRepo())
            return 2;

        var source = SourceInfo.Locate(_opt.Source, _report);
        if (source == null)
            return 2;

        if (!CheckEngine(source))
            return 2;

        var existing = ReadManifest();
        if (existing != null && !update)
        {
            _report.Error($"3D is already installed here (installer {existing.InstallerVersion}). Use `ss12 update` to upgrade, `ss12 doctor` to check it, or `ss12 uninstall` first.");
            return 2;
        }

        if (existing == null && update)
        {
            _report.Error("Nothing to update: 3D is not installed here (no .ss12/manifest.json). Use `ss12 install`.");
            return 2;
        }

        var git = new Git(_root);
        var useGit = git.IsRepo;
        if (!useGit)
            _report.Warn("Not a git repository: no branch or commit will be made, and your changes cannot be reviewed with git diff.");
        else if (HasForeignChanges(git, existing) && !_opt.Force && !_opt.DryRun)
        {
            _report.Error("The git working tree has uncommitted changes. Commit or stash them first (or use --force).");
            return 2;
        }

        // ---- plan overlay files
        var overlayFiles = source.OverlayFiles();
        var overlayPlan = new List<(string Rel, string SourcePath, string Why)>();
        var previous = existing?.Overlay.ToDictionary(f => f.Path, f => f.Sha256, StringComparer.Ordinal)
                       ?? new Dictionary<string, string>();
        foreach (var rel in overlayFiles)
        {
            var src = Path.Combine(source.Root, rel);
            var dst = Path.Combine(_root, rel);
            if (!File.Exists(dst))
            {
                overlayPlan.Add((rel, src, "new"));
                continue;
            }

            var current = Hashing.Sha256(dst);
            if (current == Hashing.Sha256(src))
                continue; // identical already

            if (previous.TryGetValue(rel, out var recorded) && recorded == current)
            {
                overlayPlan.Add((rel, src, "updated"));
            }
            else if (_opt.Force)
            {
                overlayPlan.Add((rel, src, "overwritten (--force)"));
                _report.Warn($"Overwriting {rel}, which exists in your codebase and differs.");
            }
            else
            {
                _report.Error($"{rel} already exists and was not installed by this tool. Move it away or use --force.");
                return 2;
            }
        }

        // ---- plan transforms (each against the text as changed by the transforms before it, so two transforms that
        // touch the same file build on each other instead of overwriting each other)
        var results = new List<TransformResult>();
        var original = new Dictionary<string, TextFile.Loaded>();
        var working = new Dictionary<string, string>();
        var touchedBy = new Dictionary<string, List<string>>();
        string? Read(string rel)
        {
            if (working.TryGetValue(rel, out var text))
                return text;

            var path = Path.Combine(_root, rel);
            if (!File.Exists(path))
                return null;

            var loaded = TextFile.Read(path);
            original[rel] = loaded;
            working[rel] = loaded.Lf;
            return loaded.Lf;
        }

        foreach (var transform in BuiltIn.All)
        {
            var planned = TransformEngine.Plan(_root, transform, Read);
            results.Add(planned);
            foreach (var (rel, newText) in planned.Changes)
            {
                working[rel] = newText;
                if (!touchedBy.TryGetValue(rel, out var ids))
                    touchedBy[rel] = ids = new List<string>();
                ids.Add(transform.Id);
            }
        }

        var blocked = false;
        _report.Section("Edits to existing files");
        foreach (var r in results)
        {
            var t = r.Transform;
            var tag = t.Required ? "required" : "optional";
            switch (r.State)
            {
                case TransformState.Pending:
                    _report.Info($"[apply]   {t.Id} ({tag}): {t.Title}  -> {string.Join(", ", r.Files)}");
                    break;
                case TransformState.Applied:
                    _report.Info($"[present] {t.Id} ({tag}): already applied");
                    break;
                case TransformState.AnchorMissing:
                    if (t.Required)
                    {
                        blocked = true;
                        _report.Error($"[MISSING] {t.Id} (required): the code to change was not found.");
                    }
                    else
                    {
                        _report.Warn($"[skip]    {t.Id} (optional): the code to change was not found. {t.Impact}");
                    }

                    _report.Detail($"What it does: {t.Title}");
                    _report.Detail($"Why it matters: {t.Impact}");
                    _report.Detail($"By hand: {t.Manual}");
                    foreach (var note in r.Notes)
                        _report.Detail(note);
                    break;
            }
        }

        if (blocked)
        {
            _report.Error("A required edit cannot be applied automatically, so nothing was changed. Apply it by hand as described above (or ask for a transform for your fork), then run the installer again.");
            return 3;
        }

        _report.Section("Summary");
        _report.Info($"{overlayPlan.Count} file(s) to add or update, {results.Sum(r => r.Files.Count)} existing file(s) to edit, engine {EngineVersion() ?? "?"}.");
        if (_opt.DryRun)
        {
            _report.Info("Dry run: nothing was written.");
            return 0;
        }

        var baseCommit = existing?.BaseCommit ?? (useGit ? git.Head : null);

        // ---- branch
        if (useGit && !update && _opt.Commit && git.CurrentBranch != _opt.Branch)
        {
            var created = git.BranchExists(_opt.Branch) ? git.Checkout(_opt.Branch) : git.CheckoutNew(_opt.Branch);
            if (created.ExitCode != 0)
            {
                _report.Error($"Could not switch to branch '{_opt.Branch}': {created.Output.Trim()}");
                return 2;
            }

            _report.Info($"On branch '{_opt.Branch}'.");
        }

        // ---- apply
        Directory.CreateDirectory(StatePath("backup"));
        var manifest = existing ?? new InstallManifest(InstallerVersion, DateTime.UtcNow, EngineVersion(), _opt.Enforce,
            new List<FileRecord>(), new List<ModifiedRecord>(), new List<FileRecord>());

        var overlayRecords = manifest.Overlay.ToDictionary(f => f.Path, StringComparer.Ordinal);
        foreach (var (rel, src, _) in overlayPlan)
        {
            var dst = Path.Combine(_root, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            File.Copy(src, dst, true);
            overlayRecords[rel] = new FileRecord(rel, Hashing.Sha256(dst));
        }

        // files that were identical already still belong to the installation
        foreach (var rel in overlayFiles)
        {
            if (!overlayRecords.ContainsKey(rel) && File.Exists(Path.Combine(_root, rel)))
                overlayRecords[rel] = new FileRecord(rel, Hashing.Sha256(Path.Combine(_root, rel)));
        }

        var modifiedRecords = manifest.Modified.ToDictionary(f => f.Path, StringComparer.Ordinal);
        foreach (var (rel, ids) in touchedBy)
        {
            var path = Path.Combine(_root, rel);
            var loaded = original[rel];

            // keep the pristine original once (first change wins)
            var backup = StatePath(Path.Combine("backup", rel));
            var originalHash = modifiedRecords.TryGetValue(rel, out var old) ? old.OriginalSha256 : Hashing.Sha256(path);
            if (!File.Exists(backup))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                File.Copy(path, backup, false);
            }

            TextFile.Write(path, working[rel], loaded.HasBom, loaded.Crlf);
            var all = (old?.Transforms ?? Array.Empty<string>()).Concat(ids).Distinct().ToArray();
            modifiedRecords[rel] = new ModifiedRecord(rel, originalHash, Hashing.Sha256(path), all);
        }

        // ---- server preset
        var generated = new List<FileRecord>();
        var presetPath = Path.Combine(_root, PresetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(presetPath)!);
        var enforce = update ? manifest.Enforce : _opt.Enforce;
        File.WriteAllText(presetPath, PresetText(enforce));
        generated.Add(new FileRecord(PresetPath, Hashing.Sha256(presetPath)));

        var newManifest = new InstallManifest(InstallerVersion, DateTime.UtcNow, EngineVersion(), enforce,
            overlayRecords.Values.OrderBy(f => f.Path, StringComparer.Ordinal).ToList(),
            modifiedRecords.Values.OrderBy(f => f.Path, StringComparer.Ordinal).ToList(),
            generated,
            baseCommit);
        File.WriteAllText(ManifestFile, JsonSerializer.Serialize(newManifest, Json));

        // ---- build
        if (_opt.Build)
        {
            _report.Section("Build");
            if (!BuildAll())
            {
                _report.Error("The build failed. The 3D files are in place but NOT committed yet. Fix the problem and run `ss12 doctor --build` to check again (then commit with git), or run `ss12 uninstall` to go back.");
                return 4;
            }
        }

        // ---- commit
        if (useGit && _opt.Commit)
        {
            var paths = newManifest.Overlay.Select(f => f.Path)
                .Concat(newManifest.Modified.Select(f => f.Path))
                .Concat(newManifest.Generated.Select(f => f.Path))
                .Append(StateDir + "/" + ManifestName);
            git.ExcludeLocally(StateDir + "/backup/");
            var add = git.Add(paths);
            if (add.ExitCode != 0)
            {
                _report.Warn("git add failed: " + add.Output.Trim());
            }
            else
            {
                var commit = git.Commit($"{(update ? "Update" : "Add")} SS12 {InstallerVersion}\n\nInstalled with the SS12 installer. See docs/ss12/README.md. Run `ss12 uninstall` to remove it again.");
                _report.Info(commit.ExitCode == 0 ? "Committed to git (not pushed)." : "git commit failed: " + commit.Output.Trim());
            }
        }

        _report.Section("Done");
        _report.Info("3D is installed. Next steps:");
        _report.Info($"  1. Server: apply the preset (see {PresetPath}) or pass the cvars it lists; `render3d.enforced` is {(enforce ? "ON (everyone alive plays in 3D)" : "OFF (players choose with F12)")}.");
        _report.Info("  2. Publish a server build with `ss12 package`; players download the 3D client from your server with the normal launcher.");
        _report.Info("  3. Read docs/ss12/ONBOARDING.md, run `ss12 doctor`. It also lists names in the 3D rules that your fork does not have;");
        _report.Info("     rules for your own prototypes go in Resources/Prototypes/Render3D/<yourserver>.yml (see PORTING-YOUR-SERVER.md in the docs folder of the SS12 repository).");
        return 0;
    }

    public static string PresetText(bool enforce) =>
        "# Configuration preset for the 3D view (docs/ss12/README.md). Generated by the SS12 installer.\n"
        + "# Forces the 3D view on for living players (set enforced = false to let players choose) and keeps WASD camera-relative.\n\n"
        + "[render3d]\n"
        + $"enforced = {(enforce ? "true" : "false")}\n\n"
        + "[physics]\n"
        + "relative_movement = true\n\n"
        + "[shuttle]\n"
        + "# The 90 degree camera rotate keys are meaningless in 3D; yaw comes from mouse-look.\n"
        + "camera_rotation_locked = true\n";

    // ------------------------------------------------------------------ uninstall

    public int Uninstall()
    {
        var manifest = ReadManifest();
        if (manifest == null)
        {
            _report.Error("3D is not installed here (no .ss12/manifest.json).");
            return 2;
        }

        var git = new Git(_root);
        if (git.IsRepo && HasForeignChanges(git, manifest) && !_opt.Force && !_opt.DryRun)
        {
            _report.Error("The git working tree has uncommitted changes. Commit or stash them first (or use --force).");
            return 2;
        }

        var problems = 0;
        _report.Section("Restoring edited files");
        foreach (var m in manifest.Modified)
        {
            var path = Path.Combine(_root, m.Path);
            var backup = StatePath(Path.Combine("backup", m.Path));
            if (!File.Exists(backup) && !CanRestoreFromGit(git, manifest, m))
            {
                _report.Warn($"{m.Path}: no backup and the original is not in git either, left as is.");
                problems++;
                continue;
            }

            if (File.Exists(path) && Hashing.Sha256(path) != m.NewSha256 && !_opt.Force)
            {
                _report.Warn($"{m.Path}: changed since the install, left as is (use --force to restore the original anyway).");
                problems++;
                continue;
            }

            _report.Info($"restore {m.Path}");
            if (!_opt.DryRun)
            {
                if (File.Exists(backup))
                    File.Copy(backup, path, true);
                else
                    File.WriteAllBytes(path, git.ShowBytes(manifest.BaseCommit!, m.Path)!);
            }
        }

        _report.Section("Removing added files");
        foreach (var f in manifest.Overlay.Concat(manifest.Generated))
        {
            var path = Path.Combine(_root, f.Path);
            if (!File.Exists(path))
                continue;

            if (Hashing.Sha256(path) != f.Sha256 && !_opt.Force)
            {
                _report.Warn($"{f.Path}: changed since the install, kept.");
                problems++;
                continue;
            }

            if (!_opt.DryRun)
            {
                File.Delete(path);
                RemoveEmptyParents(Path.GetDirectoryName(path)!);
            }
        }

        if (!_opt.DryRun)
        {
            Directory.Delete(Path.Combine(_root, StateDir), true);
        }

        _report.Info(_opt.DryRun ? "Dry run: nothing was changed." : $"3D removed.{(problems > 0 ? $" {problems} file(s) were kept; see above." : "")}");
        return problems > 0 ? 1 : 0;
    }

    /// <summary>The backup copies are kept out of git (the history already has the originals), so a fresh clone restores from the commit the install started from.</summary>
    private static bool CanRestoreFromGit(Git git, InstallManifest manifest, ModifiedRecord record)
    {
        if (manifest.BaseCommit == null || !git.IsRepo)
            return false;

        var bytes = git.ShowBytes(manifest.BaseCommit, record.Path);
        return bytes != null && Hashing.Sha256(bytes) == record.OriginalSha256;
    }

    private void RemoveEmptyParents(string dir)
    {
        while (dir.Length > _root.Length && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
        {
            Directory.Delete(dir);
            dir = Path.GetDirectoryName(dir)!;
        }
    }

    // ------------------------------------------------------------------ doctor

    public int Doctor(bool build)
    {
        if (!ValidateRepo())
            return 2;

        var manifest = ReadManifest();
        var failures = 0;

        _report.Section("Installation");
        if (manifest == null)
        {
            _report.Error("Not installed (no .ss12/manifest.json). Run `ss12 install`.");
            return 2;
        }

        _report.Info($"Installed by installer {manifest.InstallerVersion} on {manifest.InstalledUtc:u}; current installer {InstallerVersion}.");
        if (manifest.InstallerVersion != InstallerVersion)
            _report.Warn("A different installer version made this install; run `ss12 update` to upgrade.");

        var source = SourceInfo.Locate(_opt.Source, _report, quiet: true);
        if (source != null)
            CheckEngine(source);
        else
            _report.Info($"Engine {EngineVersion() ?? "?"}.");

        _report.Section("Files");
        var missing = manifest.Overlay.Where(f => !File.Exists(Path.Combine(_root, f.Path))).ToList();
        var changed = manifest.Overlay.Where(f => File.Exists(Path.Combine(_root, f.Path)) && Hashing.Sha256(Path.Combine(_root, f.Path)) != f.Sha256).ToList();
        foreach (var f in missing)
        {
            _report.Error($"missing: {f.Path}");
            failures++;
        }

        foreach (var f in changed)
            _report.Warn($"modified since install: {f.Path}");

        _report.Info($"{manifest.Overlay.Count - missing.Count} of {manifest.Overlay.Count} added files present, {changed.Count} modified.");

        _report.Section("Edits");
        var cache = new Dictionary<string, TextFile.Loaded>();
        string? Read(string rel)
        {
            var path = Path.Combine(_root, rel);
            if (!File.Exists(path))
                return null;

            if (!cache.TryGetValue(rel, out var loaded))
                cache[rel] = loaded = TextFile.Read(path);

            return loaded.Lf;
        }

        foreach (var t in BuiltIn.All)
        {
            var r = TransformEngine.Plan(_root, t, Read);
            switch (r.State)
            {
                case TransformState.Applied:
                    _report.Info($"[ok]      {t.Id}");
                    break;
                case TransformState.Pending:
                    if (t.Required)
                    {
                        _report.Error($"[MISSING] {t.Id}: required edit is not applied in {string.Join(", ", r.Files)}. {t.Impact}");
                        failures++;
                    }
                    else
                    {
                        _report.Warn($"[missing] {t.Id}: not applied in {string.Join(", ", r.Files)}. {t.Impact}");
                    }

                    break;
                case TransformState.AnchorMissing:
                    if (t.Required)
                    {
                        _report.Error($"[MISSING] {t.Id}: the code to change is gone. {t.Impact}\n      By hand: {t.Manual}");
                        failures++;
                    }
                    else
                    {
                        _report.Warn($"[skip]    {t.Id}: {t.Impact}");
                    }

                    break;
            }
        }

        _report.Section("Your content");
        CheckContent();

        _report.Section("Server configuration");
        var preset = Path.Combine(_root, PresetPath);
        _report.Info(File.Exists(preset) ? $"{PresetPath} present." : $"{PresetPath} missing (the server will not force 3D).");

        if (build)
        {
            _report.Section("Build");
            if (!BuildAll())
                failures++;
        }

        _report.Section(failures == 0 ? "Result: healthy" : $"Result: {failures} problem(s)");
        return failures == 0 ? 0 : 1;
    }

    /// <summary>Warnings only: names in the 3D rules that this codebase does not have, and cursor aiming the installer did not recognise.</summary>
    private void CheckContent()
    {
        var unknown = ContentCheck.UnknownRuleNames(_root);
        foreach (var (file, names) in unknown)
        {
            _report.Warn($"{file} names {names.Count} prototype(s) this codebase does not define: {string.Join(", ", names.Take(12))}{(names.Count > 12 ? ", ..." : "")}");
            _report.Detail("A rule for a name that does not exist matches nothing (harmless), but what it was written for is drawn by the generic guesses.");
        }

        if (unknown.Count > 0)
        {
            _report.Detail("If your fork renamed them, add rules under the new names in your own file, Resources/Prototypes/Render3D/<yourserver>.yml");
            _report.Detail("(not in the shipped rules.yml, which `ss12 update` replaces). See PORTING-YOUR-SERVER.md in the docs folder of the SS12 repository.");
        }

        var aims = ContentCheck.CursorAimCalls(_root);
        if (aims.Count > 0)
        {
            _report.Warn($"{aims.Count} place(s) in Content.Client turn the mouse position into a world position without the 3D pointer: {string.Join(", ", aims.Take(8))}{(aims.Count > 8 ? ", ..." : "")}");
            _report.Detail("In 3D the real cursor is not where the player looks, so what these aim at will be in the wrong place.");
            _report.Detail("Change them to Content.Client.Render3D.Render3DPointer.PixelToMap(eyeManager, inputManager.MouseScreenPosition).");
        }

        if (unknown.Count == 0 && aims.Count == 0)
            _report.Info("Every prototype the rules name exists, and no other code aims with the real cursor.");
    }

    // ------------------------------------------------------------------ package

    public int Package(IReadOnlyList<string> extraArgs)
    {
        if (!ValidateRepo())
            return 2;

        var packaging = Path.Combine(_root, "Content.Packaging", "Content.Packaging.csproj");
        if (!File.Exists(packaging))
        {
            _report.Error("Content.Packaging was not found in this codebase. Use your normal release process to package a server build; the 3D client is part of Content.Client and ships with it.");
            return 2;
        }

        var args = extraArgs.Count > 0 ? string.Join(' ', extraArgs) : "server --platform " + CurrentPlatform() + " --hybrid-acz";
        ProcResult result;
        if (OperatingSystem.IsWindows())
        {
            // Windows locks the files of a running program, and packaging rebuilds Content.Packaging's own libraries while
            // it runs ("dotnet run" then fails with "cannot copy ... used by another process"). Run a copy instead.
            _report.Info("Building the packaging tool (Content.Packaging)...");
            var build = Proc.Run(_root, "dotnet", "build Content.Packaging/Content.Packaging.csproj -c Release --nologo -v q -p:WarningLevel=0");
            var built = Path.Combine(_root, "Content.Packaging", "bin", "Release", "net10.0");
            if (build.ExitCode != 0 || !Directory.Exists(built))
            {
                _report.Error("Could not build Content.Packaging.");
                return 4;
            }

            var copy = Path.Combine(_root, StateDir, "packager");
            if (Directory.Exists(copy))
                Directory.Delete(copy, true);

            Directory.CreateDirectory(copy);
            new Git(_root).ExcludeLocally(StateDir + "/packager/");
            foreach (var file in Directory.EnumerateFiles(built))
                File.Copy(file, Path.Combine(copy, Path.GetFileName(file)));

            _report.Info($"Running: dotnet Content.Packaging.dll {args}");
            result = Proc.Run(_root, "dotnet", $"\"{Path.Combine(copy, "Content.Packaging.dll")}\" {args}", echo: true);
        }
        else
        {
            _report.Info($"Running: dotnet run --project Content.Packaging -c Release -- {args}");
            result = Proc.Run(_root, "dotnet", $"run --project Content.Packaging -c Release -- {args}", echo: true);
        }

        if (result.ExitCode != 0)
        {
            _report.Error("Packaging failed.");
            return 4;
        }

        _report.Info("Packaged. The build is under release/. Run that server build; it is packaged with HybridACZ, so players with the stock launcher download the 3D client from your server.");
        return 0;
    }

    private static string CurrentPlatform()
    {
        if (OperatingSystem.IsWindows())
            return "win-x64";
        if (OperatingSystem.IsMacOS())
            return System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.Arm64 ? "osx-arm64" : "osx-x64";

        return System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.Arm64 ? "linux-arm64" : "linux-x64";
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Compiles client and server and reports the errors (0 = both build).</summary>
    public int BuildOnly()
    {
        if (!ValidateRepo())
            return 2;

        _report.Section("Build");
        return BuildAll() ? 0 : 4;
    }

    private bool BuildAll()
    {
        // People who only host a published build may not have the .NET SDK on this machine: skip the check build.
        if (Proc.Run(_root, "dotnet", "--version").ExitCode != 0)
        {
            _report.Warn("The .NET SDK was not found on this computer, so the check build was skipped. Build your server as you normally do to make sure it compiles.");
            return true;
        }

        if (MissingSubmodules() is { Count: > 0 } missingParts)
        {
            _report.Error(SubmoduleAdvice(missingParts));
            return false;
        }

        foreach (var project in new[] { "Content.Client/Content.Client.csproj", "Content.Server/Content.Server.csproj" })
        {
            _report.Info($"dotnet build {project} -c {_opt.Configuration}");
            var result = Proc.Run(_root, "dotnet", $"build {project} -c {_opt.Configuration} --nologo -v q -p:WarningLevel=0");
            var errors = result.Output.Split('\n').Where(l => l.Contains(" error ", StringComparison.Ordinal)).Select(l => l.Trim()).Distinct().ToList();
            if (result.ExitCode != 0 || errors.Count > 0)
            {
                _report.Error($"{project} did not build ({errors.Count} error(s)).");
                foreach (var e in errors.Take(15))
                    _report.Detail(Hints.Explain(e));

                return false;
            }
        }

        _report.Info("Client and server build.");
        return true;
    }

    private InstallManifest? ReadManifest()
    {
        if (!File.Exists(ManifestFile))
            return null;

        return JsonSerializer.Deserialize<InstallManifest>(File.ReadAllText(ManifestFile), Json);
    }
}

/// <summary>Turns known compiler errors into advice.</summary>
public static class Hints
{
    public static string Explain(string error)
    {
        if (error.Contains("Render3D", StringComparison.Ordinal) && error.Contains("CS0103", StringComparison.Ordinal))
            return error + "\n      hint: a Render3D helper is missing; run `ss12 doctor` to see which files are not installed.";

        if (error.Contains("CS1061", StringComparison.Ordinal) || error.Contains("CS0117", StringComparison.Ordinal) || error.Contains("CS7036", StringComparison.Ordinal))
            return error + "\n      hint: an engine or content API differs from the version the 3D view was written against (see installer/compat.json in the SS12 repository); report it with `ss12 doctor --report`.";

        return error;
    }
}

/// <summary>Collects messages for the console and for <c>--report</c>.</summary>
public sealed class Report
{
    private readonly StringBuilder _text = new();
    private readonly bool _echo;
    public int Errors { get; private set; }
    public int Warnings { get; private set; }

    public Report(bool echo = true)
    {
        _echo = echo;
    }

    public void Section(string title) => Write(ConsoleColor.Cyan, $"\n== {title}");
    public void Info(string message) => Write(null, message);
    public void Detail(string message) => Write(ConsoleColor.DarkGray, "      " + message);

    public void Warn(string message)
    {
        Warnings++;
        Write(ConsoleColor.Yellow, "warning: " + message);
    }

    public void Error(string message)
    {
        Errors++;
        Write(ConsoleColor.Red, "error: " + message);
    }

    private void Write(ConsoleColor? color, string line)
    {
        _text.AppendLine(line);
        if (!_echo)
            return;

        if (color != null)
            Console.ForegroundColor = color.Value;

        Console.WriteLine(line);
        if (color != null)
            Console.ResetColor();
    }

    public override string ToString() => _text.ToString();
}

/// <summary>Where the overlay files come from: a checkout of the 3D repository, or an <c>overlay/</c> folder next to the tool.</summary>
public sealed record Compat(string MinEngineVersion, List<string> TestedEngineVersions, int WarnWithin);

public sealed class SourceInfo
{
    public required string Root { get; init; }
    public required string ManifestPath { get; init; }
    public required Compat Compat { get; init; }

    public static SourceInfo? Locate(string? explicitRoot, Report report, bool quiet = false)
    {
        var candidates = new List<string>();
        if (explicitRoot != null)
            candidates.Add(Path.GetFullPath(explicitRoot));

        // walk up from the tool: <repo>/installer/bin/... -> <repo>
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            candidates.Add(dir.FullName);
            candidates.Add(Path.Combine(dir.FullName, "overlay")); // standalone layout: <project>/overlay next to <project>/installer
            dir = dir.Parent;
        }

        candidates.Add(Path.Combine(AppContext.BaseDirectory, "overlay"));

        foreach (var c in candidates)
        {
            if (!Directory.Exists(Path.Combine(c, "Content.Client", "Render3D")))
                continue;

            // the manifest ships next to the tool; fall back to the one in the source tree
            var manifest = Path.Combine(AppContext.BaseDirectory, "overlay.manifest");
            if (!File.Exists(manifest))
                manifest = Path.Combine(c, "Tools", "ss12", "overlay.manifest");

            if (!File.Exists(manifest))
                continue;

            var compatFile = Path.Combine(AppContext.BaseDirectory, "compat.json");
            var compat = File.Exists(compatFile)
                ? JsonSerializer.Deserialize<CompatFile>(File.ReadAllText(compatFile), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                : null;
            return new SourceInfo
            {
                Root = c,
                ManifestPath = manifest,
                Compat = new Compat(compat?.MinEngineVersion ?? "0.0.0", compat?.TestedEngineVersions ?? new List<string>(), compat?.WarnWithin ?? 15),
            };
        }

        if (!quiet)
            report.Error("Could not find the 3D files to install. Run the tool from a checkout of the SS12 repository, or pass --source <path to that checkout>.");

        return null;
    }

    public List<string> OverlayFiles()
    {
        var globs = File.ReadAllLines(ManifestPath)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .ToArray();
        return FileSet.Find(Root, globs);
    }

    private sealed class CompatFile
    {
        public string MinEngineVersion { get; set; } = "0.0.0";
        public List<string> TestedEngineVersions { get; set; } = new();
        public int WarnWithin { get; set; } = 15;
    }
}
