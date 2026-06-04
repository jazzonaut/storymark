using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Storymark.Api.Domain.Users;

namespace Storymark.Api.Infrastructure.Database.Configurations;

internal sealed class UsersConfiguration : IEntityTypeConfiguration<StorymarkUser>
{
    public void Configure(EntityTypeBuilder<StorymarkUser> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id");
        builder.Property(u => u.UserName).HasColumnName("user_name").HasMaxLength(64).IsRequired();
        builder.Property(u => u.NormalizedUserName)
            .HasColumnName("normalized_user_name")
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(u => u.PasswordHash).HasColumnName("password_hash");
        builder.Property(u => u.SecurityStamp).HasColumnName("security_stamp");
        builder.Property(u => u.ConcurrencyStamp).HasColumnName("concurrency_stamp").IsConcurrencyToken();
        builder.Property(u => u.LockoutEnd).HasColumnName("lockout_end");
        builder.Property(u => u.LockoutEnabled).HasColumnName("lockout_enabled");
        builder.Property(u => u.AccessFailedCount).HasColumnName("access_failed_count");
        builder.Property(u => u.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(u => u.NormalizedUserName).IsUnique();

        // Email/phone/2FA are deliberately unmapped (ADR-0023): password auth
        // only. UserOnlyStore methods that need them throw if ever called.
        builder.Ignore(u => u.Email);
        builder.Ignore(u => u.NormalizedEmail);
        builder.Ignore(u => u.EmailConfirmed);
        builder.Ignore(u => u.PhoneNumber);
        builder.Ignore(u => u.PhoneNumberConfirmed);
        builder.Ignore(u => u.TwoFactorEnabled);
    }
}
