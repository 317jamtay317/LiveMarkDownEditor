using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Infrastructure.Storage;

/// <summary>
/// Asks Git which paths beneath a Folder Workspace's root it ignores, so the Folder Tree can dim them
/// (INV-084). It does exactly what VS Code's Git extension does: it runs
/// <c>git check-ignore -v -z --stdin</c> in the root, and drops a path whose matching pattern is a
/// <c>!</c> negation, which re-includes it rather than ignoring it. Git itself leaves out a file it
/// tracks, and reports everything inside an ignored folder. A root outside any repository, a machine
/// without Git, and a Git that fails or takes too long each answer that nothing is ignored.
/// </summary>
/// <param name="gitExecutable">The Git to run: <c>git</c> on the <c>PATH</c> unless a test says otherwise.</param>
public sealed class GitIgnoreChecker(string gitExecutable = "git")
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static readonly IReadOnlySet<string> Nothing = new HashSet<string>();

    /// <summary>
    /// The paths among <paramref name="relativePaths"/> that the Git repository holding
    /// <paramref name="rootPath"/> ignores (INV-084).
    /// </summary>
    /// <param name="rootPath">The absolute path of the Folder Workspace's root.</param>
    /// <param name="relativePaths">Root-relative, <c>/</c>-separated paths of folders and files to ask about.</param>
    /// <param name="cancellationToken">Cancels the question, answering it with nothing.</param>
    /// <returns>The ignored paths, spelled exactly as they were asked about; empty when Git cannot say.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="rootPath"/> is null or blank.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="relativePaths"/> is null.</exception>
    public async Task<IReadOnlySet<string>> IgnoredAsync(
        string rootPath, IEnumerable<string> relativePaths, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(relativePaths);

        var paths = relativePaths.Where(path => !string.IsNullOrWhiteSpace(path)).ToList();
        if (paths.Count == 0)
        {
            return Nothing;
        }

        try
        {
            return await CheckAsync(rootPath, paths, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or InvalidOperationException
                                              or OperationCanceledException)
        {
            // No Git to run, a root Git cannot start in, a pipe Git closed early, or a Git that was too
            // slow: nothing is ignored, and the Folder Tree is still built.
            return Nothing;
        }
    }

    private async Task<IReadOnlySet<string>> CheckAsync(string rootPath, List<string> paths, CancellationToken cancellationToken)
    {
        using var git = Process.Start(StartInfo(rootPath))
                        ?? throw new InvalidOperationException("Git did not start.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        try
        {
            // Read while writing: a long answer fills the pipe, and Git stops until it is drained.
            var output = git.StandardOutput.ReadToEndAsync(timeout.Token);
            var error = git.StandardError.ReadToEndAsync(timeout.Token);

            await git.StandardInput.WriteAsync(string.Join('\0', paths).AsMemory(), timeout.Token).ConfigureAwait(false);
            git.StandardInput.Close();

            await git.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            await error.ConfigureAwait(false);

            // 0: something is ignored. 1: nothing is. Anything else (128: not a repository) says nothing.
            return git.ExitCode == 0 ? Parse(await output.ConfigureAwait(false)) : Nothing;
        }
        finally
        {
            if (!git.HasExited)
            {
                git.Kill(entireProcessTree: true);
            }
        }
    }

    private ProcessStartInfo StartInfo(string rootPath)
    {
        var start = new ProcessStartInfo(gitExecutable)
        {
            WorkingDirectory = rootPath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        // Asking never takes Git's optional locks, so it cannot get in the way of the user's own Git.
        start.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        foreach (var argument in (string[])["check-ignore", "-v", "-z", "--stdin"])
        {
            start.ArgumentList.Add(argument);
        }

        return start;
    }

    // With -v -z, each answer is four NUL-terminated fields: source, line number, pattern, and path.
    private static HashSet<string> Parse(string output)
    {
        var fields = output.Split('\0');
        var ignored = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index + 3 < fields.Length; index += 4)
        {
            var pattern = fields[index + 2];
            if (pattern.Length > 0 && !pattern.StartsWith('!'))
            {
                ignored.Add(fields[index + 3]);
            }
        }

        return ignored;
    }
}
