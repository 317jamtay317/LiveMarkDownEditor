namespace UI.Tests.Wysiwyg;

/// <summary>
/// Runs a delegate on a dedicated STA thread and marshals any exception back to the caller.
/// WPF document objects (<c>FlowDocument</c> and friends) must be created and read on a single
/// STA thread; xUnit runs tests on MTA thread-pool threads, so tests that touch the WYSIWYG
/// projection route their body through here.
/// </summary>
internal static class StaThread
{
    /// <summary>Executes <paramref name="action"/> on an STA thread, rethrowing any failure.</summary>
    /// <param name="action">The work to run (typically the test body and its assertions).</param>
    public static void Run(Action action)
    {
        Exception? captured = null;

        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                captured = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (captured is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(captured).Throw();
        }
    }
}
