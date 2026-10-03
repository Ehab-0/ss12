using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Ss12;

/// <summary>Reads and writes text files without changing their BOM or line endings.</summary>
public static class TextFile
{
    private static readonly byte[] Bom = { 0xEF, 0xBB, 0xBF };

    public sealed record Loaded(string Lf, bool HasBom, bool Crlf);

    public static Loaded Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var hasBom = bytes.Length >= 3 && bytes[0] == Bom[0] && bytes[1] == Bom[1] && bytes[2] == Bom[2];
        var text = Encoding.UTF8.GetString(bytes, hasBom ? 3 : 0, bytes.Length - (hasBom ? 3 : 0));
        var crlf = text.Contains("\r\n", StringComparison.Ordinal);
        return new Loaded(crlf ? text.Replace("\r\n", "\n") : text, hasBom, crlf);
    }

    public static void Write(string path, string lf, bool hasBom, bool crlf)
    {
        var text = crlf ? lf.Replace("\n", "\r\n") : lf;
        var body = Encoding.UTF8.GetBytes(text);
        using var stream = File.Create(path);
        if (hasBom)
            stream.Write(Bom);
        stream.Write(body);
    }
}

public static class Hashing
{
    public static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    public static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}

public sealed record ProcResult(int ExitCode, string Output);

public static class Proc
{
    public static ProcResult Run(string workingDir, string file, string args, bool echo = false, TimeSpan? timeout = null)
    {
        var info = new ProcessStartInfo(file, args)
        {
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        var sb = new StringBuilder();
        var gate = new object();
        using var process = new Process { StartInfo = info };

        void OnData(string? line)
        {
            if (line == null)
                return;

            lock (gate)
            {
                sb.AppendLine(line);
                if (echo)
                    Console.WriteLine("    " + line);
            }
        }

        process.OutputDataReceived += (_, e) => OnData(e.Data);
        process.ErrorDataReceived += (_, e) => OnData(e.Data);

        try
        {
            process.Start();
        }
        catch (Exception e)
        {
            return new ProcResult(-1, $"could not start {file}: {e.Message}");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (!process.WaitForExit((int) (timeout ?? TimeSpan.FromMinutes(30)).TotalMilliseconds))
        {
            try { process.Kill(true); } catch { /* already gone */ }
            return new ProcResult(-2, sb + "\ntimed out");
        }

        process.WaitForExit();
        return new ProcResult(process.ExitCode, sb.ToString());
    }
}

/// <summary>The few git operations the installer needs. It never pushes and never talks to a remote.</summary>
public sealed class Git
{
    private readonly string _root;

    public Git(string root)
    {
        _root = root;
    }

    public bool IsRepo => Proc.Run(_root, "git", "rev-parse --is-inside-work-tree").Output.Trim() == "true";

    public bool IsDirty => Proc.Run(_root, "git", "status --porcelain --untracked-files=no").Output.Trim().Length > 0;

    /// <summary>Files with uncommitted changes (tracked files only), as '/'-separated repository paths.</summary>
    public List<string> DirtyFiles() => Proc.Run(_root, "git", "status --porcelain --untracked-files=no").Output
        .Split('\n')
        .Select(l => l.TrimEnd('\r'))
        .Where(l => l.Length > 3)
        .Select(l => l[3..].Trim().Trim('"').Replace('\\', '/'))
        .ToList();

    public string CurrentBranch => Proc.Run(_root, "git", "rev-parse --abbrev-ref HEAD").Output.Trim();

    public string? Head
    {
        get
        {
            var result = Proc.Run(_root, "git", "rev-parse HEAD");
            return result.ExitCode == 0 ? result.Output.Trim() : null;
        }
    }

    /// <summary>File contents at a commit (null when git cannot produce them).</summary>
    public byte[]? ShowBytes(string commit, string path)
    {
        var info = new ProcessStartInfo("git", $"show {commit}:{path}")
        {
            WorkingDirectory = _root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        try
        {
            using var process = Process.Start(info)!;
            using var memory = new MemoryStream();
            process.StandardOutput.BaseStream.CopyTo(memory);
            process.WaitForExit();
            return process.ExitCode == 0 ? memory.ToArray() : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Adds a line to .git/info/exclude (local only, never committed).</summary>
    public void ExcludeLocally(string pattern)
    {
        var result = Proc.Run(_root, "git", "rev-parse --git-path info/exclude");
        if (result.ExitCode != 0)
            return;

        var file = Path.GetFullPath(Path.Combine(_root, result.Output.Trim()));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var lines = File.Exists(file) ? File.ReadAllLines(file).ToList() : new List<string>();
        if (lines.Any(l => l.Trim() == pattern))
            return;

        lines.Add(pattern);
        File.WriteAllLines(file, lines);
    }

    public bool BranchExists(string name) => Proc.Run(_root, "git", $"rev-parse --verify --quiet refs/heads/{name}").ExitCode == 0;

    public ProcResult CheckoutNew(string name) => Proc.Run(_root, "git", $"checkout -b {name}");

    public ProcResult Checkout(string name) => Proc.Run(_root, "git", $"checkout {name}");

    public ProcResult Add(IEnumerable<string> paths)
    {
        // pass paths through a file-less batch to stay clear of command line length limits
        var result = new ProcResult(0, "");
        var batch = new List<string>();
        foreach (var path in paths)
        {
            batch.Add("\"" + path + "\"");
            if (batch.Count >= 100)
            {
                result = Proc.Run(_root, "git", "add -f -- " + string.Join(' ', batch));
                batch.Clear();
                if (result.ExitCode != 0)
                    return result;
            }
        }

        if (batch.Count > 0)
            result = Proc.Run(_root, "git", "add -f -- " + string.Join(' ', batch));

        return result;
    }

    public ProcResult Commit(string message)
    {
        var file = Path.GetTempFileName();
        File.WriteAllText(file, message);
        try
        {
            return Proc.Run(_root, "git", $"commit -q -F \"{file}\"");
        }
        finally
        {
            File.Delete(file);
        }
    }
}
