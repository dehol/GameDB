# GameDB - Запуск проекту

## Швидкий старт

### Простий спосіб (рекомендовано)
```powershell
# Запуск всеї інфраструктури
.\start.cmd

# Або PowerShell версія з більше контролем:
.\start.ps1
```

### Варіанти запуску:
```powershell
# Запуск без Docker (якщо PostgreSQL вже запущений окремо)
.\start.ps1 -NoDocker

# Тільки Backend
.\start.ps1 -BackendOnly

# Тільки Frontend
.\start.ps1 -FrontendOnly

# Перезапуск (вбити всі процеси і запустити заново)
.\start.ps1 -KillExisting

# Тільки вбити процеси (без запуску)
.\start.ps1 -KillOnly
```

### Зупинка
```powershell
# Зупинка через меню
.\start.cmd
# Потім вибери опцію 6

# Або PowerShell
.\stop.ps1

# Або швидко
.\start.ps1 -KillOnly
```

## Ручний запуск (якщо скрипти не працюють)

### Термінал 1 - Backend:
```powershell
cd GameDB.Api
dotnet watch
```

### Термінал 2 - Frontend:
```powershell
cd gamedb-client
npm run dev
```

### Термінал 3 - Docker (опціонально):
```powershell
docker compose up -d
```

## URL
- Frontend: http://localhost:3000
- Backend API: http://localhost:5212
- Swagger Docs: http://localhost:5212/swagger
- PostgreSQL: localhost:5432

## Вирішення проблем

### Порт вже зайнятий
```powershell
# Перевірити що займає порт 3000
netstat -ano | findstr :3000

# Вбити процес за PID
taskkill /F /PID <PID>
```

### Скрипт не запускається (Execution Policy)
```powershell
# Відкрити PowerShell як Адміністратор
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

### Відсутні залежності
```powershell
# Перевірити
npm --version
dotnet --version
docker --version
```
