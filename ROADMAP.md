# GameDB — Roadmap розробки

Кожен крок — окреме завдання яке можна дати AI.
Для кожного кроку є окремий артефакт з деталями.

---

## КРОК 1 — Docker + БД + EF Core setup
**Артефакт**: `STEP_01_setup.md`
- `docker-compose.yml` з PostgreSQL
- `.sln` + три проекти (`Api`, `Core`, `Infrastructure`)
- NuGet пакети
- `AppDbContext.cs` з усіма entity (14 таблиць)
- `appsettings.json` з connection string
- Перша міграція + seed дані (ролі: guest/user/admin, магазини: steam/gog)
- Друга міграція `AddAlertsLibraryAndDbObjects` — нові таблиці, індекси, тригери, функції, процедури, view-и
- Перевірка: `dotnet ef database update` відпрацьовує без помилок

---

## КРОК 2 — Auth (реєстрація / логін / JWT)
**Артефакт**: `STEP_02_auth.md`
- `User`, `Role` моделі вже є в БД
- `AuthController`: POST /register, POST /login
- `AuthService`: хешування паролю (BCrypt), генерація JWT
- JWT middleware в `Program.cs`
- Перевірка: логін повертає токен, захищений ендпоінт відхиляє без токена

---

## КРОК 3 — CRUD ігор (бекенд)
**Артефакт**: `STEP_03_games_api.md`
- `GamesController`: GET список, GET деталі, POST, PUT, DELETE
- GET список: підтримка query параметрів `search`, `genreId`, `shopId`, `page`, `pageSize`
- GET деталі: повертає гру + всі offers + price history (останні 180 днів) + deal score
- POST/PUT/DELETE тільки для admin (JWT + role check)
- Використовує view `vw_game_catalog` для списку та функцію `fn_get_deal_score` для деталей
- Перевірка: curl запити до всіх ендпоінтів

---

## КРОК 4 — Вішліст (бекенд)
**Артефакт**: `STEP_04_wishlist_api.md`
- `WishlistController`: GET, POST /:gameId, DELETE /:gameId
- POST /import-steam: бере external_uid зі UserShopProfile юзера → викликає Steam API → матчить ігри по ExternalId в GameOffer → додає в wishlist з `SourceShopId = 1` (Steam)
- Модель `Wishlist` використовує `SourceShopId` (FK → GameShop) замість текстового `Source`:
  - `NULL` = додано вручну
  - `1` = імпортовано зі Steam
  - `2` = імпортовано з GOG
- Перевірка: додати гру, перевірити що вона в списку, видалити

---

## КРОК 5 — Синхронізація цін (бекенд)
**Артефакт**: `STEP_05_sync_api.md`
- `PriceSyncService`: логіка синхронізації (Steam Store API + GOG API)
- `SyncController`: POST /steam, POST /gog, GET /log  [admin]
- При UPDATE ціни в `GameOffer` автоматично спрацьовують тригери:
  - `trg_auto_price_history` — записує стару ціну в `PriceHistory`
  - `trg_check_price_alerts` — перевіряє та активує алерти
- Перевірка: POST /api/sync/steam повертає `{ updated: N, changed: M }`

---

## КРОК 6 — Профіль і прив'язка магазинів (бекенд)
**Артефакт**: `STEP_06_profile_api.md`
- `ProfileController`: GET /profile, PUT /profile/shop-profile
- Зберігає external_uid в UserShopProfile
- Перевірка: прив'язати Steam ID, перевірити що він зберігся

---

## КРОК 7 — Алерти (бекенд)
**Артефакт**: `STEP_07_alerts_api.md`
- `AlertController`:
  - GET /api/alerts — список алертів юзера (використовує `fn_get_user_alerts`)
  - POST /api/alerts — створити алерт (TargetPrice та/або TargetDiscount)
  - PUT /api/alerts/:id — оновити алерт
  - DELETE /api/alerts/:id — видалити алерт
- `AlertService`: CRUD + виклик процедури `pr_set_alert` для створення/оновлення
- Автоматичне спрацювання через тригер `trg_check_price_alerts` при зміні ціни
- Автоматична деактивація при покупці (тригер `trg_handle_library_insert`)
- Перевірка: створити алерт, оновити ціну нижче порогу → алерт деактивований

---

## КРОК 8 — Бібліотека (бекенд)
**Артефакт**: `STEP_08_library_api.md`
- `LibraryController`:
  - GET /api/library — бібліотека юзера (використовує view `vw_user_library`)
  - POST /api/library — додати гру (використовує процедуру `pr_add_to_library`)
  - GET /api/games/:id/deal-score — deal score (використовує `fn_get_deal_score`)
- `LibraryService`: логіка роботи з бібліотекою
- При додаванні в бібліотеку тригер `trg_handle_library_insert`:
  - Автоматично видаляє гру з вішлісту
  - Деактивує алерт на цю гру
- Перевірка: додати гру в бібліотеку → перевірити що видалена з wishlist

---

## КРОК 9 — React setup + Auth сторінки (фронтенд)
**Артефакт**: `STEP_09_frontend_setup.md`
- `create-react-app` або Vite + Ant Design + React Router
- `AuthContext` — зберігає JWT токен, роль юзера
- Сторінки: Login, Register
- `PrivateRoute` компонент — редирект якщо не залогінений
- Перевірка: логін зберігає токен в localStorage, редирект на каталог

---

## КРОК 10 — Каталог ігор (фронтенд)
**Артефакт**: `STEP_10_catalog.md`
- `CatalogPage` — сітка карток з іграми
- Пошук за назвою (Input з debounce), фільтр жанру (Select), фільтр магазину (Select)
- Пагінація
- Картка гри: назва, знижка (бейдж), мінімальна ціна, кнопка "+ Вішліст", бейдж deal score
- Перевірка: пошук фільтрує результати без перезавантаження

---

## КРОК 11 — Детальна сторінка гри (фронтенд)
**Артефакт**: `STEP_11_game_detail.md`
- `GameDetailPage` — відкривається по `/games/:id`
- Блок інфо: назва, опис, розробник, видавець, жанри (теги)
- Таблиця offers: магазин, ціна, знижка, deal score, посилання
- Графік Recharts (LineChart) — динаміка ціни за 6 місяців
- Кнопка "Встановити алерт" — модальне вікно з полями TargetPrice / TargetDiscount
- Перевірка: графік рендериться з даними з PriceHistory

---

## КРОК 12 — Вішліст і профіль (фронтенд)
**Артефакт**: `STEP_12_wishlist_profile.md`
- `WishlistPage` — таблиця ігор з вішлісту + кнопка видалення
- Кнопка "Імпорт зі Steam" → модальне вікно (якщо Steam не прив'язаний — редирект на профіль)
- `ProfilePage` — дані акаунту + форма прив'язки Steam ID
- Перевірка: додати гру у вішліст через каталог, перевірити на сторінці вішлісту

---

## КРОК 13 — Алерти і бібліотека (фронтенд)
**Артефакт**: `STEP_13_alerts_library.md`
- `AlertsPage` — список алертів юзера з поточним статусом:
  - Назва гри, цільова ціна/знижка, поточна ціна, відсоток до цілі
  - Статус: активний / спрацював / деактивований
  - Кнопки: редагувати, видалити
- `LibraryPage` — таблиця куплених ігор з магазинами
- Перевірка: створити алерт через детальну сторінку, побачити на AlertsPage

---

## КРОК 14 — Адмін панель (фронтенд)
**Артефакт**: `STEP_14_admin.md`
- `GamesAdminPage` — таблиця всіх ігор + кнопки Edit/Delete + кнопка Add
- Модальне вікно з формою (назва, опис, дата, розробник, видавець, жанри)
- `SyncPage` — кнопка "Запустити синхронізацію", відображення результату
- Видимість роутів тільки для admin
- Перевірка: додати гру через форму, перевірити що вона з'явилась в каталозі

---

## Порядок виконання
1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 (бекенд повністю готовий)
9 → 10 → 11 → 12 → 13 → 14 (фронтенд)

Кожен крок незалежний — можна давати окремій AI сесії.
Завжди передавай CONTEXT.md разом з відповідним STEP_XX.md.
