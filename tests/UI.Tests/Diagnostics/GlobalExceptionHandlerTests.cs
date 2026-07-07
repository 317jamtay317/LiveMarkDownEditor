using Microsoft.Extensions.Logging;
using Shouldly;
using UI.Diagnostics;
using UI.Notifications;
using UI.Tests.TestDoubles;
using Xunit;

namespace UI.Tests.Diagnostics;

/// <summary>Behavioural tests for <see cref="GlobalExceptionHandler"/>.</summary>
public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public void Handle_GivenException_LogsAtErrorWithTheException()
    {
        var logger = new RecordingLogger<GlobalExceptionHandler>();
        var handler = new GlobalExceptionHandler(logger, new SnackbarService());
        var exception = new InvalidOperationException("boom");

        handler.Handle(exception, "AppDomain");

        var entry = logger.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Error);
        entry.Exception.ShouldBe(exception);
        entry.Message.ShouldContain("AppDomain");
    }

    [Fact]
    public void Handle_GivenException_SurfacesASnackbarMessage()
    {
        var snackbar = new SnackbarService();
        var handler = new GlobalExceptionHandler(new RecordingLogger<GlobalExceptionHandler>(), snackbar);

        handler.Handle(new InvalidOperationException("boom"), "Dispatcher");

        snackbar.Messages.ShouldHaveSingleItem();
    }

    [Fact]
    public void Handle_GivenNullException_Throws()
    {
        var handler = new GlobalExceptionHandler(
            new RecordingLogger<GlobalExceptionHandler>(),
            new SnackbarService());

        Should.Throw<ArgumentNullException>(() => handler.Handle(null!, "AppDomain"));
    }
}
