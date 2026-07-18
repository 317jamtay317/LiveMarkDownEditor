using System.IO;
using System.Windows.Shell;
using UI.Core;

namespace UI.Platform;

/// <summary>
/// <see cref="IJumpList"/> backed by the WPF <see cref="JumpList"/>. Each Recent File becomes a
/// <see cref="JumpTask"/> that relaunches the app with the file's path, which the Single Instance
/// forwards to the running editor as a Startup Document (INV-020).
/// </summary>
public sealed class WpfJumpList : IJumpList
{
    /// <inheritdoc />
    public void ShowRecentFiles(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var application = System.Windows.Application.Current;
        if (application is null)
        {
            return;
        }

        var jumpList = new JumpList { ShowRecentCategory = false, ShowFrequentCategory = false };
        var executable = Environment.ProcessPath;

        foreach (var path in paths)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            jumpList.JumpItems.Add(new JumpTask
            {
                Title = Path.GetFileName(path),
                Description = path,
                ApplicationPath = executable,
                Arguments = QuoteIfNeeded(path),
                CustomCategory = "Recent",
            });
        }

        JumpList.SetJumpList(application, jumpList);
        jumpList.Apply();
    }

    private static string QuoteIfNeeded(string path) => path.Contains(' ') ? $"\"{path}\"" : path;
}
