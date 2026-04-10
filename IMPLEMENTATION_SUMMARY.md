# GameDB Import Pipeline - Production Implementation Summary

## Огляд впровадження

Реалізовано повністю перероблену систему синхронізації та імпорту ігор з production-level архітектурою на основі рекомендацій ChatGPT та ваших вподобань.

## Архітектурні рішення

### 1. IGDB як основне джерело метаданих ✅
- **IgdbApiService** з Twitch OAuth авторизацією
- Отримання нормалізованих метаданих (жанри, розробники, видавці)
- Автоматичне зв'язування з Steam AppIds через IGDB External Games API
- Налаштовувана кількість ігор (за замовчуванням: 5000 популярних ігор)

### 2. Pipeline Architecture з Staging Tables ✅
**Трифазний pipeline:**
1. **Collect Phase** - Збір raw JSON даних з IGDB + Steam
2. **Staging Phase** - Обробка та нормалізація в staging tables
3. **Import Phase** - Імпорт в основні таблиці з кешуванням довідкових даних

**Переваги:**
- Розділення відповідальності між фазами
- Можливість повторної обробки при помилках
- Збереження raw даних для аналізу та дебагінгу
- Підтримка транзакцій на рівні batch

### 3. ASP.NET Core BackgroundService ✅
**GameImportWorker:**
- Обробка pipeline через Channel (thread-safe черга)
- Підтримка cancellation tokens
- Автоматична обробка помилок з retry logic
- Інтеграція з Health Checks

## Ключові компоненти

### Моделі даних

#### RawGameData
```csharp
- Source: "IGDB" | "Steam" | "GOG" | "EGS"
- ExternalId: ID з джерела
- RawJson: Повний JSON відповіді
- FetchedAt: Час отримання
- Processed: Статус обробки
- ProcessingAttempts: Кількість спроб
- ProcessingError: Помилка обробки
```

#### StagingGame
```csharp
- NormalizedTitle: Для матчингу між магазинами
- IgdbId, SteamAppId, GogId, EgsId: IDs з різних джерел
- Title, Description, ReleaseDate: Нормалізовані метадані
- Genres: JSON масив жанрів
- Ціни та знижки для кожного магазину
```

### Сервіси

#### IgdbApiService
- OAuth авторизація з автоматичним оновленням токена
- Batch отримання ігор, жанрів, компаній
- Пошук за назвою та отримання популярних ігор
- Rate limiting та error handling

#### ImportPipelineService
- Керування pipeline lifecycle
- Перевірка на active pipelines (лише один одночасно)
- Cancellation support
- Status tracking

#### ReferenceDataCache
- Thread-safe кеш для developers, publishers, genres
- Double-checked locking pattern
- Preloading для оптимізації швидкості
- Автоматичне створення нових сутностей

### Background Services

#### GameImportWorker
**Phase 1: Collect Raw Data**
- Отримання popular games з IGDB
- Batch отримання Steam цін
- Збереження raw JSON в RawGameData

**Phase 2: Process to Staging**
- Паралельна обробка raw даних
- Створення/оновлення StagingGame записів
- Мэтчинг за NormalizedTitle

**Phase 3: Import to Main Tables**
- Створення/оновлення Game records
- Batch створення GameOffer records
- Кешування довідкових даних

### API Endpoints

#### POST /api/import/start
Запуск нового import pipeline
```json
Response: {
  "pipelineId": 123,
  "message": "Import pipeline started successfully",
  "statusUrl": "/api/import/status/123"
}
```

#### GET /api/import/status/{pipelineId}
Статус pipeline з прогресом
```json
Response: {
  "pipelineId": 123,
  "status": "running",
  "phase": "importing",
  "totalGames": 5000,
  "processedGames": 3500,
  "importedGames": 2800,
  "errorCount": 12,
  "progressPercent": 70.0,
  "duration": "00:15:32"
}
```

#### POST /api/import/cancel/{pipelineId}
Скасування active pipeline

#### GET /api/import/current
Поточний active pipeline

#### GET /api/import/jobs
Історія import jobs з пагінацією

#### GET /api/import/stats
Статистика import operations

### Health Checks

#### ImportWorkerHealthCheck
Перевіряє:
- Stuck jobs (running > timeout)
- Recent failures
- Error rate
- Повертає статус: Healthy / Degraded / Unhealthy

**Endpoint:** `GET /health`

## Конфігурація

### appsettings.json
```json
{
  "Igdb": {
    "ClientId": "YOUR_TWITCH_CLIENT_ID",
    "ClientSecret": "YOUR_TWITCH_CLIENT_SECRET",
    "ApiBaseUrl": "https://api.igdb.com/v4",
    "AuthUrl": "https://id.twitch.tv/oauth2/token",
    "RequestTimeoutSeconds": 60,
    "TokenRefreshMinutes": 30
  },
  "Import": {
    "BatchSize": 100,
    "SteamBatchSize": 50,
    "MaxConcurrentApiCalls": 10,
    "MaxConcurrentProcessing": 20,
    "MaxConcurrentDbOperations": 30,
    "MaxRetries": 3,
    "RetryBaseDelaySeconds": 1.0,
    "JobTimeoutMinutes": 120,
    "EnableDetailedLogging": true,
    "IgdbPopularGamesLimit": 5000,
    "RawDataRetentionDays": 7
  }
}
```

## Налаштування IGDB

### Крок 1: Реєстрація на Twitch
1. Перейдіть на https://dev.twitch.tv/console
2. Створіть новий додаток
3. Отримайте Client ID та Client Secret

### Крок 2: Конфігурація
Додайте credentials в `appsettings.json`:
```json
"Igdb": {
  "ClientId": "your_client_id_here",
  "ClientSecret": "your_client_secret_here"
}
```

**Важливо:** НЕ комітьте credentials в git! Використовуйте:
- User Secrets для development: `dotnet user-secrets set "Igdb:ClientId" "your_id"`
- Environment variables для production
- Azure Key Vault / AWS Secrets Manager

## Використання

### Запуск import pipeline
```bash
# Через API
curl -X POST https://your-api/api/import/start \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"

# Перевірка статусу
curl https://your-api/api/import/current

# Перегляд історії
curl https://your-api/api/import/jobs?page=1&pageSize=20
```

### Monitoring
```bash
# Health check
curl https://your-api/health

# Перевірка stuck jobs
curl https://your-api/health | jq '.checks."import-worker"'
```

## Performance Optimizations

### 1. Parallel Processing
- Configurable concurrency levels (API: 10, Processing: 20, DB: 30)
- SemaphoreSlim для thread-safe rate limiting
- Batch operations для зменшення DB round trips

### 2. Caching
- ReferenceDataCache для developers/publishers/genres
- Preloading перед import
- Double-checked locking для thread safety

### 3. Staging Architecture
- Raw JSON збереження для retry без повторного API call
- Partitioned processing (collect → stage → import)
- Batch inserts/updates

### 4. Database Optimizations
- Indexes на staging tables
- Efficient batch operations
- Connection pooling (managed by EF Core)

## Error Handling

### Retry Logic
- Configurable max retries (default: 3)
- Exponential backoff
- Error tracking per item
- Automatic skip після max retries

### Error Classification
- Transient errors (API rate limits, timeouts) → retry
- Permanent errors (invalid data) → skip with log
- Critical errors (DB connection) → fail pipeline

### Monitoring
- Health checks для stuck jobs
- Error rate tracking
- Structured logging with EventIds
- Detailed error messages в ImportJob record

## Logging

### Structured Logging
```csharp
_logger.LogInformation(
    ImportLogEvents.PipelineStarted,
    "Pipeline {PipelineId} started with {Limit} games",
    pipelineId, limit);
```

### Log Levels
- **Information:** Pipeline lifecycle, phases, batches
- **Warning:** Retry attempts, non-critical errors
- **Error:** Failed operations, API errors
- **Debug:** Detailed processing (enable via config)

## Next Steps (Рекомендації)

### 1. Polly Retry Policies (Optional)
Додати Polly для більш robust retry logic:
```bash
dotnet add package Microsoft.Extensions.Http.Polly
```

### 2. Distributed Tracing (Optional)
Додати OpenTelemetry для distributed tracing:
```bash
dotnet add package OpenTelemetry.Extensions.Hosting
```

### 3. Cleanup Jobs
Додати periodic cleanup для старих raw data:
```csharp
// Delete RawGameData older than RetentionDays
// Delete processed StagingGame records
```

### 4. Incremental Updates
Додати support для incremental updates:
```csharp
// Only fetch games updated since last sync
// Track last sync timestamp per store
```

### 5. Real-time Progress
Додати SignalR для real-time progress updates:
```csharp
// Hub for broadcasting pipeline progress
// Client subscriptions to pipeline events
```

## Тестування

### Unit Tests (Рекомендовано додати)
- IgdbApiService OAuth flow
- ReferenceDataCache thread safety
- Pipeline state machine transitions
- Error handling scenarios

### Integration Tests (Рекомендовано додати)
- End-to-end pipeline execution
- API endpoint responses
- Health check scenarios
- Concurrent pipeline prevention

### Load Testing (Рекомендовано для production)
- Concurrent API requests
- Database connection pool
- Memory usage during large imports
- Background service stability

## Deployment Checklist

- [ ] Налаштувати IGDB credentials (User Secrets / Environment Variables)
- [ ] Перевірити Connection String до PostgreSQL
- [ ] Налаштувати JWT ключі
- [ ] Сконфігурувати logging provider (Serilog, Application Insights, etc.)
- [ ] Налаштувати health checks monitoring
- [ ] Тестовий запуск pipeline з малим лімітом (100 ігор)
- [ ] Перевірити logs та errors
- [ ] Запустити повний import

## Файли створені/змінені

### Нові файли:
1. `GameDB.Core/Interfaces/IIgdbApiService.cs`
2. `GameDB.Core/Configuration/IgdbSettings.cs`
3. `GameDB.Core/Configuration/ImportSettings.cs`
4. `GameDB.Core/Models/RawGameData.cs`
5. `GameDB.Core/Models/StagingGame.cs`
6. `GameDB.Core/Interfaces/IPipelineService.cs`
7. `GameDB.Infrastructure/Services/IgdbApiService.cs`
8. `GameDB.Infrastructure/Services/ImportPipelineService.cs`
9. `GameDB.Infrastructure/BackgroundServices/GameImportWorker.cs`
10. `GameDB.Infrastructure/Caching/ReferenceDataCache.cs`
11. `GameDB.Infrastructure/HealthChecks/ImportWorkerHealthCheck.cs`
12. `GameDB.Infrastructure/Logging/ImportLogEvents.cs`
13. `GameDB.Api/Controllers/ImportController.cs`

### Змінені файли:
1. `GameDB.Infrastructure/AppDbContext.cs` - додано DbSets та indexes
2. `GameDB.Api/Program.cs` - реєстрація сервісів, health checks
3. `GameDB.Api/appsettings.json` - нові конфігурації

### Міграції:
- `AddStagingTables` - створення RawGameData та StagingGame tables

## Підсумок

Впроваджено повноцінну production-ready систему імпорту з:
- ✅ IGDB як основним джерелом метаданих
- ✅ Pipeline архітектурою з staging tables
- ✅ BackgroundService для background processing
- ✅ Health checks та structured logging
- ✅ Configurable performance settings
- ✅ Comprehensive error handling
- ✅ RESTful API endpoints

Система готова до використання після налаштування IGDB credentials. Всі компоненти протестовані на рівні компіляції, міграція застосована успішно.
