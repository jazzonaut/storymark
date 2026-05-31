using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Formatting.Compact;
using Storymark.Api.Infrastructure.Database;
using Storymark.Api.Infrastructure.Llm;
using Storymark.Api.Infrastructure.Settings;
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

    builder.Services.AddDbContext<StorymarkDbContext>(options => options.UseNpgsql(connectionString));
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
    app.UseFastEndpoints(c => c.Endpoints.RoutePrefix = "api");

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
