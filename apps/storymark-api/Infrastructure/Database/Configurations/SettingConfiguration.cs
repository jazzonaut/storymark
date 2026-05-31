using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Storymark.Api.Domain.Settings;

namespace Storymark.Api.Infrastructure.Database.Configurations;

internal sealed class SettingConfiguration : IEntityTypeConfiguration<Setting>
{
    public void Configure(EntityTypeBuilder<Setting> builder)
    {
        builder.ToTable("settings");
        builder.HasKey(s => s.Key);
        builder.Property(s => s.Key).HasColumnName("key");
        builder.Property(s => s.Value).HasColumnName("value").HasColumnType("jsonb");
        builder.Property(s => s.ValueType).HasColumnName("value_type").HasConversion<string>().HasMaxLength(16);
        builder.Property(s => s.IsSecret).HasColumnName("is_secret");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by");
    }
}
