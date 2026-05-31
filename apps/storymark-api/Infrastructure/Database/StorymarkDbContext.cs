using Microsoft.EntityFrameworkCore;

namespace Storymark.Api.Infrastructure.Database;

internal sealed class StorymarkDbContext(DbContextOptions<StorymarkDbContext> options) : DbContext(options);
