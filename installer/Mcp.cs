using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Ss12;

/// <summary>
///     A Model Context Protocol server (JSON-RPC 2.0, one message per line over stdin/stdout) so an AI assistant can
///     add 3D to a server codebase that is customised beyond what the built-in edits expect: it can inspect the code,
///     read exactly what each edit does, find the right place in unfamiliar code, install, verify and build.
///     Standard library only. stdout is the protocol channel, so nothing else may write to it.
/// </summary>
public static class McpServer
{
    private const string DefaultProtocol = "2024-11-05";

    // Where to look in an unfamiliar codebase for each edit (identifiers the edit's anchor is built around).
    private static readonly Dictionary<string, string[]> Hints = new()
    {
        ["pointer-aim"] = new[] { "PixelToMap(", "MouseScreenPosition" },
        ["pick-merge"] = new[] { "GetClickableEntities(" },
        ["drag-drop"] = new[] { "_mouseDownScreenPos", "Deadzone", "_deadzone" },
        ["popups"] = new[] { "GetWorldToScreenMatrix", "Vector2.Transform(" },
        ["map-text"] = new[] { "GetWorldToScreenMatrix", "mapText.CachedFont", "DrawString(" },
        ["sprite-fade"] = new[] { "FadeIn(", "FadeOut(" },
        ["health-bars"] = new[] { "class EntityHealthBarOverlay", "CalcProgress(", "GetProgressColor(" },
    };

    private const string Guide = """
        # How to add SS12 (SS13 in 3D, using SS14) to a customised Space Station 14 codebase (guide for an AI assistant)

        Goal: the 3D view works in the user's codebase with the smallest possible change to their existing files.

        What the installer does: it COPIES new files (everything under Content.*/Render3D, shaders, a locale file) and makes a
        few small EDITS to existing files. Every edit is one call into the new code, written with fully qualified names so no
        `using` lines change. The edits are in 'edit ids' (use list_edits). Edits are either required (the game would not play
        correctly in 3D without them) or optional (a feature degrades without them).

        Recommended workflow:
        1. inspect_codebase(path)  - is it a valid codebase, engine version OK (needs RobustToolbox 286+), which edits apply
           automatically, which do not and why.
        2. If every required edit is 'applies' or 'present': install_3d(path, dry_run=true) to preview, then
           install_3d(path, dry_run=false) after the user agrees. Done; run check_install(path, build=true).
        3. If an edit is MISSING (the user's code differs): explain_edit(id) tells you exactly what the edit must achieve and
           shows the regular expression and replacement text for the standard shape of the code.
           find_edit_location(path, id) shows candidate places in the user's files.
           Make the equivalent change BY HAND in the user's file (using your own file editing tools), keeping it a one-line
           call into Content.Client.Render3D wherever possible. The tell-tale markers are listed in explain_edit; inspect
           uses them to recognise an applied edit.
        4. verify with inspect_codebase(path) (the edit should now read 'present'), then install_3d for the rest.
        5. check_install(path, build=true) and fix compile errors. Common causes: an engine API that differs from the
           tested version (see compat), a namespace that moved. Prefer making the new Render3D code tolerant over
           editing the user's code further.

        Rules:
        - Never edit anything under RobustToolbox/ (the engine). 3D must work without engine changes.
        - Do not change gameplay behaviour; 3D only changes how the world is shown and which way WASD points.
        - Ask the user before install_3d with dry_run=false, update_3d, or uninstall_3d with dry_run=false.
        - The user's git working tree must be clean for install (the installer creates a branch and one commit and never pushes).
        - If something is unclear about the user's code, say so instead of guessing.
        """;

    public static int Run(Options baseOptions, TextReader? input = null, TextWriter? output = null)
    {
        var stdin = input ?? Console.In;
        var stdout = output ?? new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };

        string? line;
        while ((line = stdin.ReadLine()) != null)
        {
            if (line.Trim().Length == 0)
                continue;

            JsonNode? request;
            try
            {
                request = JsonNode.Parse(line);
            }
            catch (JsonException)
            {
                Send(stdout, Error(null, -32700, "Parse error"));
                continue;
            }

            if (request is not JsonObject obj)
            {
                Send(stdout, Error(null, -32600, "Invalid request"));
                continue;
            }

            var response = Handle(obj, baseOptions);
            if (response != null)
                Send(stdout, response);
        }

        return 0;
    }

    private static void Send(TextWriter output, JsonNode message) => output.WriteLine(message.ToJsonString());

    // ------------------------------------------------------------------ protocol

    private static JsonNode? Handle(JsonObject request, Options baseOptions)
    {
        var id = request["id"]?.DeepClone();
        var method = request["method"]?.GetValue<string>() ?? "";
        var args = request["params"] as JsonObject;

        // notifications carry no id and get no answer
        if (id == null)
            return null;

        try
        {
            switch (method)
            {
                case "initialize":
                {
                    var protocol = args?["protocolVersion"]?.GetValue<string>() ?? DefaultProtocol;
                    return Result(id, new JsonObject
                    {
                        ["protocolVersion"] = protocol,
                        ["capabilities"] = new JsonObject
                        {
                            ["tools"] = new JsonObject(),
                            ["resources"] = new JsonObject(),
                            ["prompts"] = new JsonObject(),
                        },
                        ["serverInfo"] = new JsonObject { ["name"] = "ss12", ["version"] = Installer.InstallerVersion },
                        ["instructions"] = "Adds a 3D view to a Space Station 14 server codebase. Read the resource ss12://guide first, then use inspect_codebase.",
                    });
                }
                case "ping":
                    return Result(id, new JsonObject());
                case "tools/list":
                    return Result(id, new JsonObject { ["tools"] = ToolList() });
                case "tools/call":
                {
                    var name = args?["name"]?.GetValue<string>() ?? "";
                    var toolArgs = args?["arguments"] as JsonObject ?? new JsonObject();
                    return Result(id, CallTool(name, toolArgs, baseOptions));
                }
                case "resources/list":
                    return Result(id, new JsonObject
                    {
                        ["resources"] = new JsonArray(
                            new JsonObject
                            {
                                ["uri"] = "ss12://guide",
                                ["name"] = "How to add 3D to a customised server",
                                ["mimeType"] = "text/markdown",
                                ["description"] = "Workflow and rules for an assistant adapting SS12 to a custom codebase.",
                            },
                            new JsonObject
                            {
                                ["uri"] = "ss12://edits",
                                ["name"] = "The edits the installer makes",
                                ["mimeType"] = "text/markdown",
                                ["description"] = "Every built-in edit: what it does, why, severity.",
                            }),
                    });
                case "resources/read":
                {
                    var uri = args?["uri"]?.GetValue<string>() ?? "";
                    var text = uri switch
                    {
                        "ss12://guide" => Guide,
                        "ss12://edits" => ListEdits(),
                        _ => null,
                    };
                    if (text == null)
                        return Error(id, -32602, $"Unknown resource {uri}");

                    return Result(id, new JsonObject
                    {
                        ["contents"] = new JsonArray(new JsonObject { ["uri"] = uri, ["mimeType"] = "text/markdown", ["text"] = text }),
                    });
                }
                case "prompts/list":
                    return Result(id, new JsonObject
                    {
                        ["prompts"] = new JsonArray(new JsonObject
                        {
                            ["name"] = "add_3d_to_my_server",
                            ["description"] = "Add the 3D view to my Space Station 14 server code, adapting to my customisations.",
                            ["arguments"] = new JsonArray(new JsonObject
                            {
                                ["name"] = "path",
                                ["description"] = "Folder of the server's code (contains Content.Client, Content.Server, Content.Shared).",
                                ["required"] = true,
                            }),
                        }),
                    });
                case "prompts/get":
                {
                    var path = args?["arguments"]?["path"]?.GetValue<string>() ?? "<path>";
                    return Result(id, new JsonObject
                    {
                        ["description"] = "Add SS12 to a customised server",
                        ["messages"] = new JsonArray(new JsonObject
                        {
                            ["role"] = "user",
                            ["content"] = new JsonObject
                            {
                                ["type"] = "text",
                                ["text"] = $"Add the SS12 3D view to my Space Station 14 server code at {path}. Read the ss12://guide resource first. "
                                           + "Inspect the code, show me what will change and which edits need adapting to my customisations, ask me before you change anything, "
                                           + "then make the changes, check them, and tell me in plain words what you did and what to do next.",
                            },
                        }),
                    });
                }
                default:
                    return Error(id, -32601, $"Method not found: {method}");
            }
        }
        catch (Exception e)
        {
            return Error(id, -32603, "Internal error: " + e.Message);
        }
    }

    private static JsonObject Result(JsonNode id, JsonNode result) => new() { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result };

    private static JsonObject Error(JsonNode? id, int code, string message) => new()
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id,
        ["error"] = new JsonObject { ["code"] = code, ["message"] = message },
    };

    // ------------------------------------------------------------------ tools

    private static JsonObject Prop(string type, string description) => new() { ["type"] = type, ["description"] = description };

    private static JsonObject Schema(JsonObject properties, params string[] required)
    {
        var req = new JsonArray();
        foreach (var r in required)
            req.Add(r);

        return new JsonObject { ["type"] = "object", ["properties"] = properties, ["required"] = req };
    }

    private static JsonObject Tool(string name, string description, JsonObject schema, bool readOnly, bool destructive = false) => new()
    {
        ["name"] = name,
        ["description"] = description,
        ["inputSchema"] = schema,
        ["annotations"] = new JsonObject { ["readOnlyHint"] = readOnly, ["destructiveHint"] = destructive, ["openWorldHint"] = false },
    };

    private static JsonArray ToolList()
    {
        Func<JsonObject> path = () => Prop("string", "Folder of the server code (contains Content.Client, Content.Server and Content.Shared).");
        Func<JsonObject> id = () => Prop("string", "Edit id, see list_edits (for example pointer-aim, pick-merge).");

        return new JsonArray(
            Tool("list_edits", "List every edit the installer makes to existing files: id, what it does, whether it is required, what is lost without it.",
                Schema(new JsonObject()), readOnly: true),
            Tool("inspect_codebase", "Check a Space Station 14 code folder: engine version, git state, whether 3D is installed, and for every edit whether it applies automatically, is already present, or cannot be found (with why and how to do it by hand). Changes nothing.",
                Schema(new JsonObject { ["path"] = path() }, "path"), readOnly: true),
            Tool("explain_edit", "Explain one edit precisely: what it must achieve, the marker text that shows it is applied, the pattern it looks for and the replacement it writes. Use it to adapt an edit to unfamiliar code.",
                Schema(new JsonObject { ["id"] = id() }, "id"), readOnly: true),
            Tool("find_edit_location", "Search the codebase for the places where an edit most likely belongs (the identifiers it is built around), with surrounding lines. Use it when inspect_codebase reports an edit as missing.",
                Schema(new JsonObject { ["path"] = path(), ["id"] = id() }, "path", "id"), readOnly: true),
            Tool("install_3d", "Add 3D to the codebase: copy the new files and apply the edits. DRY RUN BY DEFAULT: pass dry_run=false only after the user agreed. Creates a git branch and one commit (never pushes) unless commit=false. Stops without changing anything if a required edit cannot be applied.",
                Schema(new JsonObject
                {
                    ["path"] = path(),
                    ["dry_run"] = Prop("boolean", "Only show what would change (default true)."),
                    ["enforce"] = Prop("boolean", "Make 3D mandatory for living players in the generated server preset (default false)."),
                    ["build"] = Prop("boolean", "Compile client and server afterwards (default false; slow)."),
                    ["commit"] = Prop("boolean", "Create a git branch and commit (default true)."),
                    ["branch"] = Prop("string", "Branch name (default 3d)."),
                    ["force"] = Prop("boolean", "Install despite a dirty git tree, an unusual engine version or changed files (default false)."),
                }, "path"), readOnly: false, destructive: true),
            Tool("check_install", "Check an existing install: files present, every edit still applied, server preset present; optionally compile client and server.",
                Schema(new JsonObject { ["path"] = path(), ["build"] = Prop("boolean", "Also compile client and server (slow, default false).") }, "path"), readOnly: true),
            Tool("update_3d", "Re-apply after the installer was updated. Dry run by default.",
                Schema(new JsonObject { ["path"] = path(), ["dry_run"] = Prop("boolean", "Only show what would change (default true)."), ["force"] = Prop("boolean", "Overwrite changed files (default false).") }, "path"),
                readOnly: false, destructive: true),
            Tool("uninstall_3d", "Remove 3D and restore the original files exactly. Dry run by default.",
                Schema(new JsonObject { ["path"] = path(), ["dry_run"] = Prop("boolean", "Only show what would change (default true)."), ["force"] = Prop("boolean", "Restore even files changed since the install (default false).") }, "path"),
                readOnly: false, destructive: true),
            Tool("build_codebase", "Compile Content.Client and Content.Server and return the compiler errors with hints (engine API differences, moved namespaces).",
                Schema(new JsonObject { ["path"] = path(), ["configuration"] = Prop("string", "Build configuration (default DebugOpt).") }, "path"), readOnly: true));
    }

    private static JsonObject Text(string text, bool isError = false) => new()
    {
        ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = text }),
        ["isError"] = isError,
    };

    private static bool Flag(JsonObject a, string name, bool fallback) => a[name] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : fallback;

    private static string? Str(JsonObject a, string name) => a[name] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static JsonObject CallTool(string name, JsonObject a, Options baseOptions)
    {
        try
        {
            switch (name)
            {
                case "list_edits":
                    return Text(ListEdits());
                case "explain_edit":
                    return ExplainEdit(Str(a, "id"));
            }

            var path = Str(a, "path");
            if (string.IsNullOrWhiteSpace(path))
                return Text("The 'path' argument is required.", true);

            var options = new Options
            {
                Target = path,
                Source = baseOptions.Source,
                DryRun = true,
                Build = false,
                Commit = true,
                Force = Flag(a, "force", false),
                Enforce = Flag(a, "enforce", false),
                Branch = Str(a, "branch") ?? "ss12",
                Configuration = Str(a, "configuration") ?? "DebugOpt",
            };
            var report = new Report(echo: false);
            var installer = new Installer(options, report);

            switch (name)
            {
                case "inspect_codebase":
                    return Inspect(installer, report, baseOptions);
                case "find_edit_location":
                    return FindLocation(installer, Str(a, "id"));
                case "install_3d":
                    options.DryRun = Flag(a, "dry_run", true);
                    options.Build = Flag(a, "build", false);
                    options.Commit = Flag(a, "commit", true);
                    return Finish(installer.Install(update: false), report, options.DryRun ? "Dry run only: nothing was changed. Ask the user, then call again with dry_run=false." : null);
                case "update_3d":
                    options.DryRun = Flag(a, "dry_run", true);
                    return Finish(installer.Install(update: true), report, options.DryRun ? "Dry run only: nothing was changed." : null);
                case "uninstall_3d":
                    options.DryRun = Flag(a, "dry_run", true);
                    return Finish(installer.Uninstall(), report, options.DryRun ? "Dry run only: nothing was changed." : null);
                case "check_install":
                    return Finish(installer.Doctor(Flag(a, "build", false)), report, null);
                case "build_codebase":
                    options.Build = true;
                    return Finish(installer.BuildOnly(), report, null);
                default:
                    return Text($"Unknown tool: {name}", true);
            }
        }
        catch (Exception e)
        {
            return Text("The tool failed: " + e.Message, true);
        }
    }

    private static JsonObject Finish(int exitCode, Report report, string? note)
    {
        var sb = new StringBuilder();
        sb.AppendLine(report.ToString().TrimEnd());
        sb.AppendLine();
        sb.AppendLine($"exit code: {exitCode} ({Describe(exitCode)})");
        if (note != null && exitCode == 0)
            sb.AppendLine(note);

        return Text(sb.ToString(), isError: exitCode != 0);
    }

    private static string Describe(int code) => code switch
    {
        0 => "ok",
        1 => "finished with problems",
        2 => "refused: not a usable codebase, engine too old, dirty tree, already (not) installed, or a file conflict",
        3 => "stopped: a required edit cannot be applied automatically; nothing was changed",
        4 => "installed, but the check build failed",
        _ => "error",
    };

    // ------------------------------------------------------------------ content

    private static string ListEdits()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Edits the installer makes to existing files");
        sb.AppendLine();
        foreach (var t in BuiltIn.All)
        {
            sb.AppendLine($"## {t.Id} ({(t.Required ? "required" : "optional")})");
            sb.AppendLine(t.Title);
            sb.AppendLine($"- Without it: {t.Impact}");
            sb.AppendLine($"- Files: {string.Join(", ", t.Include)}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static JsonObject ExplainEdit(string? id)
    {
        var t = BuiltIn.All.FirstOrDefault(x => x.Id == id);
        if (t == null)
            return Text($"Unknown edit id '{id}'. Known ids: {string.Join(", ", BuiltIn.All.Select(x => x.Id))}", true);

        var sb = new StringBuilder();
        sb.AppendLine($"# {t.Id}: {t.Title}");
        sb.AppendLine($"Severity: {(t.Required ? "REQUIRED" : "optional")}");
        sb.AppendLine($"What is lost without it: {t.Impact}");
        sb.AppendLine($"By hand: {t.Manual}");
        sb.AppendLine($"Files it looks in: {string.Join(", ", t.Include)}{(t.Exclude.Length > 0 ? " (not " + string.Join(", ", t.Exclude) + ")" : "")}");
        if (t.RequiresText != null)
            sb.AppendLine($"Only applies to files that contain: {string.Join(" and ", t.RequiresText.Select(r => "`" + r + "`"))}");

        sb.AppendLine();
        sb.AppendLine("The change consists of these steps. 'Marker' is text that is present once the step is done (inspect_codebase uses it to recognise a manual edit).");
        var n = 0;
        foreach (var e in t.Edits)
        {
            n++;
            sb.AppendLine();
            sb.AppendLine($"## Step {n}: {e.GetType().Name.Replace("Edit", "")}");
            sb.AppendLine($"Marker: `{e.Done}`");
            switch (e)
            {
                case ReplaceEdit r:
                    sb.AppendLine("Find (regular expression, multiline):");
                    sb.AppendLine("```");
                    sb.AppendLine(r.Find);
                    sb.AppendLine("```");
                    sb.AppendLine("Replace with:");
                    sb.AppendLine("```");
                    sb.AppendLine(r.Replace);
                    sb.AppendLine("```");
                    break;
                case InsertAfterEdit i:
                    sb.AppendLine("Insert after the first match of (regular expression, multiline):");
                    sb.AppendLine("```");
                    sb.AppendLine(i.Anchor);
                    sb.AppendLine("```");
                    sb.AppendLine("Insert:");
                    sb.AppendLine("```");
                    sb.AppendLine(i.Build(Match.Empty));
                    sb.AppendLine("```");
                    break;
                case InsertBeforeEdit b:
                    sb.AppendLine("Insert before the first match of (regular expression, multiline):");
                    sb.AppendLine("```");
                    sb.AppendLine(b.Anchor);
                    sb.AppendLine("```");
                    sb.AppendLine("Insert:");
                    sb.AppendLine("```");
                    sb.AppendLine(b.Build(Match.Empty));
                    sb.AppendLine("```");
                    break;
            }
        }

        sb.AppendLine();
        sb.AppendLine("The helper types are in Content.Client/Render3D/Render3DPointer.cs (Render3DPointer, Render3DPicking, Render3DOverlays, IRender3DHealthBar).");
        return Text(sb.ToString());
    }

    private static JsonObject Inspect(Installer installer, Report report, Options baseOptions)
    {
        var sb = new StringBuilder();
        if (!installer.ValidateRepo())
            return Text(report.ToString(), true);

        sb.AppendLine($"Codebase: {installer.Root}");
        var engine = installer.EngineVersion();
        sb.AppendLine($"Engine (RobustToolbox) version: {engine ?? "unknown (RobustToolbox/MSBuild/Robust.Engine.Version.props not found; is the submodule checked out?)"}");

        var source = SourceInfo.Locate(baseOptions.Source, report, quiet: true);
        if (source != null)
        {
            var before = report.Errors;
            installer.CheckEngine(source);
            sb.AppendLine(report.Errors > before ? "Engine verdict: TOO OLD or too far from the tested versions (see messages)." : "Engine verdict: acceptable.");
        }

        var git = new Git(installer.Root);
        sb.AppendLine(git.IsRepo ? $"Git: branch {git.CurrentBranch}, working tree {(git.IsDirty ? "has UNCOMMITTED changes (install needs it clean)" : "clean")}" : "Git: not a git repository");
        sb.AppendLine(File.Exists(Path.Combine(installer.Root, Installer.StateDir, Installer.ManifestName)) ? "3D: already installed." : "3D: not installed.");
        sb.AppendLine();
        sb.AppendLine("Edits to existing files:");

        var anyRequiredMissing = false;
        foreach (var r in installer.PlanAll())
        {
            var t = r.Transform;
            var tag = t.Required ? "required" : "optional";
            switch (r.State)
            {
                case TransformState.Pending:
                    sb.AppendLine($"- {t.Id} ({tag}): APPLIES AUTOMATICALLY to {string.Join(", ", r.Files)}");
                    break;
                case TransformState.Applied:
                    sb.AppendLine($"- {t.Id} ({tag}): already present ({r.AppliedFiles} file(s))");
                    break;
                default:
                    if (t.Required)
                        anyRequiredMissing = true;
                    sb.AppendLine($"- {t.Id} ({tag}): MISSING - the code to change was not found. {t.Impact}");
                    foreach (var note in r.Notes)
                        sb.AppendLine($"    {note}");
                    sb.AppendLine($"    Next: explain_edit id={t.Id}, then find_edit_location id={t.Id}.");
                    break;
            }
        }

        sb.AppendLine();
        sb.AppendLine(anyRequiredMissing
            ? "At least one REQUIRED edit cannot be applied automatically: install_3d would stop. Adapt it by hand (see explain_edit / find_edit_location), then inspect again."
            : "Every required edit applies or is present: install_3d (dry_run first) can proceed.");
        return Text(sb.ToString());
    }

    private static JsonObject FindLocation(Installer installer, string? id)
    {
        var t = BuiltIn.All.FirstOrDefault(x => x.Id == id);
        if (t == null)
            return Text($"Unknown edit id '{id}'.", true);

        var tokens = Hints.TryGetValue(t.Id, out var h) ? h : Array.Empty<string>();
        var root = installer.Root;

        // the files the edit normally targets first; if there are none (renamed, moved) search the whole client
        var files = FileSet.Find(root, t.Include, t.Exclude);
        if (files.Count == 0)
            files = FileSet.Find(root, new[] { "Content.Client/**/*.cs" }, new[] { "Content.Client/Render3D/**" });

        var sb = new StringBuilder();
        sb.AppendLine($"# Candidate places for '{t.Id}' (searching for: {string.Join(", ", tokens.Select(x => "`" + x + "`"))})");
        var found = 0;
        foreach (var rel in files)
        {
            string[] lines;
            try
            {
                lines = File.ReadAllLines(Path.Combine(root, rel));
            }
            catch
            {
                continue;
            }

            for (var i = 0; i < lines.Length && found < 14; i++)
            {
                if (!tokens.Any(tok => lines[i].Contains(tok, StringComparison.Ordinal)))
                    continue;

                found++;
                sb.AppendLine();
                sb.AppendLine($"{rel}:{i + 1}");
                sb.AppendLine("```");
                for (var k = Math.Max(0, i - 2); k <= Math.Min(lines.Length - 1, i + 3); k++)
                    sb.AppendLine($"{k + 1,5}: {lines[k]}");
                sb.AppendLine("```");
            }

            if (found >= 14)
                break;
        }

        if (found == 0)
            sb.AppendLine("Nothing found. This codebase may not have the feature the edit targets (then skip an optional edit), or it was renamed: search for a similar concept.");

        sb.AppendLine();
        sb.AppendLine($"What the edit must achieve: {t.Manual}");
        return Text(sb.ToString());
    }
}
