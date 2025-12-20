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

        builder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        builder.Entity<RefreshToken>()
            .HasOne(e => e.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Event>()
            .HasOne(e => e.User)
            .WithMany(u => u.Events)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Summary>()
            .HasOne(d => d.User)
            .WithMany(u => u.Summaries)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Summary>()
            .HasIndex(d => new { d.UserId, d.Date })
            .IsUnique();
    }
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
    public DbSet<Event> Events { get; set; } = null!;
    public DbSet<Summary> Summaries { get; set; } = null!;
    public DbSet<FailedLoginAttempt> FailedLoginAttempts { get; set; } = null!;
}
