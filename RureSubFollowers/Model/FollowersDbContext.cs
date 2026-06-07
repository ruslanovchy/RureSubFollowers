using Microsoft.EntityFrameworkCore;
using RureSubFollowers.Models;

namespace RureSubFollowers.Model;

public class FollowersDbContext : DbContext
{
    public DbSet<Subscription> Subscriptions { get; set; } = null!;
    public DbSet<OutboxMessage> OutboxMessages { get; set; }

    public FollowersDbContext(DbContextOptions<FollowersDbContext> options) : base(options)
    {
        
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Subscription>().ToTable("Subscriptions");
        modelBuilder.Entity<Subscription>().ToTable(t => t.HasCheckConstraint("CK_Not_Self_Follow", "\"FollowerId\" <> \"FollowingId\""));
        modelBuilder.Entity<OutboxMessage>().ToTable("OutboxMessages");

        modelBuilder.Entity<Subscription>()
            .HasKey(s => new { s.FollowerId, s.FollowingId });
    }
}
