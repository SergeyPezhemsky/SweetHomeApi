using Application.Modules.Auth;
using Application.Modules.Books;
using Application.Modules.Coins;
using Application.Modules.Finance;
using Application.Modules.Health;
using Application.Modules.Movies;
using Application.Modules.SmartHome;
using Application.Modules.Wardrobe;
using Application.Modules.Widgets;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Persistance.Monitoring;

namespace Persistance;

public class SweetHomeDbContext(DbContextOptions<SweetHomeDbContext> options)
    : IdentityDbContext<ApplicationUser>(options), IDataProtectionKeyContext
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(user => user.IsAdmin).HasDefaultValue(false);
            entity.Property(user => user.ApprovalStatus)
                .HasConversion<string>()
                .HasDefaultValue(ApprovalStatus.Pending);
        });

        builder.Entity<DailySectionActivity>(entity =>
        {
            entity.HasKey(activity => new { activity.Day, activity.UserId, activity.Section });
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(activity => activity.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ApiFailure>(entity =>
        {
            entity.HasIndex(failure => new { failure.OccurredAt, failure.Id });
        });

        builder.Entity<Book>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(300).IsRequired();
            entity.Property(e => e.Author).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Genre).HasMaxLength(100);
            entity.Property(e => e.Notes).HasMaxLength(5000);
            entity.Property(e => e.Status).HasConversion<string>();
            entity.HasIndex(e => new { e.UserId, e.Status });
            entity.HasIndex(e => new { e.UserId, e.UpdatedAt });
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Coin>(entity =>
        {
            entity.Property(coin => coin.Name).HasMaxLength(96).IsRequired();
            entity.Property(coin => coin.Country).HasMaxLength(60);
            entity.Property(coin => coin.Value).HasMaxLength(50);
            entity.Property(coin => coin.Grade).HasMaxLength(5);
            entity.Property(coin => coin.Location).HasMaxLength(120);
            entity.Property(coin => coin.Notes).HasMaxLength(2000);
            entity.HasIndex(coin => new { coin.UserId, coin.CreatedAt });
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(coin => coin.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

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

        builder.Entity<FinanceReceipt>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => new { e.UserId, e.PurchasedOn });
            entity.HasIndex(e => e.Status);

            entity.Property(e => e.Source).HasConversion<string>();
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.FailureReason).HasConversion<string>();
            entity.Property(e => e.Category).HasConversion<string>();
            entity.Property(e => e.Total).HasPrecision(12, 2);
            entity.Property(e => e.Currency).HasMaxLength(3);
            entity.Property(e => e.Store).HasMaxLength(FinanceDefaults.MaxStoreLength);
            entity.Property(e => e.Note).HasMaxLength(FinanceDefaults.MaxNoteLength);
            entity.Property(e => e.RecognitionModel).HasMaxLength(FinanceDefaults.MaxModelLength);

            entity
                .HasMany(e => e.Items)
                .WithOne(e => e.Receipt)
                .HasForeignKey(e => e.ReceiptId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<FinanceReceiptItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ReceiptId);

            entity.Property(e => e.Name).HasMaxLength(FinanceDefaults.MaxItemNameLength);
            entity.Property(e => e.Quantity).HasPrecision(12, 3);
            entity.Property(e => e.Price).HasPrecision(12, 2);
            entity.Property(e => e.Amount).HasPrecision(12, 2);
            entity.Property(e => e.Category).HasConversion<string>();
            entity.Property(e => e.CategorySource).HasConversion<string>();
        });

        builder.Entity<FinanceReceiptImage>(entity =>
        {
            entity.HasKey(e => e.ReceiptId);

            entity
                .HasOne(e => e.Receipt)
                .WithOne()
                .HasForeignKey<FinanceReceiptImage>(e => e.ReceiptId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<FinanceCategoryRule>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.Key });
            entity.Property(e => e.Key).HasMaxLength(100);
            entity.Property(e => e.Category).HasConversion<string>();

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

    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }
    public DbSet<DailySectionActivity> DailySectionActivities { get; set; }
    public DbSet<ApiFailure> ApiFailures { get; set; }
    public DbSet<MainWidget> MainWidgets { get; set; }
    public DbSet<Book> Books { get; set; }
    public DbSet<Coin> Coins { get; set; }
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
    public DbSet<FinanceReceipt> FinanceReceipts { get; set; }
    public DbSet<FinanceReceiptItem> FinanceReceiptItems { get; set; }
    public DbSet<FinanceReceiptImage> FinanceReceiptImages { get; set; }
    public DbSet<FinanceCategoryRule> FinanceCategoryRules { get; set; }
}
