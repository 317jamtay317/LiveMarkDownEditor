using System.Diagnostics;

namespace Infrastructure.Tests;

/// <summary>
/// Test helper that makes a real Git repository in a scratch folder, and deletes one again — Git marks
/// its object files read-only, which a plain recursive delete refuses.
/// </summary>
internal static class GitRepository
{
    /// <summary>Makes <paramref name="folder"/> (creating it if need be) the root of a new Git repository.</summary>
    /// <param name="folder">The folder to make a repository.</param>
    internal static void Init(string folder)
    {
        Directory.CreateDirectory(folder);
        Run(folder, "init", "--quiet");
    }

    /// <summary>Runs Git in <paramref name="folder"/> with the given arguments, failing the test if Git does.</summary>
    /// <param name="folder">The folder Git runs in.</param>
    /// <param name="arguments">Git's arguments.</param>
    internal static void Run(string folder, params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = folder,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var git = Process.Start(start) ?? throw new InvalidOperationException("Git did not start.");
        var error = git.StandardError.ReadToEnd();
        git.StandardOutput.ReadToEnd();
        git.WaitForExit();
        if (git.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {error}");
        }
    }

    /// <summary>Deletes <paramref name="folder"/> and everything in it, read-only Git objects included.</summary>
    /// <param name="folder">The folder to delete; nothing happens when it is not there.</param>
    internal static void Delete(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(folder, recursive: true);
    }
}
