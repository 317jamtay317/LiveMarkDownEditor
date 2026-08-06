using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace UI.Diagnostics;

/// <summary>
/// A measurement harness for the repaint paths that run while the user scrolls: the Editor Gutter
/// rebuild and each annotation adorner's render. It records how long a path took and how many items
/// it walked to get there, so the cost of a repaint can be related to the size of the document
/// rather than to the size of the viewport.
/// </summary>
/// <remarks>
/// Inert unless the <c>LMDE_SCROLL_PROFILE</c> environment variable names an output file, so a normal
/// run pays only a null check per repaint. Samples buffer in memory and are written on
/// <see cref="Flush"/> or at process exit — writing per repaint would itself perturb the measurement
/// it is taking. Every call happens on the UI thread, so the buffer needs no synchronisation.
/// </remarks>
public static class ScrollProfiler
{
    private const string PathVariable = "LMDE_SCROLL_PROFILE";

    private static readonly string? OutputPath = Environment.GetEnvironmentVariable(PathVariable);
    private static readonly List<string> Samples = [];

    static ScrollProfiler()
    {
        if (IsEnabled)
        {
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Flush();
        }
    }

    /// <summary>
    /// Whether profiling is switched on — that is, whether <c>LMDE_SCROLL_PROFILE</c> names a file to
    /// write samples to.
    /// </summary>
    public static bool IsEnabled => !string.IsNullOrWhiteSpace(OutputPath);

    /// <summary>
    /// Starts timing a repaint path, or returns <see langword="null"/> when profiling is off so the
    /// caller does no work.
    /// </summary>
    /// <returns>A running <see cref="Stopwatch"/>, or <see langword="null"/> when profiling is off.</returns>
    public static Stopwatch? Start() => IsEnabled ? Stopwatch.StartNew() : null;

    /// <summary>
    /// Records one completed repaint. Does nothing when <paramref name="stopwatch"/> is
    /// <see langword="null"/> (profiling off).
    /// </summary>
    /// <param name="stopwatch">The stopwatch returned by <see cref="Start"/>.</param>
    /// <param name="path">The repaint path being measured, e.g. <c>EditorGutter.BuildLineNumbers</c>.</param>
    /// <param name="itemsWalked">
    /// How many items the path iterated — lines, misspellings, code regions. This is the number that
    /// should track the viewport but currently tracks the document.
    /// </param>
    /// <param name="itemsDrawn">How many of those items actually produced output on screen.</param>
    /// <param name="layoutQueries">
    /// How many <c>GetCharacterRect</c> calls the path made. Each one forces WPF to resolve text
    /// layout, so this — not the item count — is the number that governs the cost.
    /// </param>
    public static void Record(
        Stopwatch? stopwatch,
        string path,
        int itemsWalked,
        int itemsDrawn,
        int layoutQueries)
    {
        if (stopwatch is null)
        {
            return;
        }

        stopwatch.Stop();
        var micros = stopwatch.Elapsed.TotalMicroseconds;
        Samples.Add(string.Create(
            CultureInfo.InvariantCulture,
            $"{Marker}\t{path}\t{micros:F1}\t{itemsWalked}\t{itemsDrawn}\t{layoutQueries}"));
    }

    /// <summary>
    /// Stamps the samples that follow with a caller-chosen label, so a run can attribute them to the
    /// scroll offset that provoked them.
    /// </summary>
    /// <param name="marker">The label to apply, e.g. the vertical offset being measured.</param>
    public static void Mark(string marker) => Marker = marker;

    /// <summary>Writes the buffered samples to the profile file and clears the buffer.</summary>
    public static void Flush()
    {
        if (!IsEnabled || Samples.Count == 0)
        {
            return;
        }

        var builder = new StringBuilder();
        foreach (var sample in Samples)
        {
            builder.AppendLine(sample);
        }

        System.IO.File.AppendAllText(OutputPath!, builder.ToString());
        Samples.Clear();
    }

    private static string Marker { get; set; } = "-";
}
