using AuthService.Domain.Entities;
using AuthService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<LoginAttempt> LoginAttempts => Set<LoginAttempt>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Id).HasColumnName("id");
            entity.Property(u => u.FirstName).HasColumnName("first_name").IsRequired().HasMaxLength(100);
            entity.Property(u => u.LastName).HasColumnName("last_name").IsRequired().HasMaxLength(100);
            entity.Property(u => u.Email).HasColumnName("email").IsRequired().HasMaxLength(255);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.PasswordHash).HasColumnName("password_hash").IsRequired();
            entity.Property(u => u.Role).HasColumnName("role")
                  .HasConversion(r => r.ToString().ToLowerInvariant(),
                                 s => Enum.Parse<UserRole>(s, true))
                  .HasDefaultValue(UserRole.Buyer)
                  .IsRequired();
            entity.Property(u => u.LockedUntil).HasColumnName("locked_until");
            entity.Property(u => u.FailedLoginAttempts).HasColumnName("failed_login_attempts").HasDefaultValue(0);
            entity.Property(u => u.CreatedAt).HasColumnName("created_at");
            entity.Ignore(u => u.State); // computed property, not persisted
        });

        modelBuilder.Entity<LoginAttempt>(entity =>
        {
            entity.ToTable("login_attempts");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Id).HasColumnName("id");
            entity.Property(a => a.UserId).HasColumnName("user_id");
            entity.Property(a => a.EmailAttempted).HasColumnName("email_attempted").IsRequired().HasMaxLength(255);
            entity.Property(a => a.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
            entity.Property(a => a.AttemptedAt).HasColumnName("attempted_at");
            entity.Property(a => a.Successful).HasColumnName("successful");
        });
    }
}
