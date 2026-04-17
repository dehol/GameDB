# GameDB Stop Script
# Stops all GameDB development processes

Write-Host "Stopping GameDB services..." -ForegroundColor Yellow

$killed = $false

# Stop Backend (dotnet)
$dotnetProcesses = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Where-Object { 
    $_.CommandLine -like "*GameDB.Api*"
}
if ($dotnetProcesses) {
    $dotnetProcesses | Stop-Process -Force
    Write-Host "  Stopped Backend (dotnet)" -ForegroundColor Green
    $killed = $true
}

# Stop Frontend (node)
$nodeProcesses = Get-Process -Name "node" -ErrorAction SilentlyContinue | Where-Object {
    $_.CommandLine -like "*gamedb-client*" -or $_.CommandLine -like "*vite*"
}
if ($nodeProcesses) {
    $nodeProcesses | Stop-Process -Force
    Write-Host "  Stopped Frontend (node)" -ForegroundColor Green
    $killed = $true
}

# Stop Docker (optional)
$response = Read-Host "`nStop Docker containers too? (y/n)"
if ($response -eq 'y') {
    docker compose down
    Write-Host "  Stopped Docker containers" -ForegroundColor Green
    $killed = $true
}

if (-not $killed) {
    Write-Host "No GameDB processes found." -ForegroundColor Cyan
} else {
    Write-Host "`nAll GameDB services stopped!" -ForegroundColor Green
}
