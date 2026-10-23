using BlazorClient.Components;
using Bunit;
using ClientCore;
using Microsoft.Extensions.DependencyInjection;

namespace Specializations.Tests;

public sealed class UiTests
{
    [Fact]
    public async Task FormRendersDataAsTextAndKeepsTwoUsersSeparate()
    {
        await using var context = new BunitContext();
        var source = new ControlledSource();
        source.Completion.SetResult([new("<script>bad()</script>", 1, 100)]);
        context.Services.AddSingleton<IReportSource>(source);
        var first = context.Render<ReportPanel>();
        var second = context.Render<ReportPanel>();
        first.Find("input").Change("alice");
        first.Find("form").Submit();
        first.WaitForAssertion(() => Assert.Equal("Ready", first.Find("[role=status]").TextContent));
        Assert.Single(first.FindAll("tbody tr"));
        Assert.Empty(first.FindAll("script"));
        Assert.Contains("<script>", first.Find("tbody td").TextContent);
        Assert.Empty(second.FindAll("tbody tr"));
        Assert.Equal("Idle", second.Find("[role=status]").TextContent);
        Assert.Equal("alice", source.Token);
    }

    [Fact]
    public async Task ComponentShowsLoadingAndAcceptsCancel()
    {
        await using var context = new BunitContext();
        var source = new ControlledSource();
        context.Services.AddSingleton<IReportSource>(source);
        var cut = context.Render<ReportPanel>();
        var submission = cut.Find("form").SubmitAsync();
        cut.WaitForAssertion(() => Assert.Equal("Loading", cut.Find("[role=status]").TextContent));
        Assert.True(cut.Find("button[type=submit]").HasAttribute("disabled"));
        cut.Find("button[type=button]").Click();
        await submission;
        cut.WaitForAssertion(() => Assert.Equal("Canceled", cut.Find("[role=status]").TextContent));
        Assert.Empty(cut.FindAll("tbody tr"));
    }

    [Fact]
    public async Task ComponentDisplaysNetworkFailure()
    {
        await using var context = new BunitContext();
        var source = new ControlledSource();
        source.Completion.SetException(new HttpRequestException("internal-details"));
        context.Services.AddSingleton<IReportSource>(source);
        var cut = context.Render<ReportPanel>();
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[role=alert]")));
        Assert.DoesNotContain("internal-details", cut.Markup);
    }
}
