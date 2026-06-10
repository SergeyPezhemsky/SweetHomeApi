using Application.Modules.Health;
using Application.Modules.SmartHome;
using Application.Modules.Widgets;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Persistance;

public class SweetHomeDbContext(DbContextOptions<SweetHomeDbContext> options)
    : IdentityDbContext<IdentityUser>(options)

{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<MainWidget>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity
                .HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<HealthEntry>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => new { e.UserId, e.Date })
                .IsUnique();

            entity
                .HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SmartHomeRoom>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.UserId);

            entity
                .HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SmartHomeWidget>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.RoomId);

            entity
                .HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(e => e.Room)
                .WithMany()
                .HasForeignKey(e => e.RoomId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    public DbSet<MainWidget> MainWidgets { get; set; }
    public DbSet<HealthEntry> HealthEntries { get; set; }
    public DbSet<SmartHomeRoom> SmartHomeRooms { get; set; }
    public DbSet<SmartHomeWidget> SmartHomeWidgets { get; set; }
}
