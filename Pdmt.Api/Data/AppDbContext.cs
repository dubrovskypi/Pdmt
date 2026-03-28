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
            .Property(u => u.Email)
            .HasMaxLength(128)
            .IsRequired();
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
        builder.Entity<RefreshToken>()
            .HasIndex(rt => new { rt.UserId, rt.IsRevoked });

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
            .Property(f => f.Email)
            .HasMaxLength(128)
            .IsRequired();
        builder.Entity<FailedLoginAttempt>()
            .HasIndex(f => f.Email);
        builder.Entity<FailedLoginAttempt>()
            .HasIndex(f => f.OccurredAtUtc);

        builder.Entity<Tag>()
            .Property(t => t.Name)
            .HasMaxLength(100)
            .IsRequired();
        builder.Entity<Tag>()
            .HasOne(t => t.User)
            .WithMany(u => u.Tags)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Tag>()
            .HasIndex(t => new { t.UserId, t.Name })
            .IsUnique();

        builder.Entity<EventTag>()
            .HasKey(et => new { et.EventId, et.TagId });
        builder.Entity<EventTag>()
            .HasOne(et => et.Event)
            .WithMany(e => e.EventTags)
            .HasForeignKey(et => et.EventId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<EventTag>()
            .HasOne(et => et.Tag)
            .WithMany(t => t.EventTags)
            .HasForeignKey(et => et.TagId)
            .OnDelete(DeleteBehavior.ClientCascade);
        builder.Entity<EventTag>()
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
