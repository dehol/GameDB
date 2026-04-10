# Start-Dev.ps1 - Запуск бекенду і фронтенду разом

$backend = Start-Process -FilePath "dotnet" -ArgumentList "watch", "--project", "GameDB.Api\GameDB.Api.csproj", "run" -PassThru -WindowStyle Normal

Start-Sleep -Seconds 5

$frontend = Start-Process -FilePath "npm" -ArgumentList "run", "dev" -WorkingDirectory "gamedb-client" -PassThru -WindowStyle Normal

Write-Host "`nBackend: http://localhost:5212" -ForegroundColor Green
Write-Host "Frontend: http://localhost:5173" -ForegroundColor Green
Write-Host "`nPress Ctrl+C to stop both..." -ForegroundColor Yellow

# Wait for user input
Read-Host "Press Enter to stop all processes"

Stop-Process -Id $backend.Id -Force -ErrorAction SilentlyContinue
Stop-Process -Id $frontend.Id -Force -ErrorAction SilentlyContinue
