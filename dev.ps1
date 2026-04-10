param(
  [switch]$NoDocker
)

$ErrorActionPreference = "Stop"

function Ensure-Command($name) {
  if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
    throw "Required command '$name' was not found in PATH."
  }
}

Ensure-Command dotnet
Ensure-Command npm

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$backendProj = Join-Path $repoRoot "GameDB.Api\GameDB.Api.csproj"
$clientDir = Join-Path $repoRoot "gamedb-client"

if (-not $NoDocker) {
  Ensure-Command docker
  Write-Host "Starting Postgres (docker compose)..." -ForegroundColor Cyan
  docker compose -f (Join-Path $repoRoot "docker-compose.yml") up -d
}

Write-Host "Starting backend (dotnet watch)..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList @(
  "-NoProfile",
  "-ExecutionPolicy", "Bypass",
  "-Command",
  "dotnet watch --non-interactive --project `"$backendProj`" run"
)

Write-Host "Starting frontend (Vite)..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList @(
  "-NoProfile",
  "-ExecutionPolicy", "Bypass",
  "-Command",
  "cd `"$clientDir`"; npm install; npm run dev"
)

Write-Host ""
Write-Host "Frontend: http://localhost:3000/" -ForegroundColor Green
Write-Host "Backend:  http://localhost:5212/" -ForegroundColor Green
