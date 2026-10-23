using BlazorClient.Components;
using ClientCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
if (builder.Configuration["Ledger:Api"] is { } url)
{
    // The destination is server configuration, never a URL entered by a browser user.
    builder.Services.AddScoped(_ => LedgerHttpClient.CreateHttpClient(new Uri(url)));
    builder.Services.AddScoped<IReportSource>(sp => new LedgerHttpClient(sp.GetRequiredService<HttpClient>()));
}
else builder.Services.AddScoped<IReportSource, DemoReportSource>();
var app = builder.Build();
if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
