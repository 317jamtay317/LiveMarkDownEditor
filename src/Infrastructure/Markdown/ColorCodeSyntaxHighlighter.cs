using ColorCode;
using ColorCode.Parsing;
using Domain;

namespace Infrastructure.Markdown;

/// <summary>
/// ColorCode-backed adapter for the <see cref="ISyntaxHighlighter"/> port. Resolves a Code Block's
/// fence info string to a grammar and tokenizes its code into <see cref="CodeToken"/>s (INV-064).
/// </summary>
/// <remarks>
/// The adapter is <em>lossless by construction</em>: it walks ColorCode's scopes to find the
/// colored runs, and emits the code between and around them as
/// <see cref="CodeTokenKind.Plain"/> tokens, so every character of the input is carried by exactly
/// one token. That matters more than it might seem — Capture reads a Code Block's Runs back as its
/// code (INV-004), so a tokenizer that dropped or added a character would silently rewrite the
/// user's document.
/// <para>
/// Stateless and safe to share: each call tokenizes through its own collector, over ColorCode's own
/// process-wide compiled-grammar cache.
/// </para>
/// </remarks>
public sealed class ColorCodeSyntaxHighlighter : ISyntaxHighlighter
{
    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="code"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<CodeToken> Highlight(string code, string? language)
    {
        ArgumentNullException.ThrowIfNull(code);

        var grammar = HighlightingLanguages.Resolve(language);
        if (grammar is null || code.Length == 0)
        {
            return [];
        }

        var collector = new TokenCollector();
        return collector.Collect(code, grammar);
    }

    // Drives ColorCode's parser and turns its nested scopes into a flat, gapless run of Code Tokens.
    // ColorCode hands the parse back in chunks (a grammar that embeds another — HTML around
    // JavaScript — yields several), each with the scopes found within that chunk; the tokens are
    // accumulated across every chunk in the order they arrive.
    private sealed class TokenCollector : CodeColorizerBase
    {
        private readonly List<CodeToken> _tokens = [];

        internal TokenCollector()
            : base(null, null)
        {
        }

        internal IReadOnlyList<CodeToken> Collect(string code, ILanguage grammar)
        {
            languageParser.Parse(code, grammar, (chunk, scopes) => Write(chunk, scopes));
            return _tokens;
        }

        /// <inheritdoc />
        protected override void Write(string parsedSourceCode, IList<Scope> scopes)
        {
            var runs = new List<(int Index, int Length, CodeTokenKind Kind)>();
            foreach (var scope in scopes)
            {
                Flatten(scope, runs);
            }

            runs.Sort((left, right) => left.Index.CompareTo(right.Index));

            var cursor = 0;
            foreach (var run in runs)
            {
                // A malformed or overlapping scope must never make the output disagree with the
                // input; skipping one that has already been passed keeps the emit strictly forward.
                if (run.Index < cursor || run.Length <= 0)
                {
                    continue;
                }

                Emit(parsedSourceCode, cursor, run.Index - cursor, CodeTokenKind.Plain);
                Emit(parsedSourceCode, run.Index, run.Length, run.Kind);
                cursor = run.Index + run.Length;
            }

            Emit(parsedSourceCode, cursor, parsedSourceCode.Length - cursor, CodeTokenKind.Plain);
        }

        // Reduces a scope tree to the innermost runs that actually color text: a scope's children
        // win over the scope itself, and the parent's uncovered stretches are kept at its own kind.
        // Flattening this way is what lets the caller emit a single, non-overlapping sequence.
        private static void Flatten(Scope scope, List<(int Index, int Length, CodeTokenKind Kind)> runs)
        {
            var kind = CodeTokenKinds.From(scope.Name);
            if (scope.Children is not { Count: > 0 })
            {
                runs.Add((scope.Index, scope.Length, kind));
                return;
            }

            var cursor = scope.Index;
            foreach (var child in scope.Children.OrderBy(child => child.Index))
            {
                if (child.Index > cursor)
                {
                    runs.Add((cursor, child.Index - cursor, kind));
                }

                Flatten(child, runs);
                cursor = child.Index + child.Length;
            }

            var end = scope.Index + scope.Length;
            if (cursor < end)
            {
                runs.Add((cursor, end - cursor, kind));
            }
        }

        private void Emit(string source, int index, int length, CodeTokenKind kind)
        {
            if (length <= 0 || index < 0 || index + length > source.Length)
            {
                return;
            }

            _tokens.Add(new CodeToken(source.Substring(index, length), kind));
        }
    }
}
