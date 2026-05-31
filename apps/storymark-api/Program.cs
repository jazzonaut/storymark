using FastEndpoints;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using Serilog;
using Serilog.Formatting.Compact;
using Storymark.Api.Infrastructure.Auth;
using Storymark.Api.Infrastructure.Database;
using Storymark.Api.Infrastructure.Llm;
using Storymark.Api.Infrastructure.Settings;
using Storymark.Api.Infrastructure.Storage;
using Storymark.Api.Jobs;
using Storymark.Api.Services.Books;
using Storymark.Api.Services.Llm;
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
    builder.Services.AddHostedService<SettingsChangeListener>();

    var ollamaUrl = builder.Configuration["Ollama:Url"]
        ?? throw new InvalidOperationException("Ollama:Url is not configured.");
    builder.Services.AddHttpClient<OllamaClient>(client =>
        client.BaseAddress = new Uri(ollamaUrl.TrimEnd('/') + "/"));
    builder.Services.AddHttpClient<OpenAiCompatibleClient>();
    builder.Services.AddTransient<IChatModelClient>(sp => sp.GetRequiredService<OllamaClient>());
    builder.Services.AddTransient<IChatModelClient>(sp => sp.GetRequiredService<OpenAiCompatibleClient>());
    builder.Services.AddScoped<LlmCallRecorder>();
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
    builder.Services.AddScoped<ReadingProgressService>();
    builder.Services.AddScoped<BookmarkService>();
    builder.Services.AddScoped<ProcessBookJob>();

    builder.Services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(o => o.UseNpgsqlConnection(connectionString)));
    builder.Services.AddHangfireServer();

    // Ceiling for the upload endpoint; the precise per-request cap is the live
    // `upload.maxBytes` setting, enforced while streaming in FileStorageService.
    const long uploadBodyCeiling = 256L * 1024 * 1024;
    builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = uploadBodyCeiling);
    builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = uploadBodyCeiling);

    builder.Services.AddSingleton<AdminAuthPreProcessor>();

    builder.Services.AddFastEndpoints();

    var app = builder.Build();

    await using (var scope = app.Services.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<StorymarkDbContext>();
        await db.Database.MigrateAsync();
    }

    await app.Services.GetRequiredService<SettingsSeeder>().SeedAsync(CancellationToken.None);
    await app.Services.GetRequiredService<ISettingsService>().LoadAllAsync(CancellationToken.None);

    app.UseSerilogRequestLogging();

    // The built SPA ships in wwwroot (ADR-0013) and is served on the same origin
    // as the API. .webmanifest isn't in the default content-type map; without it
    // browsers reject the PWA manifest.
    var staticContentTypes = new FileExtensionContentTypeProvider();
    staticContentTypes.Mappings[".webmanifest"] = "application/manifest+json";
    app.UseDefaultFiles();
    app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = staticContentTypes });

    var settingsService = app.Services.GetRequiredService<ISettingsService>();
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = [new HangfireDashboardAuthFilter(settingsService)],
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
