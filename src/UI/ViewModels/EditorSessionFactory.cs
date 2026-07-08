namespace UI.ViewModels;

/// <summary>
/// Creates a fresh, empty <see cref="EditorSessionViewModel"/> — one per Tab — so the Workspace can
/// open documents on demand. Each created session is given its own Watched File watcher, so several
/// Tabs can watch different files at once.
/// </summary>
/// <returns>A new, empty Editor Session.</returns>
public delegate EditorSessionViewModel EditorSessionFactory();
