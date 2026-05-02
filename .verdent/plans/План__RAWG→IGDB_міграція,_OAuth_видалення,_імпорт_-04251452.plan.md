## План

### 1. Видалити RAWG залишки

Видалити файли, прибрати DI, перейменувати `Game.RawgId` → `Game.IgdbId` з міграцією.

### 2. Видалити OAuth (частково)

Прибрати Steam OpenID flow, OAuth токени з `UserShopProfile`, `OAuthSettings`. Залишити тільки direct link (Steam64 ID, GOG username).

### 3. Видалити Epic Games Store

Прибрати EGS з імпорту, констант, фронтенду. Зупинити створення EGS GameOffers.

### 4. Спростити ShopOAuthService → ShopLinkService

Перейменувати, прибрати мертвий OAuth код, залишити тільки link/unlink/status.

### 5. План синхронізації цін

Додати BackgroundService для періодичного прайс-синку + IsThereAnyDeal API.

### 6. Оновити документацію

ROADMAP.md, CONTEXT.md, IMPLEMENTATION\_SUMMARY.md.

***

## Файли

### Видалити повністю

| FILE                                               | reason                                           |
| -------------------------------------------------- | ------------------------------------------------ |
| `GameDB.Core/Interfaces/IRawgApiService.cs`        | RAWG інтерфейс + DTO (RawgGame, RawgGenre, etc.) |
| `GameDB.Core/Configuration/RawgSettings.cs`        | RAWG конфігурація                                |
| `GameDB.Infrastructure/Services/RawgApiService.cs` | RAWG сервіс                                      |

### Змінити

| FILE                                                  | change                                                                                                                                                                                                                                                                             | reason                                   |
| ----------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------- |
| `GameDB.Core/Models/Game.cs`                          | `RawgId` → `IgdbId`, оновити коментар                                                                                                                                                                                                                                              | Колонка зберігає IGDB ID, назва оманлива |
| `GameDB.Core/DTOs/GameImport.cs`                      | `RawgId` → `IgdbId`                                                                                                                                                                                                                                                                | Sync з моделлю Game                      |
| `GameDB.Infrastructure/Services/GameImportService.cs` | `RawgId` → `IgdbId`; видалити EGS блок з `BuildOffers` + `ExtractEgsId`                                                                                                                                                                                                            | RAWG→IGDB rename; EGS видалення          |
| `GameDB.Infrastructure/Services/GameService.cs`       | `RawgId` → `IgdbId` (5 місць); оновити коментарі                                                                                                                                                                                                                                   | RAWG→IGDB rename                         |
| `GameDB.Infrastructure/Services/IgdbApiService.cs`    | Видалити `EgsCategory`, `EgsUrl`, `IsEgsUrl` з маппінгу                                                                                                                                                                                                                            | EGS видалення                            |
| `GameDB.Core/Interfaces/IIgdbApiService.cs`           | Видалити `EgsUrl` з `IgdbGame` record                                                                                                                                                                                                                                              | EGS видалення                            |
| `GameDB.Core/Constants/ShopConstants.cs`              | Видалити `EpicGames = 3`, `GetStoreUrl`/`GetName` для EGS                                                                                                                                                                                                                          | EGS видалення                            |
| `GameDB.Infrastructure/Services/ShopOAuthService.cs`  | Перейменувати → `ShopLinkService`; видалити `GetSteamAuthUrl`, `HandleSteamCallbackAsync`, `_pendingStates`, `BuildSteamRedirectUri`, `GenerateState`; видалити EGS з `ShopSlugToId`/`ShopIdToSlug`; прибрати `AccessToken`/`RefreshToken`/`TokenExpiresAt` з `UpsertProfileAsync` | OAuth→direct link                        |
| `GameDB.Core/Configuration/OAuthSettings.cs`          | Видалити файл (або залишити тільки `FrontendBaseUrl`)                                                                                                                                                                                                                              | OAuth не потрібен                        |
| `GameDB.Core/Models/UserShopProfile.cs`               | Видалити `AccessToken`, `RefreshToken`, `TokenExpiresAt`                                                                                                                                                                                                                           | OAuth токени не використовуються         |
| `GameDB.Api/Controllers/OAuthController.cs`           | Перейменувати → `ShopLinkController`; видалити `SteamAuthorize`, `SteamCallback`; видалити EGS з `LinkByExternalId`                                                                                                                                                                | OAuth→direct link                        |
| `GameDB.Api/Controllers/WishlistController.cs`        | Оновити inject `ShopLinkService` замість `ShopOAuthService`                                                                                                                                                                                                                        | Rename                                   |
| `GameDB.Api/Controllers/LibraryController.cs`         | Оновити inject `ShopLinkService` замість `ShopOAuthService`; видалити EGS з error message                                                                                                                                                                                          | Rename + EGS                             |
| `GameDB.Api/Controllers/ProfileController.cs`         | Оновити inject `ShopLinkService`                                                                                                                                                                                                                                                   | Rename                                   |
| `GameDB.Infrastructure/Services/ProfileService.cs`    | Оновити inject `ShopLinkService`                                                                                                                                                                                                                                                   | Rename                                   |
| `GameDB.Infrastructure/Services/WishlistService.cs`   | Оновити inject `ShopLinkService`; видалити EGS з `ImportAsync` switch                                                                                                                                                                                                              | Rename + EGS                             |
| `GameDB.Infrastructure/Services/LibraryService.cs`    | Оновити inject `ShopLinkService`; видалити EGS з `ImportAsync` switch                                                                                                                                                                                                              | Rename + EGS                             |
| `GameDB.Api/Program.cs`                               | Видалити `RawgSettings` DI + `RawgApiService` HttpClient + `IRawgApiService`; видалити `OAuthSettings` DI; оновити `ShopOAuthService` → `ShopLinkService`                                                                                                                          | RAWG + OAuth cleanup                     |
| `GameDB.Api/appsettings.json`                         | Видалити `"Rawg"` секцію; видалити `"OAuth"` секцію (залишити тільки `FrontendBaseUrl` якщо потрібно)                                                                                                                                                                              | RAWG + OAuth cleanup                     |
| `GameDB.Infrastructure/AppDbContext.cs`               | Оновити seed: видалити EGS GameShop (ShopId=3)                                                                                                                                                                                                                                     | EGS видалення                            |

### Фронтенд

| FILE                                         | change                                                                                                                         | reason             |
| -------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------ | ------------------ |
| `gamedb-client/src/pages/WishlistPage.jsx`   | Видалити `egs` з `SHOP_META`; видалити `useOAuth` логіку; змінити Steam на direct input (як GOG)                               | OAuth→direct + EGS |
| `gamedb-client/src/pages/ProfilePage.jsx`    | Видалити `egs` з `SHOPS`; змінити Steam на direct input                                                                        | OAuth→direct + EGS |
| `gamedb-client/src/pages/admin/SyncPage.jsx` | Замінити "RAWG" на "IGDB" в описі                                                                                              | RAWG→IGDB          |
| `gamedb-client/src/pages/CatalogPage.jsx`    | Оновити коментар "RAWG" → "IGDB"                                                                                               | RAWG→IGDB          |
| `gamedb-client/src/api/index.js`             | Перейменувати `getOAuthAuthorizeUrl` → видалити; оновити `linkShopByExternalId`, `unlinkShop` шляхи з `/oauth/` → `/shoplink/` | OAuth→direct       |

### Міграція

| FILE          | change                                                                                                                                                          | reason              |
| ------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------- |
| Нова міграція | `Game.RawgId` → `Game.IgdbId` (RenameColumn); видалити `UserShopProfile.AccessToken`, `RefreshToken`, `TokenExpiresAt`; видалити EGS GameOffers + GameShop seed | DB schema оновлення |

***

## Видалити

* **RAWG**: `IRawgApiService`, `RawgApiService`, `RawgSettings`, всі RAWG DTOs, `appsettings.Rawg` секцію, DI реєстрацію

* **EGS**: `ShopConstants.EpicGames`, EGS URL extraction з IGDB, EGS GameOffer seed, EGS з фронтенду, EGS з import switches

* **OAuth**: Steam OpenID flow, `_pendingStates`, `OAuthSettings` (частково), `UserShopProfile` OAuth поля (`AccessToken`, `RefreshToken`, `TokenExpiresAt`)

## Додати

* **Перейменування**: `ShopOAuthService` → `ShopLinkService`, `OAuthController` → `ShopLinkController`, `Game.RawgId` → `Game.IgdbId`

* **Steam direct input**: Замість OpenID redirect → форма введення Steam64 ID (як для GOG username)

* **PriceSync BackgroundService**: Автоматичний періодичний прайс-синк (кожні N годин) замість ручного запуску через SyncController

* **IsThereAnyDeal API**: План інтеграції для крос-стор цін (cheapshark.io як безкоштовна альтернатива)

## План синхронізації цін

```mermaid
graph TD
    A[PriceSyncWorker<br/>BackgroundService] -->|кожні 6 годин| B[PriceSyncService]
    B -->|batch| C[Steam Store API<br/>appdetails]
    B -->|per-game| D[GOG API<br/>products/{id}]
    B -->|future| E[CheapShark API<br/>deals]
    C --> F[Update GameOffer<br/>+ PriceHistory via trigger]
    D --> F
    E --> F
```

**Етапи:**

1. Створити `PriceSyncWorker : BackgroundService` з конфігурованим інтервалом (6 годин)
2. Worker викликає `PriceSyncService.SyncSteamPricesAsync()` + `SyncGogPricesAsync()`
3. Додати `appsettings.PriceSync.IntervalHours` конфігурацію
4. Зареєструвати Worker в `Program.cs`
5. **Future**: додати CheapShark API (`https://www.cheapshark.com/api/1.0/deals`) для цінової історії та deal scores

## Ризики

* **Міграція** **`RawgId`** **→** **`IgdbId`**: PostgreSQL `ALTER TABLE "Game" RENAME COLUMN "RawgId" TO "IgdbId"` — безпечна операція, але може зламати існуючі views/функції що посилаються на "RawgId"

* **Видалення EGS GameShop seed (ShopId=3)**: Якщо в БД є GameOffers з ShopId=3, FK constraint не дозволить видалити GameShop. Потрібно спочатку видалити EGS GameOffers або залишити GameShop запис

* **Steam OpenID видалення**: Користувачі що прив'язали Steam через OpenID можуть мати Steam64 ID вже збережений — це збережеться, просто нові прив'язки будуть через direct input

* **Frontend API шляхи**: Перейменування `/oauth/` → `/shoplink/` зламає існуючі bookmarks — потрібен backward compat або redirect

## ROADMAP.md — що додати/оновити

* Оновити КРОК 4: замінити "import-steam via OAuth" на "import via Steam64 ID"

* Оновити КРОК 5: додати автоматичний прайс-синк (PriceSyncWorker)

* Оновити КРОК 6: прибрати OAuth з прив'язки магазинів

* Додати КРОК 15: RAWG→IGDB міграція + cleanup

* Видалити згадки EGS з усіх кроків

## CONTEXT.md — що змінити

* Схема БД: `Game.RawgId` → `Game.IgdbId`; видалити `UserShopProfile.AccessToken/RefreshToken/TokenExpiresAt`; прибрати EGS GameShop seed

* Стек: "RAWG API" → "IGDB API (Twitch OAuth)"

* Зовнішні API: додати CheapShark, видалити RAWG

* Структура проекту: `ShopOAuthService` → `ShopLinkService`, `OAuthController` → `ShopLinkController`; видалити `RawgApiService`, `IRawgApiService`, `RawgSettings`

* Ключові ендпоінти: `/api/oauth/*` → `/api/shoplink/*`; видалити Steam authorize/callback

* Додати PriceSyncWorker в background services
