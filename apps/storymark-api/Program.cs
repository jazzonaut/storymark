using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Formatting.Compact;
using Storymark.Api.Infrastructure.Database;

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
    builder.Services.AddFastEndpoints();

    var app = builder.Build();

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
