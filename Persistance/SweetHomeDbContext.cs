using Application.Modules.Health;
using Application.Modules.Movies;
using Application.Modules.SmartHome;
using Application.Modules.Wardrobe;
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

        builder.Entity<Movie>(entity =>
        {
            entity.HasKey(e => e.MovieId);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Title);
            entity.HasIndex(e => e.Rating);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.UpdatedAt);
            entity.HasIndex(e => new { e.UserId, e.ImportedFromMovieId });

            entity.Property(e => e.ContentType)
                .HasConversion<string>();

            entity.Property(e => e.Genres)
                .HasColumnType("text[]");

            entity
                .HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<MovieShareSetting>(entity =>
        {
            entity.HasKey(e => e.UserId);

            entity
                .HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<WardrobeItem>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.Category });
            entity.HasIndex(e => e.CreatedAt);

            entity.Property(e => e.Name).HasMaxLength(120);
            entity.Property(e => e.Brand).HasMaxLength(80);
            entity.Property(e => e.Notes).HasMaxLength(1000);

            entity.Property(e => e.Category)
                .HasConversion<string>();

            entity.Property(e => e.ImageStatus)
                .HasConversion<string>()
                .HasDefaultValue(WardrobeImageStatus.NONE)
                .HasSentinel(WardrobeImageStatus.NONE);

            entity.Property(e => e.ImageVariant)
                .HasConversion<string>()
                .HasDefaultValue(WardrobeImageVariant.ORIGINAL)
                .HasSentinel(WardrobeImageVariant.ORIGINAL);

            entity.Property(e => e.Colors).HasColumnType("text[]");
            entity.Property(e => e.Seasons).HasColumnType("text[]");
            entity.Property(e => e.Styles).HasColumnType("text[]");

            entity
                .HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<WardrobeItemImage>(entity =>
        {
            entity.HasKey(e => e.ItemId);

            entity.Property(e => e.ProcessingStatus)
                .HasConversion<string>()
                .HasDefaultValue(WardrobeImageStatus.NONE)
                .HasSentinel(WardrobeImageStatus.NONE);

            entity.Property(e => e.Revision).HasDefaultValue(0);
            entity.HasIndex(e => e.ProcessingStatus);

            entity
                .HasOne(e => e.Item)
                .WithOne()
                .HasForeignKey<WardrobeItemImage>(e => e.ItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<WardrobeOutfit>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.CreatedAt);

            entity.Property(e => e.Name).HasMaxLength(80);
            entity.Property(e => e.Note).HasMaxLength(500);

            entity.Property(e => e.Occasion)
                .HasConversion<string>();

            entity.Property(e => e.ItemIds).HasColumnType("text[]");

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

        builder.Entity<SmartHomeScenario>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.UserId);

            entity
                .HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SmartHomeAutomation>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.UserId);

            entity
                .HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SmartHomeEvent>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.CreatedAt);

            entity
                .HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    public DbSet<MainWidget> MainWidgets { get; set; }
    public DbSet<HealthEntry> HealthEntries { get; set; }
    public DbSet<Movie> Movies { get; set; }
    public DbSet<MovieShareSetting> MovieShareSettings { get; set; }
    public DbSet<SmartHomeRoom> SmartHomeRooms { get; set; }
    public DbSet<SmartHomeWidget> SmartHomeWidgets { get; set; }
    public DbSet<SmartHomeScenario> SmartHomeScenarios { get; set; }
    public DbSet<SmartHomeAutomation> SmartHomeAutomations { get; set; }
    public DbSet<SmartHomeEvent> SmartHomeEvents { get; set; }
    public DbSet<WardrobeItem> WardrobeItems { get; set; }
    public DbSet<WardrobeItemImage> WardrobeItemImages { get; set; }
    public DbSet<WardrobeOutfit> WardrobeOutfits { get; set; }
}
