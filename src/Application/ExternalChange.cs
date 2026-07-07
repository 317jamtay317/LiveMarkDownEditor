namespace Application;

/// <summary>
/// Signals that the Watched File at <paramref name="Path"/> was modified outside the Editor Session
/// (an External Change) — by another user, process, or AI.
/// </summary>
/// <param name="Path">The path of the Watched File that changed on disk.</param>
public sealed record ExternalChange(string Path);
