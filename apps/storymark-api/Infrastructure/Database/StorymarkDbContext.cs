using Microsoft.EntityFrameworkCore;
using Storymark.Api.Domain.Books;
using Storymark.Api.Domain.Llm;
using Storymark.Api.Domain.LlmCalls;
using Storymark.Api.Domain.Settings;

namespace Storymark.Api.Infrastructure.Database;

internal sealed class StorymarkDbContext(DbContextOptions<StorymarkDbContext> options) : DbContext(options)
{
    public DbSet<Setting> Settings => Set<Setting>();

    public DbSet<LlmCall> LlmCalls => Set<LlmCall>();

    public DbSet<Book> Books => Set<Book>();

    public DbSet<BookSection> BookSections => Set<BookSection>();

    public DbSet<BookParagraph> BookParagraphs => Set<BookParagraph>();

    public DbSet<BookChunk> BookChunks => Set<BookChunk>();

    public DbSet<ReadingProgress> ReadingProgress => Set<ReadingProgress>();

    public DbSet<BookBookmark> BookBookmarks => Set<BookBookmark>();

    public DbSet<QuestionAnswer> QuestionAnswers => Set<QuestionAnswer>();

    public DbSet<BookEntity> BookEntities => Set<BookEntity>();

    public DbSet<LlmProvider> LlmProviders => Set<LlmProvider>();

    public DbSet<LlmModel> LlmModels => Set<LlmModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StorymarkDbContext).Assembly);
    }
}
