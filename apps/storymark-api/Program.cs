using System.Threading.RateLimiting;
using FastEndpoints;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using Serilog;
using Serilog.Formatting.Compact;
using Storymark.Api.Domain.Users;
using Storymark.Api.Infrastructure.Auth;
using Storymark.Api.Infrastructure.Database;
using Storymark.Api.Infrastructure.Llm;
using Storymark.Api.Infrastructure.Settings;
using Storymark.Api.Infrastructure.Storage;
using Storymark.Api.Jobs;
using Storymark.Api.Services.Assistant;
using Storymark.Api.Services.Books;
using Storymark.Api.Services.Entities;
using Storymark.Api.Services.Llm;
using Storymark.Api.Services.Retrieval;
using Storymark.Api.Services.Selection;
using Storymark.Api.Services.Settings;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .WriteTo.Console(new CompactJsonFormatter()));

    var connectionString = builder.Configuration.GetConnectionString("Postgres")
        ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");

    builder.Services.AddDbContext<StorymarkDbContext>(
        options => options.UseNpgsql(connectionString, npgsql => npgsql.UseVector()));
    builder.Services.Configure<SettingsDefaultsOptions>(
        builder.Configuration.GetSection(SettingsDefaultsOptions.SectionName));
    builder.Services.AddSingleton<ISettingsEncryptor, AesGcmSettingsEncryptor>();
    builder.Services.AddSingleton<ISettingsService, SettingsService>();
    builder.Services.AddSingleton<SettingsSeeder>();
    builder.Services.AddSingleton<LlmRegistrySeeder>();
    builder.Services.AddHostedService<SettingsChangeListener>();

    // Base URL comes from the resolved provider per call (ADR-0016), so the typed
    // clients carry no fixed BaseAddress. The timeout is generous: local CPU
    // inference of a long section can take minutes, and the default 100s would cut
    // it off (silently dropping an extraction section). Override via
    // `Llm:HttpTimeoutSeconds`. Transient blips are retried in HttpResilience.
    var llmTimeout = TimeSpan.FromSeconds(builder.Configuration.GetValue<int?>("Llm:HttpTimeoutSeconds") ?? 300);
    builder.Services.AddHttpClient<OllamaClient>(c => c.Timeout = llmTimeout);
    builder.Services.AddHttpClient<OpenAiCompatibleClient>(c => c.Timeout = llmTimeout);
    builder.Services.AddTransient<IChatModelClient>(sp => sp.GetRequiredService<OllamaClient>());
    builder.Services.AddTransient<IChatModelClient>(sp => sp.GetRequiredService<OpenAiCompatibleClient>());
    builder.Services.AddScoped<LlmCallRecorder>();
    builder.Services.AddScoped<LlmRegistryService>();
    builder.Services.AddScoped<ChatModelRouter>();
    builder.Services.AddScoped<LlmCallLogService>();

    builder.Services.Configure<StorageOptions>(
        builder.Configuration.GetSection(StorageOptions.SectionName));
    builder.Services.AddSingleton<FileStorageService>();
    builder.Services.AddSingleton<IBookImportHandler, EpubImportHandler>();
    builder.Services.AddSingleton<ContentSanitizationService>();
    builder.Services.AddScoped<BookImportService>();
    builder.Services.AddScoped<BookCatalogService>();
    builder.Services.AddScoped<BookSectionService>();
    builder.Services.AddScoped<BookExtractionService>();
    builder.Services.AddScoped<BookMemoryService>();
    builder.Services.AddScoped<BookEntityService>();
    builder.Services.AddScoped<EntitySummaryService>();
    builder.Services.AddScoped<EntityMergeService>();
    builder.Services.AddScoped<ReadingProgressService>();
    builder.Services.AddScoped<BookmarkService>();
    builder.Services.AddScoped<BookDeletionService>();
    builder.Services.AddScoped<BookProcessingAdminService>();
    builder.Services.AddScoped<BookProcessingRun>();
    builder.Services.AddScoped<ProcessBookJob>();
    builder.Services.AddScoped<EmbeddingJob>();
    builder.Services.AddScoped<SectionExtractionJob>();
    builder.Services.AddScoped<BookMemorySnapshotJob>();
    builder.Services.AddScoped<BackfillSectionTitlesJob>();
    builder.Services.AddScoped<SpoilerSafeRetrievalService>();
    builder.Services.AddScoped<AssistantService>();
    builder.Services.AddScoped<SelectionHintService>();
    builder.Services.AddScoped<CurrentSceneService>();

    builder.Services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(
            o => o.UseNpgsqlConnection(connectionString),
            // A whole-book job can outlive the queue's 30-minute invisibility timeout.
            new PostgreSqlStorageOptions { UseSlidingInvisibilityTimeout = true }));
    builder.Services.AddHangfireServer();

    // Ceiling for the upload endpoint; the precise per-request cap is the live
    // `upload.maxBytes` setting, enforced while streaming in FileStorageService.
    const long uploadBodyCeiling = 256L * 1024 * 1024;
    builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = uploadBodyCeiling);
    builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = uploadBodyCeiling);

    builder.Services.AddSingleton<AdminAuthPreProcessor>();

    // Cookie tickets are encrypted with data-protection keys; the default key
    // store is the container filesystem, which a redeploy wipes — signing every
    // reader out. Persist keys under the storage root instead (ADR-0023).
    var storageRoot = builder.Configuration.GetValue<string>("Storage:RootPath")
        ?? throw new InvalidOperationException("Storage:RootPath is not configured.");
    var dataProtectionKeysPath = Directory.CreateDirectory(Path.Combine(storageRoot, "dataprotection-keys"));
    builder.Services.AddDataProtection().PersistKeysToFileSystem(dataProtectionKeysPath);

    builder.Services
        .AddIdentityCore<StorymarkUser>(options =>
        {
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            // Length-only policy: composition rules push a household toward
            // weaker memorable-but-mangled passwords; a longer minimum doesn't.
            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
        })
        .AddUserStore<UserOnlyStore<StorymarkUser, StorymarkDbContext, Guid>>();

    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.Cookie.Name = "storymark.auth";
            options.Cookie.HttpOnly = true;
            // Strict + the same-origin SPA (ADR-0013) is the CSRF posture
            // (ADR-0023, ADR-0025). SameAsRequest marks the cookie Secure
            // behind TLS — the forwarded proto makes the request look HTTPS
            // (UseForwardedHeaders below) — while keeping plain-HTTP LAN
            // access working.
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromDays(30);
            options.SlidingExpiration = true;
            // An API answers 401/403; the cookie handler's default is an MVC
            // login-page redirect.
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
            // A password set/change/reset rotates the account's security stamp;
            // this is what makes that rotation actually end other sessions.
            options.Events.OnValidatePrincipal = ReaderCookie.ValidatePrincipalAsync;
        });
    builder.Services.AddAuthorization();

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        // Partitioned by source address — the real client once forwarded
        // headers have run, not the proxy. Lockout (per account) and this
        // (per IP) cover different attacks: targeted guessing vs
        // cross-account spraying (ADR-0025).
        options.AddPolicy(LoginRateLimit.PolicyName, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = LoginRateLimit.PermitLimit,
                    Window = LoginRateLimit.Window,
                }));
    });

    builder.Services.AddFastEndpoints();

    var app = builder.Build();

    await using (var scope = app.Services.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<StorymarkDbContext>();
        await db.Database.MigrateAsync();
        await AdminBootstrap.EnsurePasswordAsync(scope.ServiceProvider, builder.Configuration);
    }

    await app.Services.GetRequiredService<SettingsSeeder>().SeedAsync(CancellationToken.None);
    await app.Services.GetRequiredService<LlmRegistrySeeder>().SeedAsync(CancellationToken.None);
    await app.Services.GetRequiredService<ISettingsService>().LoadAllAsync(CancellationToken.None);

    // Caddy terminates TLS, so without the forwarded scheme the cookie handler
    // sees plain HTTP and never marks the session cookie Secure (ADR-0025).
    // Trusting any proxy is deliberate: production ingress is the caddy docker
    // network only (the API publishes no host port), and a direct LAN caller
    // spoofing X-Forwarded-* gains nothing the LAN doesn't already grant.
    var forwardedHeaders = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    };
    forwardedHeaders.KnownIPNetworks.Clear();
    forwardedHeaders.KnownProxies.Clear();
    app.UseForwardedHeaders(forwardedHeaders);

    app.UseSerilogRequestLogging();

    // The built SPA ships in wwwroot (ADR-0013) and is served on the same origin
    // as the API. .webmanifest isn't in the default content-type map; without it
    // browsers reject the PWA manifest.
    var staticContentTypes = new FileExtensionContentTypeProvider();
    staticContentTypes.Mappings[".webmanifest"] = "application/manifest+json";
    app.UseDefaultFiles();
    app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = staticContentTypes });

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = [new HangfireDashboardAuthFilter()],
    });

    app.UseFastEndpoints(c => c.Endpoints.RoutePrefix = "api");

    // SPA deep links (e.g. /books/{id}/read) fall back to the app shell, but an
    // unknown /api or /hangfire path must still 404 rather than return HTML.
    var webRoot = app.Environment.WebRootPath;
    app.MapFallback(async context =>
    {
        var indexHtml = Path.Combine(webRoot ?? string.Empty, "index.html");
        if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
            || context.Request.Path.StartsWithSegments("/hangfire", StringComparison.OrdinalIgnoreCase)
            || !File.Exists(indexHtml))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(indexHtml);
    });

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Storymark API terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
