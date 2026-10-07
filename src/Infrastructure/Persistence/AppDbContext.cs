using auth.Domain.Entities;
using auth.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace auth.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Session> Sessions => Set<Session>();

    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

    public DbSet<ApiKeyPermission> ApiKeyPermissions => Set<ApiKeyPermission>();

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await Database.BeginTransactionAsync(cancellationToken);

        await operation(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Login).HasMaxLength(100).IsRequired();
            entity.Property(user => user.PasswordHash).HasMaxLength(500).IsRequired();
            entity.HasIndex(user => user.Login).IsUnique();

            entity.HasMany<ApiKey>()
                .WithOne()
                .HasForeignKey(apiKey => apiKey.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany<Session>()
                .WithOne()
                .HasForeignKey(session => session.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Session>(entity =>
        {
            entity.ToTable("sessions");
            entity.HasKey(session => session.Id);
            entity.Property(session => session.RefreshTokenJti).HasMaxLength(200).IsRequired();
            entity.Property(session => session.ExpiresAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(session => session.RevokedAtUtc)
                .HasColumnType("timestamp with time zone")
                .IsRequired(false);
            entity.HasIndex(session => session.RefreshTokenJti).IsUnique();
            entity.HasIndex(session => new { session.UserId, session.ExpiresAtUtc });
        });

        modelBuilder.Entity<ApiKey>(entity =>
        {
            entity.ToTable("api_keys");
            entity.HasKey(apiKey => apiKey.Id);
            entity.Property(apiKey => apiKey.Name).HasMaxLength(100).IsRequired();
            entity.Property(apiKey => apiKey.Prefix).HasMaxLength(32).IsRequired();
            entity.Property(apiKey => apiKey.SecretHash).HasMaxLength(500).IsRequired();
            entity.Property(apiKey => apiKey.Last4).HasMaxLength(4).IsRequired();
            entity.Property(apiKey => apiKey.LastUsedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(apiKey => apiKey.CreatedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(apiKey => apiKey.RevokedAtUtc)
                .HasColumnType("timestamp with time zone")
                .IsRequired(false);
            entity.HasIndex(apiKey => apiKey.Prefix).IsUnique();
            entity.HasIndex(apiKey => new { apiKey.UserId, apiKey.Name }).IsUnique();

            entity.HasOne<ApiKeyPermission>()
                .WithMany()
                .HasForeignKey(apiKey => apiKey.ApiKeyPermissionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ApiKeyPermission>(entity =>
        {
            entity.ToTable("api_key_permissions");
            entity.HasKey(permission => permission.Id);
            entity.Property(permission => permission.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(permission => permission.Name).IsUnique();
            entity.HasData(
                new ApiKeyPermission
                {
                    Id = new Guid("d7c4b8a1-0c2e-4b16-9f4b-1f0e1a2c3d01"),
                    Name = "read"
                },
                new ApiKeyPermission
                {
                    Id = new Guid("d7c4b8a1-0c2e-4b16-9f4b-1f0e1a2c3d02"),
                    Name = "readwrite"
                });
        });
    }
}
