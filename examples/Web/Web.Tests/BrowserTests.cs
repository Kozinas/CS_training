using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using static Course.Web.Tests.WebFactory;

namespace Course.Web.Tests;

public sealed class BrowserTests
{
    private static string Csrf(string html) => WebUtility.HtmlDecode(
        Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    private static FormUrlEncodedContent Form(params (string Key, string Value)[] items) =>
        new(items.ToDictionary(i => i.Key, i => i.Value));

    [Fact]
    public async Task Browser_form_requires_csrf_encodes_notes_and_uses_post_redirect_get()
    {
        using var f = new WebFactory(); var user = await f.User();
        var row = Row(note: "<script>alert(1)</script>");
        await Import(user.Client, new(Guid.NewGuid(), [row]));
        using var browser = f.Client();
        var loginHtml = await browser.GetStringAsync("/Login", Token);
        string anonymousCsrf = Csrf(loginHtml); Assert.NotEmpty(anonymousCsrf);
        var noToken = await browser.PostAsync("/Login", Form(("Email", user.Email), ("Password", Password)), Token);
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        var login = await browser.PostAsync("/Login?returnUrl=https://attacker.example", Form(
            ("Email", user.Email), ("Password", Password), ("__RequestVerificationToken", anonymousCsrf)), Token);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/", login.Headers.Location?.OriginalString);
        string cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"), v => v.StartsWith("Course.Web.Auth="));
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/v1/report", Token)).StatusCode);
        string page = await browser.GetStringAsync("/", Token);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", page);
        Assert.DoesNotContain("<script>alert(1)</script>", page);
        string csrf = Csrf(page); Assert.NotEmpty(csrf);
        foreach (string invalid in new[] { "", "forged", anonymousCsrf })
        {
            var response = await browser.PostAsync("/", Form(("Id", row.Id.ToString()), ("ExpectedVersion", "0"),
                ("Amount", "99"), ("__RequestVerificationToken", invalid)), Token);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        Assert.Equal(1250, (await user.Client.GetFromJsonAsync<ExpenseView>($"/api/v1/expenses/{row.Id}", Token))!.AmountMinor);
        var changed = await browser.PostAsync("/", Form(("Id", row.Id.ToString()), ("ExpectedVersion", "0"),
            ("Amount", "20.50"), ("__RequestVerificationToken", csrf)), Token);
        Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);
        var stale = await browser.PostAsync("/", Form(("Id", row.Id.ToString()), ("ExpectedVersion", "0"),
            ("Amount", "99"), ("__RequestVerificationToken", csrf)), Token);
        Assert.Equal(HttpStatusCode.OK, stale.StatusCode);
        Assert.Contains("stale_version", await stale.Content.ReadAsStringAsync(Token));
        Assert.Equal(2050, (await user.Client.GetFromJsonAsync<ExpenseView>($"/api/v1/expenses/{row.Id}", Token))!.AmountMinor);
        var logout = await browser.PostAsync("/?handler=Logout", Form(("__RequestVerificationToken", csrf)), Token);
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await browser.GetAsync("/", Token)).StatusCode);
    }
}
