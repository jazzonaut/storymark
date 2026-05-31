using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Storymark.Api.Infrastructure.Database;

/// <summary>Design-time factory so `dotnet ef` can build the context without the
/// web host. The connection string is only used by `database update`, not by
/// `migrations add`.</summary>
internal sealed class StorymarkDbContextFactory : IDesignTimeDbContextFactory<StorymarkDbContext>
{
    public StorymarkDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Postgres")
            ?? "Host=localhost;Port=5432;Database=storymark;Username=storymark;Password=storymark";

        var options = new DbContextOptionsBuilder<StorymarkDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new StorymarkDbContext(options);
    }
}
