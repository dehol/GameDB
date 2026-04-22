using GameDB.Core.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace GameDB.Infrastructure;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<GameShop> GameShops => Set<GameShop>();
    public DbSet<UserShopProfile> UserShopProfiles => Set<UserShopProfile>();
    public DbSet<Developer> Developers => Set<Developer>();
    public DbSet<Publisher> Publishers => Set<Publisher>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<GameGenre> GameGenres => Set<GameGenre>();
    public DbSet<GameOffer> GameOffers => Set<GameOffer>();
    public DbSet<PriceHistory> PriceHistories => Set<PriceHistory>();
    public DbSet<Wishlist> Wishlists => Set<Wishlist>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<UserLibrary> UserLibraries => Set<UserLibrary>();
    public DbSet<GuestSession> GuestSessions => Set<GuestSession>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ImportJob> ImportJobs => Set<ImportJob>();
    public DbSet<RawGameData> RawGameData => Set<RawGameData>();
    public DbSet<StagingGame> StagingGames => Set<StagingGame>();
    public DbSet<WishlistImport> WishlistImports => Set<WishlistImport>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // Table names
        mb.Entity<Role>().ToTable("Role");
        mb.Entity<User>().ToTable("User");
        mb.Entity<GameShop>().ToTable("GameShop");
        mb.Entity<UserShopProfile>().ToTable("UserShopProfile");
        mb.Entity<Developer>().ToTable("Developer");
        mb.Entity<Publisher>().ToTable("Publisher");
        mb.Entity<Genre>().ToTable("Genre");
        mb.Entity<Game>().ToTable("Game");
        mb.Entity<GameGenre>().ToTable("GameGenre");
        mb.Entity<GameOffer>().ToTable("GameOffer");
        mb.Entity<PriceHistory>().ToTable("PriceHistory");
        mb.Entity<Wishlist>().ToTable("Wishlist");
        mb.Entity<Alert>().ToTable("Alert");
        mb.Entity<UserLibrary>().ToTable("UserLibrary");
        mb.Entity<GuestSession>().ToTable("GuestSession");
        mb.Entity<Notification>().ToTable("Notification");
        mb.Entity<ImportJob>().ToTable("ImportJob");
        mb.Entity<RawGameData>().ToTable("RawGameData");
        mb.Entity<StagingGame>().ToTable("StagingGame");
        mb.Entity<WishlistImport>().ToTable("WishlistImport");

        // Composite PKs
        mb.Entity<GameGenre>().HasKey(gg => new { gg.GameId, gg.GenreId });
        mb.Entity<Wishlist>().HasKey(w => new { w.UserId, w.GameId });
        mb.Entity<UserLibrary>().HasKey(ul => new { ul.UserId, ul.GameId, ul.ShopId });

        // Unique constraints / indexes
        mb.Entity<UserShopProfile>()
            .HasIndex(p => new { p.UserId, p.ShopId }).IsUnique();
        mb.Entity<GameOffer>()
            .HasIndex(o => new { o.GameId, o.ShopId }).IsUnique();
        mb.Entity<GameOffer>()
            .HasIndex(o => new { o.ShopId, o.ExternalId }).IsUnique();
        mb.Entity<Alert>()
            .HasIndex(a => new { a.UserId, a.GameId }).IsUnique();
        mb.Entity<User>()
            .HasIndex(u => u.Email).IsUnique();
        mb.Entity<User>()
            .HasIndex(u => u.Username).IsUnique();
        mb.Entity<GameShop>()
            .HasIndex(s => s.Name).IsUnique();
        mb.Entity<Game>()
            .HasIndex(g => g.Title);
        mb.Entity<Game>()
            .HasIndex(g => g.NormalizedTitle);
        mb.Entity<User>()
            .HasIndex(u => u.IsGuest);
        mb.Entity<Wishlist>()
            .HasIndex(w => new { w.UserId, w.AddedAt });
        mb.Entity<Alert>()
            .HasIndex(a => new { a.UserId, a.IsActive, a.TriggeredAt });
        mb.Entity<GuestSession>()
            .HasIndex(s => new { s.UserId, s.LastSeen });
        mb.Entity<GuestSession>()
            .HasIndex(s => s.DeviceHash)
            .HasDatabaseName("IX_GuestSession_DeviceHash");
        mb.Entity<Notification>()
            .HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt });

        // PriceHistory index + explicit FK
        mb.Entity<PriceHistory>()
            .HasIndex(ph => new { ph.GameOfferId, ph.RecordedAt });
        mb.Entity<PriceHistory>()
            .HasOne(ph => ph.Offer)
            .WithMany(go => go.PriceHistories)
            .HasForeignKey(ph => ph.GameOfferId)
            .OnDelete(DeleteBehavior.Cascade);

        // Wishlist FK to SourceShop
        mb.Entity<Wishlist>()
            .HasOne(w => w.SourceShop)
            .WithMany()
            .HasForeignKey(w => w.SourceShopId)
            .OnDelete(DeleteBehavior.SetNull);

        mb.Entity<GuestSession>()
            .HasOne(gs => gs.User)
            .WithMany(u => u.GuestSessions)
            .HasForeignKey(gs => gs.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<Notification>()
            .HasOne(n => n.User)
            .WithMany(u => u.Notifications)
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // CHECK constraints — GameOffer
        mb.Entity<GameOffer>()
            .ToTable(t => t.HasCheckConstraint("chk_price", "\"CurrentPrice\" >= 0"));
        mb.Entity<GameOffer>()
            .ToTable(t => t.HasCheckConstraint("chk_discount", "\"CurrentDiscount\" BETWEEN 0 AND 100"));

        // CHECK constraints — Alert
        mb.Entity<Alert>()
            .ToTable(t => t.HasCheckConstraint("chk_alert_condition",
                "\"TargetPrice\" IS NOT NULL OR \"TargetDiscount\" IS NOT NULL"));
        mb.Entity<Alert>()
            .ToTable(t => t.HasCheckConstraint("chk_alert_price",
                "\"TargetPrice\" IS NULL OR \"TargetPrice\" > 0"));
        mb.Entity<Alert>()
            .ToTable(t => t.HasCheckConstraint("chk_alert_discount",
                "\"TargetDiscount\" IS NULL OR (\"TargetDiscount\" BETWEEN 1 AND 100)"));

        mb.Entity<User>()
            .ToTable(t => t.HasCheckConstraint("chk_user_guest_identity",
                "\"IsGuest\" = FALSE OR (\"Email\" IS NULL AND \"PasswordHash\" IS NULL)"));

        // Staging tables indexes
        mb.Entity<RawGameData>()
            .HasIndex(r => new { r.Source, r.ExternalId })
            .IsUnique();
        mb.Entity<RawGameData>()
            .HasIndex(r => r.Processed);
        mb.Entity<RawGameData>()
            .HasIndex(r => r.FetchedAt);

        mb.Entity<StagingGame>()
            .HasIndex(s => s.NormalizedTitle);
        mb.Entity<StagingGame>()
            .HasIndex(s => s.IsProcessed);
        mb.Entity<StagingGame>()
            .HasIndex(s => new { s.IgdbId, s.SteamAppId, s.GogId, s.EgsId });

        // WishlistImport indexes and relations
        mb.Entity<WishlistImport>()
            .HasIndex(wi => wi.UserId);
        mb.Entity<WishlistImport>()
            .HasIndex(wi => wi.ShopId);
        mb.Entity<WishlistImport>()
            .HasIndex(wi => wi.Status);
        mb.Entity<WishlistImport>()
            .HasOne(wi => wi.User)
            .WithMany()
            .HasForeignKey(wi => wi.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<WishlistImport>()
            .HasOne(wi => wi.Shop)
            .WithMany()
            .HasForeignKey(wi => wi.ShopId)
            .OnDelete(DeleteBehavior.Restrict);

        // Seed data
        mb.Entity<Role>().HasData(
            new Role { RoleId = 1, RoleName = "guest" },
            new Role { RoleId = 2, RoleName = "user" },
            new Role { RoleId = 3, RoleName = "admin" }
        );

        mb.Entity<GameShop>().HasData(
            new GameShop { ShopId = 1, Name = "Steam", BaseUrl = "https://store.steampowered.com", ApiBaseUrl = "https://store.steampowered.com/api" },
            new GameShop { ShopId = 2, Name = "GOG", BaseUrl = "https://www.gog.com", ApiBaseUrl = "https://api.gog.com" },
            new GameShop { ShopId = 3, Name = "Epic Games Store", BaseUrl = "https://store.epicgames.com", ApiBaseUrl = "https://store.epicgames.com/graphql" }
        );

        // Seed: Developers
        mb.Entity<Developer>().HasData(
            new Developer { DeveloperId = 1, Name = "Larian Studios" },
            new Developer { DeveloperId = 2, Name = "CD Projekt Red" },
            new Developer { DeveloperId = 3, Name = "FromSoftware" },
            new Developer { DeveloperId = 4, Name = "Supergiant Games" },
            new Developer { DeveloperId = 5, Name = "Valve" }
        );

        // Seed: Publishers
        mb.Entity<Publisher>().HasData(
            new Publisher { PublisherId = 1, Name = "Larian Studios" },
            new Publisher { PublisherId = 2, Name = "CD Projekt" },
            new Publisher { PublisherId = 3, Name = "Bandai Namco" },
            new Publisher { PublisherId = 4, Name = "Supergiant Games" },
            new Publisher { PublisherId = 5, Name = "Valve" }
        );

        // Seed: Genres
        mb.Entity<Genre>().HasData(
            new Genre { GenreId = 1, Name = "RPG" },
            new Genre { GenreId = 2, Name = "Action" },
            new Genre { GenreId = 3, Name = "Adventure" },
            new Genre { GenreId = 4, Name = "Roguelike" },
            new Genre { GenreId = 5, Name = "Open World" },
            new Genre { GenreId = 6, Name = "FPS" },
            new Genre { GenreId = 7, Name = "Indie" },
            new Genre { GenreId = 8, Name = "Strategy" }
        );

        // Seed: Games
        mb.Entity<Game>().HasData(
            new Game { GameId = 1, Title = "Baldur's Gate 3", Description = "An epic RPG set in the D&D universe", ReleaseDate = new DateOnly(2023, 8, 3), DeveloperId = 1, PublisherId = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Game { GameId = 2, Title = "Cyberpunk 2077", Description = "Open-world action RPG in a dystopian future", ReleaseDate = new DateOnly(2020, 12, 10), DeveloperId = 2, PublisherId = 2, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Game { GameId = 3, Title = "Elden Ring", Description = "Open-world action RPG by FromSoftware and George R.R. Martin", ReleaseDate = new DateOnly(2022, 2, 25), DeveloperId = 3, PublisherId = 3, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Game { GameId = 4, Title = "Hades II", Description = "Roguelike action sequel from Supergiant Games", ReleaseDate = new DateOnly(2024, 5, 6), DeveloperId = 4, PublisherId = 4, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Game { GameId = 5, Title = "Counter-Strike 2", Description = "Competitive tactical FPS", ReleaseDate = new DateOnly(2023, 9, 27), DeveloperId = 5, PublisherId = 5, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Game { GameId = 6, Title = "The Witcher 3: Wild Hunt", Description = "Story-driven open world RPG", ReleaseDate = new DateOnly(2015, 5, 19), DeveloperId = 2, PublisherId = 2, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        );

        // Seed: GameGenre (many-to-many)
        mb.Entity<GameGenre>().HasData(
            new { GameId = 1, GenreId = 1 }, // BG3 - RPG
            new { GameId = 1, GenreId = 3 }, // BG3 - Adventure
            new { GameId = 2, GenreId = 1 }, // Cyberpunk - RPG
            new { GameId = 2, GenreId = 2 }, // Cyberpunk - Action
            new { GameId = 2, GenreId = 5 }, // Cyberpunk - Open World
            new { GameId = 3, GenreId = 1 }, // Elden Ring - RPG
            new { GameId = 3, GenreId = 2 }, // Elden Ring - Action
            new { GameId = 3, GenreId = 5 }, // Elden Ring - Open World
            new { GameId = 4, GenreId = 2 }, // Hades II - Action
            new { GameId = 4, GenreId = 4 }, // Hades II - Roguelike
            new { GameId = 4, GenreId = 7 }, // Hades II - Indie
            new { GameId = 5, GenreId = 2 }, // CS2 - Action
            new { GameId = 5, GenreId = 6 }, // CS2 - FPS
            new { GameId = 6, GenreId = 1 }, // Witcher 3 - RPG
            new { GameId = 6, GenreId = 3 }, // Witcher 3 - Adventure
            new { GameId = 6, GenreId = 5 }  // Witcher 3 - Open World
        );

        // Seed: GameOffers
        mb.Entity<GameOffer>().HasData(
            new GameOffer { GameOfferId = 1, GameId = 1, ShopId = 1, ExternalId = "1086940", CurrentPrice = 59.99m, CurrentDiscount = 0, Currency = "USD" },
            new GameOffer { GameOfferId = 2, GameId = 1, ShopId = 2, ExternalId = "1086940", CurrentPrice = 59.99m, CurrentDiscount = 0, Currency = "USD" },
            new GameOffer { GameOfferId = 3, GameId = 2, ShopId = 1, ExternalId = "1091500", CurrentPrice = 29.99m, CurrentDiscount = 50, Currency = "USD" },
            new GameOffer { GameOfferId = 4, GameId = 2, ShopId = 2, ExternalId = "1423049", CurrentPrice = 29.99m, CurrentDiscount = 50, Currency = "USD" },
            new GameOffer { GameOfferId = 5, GameId = 3, ShopId = 1, ExternalId = "1245620", CurrentPrice = 59.99m, CurrentDiscount = 0, Currency = "USD" },
            new GameOffer { GameOfferId = 6, GameId = 4, ShopId = 1, ExternalId = "1145350", CurrentPrice = 29.99m, CurrentDiscount = 0, Currency = "USD" },
            new GameOffer { GameOfferId = 7, GameId = 5, ShopId = 1, ExternalId = "730", CurrentPrice = 0.00m, CurrentDiscount = 0, Currency = "USD" },
            new GameOffer { GameOfferId = 8, GameId = 6, ShopId = 1, ExternalId = "292030", CurrentPrice = 9.99m, CurrentDiscount = 75, Currency = "USD" },
            new GameOffer { GameOfferId = 9, GameId = 6, ShopId = 2, ExternalId = "1495134320", CurrentPrice = 9.99m, CurrentDiscount = 75, Currency = "USD" }
        );
    }
}
