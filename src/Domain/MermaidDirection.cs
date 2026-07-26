using System.Text.RegularExpressions;

namespace Domain;

/// <summary>
/// How a <see cref="FlowDirection"/> is written in Mermaid. A Flowchart carries it as a token on its
/// header (<c>flowchart LR</c>); every other Diagram Kind carries it as a <c>direction</c> statement on
/// the line below (<c>direction LR</c>) — which a Class Diagram must always have, because Mermaid
/// rejects a <c>classDiagram</c> with nothing in it at all. Pure: no I/O, no state.
/// </summary>
internal static partial class MermaidDirection
{
    /// <summary>The token a Flowchart header carries — Mermaid's <c>TD</c> for Top-Down.</summary>
    /// <param name="direction">The Flow Direction to write.</param>
    /// <returns>The header token.</returns>
    public static string HeaderToken(FlowDirection direction) => direction switch
    {
        FlowDirection.LeftRight => "LR",
        FlowDirection.BottomUp => "BT",
        FlowDirection.RightLeft => "RL",
        _ => "TD",
    };

    /// <summary>The whole <c>direction</c> statement line, indented as the rest of the body is.</summary>
    /// <param name="direction">The Flow Direction to write.</param>
    /// <returns>The statement line.</returns>
    public static string Statement(FlowDirection direction) =>
        "    direction " + (direction switch
        {
            FlowDirection.LeftRight => "LR",
            FlowDirection.BottomUp => "BT",
            FlowDirection.RightLeft => "RL",
            _ => "TB",
        });

    /// <summary>Reads a direction token, defaulting to Top-Down for <c>TD</c>, <c>TB</c> and anything unknown.</summary>
    /// <param name="token">The token read from the source.</param>
    /// <returns>The Flow Direction it names.</returns>
    public static FlowDirection Read(string token) => token.ToUpperInvariant() switch
    {
        "LR" => FlowDirection.LeftRight,
        "BT" => FlowDirection.BottomUp,
        "RL" => FlowDirection.RightLeft,
        _ => FlowDirection.TopDown,
    };

    /// <summary>Whether <paramref name="line"/> is a <c>direction</c> statement, and which direction it names.</summary>
    /// <param name="line">The trimmed source line to inspect.</param>
    /// <param name="direction">The Flow Direction it names, when it is one.</param>
    /// <returns><see langword="true"/> when the line is a direction statement.</returns>
    public static bool TryReadStatement(string line, out FlowDirection direction)
    {
        var match = StatementPattern().Match(line);
        direction = match.Success ? Read(match.Groups["dir"].Value) : FlowDirection.TopDown;
        return match.Success;
    }

    [GeneratedRegex(@"^direction[ \t]+(?<dir>TD|TB|LR|RL|BT)[ \t]*;?$", RegexOptions.IgnoreCase)]
    private static partial Regex StatementPattern();
}
