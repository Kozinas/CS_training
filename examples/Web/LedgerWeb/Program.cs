using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Course.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
string path = builder.Configuration["Ledger:Path"] ?? throw new InvalidOperationException("Set Ledger__Path to a writable SQLite file.");
string connection = new SqliteConnectionStringBuilder { DataSource = Path.GetFullPath(path), Pooling = false,
    ForeignKeys = true, DefaultTimeout = 5 }.ToString();
builder.Services.AddDbContextFactory<WebDb>(o => o.UseSqlite(connection));
if (builder.Configuration["Keys:Directory"] is { Length: > 0 } keyDirectory)
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.GetFullPath(keyDirectory)))
        .SetApplicationName("Course.LedgerWeb");
builder.Services.AddIdentityCore<WebUser>(o =>
{
    o.User.RequireUniqueEmail = true;
    o.Password.RequiredLength = 12;
    o.Lockout.MaxFailedAccessAttempts = 5;
}).AddEntityFrameworkStores<WebDb>().AddSignInManager().AddDefaultTokenProviders();
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddCookie(IdentityConstants.ApplicationScheme, o =>
    {
        o.LoginPath = "/Login";
        o.Cookie.Name = "Course.Web.Auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.ExpireTimeSpan = TimeSpan.FromMinutes(15);
        o.SlidingExpiration = false;
        o.Events.OnValidatePrincipal = SecurityStampValidator.ValidatePrincipalAsync;
    })
    .AddBearerToken(IdentityConstants.BearerScheme, o => o.BearerTokenExpiration = TimeSpan.FromMinutes(15));
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("api", p => p.AddAuthenticationSchemes(IdentityConstants.BearerScheme)
        .RequireAuthenticatedUser().RequireClaim(ClaimTypes.NameIdentifier));
    o.AddPolicy("admin", p => p.AddAuthenticationSchemes(IdentityConstants.BearerScheme)
        .RequireAuthenticatedUser().RequireRole("admin"));
});
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = c =>
    c.ProblemDetails.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? c.HttpContext.TraceIdentifier);
builder.Services.AddRazorPages();
builder.Services.AddAntiforgery(o => o.Cookie.SecurePolicy = CookieSecurePolicy.Always);
builder.Services.AddOpenApi(o => o.AddDocumentTransformer((document, context, token) =>
{
    document.Components ??= new OpenApiComponents();
    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
    document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
    { Type = SecuritySchemeType.Http, Scheme = "bearer", Description = "Opaque ASP.NET Core bearer token from /auth/token; not a JWT." };
    foreach (var pathItem in document.Paths.Where(p => p.Key.StartsWith("/api/", StringComparison.Ordinal)))
        if (pathItem.Value.Operations is not null)
            foreach (var operation in pathItem.Value.Operations.Values)
                operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] }];
    return Task.CompletedTask;
}));
builder.Services.AddGrpc();
builder.Services.AddSignalR(o => o.MaximumReceiveMessageSize = 16 * 1024);
builder.Services.AddHybridCache();
builder.Services.AddScoped<LedgerService>();
builder.Services.AddScoped<OutboxDispatcher>();
if (builder.Configuration.GetValue("Worker:Enabled", true)) builder.Services.AddHostedService<DeliveryWorker>();
builder.Services.AddHealthChecks().AddCheck<LedgerHealth>("storage", tags: ["ready"]);
builder.Services.AddHttpClient("rates", c =>
{
    c.BaseAddress = new Uri(builder.Configuration["Rates:BaseUrl"] ?? "http://127.0.0.1:5099/");
    c.Timeout = TimeSpan.FromSeconds(2);
});
builder.Services.AddCors(o => o.AddPolicy("local-ui", p => p.WithOrigins("https://localhost:5173")
    .WithMethods("GET", "POST", "PUT").WithHeaders("Authorization", "Content-Type")));
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("auth", c => RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString() ?? "local",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy("api-limit", c => RateLimitPartition.GetFixedWindowLimiter(c.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue("Limits:ApiPermits", 1000),
            Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.OnRejected = async (c, token) =>
    {
        c.HttpContext.Response.Headers.RetryAfter = "60";
        await Results.Problem(statusCode: 429, title: "rate_limit").ExecuteAsync(c.HttpContext);
    };
});
var telemetry = builder.Services.AddOpenTelemetry().ConfigureResource(r => r.AddService("course-ledger"));
telemetry.WithTracing(t =>
{
    t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddSource(WebTelemetry.Name);
    if (builder.Configuration.GetValue("Telemetry:Console", false)) t.AddConsoleExporter();
});
telemetry.WithMetrics(m =>
{
    m.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddMeter(WebTelemetry.Name);
    if (builder.Configuration.GetValue("Telemetry:Console", false)) m.AddConsoleExporter();
});

var app = builder.Build();
if (args.Contains("--migrate", StringComparer.Ordinal))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<WebDb>().Database.MigrateAsync();
    await app.DisposeAsync();
    return;
}
app.UseExceptionHandler();
app.UseStatusCodePages();
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; frame-ancestors 'none'; form-action 'self'";
    try { await next(context); }
    catch (ContractException ex) { await Results.Problem(statusCode: ex.Status, title: ex.Message).ExecuteAsync(context); }
    catch (SqliteException ex) when (ex.SqliteErrorCode is 5 or 6)
    { await Results.Problem(statusCode: 503, title: "storage_busy").ExecuteAsync(context); }
});
if (!app.Environment.IsEnvironment("Testing") && !builder.Configuration.GetValue("Web:AllowHttp", false))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();
app.UseCors("local-ui");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    if ((context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/auth")) &&
        context.Request.Method is "POST" or "PUT")
    {
        const int limit = 128 * 1024;
        if (context.Request.ContentLength > limit) { context.Response.StatusCode = 413; return; }
        await using var body = new MemoryStream();
        byte[] buffer = new byte[8192];
        while (true)
        {
            int read = await context.Request.Body.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, limit + 1 - (int)body.Length)), context.RequestAborted);
            if (read == 0) break;
            body.Write(buffer, 0, read);
            if (body.Length > limit) { context.Response.StatusCode = 413; return; }
        }
        body.Position = 0;
        var original = context.Request.Body;
        context.Request.Body = body;
        try { await next(context); }
        finally { context.Request.Body = original; }
        return;
    }
    await next(context);
});
app.UseAntiforgery();

var auth = app.MapGroup("/auth").RequireRateLimiting("auth");
auth.MapPost("/register", async (LoginInput input, UserManager<WebUser> users) =>
{
    if (string.IsNullOrWhiteSpace(input.Email) || input.Email.Length > 254 ||
        !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(input.Email) ||
        input.Password is null || input.Password.Length > 128) return Results.Problem(statusCode: 400, title: "invalid_credentials");
    var result = await users.CreateAsync(new WebUser { UserName = input.Email, Email = input.Email }, input.Password);
    return result.Succeeded ? Results.StatusCode(201) : Results.Problem(statusCode: 400, title: "registration_rejected");
}).Produces(201).ProducesProblem(400);
auth.MapPost("/token", async (LoginInput input, UserManager<WebUser> users, SignInManager<WebUser> signIn) =>
{
    if (string.IsNullOrEmpty(input.Email) || string.IsNullOrEmpty(input.Password) || input.Password.Length > 128)
        return Results.Unauthorized();
    var user = await users.FindByEmailAsync(input.Email);
    if (user is null || !(await signIn.CheckPasswordSignInAsync(user, input.Password, lockoutOnFailure: true)).Succeeded)
        return Results.Unauthorized();
    return Results.SignIn(await signIn.CreateUserPrincipalAsync(user), authenticationScheme: IdentityConstants.BearerScheme);
}).Produces(200).ProducesProblem(401);
var api = app.MapGroup("/api/v1").RequireAuthorization("api").RequireRateLimiting("api-limit");
api.MapPost("/imports", async (ImportInput input, ClaimsPrincipal user, LedgerService service, CancellationToken token) =>
    Results.Ok(await service.Import(Owner(user), input, token)))
    .Produces<ImportReply>().ProducesProblem(400).ProducesProblem(409).ProducesProblem(413);
api.MapGet("/expenses", async (ClaimsPrincipal user, LedgerService service, CancellationToken token, long after = 0, int size = 20) =>
    Results.Ok(await service.Page(Owner(user), after, size, token))).Produces<PageReply>().ProducesProblem(400);
api.MapGet("/expenses/{id:guid}", async (Guid id, ClaimsPrincipal user, LedgerService service, CancellationToken token) =>
    Results.Ok(await service.Find(Owner(user), id, token))).Produces<ExpenseView>().ProducesProblem(404);
api.MapPut("/expenses/{id:guid}", async (Guid id, UpdateInput input, ClaimsPrincipal user, LedgerService service, CancellationToken token) =>
{
    await service.Update(Owner(user), id, input, token);
    return Results.NoContent();
}).Produces(204).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
api.MapGet("/report", async (ClaimsPrincipal user, LedgerService service, CancellationToken token) =>
    Results.Ok(await service.Report(Owner(user), token))).Produces<ReportItem[]>();
api.MapGet("/projection", async (ClaimsPrincipal user, WebDb db, CancellationToken token) =>
{
    string owner = Owner(user);
    return Results.Ok(await db.Projections.AsNoTracking().Where(p => p.Owner == owner)
        .Select(p => new { p.Count, p.AmountMinor }).SingleOrDefaultAsync(token) ?? new { Count = 0L, AmountMinor = 0L });
});
api.MapGet("/rate", async (IHttpClientFactory clients, CancellationToken token) =>
{
    try
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(2));
        using var response = await clients.CreateClient("rates").GetAsync("rate", HttpCompletionOption.ResponseHeadersRead, deadline.Token);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
        byte[] bytes = new byte[8193];
        int length = 0;
        while (length < bytes.Length)
        {
            int read = await stream.ReadAsync(bytes.AsMemory(length), deadline.Token);
            if (read == 0) break;
            length += read;
        }
        if (length > 8192) return Results.Problem(statusCode: 502, title: "upstream_body_too_large");
        var rate = System.Text.Json.JsonSerializer.Deserialize<RateReply>(bytes.AsSpan(0, length), System.Text.Json.JsonSerializerOptions.Web);
        return rate is { Value: > 0 } ? Results.Ok(rate) : Results.Problem(statusCode: 502, title: "invalid_upstream");
    }
    catch (HttpRequestException) { return Results.Problem(statusCode: 503, title: "upstream_unavailable"); }
    catch (System.Text.Json.JsonException) { return Results.Problem(statusCode: 502, title: "invalid_upstream"); }
    catch (OperationCanceledException) when (!token.IsCancellationRequested)
    { return Results.Problem(statusCode: 504, title: "upstream_timeout"); }
}).Produces<RateReply>().ProducesProblem(502).ProducesProblem(503).ProducesProblem(504);
api.MapGet("/admin", () => Results.Ok(new { admin = true })).RequireAuthorization("admin");
app.MapRazorPages();
app.MapGrpcService<ReportRpc>();
app.MapHub<LedgerHub>("/events");
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") });
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing")) app.MapOpenApi();
app.Run();

static string Owner(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)!;
public sealed record LoginInput(string? Email, string? Password);
public sealed record RateReply(decimal Value);
public partial class Program;
