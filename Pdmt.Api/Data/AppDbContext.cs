using Microsoft.EntityFrameworkCore;
using Pdmt.Api.Domain;

namespace Pdmt.Api.Data;

public class AppDbContext(DbContextOptions options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .Property(u => u.Email)
            .HasMaxLength(128)
            .IsRequired();
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<RefreshToken>()
            .HasOne(e => e.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RefreshToken>()
            .HasIndex(u => u.Token)
            .IsUnique();
        modelBuilder.Entity<RefreshToken>()
            .HasIndex(rt => new { rt.UserId, rt.IsRevoked });

        modelBuilder.Entity<Event>()
            .HasOne(e => e.User)
            .WithMany(u => u.Events)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Event>()
            .HasIndex(u => u.UserId);
        modelBuilder.Entity<Event>()
            .HasIndex(u => u.Timestamp);
        modelBuilder.Entity<Event>()
            .HasIndex(d => new { d.UserId, d.Timestamp });

        modelBuilder.Entity<Summary>()
            .HasOne(d => d.User)
            .WithMany(u => u.Summaries)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Summary>()
            .HasIndex(d => new { d.UserId, d.Date })
            .IsUnique();

        modelBuilder.Entity<FailedLoginAttempt>()
            .Property(f => f.Email)
            .HasMaxLength(128)
            .IsRequired();
        modelBuilder.Entity<FailedLoginAttempt>()
            .HasIndex(f => f.Email);
        modelBuilder.Entity<FailedLoginAttempt>()
            .HasIndex(f => f.OccurredAtUtc);

        modelBuilder.Entity<Tag>()
            .Property(t => t.Name)
            .HasMaxLength(100)
            .IsRequired();
        modelBuilder.Entity<Tag>()
            .HasOne(t => t.User)
            .WithMany(u => u.Tags)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Tag>()
            .HasIndex(t => new { t.UserId, t.Name })
            .IsUnique();

        modelBuilder.Entity<EventTag>()
            .HasKey(et => new { et.EventId, et.TagId });
        modelBuilder.Entity<EventTag>()
            .HasOne(et => et.Event)
            .WithMany(e => e.EventTags)
            .HasForeignKey(et => et.EventId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<EventTag>()
            .HasOne(et => et.Tag)
            .WithMany(t => t.EventTags)
            .HasForeignKey(et => et.TagId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<EventTag>()
            .HasIndex(et => et.TagId);
    }
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
    public DbSet<Event> Events { get; set; } = null!;
    public DbSet<Summary> Summaries { get; set; } = null!;
    public DbSet<FailedLoginAttempt> FailedLoginAttempts { get; set; } = null!;
    public DbSet<Tag> Tags { get; set; } = null!;
    public DbSet<EventTag> EventTags { get; set; } = null!;
}
