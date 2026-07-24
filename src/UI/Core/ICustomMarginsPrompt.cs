namespace UI.Core;

/// <summary>
/// Port for the Custom Margins Prompt: the dialog that asks the user for the four custom Print
/// Margins. Keeping the WPF dialog behind a port makes the margin rules testable headlessly against a
/// stub, exactly as the Link Prompt is (INV-030's discipline, applied to margins — INV-061).
/// </summary>
public interface ICustomMarginsPrompt
{
    /// <summary>
    /// Asks the user for custom Print Margins, seeded with the current ones.
    /// </summary>
    /// <param name="current">The margins the prompt opens showing.</param>
    /// <returns>The margins the user chose, or <see langword="null"/> when the prompt was dismissed —
    /// which must change nothing.</returns>
    PrintMargins? Ask(PrintMargins current);
}
