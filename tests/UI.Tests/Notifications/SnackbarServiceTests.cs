using Shouldly;
using UI.Notifications;
using Xunit;

namespace UI.Tests.Notifications;

/// <summary>Behavioural tests for <see cref="SnackbarService"/>.</summary>
public sealed class SnackbarServiceTests
{
    [Fact]
    public void Show_GivenContent_AppendsMessageToMessages()
    {
        var service = new SnackbarService();

        service.Show("Saved.");

        service.Messages.Count.ShouldBe(1);
        service.Messages[0].Content.ShouldBe("Saved.");
    }

    [Fact]
    public void Show_GivenSeveralMessages_PreservesOrder()
    {
        var service = new SnackbarService();

        service.Show("first");
        service.Show("second");

        service.Messages.Select(message => message.Content).ShouldBe(["first", "second"]);
    }

    [Fact]
    public void Show_GivenContent_RaisesMessageShownWithTheMessage()
    {
        var service = new SnackbarService();
        SnackbarMessage? raised = null;
        service.MessageShown += (_, message) => raised = message;

        service.Show("Rendered.");

        raised.ShouldNotBeNull();
        raised.Content.ShouldBe("Rendered.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Show_GivenEmptyContent_Throws(string? content)
    {
        var service = new SnackbarService();

        Should.Throw<ArgumentException>(() => service.Show(content!));
    }
}
