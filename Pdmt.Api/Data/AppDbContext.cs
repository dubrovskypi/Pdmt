using Microsoft.EntityFrameworkCore;
using Pdmt.Api.Domain;

namespace Pdmt.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions options) : base(options)
    {
    }
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        //TODO MAKE MIGRATIONS FOR INDEXES
        builder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        builder.Entity<RefreshToken>()
            .HasOne(e => e.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<RefreshToken>()
            .HasIndex(u => u.Token)
            .IsUnique();

        builder.Entity<Event>()
            .HasOne(e => e.User)
            .WithMany(u => u.Events)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Event>()
            .HasIndex(u => u.UserId);
        builder.Entity<Event>()
            .HasIndex(u => u.Timestamp);
        builder.Entity<Event>()
            .HasIndex(d => new { d.UserId, d.Timestamp });

        builder.Entity<Summary>()
            .HasOne(d => d.User)
            .WithMany(u => u.Summaries)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Summary>()
            .HasIndex(d => new { d.UserId, d.Date })
            .IsUnique();

        builder.Entity<FailedLoginAttempt>()
            .HasIndex(f => f.Email);
        builder.Entity<FailedLoginAttempt>()
            .HasIndex(f => f.OccurredAtUtc);
    }
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
    public DbSet<Event> Events { get; set; } = null!;
    public DbSet<Summary> Summaries { get; set; } = null!;
    public DbSet<FailedLoginAttempt> FailedLoginAttempts { get; set; } = null!;
}
