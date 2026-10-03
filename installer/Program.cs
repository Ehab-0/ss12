using Ss12;

return Cli.Run(args);

namespace Ss12
{
    public static class Cli
    {
        private const string Usage = """
            ss12: SS12 is SS13 in 3D, using SS14. This tool turns a Space Station 14 server codebase into a 3D one.

              ss12 install   <path>   add the 3D view to the codebase at <path> (creates a git branch and one commit)
              ss12 doctor    <path>   check an install (files, edits, server config); --build also compiles client and server
              ss12 update    <path>   re-apply after updating this tool
              ss12 uninstall <path>   remove it again, restoring the original files exactly
              ss12 package   <path>   build a server + client package with the codebase's own Content.Packaging
              ss12 selftest           run the installer's own tests on throw-away fixtures
              ss12 mcp                start an MCP server (stdio) so an AI assistant can adapt 3D to a customised codebase

            options
              --enforce        force 3D for living players (render3d.enforced = true in the generated preset)
              --dry-run        show what would change, change nothing
              --no-build       do not compile client and server after installing
              --no-commit      leave the changes uncommitted (no branch, no commit)
              --branch <name>  git branch to create (default 3d)
              --force          install despite a dirty tree, a far-away engine version or changed files
              --report         doctor: also write ss12-report.txt (attach it when asking for help)
              --source <path>  checkout of the SS12 repository to take the files from (default: the one this tool is in)
              -c <config>      build configuration for the check build (default DebugOpt)
            """;

        public static int Run(string[] args)
        {
            if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
            {
                Console.WriteLine(Usage);
                return args.Length == 0 ? 1 : 0;
            }

            var command = args[0].ToLowerInvariant();
            var options = new Options();
            var extra = new List<string>();
            var positional = new List<string>();
            var doctorBuild = false;

            for (var i = 1; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--enforce": options.Enforce = true; break;
                    case "--dry-run": options.DryRun = true; break;
                    case "--no-build": options.Build = false; break;
                    case "--build": doctorBuild = true; break;
                    case "--no-commit": options.Commit = false; break;
                    case "--force": options.Force = true; break;
                    case "--report": options.Report = true; break;
                    case "--branch" when i + 1 < args.Length: options.Branch = args[++i]; break;
                    case "--source" when i + 1 < args.Length: options.Source = args[++i]; break;
                    case "-c" when i + 1 < args.Length: options.Configuration = args[++i]; break;
                    case "--":
                        extra.AddRange(args.Skip(i + 1));
                        i = args.Length;
                        break;
                    default:
                        if (args[i].StartsWith("--"))
                        {
                            Console.Error.WriteLine($"unknown option {args[i]}\n\n{Usage}");
                            return 1;
                        }

                        positional.Add(args[i]);
                        break;
                }
            }

            if (command == "selftest")
                return SelfTest.Run(options.Source) ? 0 : 1;

            if (command == "mcp")
                return McpServer.Run(options);

            if (positional.Count != 1)
            {
                Console.Error.WriteLine($"{command} needs the path of the server codebase.\n\n{Usage}");
                return 1;
            }

            options.Target = positional[0];
            var installer = new Installer(options);
            var code = command switch
            {
                "install" => installer.Install(update: false),
                "update" => installer.Install(update: true),
                "uninstall" => installer.Uninstall(),
                "doctor" => installer.Doctor(doctorBuild),
                "package" => installer.Package(extra),
                _ => Unknown(command),
            };

            if (options.Report)
            {
                var path = Path.Combine(Path.GetFullPath(options.Target), "ss12-report.txt");
                File.WriteAllText(path, $"ss12 {Installer.InstallerVersion}\ncommand: {string.Join(' ', args)}\nexit code: {code}\n\n{installer.Report}");
                Console.WriteLine($"\nReport written to {path}");
            }

            return code;
        }

        private static int Unknown(string command)
        {
            Console.Error.WriteLine($"unknown command '{command}'\n\n{Usage}");
            return 1;
        }
    }
}
