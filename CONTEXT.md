# GameDB — Контекст проекту для AI

## Що це
Курсова робота з БД. Інформаційна система для каталогізації та моніторингу цін відеоігор.
Веб-платформа де користувачі переглядають каталог ігор, стежать за цінами в різних магазинах, ведуть вішліст, отримують цінові алерти та керують бібліотекою куплених ігор.

## Стек
- **Backend**: C# / ASP.NET Core 8 / REST API
- **ORM**: Entity Framework Core (code-first, міграції)
- **БД**: PostgreSQL (в Docker)
- **Auth**: JWT токени
- **Frontend**: React + JavaScript + Ant Design + Recharts
- **Синхронізація цін**: окремий сервіс, Steam Store API + GOG API (публічні, без ключа)

## Ролі користувачів
- `guest` (RoleId=1) — незареєстрований користувач, може тільки переглядати каталог (реалізується через `[AllowAnonymous]` на бекенді)
- `user` (RoleId=2) — зареєстрований користувач, керує вішлістом, алертами, бібліотекою, прив'язує Steam акаунт
- `admin` (RoleId=3) — повний доступ, додає/редагує ігри, запускає синхронізацію, управляє магазинами

## Схема БД (14 таблиць, PostgreSQL)

```sql
-- EF Core використовує PascalCase для таблиць і колонок

CREATE TABLE "Role" (
    "RoleId" SERIAL PRIMARY KEY,
    "RoleName" TEXT NOT NULL UNIQUE  -- 'admin' | 'moderator' | 'user'
);

CREATE TABLE "User" (
    "UserId" SERIAL PRIMARY KEY,
    "Username" TEXT NOT NULL,
    "Email" TEXT NOT NULL,
    "PasswordHash" TEXT NOT NULL,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "LastLogin" TIMESTAMPTZ,
    "RoleId" INT NOT NULL REFERENCES "Role"("RoleId") ON DELETE CASCADE
);
CREATE UNIQUE INDEX "IX_User_Email" ON "User"("Email");
CREATE UNIQUE INDEX "IX_User_Username" ON "User"("Username");

CREATE TABLE "GameShop" (
    "ShopId" SERIAL PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "BaseUrl" TEXT,
    "ApiBaseUrl" TEXT
);
CREATE UNIQUE INDEX "IX_GameShop_Name" ON "GameShop"("Name");

CREATE TABLE "UserShopProfile" (
    "ProfileId" SERIAL PRIMARY KEY,
    "UserId" INT NOT NULL REFERENCES "User"("UserId") ON DELETE CASCADE,
    "ShopId" INT NOT NULL REFERENCES "GameShop"("ShopId") ON DELETE CASCADE,
    "ExternalUid" TEXT NOT NULL,
    "LinkedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT "IX_UserShopProfile_UserId_ShopId" UNIQUE ("UserId", "ShopId")
);

CREATE TABLE "Developer" (
    "DeveloperId" SERIAL PRIMARY KEY,
    "Name" TEXT NOT NULL
);

CREATE TABLE "Publisher" (
    "PublisherId" SERIAL PRIMARY KEY,
    "Name" TEXT NOT NULL
);

CREATE TABLE "Genre" (
    "GenreId" SERIAL PRIMARY KEY,
    "Name" TEXT NOT NULL
);

CREATE TABLE "Game" (
    "GameId" SERIAL PRIMARY KEY,
    "Title" TEXT NOT NULL,
    "Description" TEXT,
    "ReleaseDate" DATE,
    "DeveloperId" INT REFERENCES "Developer"("DeveloperId"),
    "PublisherId" INT REFERENCES "Publisher"("PublisherId"),
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX "IX_Game_Title" ON "Game"("Title");

CREATE TABLE "GameGenre" (
    "GameId" INT NOT NULL REFERENCES "Game"("GameId") ON DELETE CASCADE,
    "GenreId" INT NOT NULL REFERENCES "Genre"("GenreId") ON DELETE CASCADE,
    PRIMARY KEY ("GameId", "GenreId")
);

CREATE TABLE "GameOffer" (
    "GameOfferId" SERIAL PRIMARY KEY,
    "GameId" INT NOT NULL REFERENCES "Game"("GameId") ON DELETE CASCADE,
    "ShopId" INT NOT NULL REFERENCES "GameShop"("ShopId") ON DELETE CASCADE,
    "ExternalId" TEXT,
    "DownloadUrl" TEXT,
    "CurrentPrice" NUMERIC NOT NULL DEFAULT 0 CHECK ("CurrentPrice" >= 0),
    "CurrentDiscount" SMALLINT NOT NULL DEFAULT 0 CHECK ("CurrentDiscount" BETWEEN 0 AND 100),
    "Currency" TEXT NOT NULL DEFAULT 'USD',
    "PriceSyncedAt" TIMESTAMPTZ,
    CONSTRAINT "IX_GameOffer_GameId_ShopId" UNIQUE ("GameId", "ShopId"),
    CONSTRAINT "IX_GameOffer_ShopId_ExternalId" UNIQUE ("ShopId", "ExternalId")
);
CREATE INDEX ix_game_offer_synced_at ON "GameOffer"("PriceSyncedAt");

CREATE TABLE "PriceHistory" (
    "PriceHistoryId" SERIAL PRIMARY KEY,
    "GameOfferId" INT NOT NULL REFERENCES "GameOffer"("GameOfferId") ON DELETE CASCADE,
    "RecordedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "Price" NUMERIC NOT NULL CHECK ("Price" >= 0),
    "DiscountPercent" SMALLINT NOT NULL DEFAULT 0,
    "Currency" TEXT NOT NULL DEFAULT 'USD'
);
CREATE INDEX "IX_PriceHistory_GameOfferId_RecordedAt" ON "PriceHistory"("GameOfferId", "RecordedAt");

CREATE TABLE "Wishlist" (
    "UserId" INT NOT NULL REFERENCES "User"("UserId") ON DELETE CASCADE,
    "GameId" INT NOT NULL REFERENCES "Game"("GameId") ON DELETE CASCADE,
    "AddedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "SourceShopId" INT REFERENCES "GameShop"("ShopId") ON DELETE SET NULL,
    PRIMARY KEY ("UserId", "GameId")
);
-- SourceShopId: NULL = додано вручну, 1 = Steam import, 2 = GOG import
CREATE INDEX ix_wishlist_source_shop ON "Wishlist"("SourceShopId") WHERE "SourceShopId" IS NOT NULL;

CREATE TABLE "Alert" (
    "AlertId" SERIAL PRIMARY KEY,
    "UserId" INT NOT NULL REFERENCES "User"("UserId") ON DELETE CASCADE,
    "GameId" INT NOT NULL REFERENCES "Game"("GameId") ON DELETE CASCADE,
    "TargetPrice" NUMERIC CHECK ("TargetPrice" IS NULL OR "TargetPrice" > 0),
    "TargetDiscount" SMALLINT CHECK ("TargetDiscount" IS NULL OR ("TargetDiscount" BETWEEN 1 AND 100)),
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "TriggeredAt" TIMESTAMPTZ,
    "LastNotifiedAt" TIMESTAMPTZ,
    CONSTRAINT "IX_Alert_UserId_GameId" UNIQUE ("UserId", "GameId"),
    CONSTRAINT chk_alert_condition CHECK ("TargetPrice" IS NOT NULL OR "TargetDiscount" IS NOT NULL)
);
CREATE INDEX ix_alert_user_active ON "Alert"("UserId", "IsActive");
CREATE INDEX ix_alert_game_active ON "Alert"("GameId") WHERE "IsActive" = TRUE AND "TriggeredAt" IS NULL;

CREATE TABLE "UserLibrary" (
    "UserId" INT NOT NULL REFERENCES "User"("UserId") ON DELETE CASCADE,
    "GameId" INT NOT NULL REFERENCES "Game"("GameId") ON DELETE CASCADE,
    "ShopId" INT NOT NULL REFERENCES "GameShop"("ShopId") ON DELETE CASCADE,
    "AddedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY ("UserId", "GameId", "ShopId")
);
CREATE INDEX ix_user_library_user_added ON "UserLibrary"("UserId", "AddedAt" DESC);
```

## Тригери (4 шт.)

| Тригер | Таблиця | Тип | Опис |
|---|---|---|---|
| `trg_auto_price_history` | GameOffer | BEFORE UPDATE | При зміні ціни/знижки записує старі значення в PriceHistory |
| `trg_check_price_alerts` | GameOffer | AFTER UPDATE | Перевіряє активні алерти і деактивує ті де умови виконані |
| `trg_handle_library_insert` | UserLibrary | AFTER INSERT | Видаляє гру з Wishlist + деактивує Alert при покупці |
| `trg_update_game_timestamp` | Game | BEFORE UPDATE | Оновлює UpdatedAt при зміні гри |

## Збережені функції (2 шт.)

| Функція | Параметри | Повертає |
|---|---|---|
| `fn_get_user_alerts(p_user_id)` | INT | TABLE: алерти з поточною ціною, знижкою, відсотком до цілі |
| `fn_get_deal_score(p_game_offer_id)` | INT | TABLE: deal score 0-100, historical low/avg, is_historical_low |

## Збережені процедури (2 шт.)

| Процедура | Параметри | Опис |
|---|---|---|
| `pr_add_to_library(p_user_id, p_game_id, p_shop_id)` | INT, INT, INT | Додає гру в бібліотеку (тригер видалить з wishlist) |
| `pr_set_alert(p_user_id, p_game_id, p_target_price, p_target_discount)` | INT, INT, NUMERIC, SMALLINT | Upsert алерту з перевіркою бібліотеки |

## Views (2 шт.)

| View | Опис |
|---|---|
| `vw_game_catalog` | Каталог ігор: min price, max discount, genres (CTE для правильних агрегатів) |
| `vw_user_library` | Бібліотека юзера: game title, shop name, download url, price |

## Структура проекту (бекенд)
```
GameDB.sln
├── GameDB.Api/              ← ASP.NET Core, контролери, Program.cs
│   ├── Controllers/
│   │   ├── AuthController.cs
│   │   ├── GamesController.cs
│   │   ├── WishlistController.cs
│   │   ├── SyncController.cs
│   │   ├── ProfileController.cs
│   │   ├── AlertController.cs
│   │   └── LibraryController.cs
│   ├── Program.cs
│   └── appsettings.json
├── GameDB.Core/             ← моделі, інтерфейси сервісів
│   ├── Models/              ← EF Core entity classes (14 шт.)
│   │   ├── Role.cs
│   │   ├── User.cs
│   │   ├── GameShop.cs
│   │   ├── UserShopProfile.cs
│   │   ├── Developer.cs
│   │   ├── Publisher.cs
│   │   ├── Genre.cs
│   │   ├── Game.cs
│   │   ├── GameGenre.cs
│   │   ├── GameOffer.cs
│   │   ├── PriceHistory.cs
│   │   ├── Wishlist.cs
│   │   ├── Alert.cs
│   │   └── UserLibrary.cs
│   └── Interfaces/
└── GameDB.Infrastructure/   ← AppDbContext, сервіси, міграції
    ├── AppDbContext.cs
    ├── Services/
    │   ├── AuthService.cs
    │   ├── GameService.cs
    │   ├── WishlistService.cs
    │   ├── PriceSyncService.cs
    │   ├── AlertService.cs
    │   └── LibraryService.cs
    └── Migrations/
```

## Структура проекту (фронтенд)
```
gamedb-client/
├── src/
│   ├── api/                 ← функції fetch до бекенду
│   ├── components/          ← перевикористовувані компоненти
│   ├── pages/
│   │   ├── CatalogPage.jsx
│   │   ├── GameDetailPage.jsx
│   │   ├── WishlistPage.jsx
│   │   ├── ProfilePage.jsx
│   │   ├── AlertsPage.jsx
│   │   ├── LibraryPage.jsx
│   │   └── admin/
│   │       ├── GamesAdminPage.jsx
│   │       └── SyncPage.jsx
│   ├── context/AuthContext.jsx
│   └── App.jsx
```

## Ключові API ендпоінти
```
POST /api/auth/register
POST /api/auth/login         → повертає JWT

GET  /api/games              → каталог (query: search, genre, shop, page)
GET  /api/games/:id          → деталі гри + offers + price history + deal score
POST /api/games              [admin]
PUT  /api/games/:id          [admin]
DELETE /api/games/:id        [admin]
GET  /api/games/:id/deal-score → deal score для гри

GET  /api/wishlist           → вішліст поточного юзера
POST /api/wishlist/:gameId
DELETE /api/wishlist/:gameId
POST /api/wishlist/import-steam  → імпорт зі Steam по external_uid

GET  /api/alerts             → список алертів юзера
POST /api/alerts             → створити алерт (TargetPrice та/або TargetDiscount)
PUT  /api/alerts/:id         → оновити алерт
DELETE /api/alerts/:id       → видалити алерт

GET  /api/library            → бібліотека юзера
POST /api/library            → додати гру до бібліотеки

POST /api/sync/steam         [admin] → запуск синхронізації
GET  /api/sync/log           [admin] → журнал

GET  /api/profile
PUT  /api/profile/shop-profile   → прив'язати акаунт магазину
```

## Зовнішні API
- **Steam**: `GET https://store.steampowered.com/api/appdetails?appids={appid}&cc=us&filters=price_overview`
  - Повертає ціну в ЦЕНТАХ (ділити на 100)
  - Публічний, без ключа, rate-limit ~200 req/5хв
- **GOG**: `GET https://api.gog.com/products/{product_id}?expand=prices&countryCode=US`
  - Публічний, без ключа

## Що вже є
- Повна схема БД (14 таблиць) з міграціями EF Core
- Тригери, функції, процедури, view-и в міграції
- Seed дані (3 ролі, 2 магазини)
- Auth (реєстрація, логін, JWT) — повністю реалізовано
- Заглушки контролерів (GamesController)

## Що треба зробити
Дивись ROADMAP.md
