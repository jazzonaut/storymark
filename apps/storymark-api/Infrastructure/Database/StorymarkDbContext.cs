using Microsoft.EntityFrameworkCore;
using Storymark.Api.Domain.Books;
using Storymark.Api.Domain.LlmCalls;
using Storymark.Api.Domain.Settings;

namespace Storymark.Api.Infrastructure.Database;

internal sealed class StorymarkDbContext(DbContextOptions<StorymarkDbContext> options) : DbContext(options)
{
    public DbSet<Setting> Settings => Set<Setting>();

    public DbSet<LlmCall> LlmCalls => Set<LlmCall>();

    public DbSet<Book> Books => Set<Book>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StorymarkDbContext).Assembly);
    }
}
