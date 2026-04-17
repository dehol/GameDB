# GameDB Development Launcher v2
# Usage: .\start.ps1 [-NoDocker] [-BackendOnly] [-FrontendOnly] [-KillExisting] [-KillOnly]

param(
    [switch]$NoDocker,
    [switch]$BackendOnly,
    [switch]$FrontendOnly,
    [switch]$KillExisting,
    [switch]$KillOnly
)

$ErrorActionPreference = "Stop"
$repoRoot = $PSScriptRoot

function Test-Port($port) {
    $connection = Test-NetConnection -ComputerName localhost -Port $port -WarningAction SilentlyContinue
    return $connection.TcpTestSucceeded
}

function Stop-GameDBProcesses {
    Write-Host "`nStopping existing GameDB processes..." -ForegroundColor Yellow
    
    # Stop dotnet processes (backend)
    Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Where-Object { 
        $_.MainWindowTitle -like "*GameDB*" -or $_.CommandLine -like "*GameDB.Api*"
    } | Stop-Process -Force
    
    # Stop node processes (frontend)
    Get-Process -Name "node" -ErrorAction SilentlyContinue | Where-Object {
        $_.CommandLine -like "*gamedb-client*" -or $_.CommandLine -like "*vite*"
    } | Stop-Process -Force
    
    # Wait for ports to be released
    Start-Sleep -Seconds 2
    Write-Host "Processes stopped." -ForegroundColor Green
}

function Start-Backend {
    Write-Host "`n[1/2] Starting Backend..." -ForegroundColor Cyan
    $backendProj = Join-Path $repoRoot "GameDB.Api\GameDB.Api.csproj"
    
    $job = Start-Job -ScriptBlock {
        param($proj)
        dotnet watch --project $proj run --no-hot-reload
    } -ArgumentList $backendProj -Name "GameDB-Backend"
    
    # Wait for startup
    $attempts = 0
    while ($attempts -lt 30) {
        Start-Sleep -Milliseconds 500
        $log = Receive-Job -Job $job -Keep -ErrorAction SilentlyContinue
        if ($log -match "Now listening on" -or (Test-Port 5212)) {
            Write-Host "    Backend ready at http://localhost:5212" -ForegroundColor Green
            return $job
        }
        $attempts++
    }
    
    Write-Host "    Backend starting... (check window for errors)" -ForegroundColor Yellow
    return $job
}

function Start-Frontend {
    Write-Host "`n[2/2] Starting Frontend..." -ForegroundColor Cyan
    $clientDir = Join-Path $repoRoot "gamedb-client"
    
    $job = Start-Job -ScriptBlock {
        param($dir)
        Set-Location $dir
        npm run dev
    } -ArgumentList $clientDir -Name "GameDB-Frontend"
    
    # Wait for startup
    $attempts = 0
    while ($attempts -lt 30) {
        Start-Sleep -Milliseconds 500
        $log = Receive-Job -Job $job -Keep -ErrorAction SilentlyContinue
        if ($log -match "ready in" -or (Test-Port 3000)) {
            Write-Host "    Frontend ready at http://localhost:3000" -ForegroundColor Green
            return $job
        }
        $attempts++
    }
    
    Write-Host "    Frontend starting... (check window for errors)" -ForegroundColor Yellow
    return $job
}

function Start-DockerServices {
    Write-Host "`n[0/2] Starting Docker services..." -ForegroundColor Cyan
    docker compose up -d
    if ($LASTEXITCODE -ne 0) {
        Write-Host "    Docker failed! Make sure Docker Desktop is running." -ForegroundColor Red
        exit 1
    }
    Write-Host "    Docker services ready." -ForegroundColor Green
}

# ============ MAIN ============

# Kill only mode
if ($KillOnly) {
    Stop-GameDBProcesses
    Write-Host "`nDone! All GameDB processes stopped." -ForegroundColor Green
    exit 0
}

Clear-Host
Write-Host @"
==========================================
      GameDB Development Launcher
==========================================
"@ -ForegroundColor Blue

# Kill existing if requested
if ($KillExisting) {
    Stop-GameDBProcesses
}

# Check ports
$port3000 = Test-Port 3000
$port5212 = Test-Port 5212

if ($port3000 -or $port5212) {
    Write-Host "WARNING: Some ports are already in use!" -ForegroundColor Yellow
    if ($port3000) { Write-Host "  - Port 3000 (Frontend) is occupied" -ForegroundColor Yellow }
    if ($port5212) { Write-Host "  - Port 5212 (Backend) is occupied" -ForegroundColor Yellow }
    
    $response = Read-Host "`nKill existing processes? (y/n)"
    if ($response -eq 'y') {
        Stop-GameDBProcesses
    } else {
        Write-Host "`nExiting. Use -KillExisting flag to auto-kill." -ForegroundColor Red
        exit 1
    }
}

# Start services
$jobs = @()

try {
    if (-not $NoDocker -and -not $BackendOnly -and -not $FrontendOnly) {
        Start-DockerServices
    }
    
    if ($BackendOnly) {
        $jobs += Start-Backend
        Write-Host "`nBackend running. Press Ctrl+C to stop." -ForegroundColor Green
        while ($true) { Start-Sleep -Seconds 1 }
    }
    elseif ($FrontendOnly) {
        $jobs += Start-Frontend
        Write-Host "`nFrontend running. Press Ctrl+C to stop." -ForegroundColor Green
        while ($true) { Start-Sleep -Seconds 1 }
    }
    else {
        $jobs += Start-Backend
        $jobs += Start-Frontend
        
        Write-Host @"

==========================================
      All services started!
==========================================

  Frontend: http://localhost:3000
  Backend:  http://localhost:5212
  API Docs: http://localhost:5212/swagger

  Press Ctrl+C to stop all services
==========================================
"@ -ForegroundColor Green
        
        # Monitor jobs
        try {
            while ($true) {
                Start-Sleep -Seconds 1
                foreach ($job in $jobs) {
                    Receive-Job -Job $job | ForEach-Object { Write-Host $_ }
                }
            }
        }
        finally {
            Write-Host "`nStopping services..." -ForegroundColor Yellow
            $jobs | Stop-Job -ErrorAction SilentlyContinue
            $jobs | Remove-Job -Force -ErrorAction SilentlyContinue
            Write-Host "Done!" -ForegroundColor Green
        }
    }
}
catch {
    Write-Host "`nERROR: $_" -ForegroundColor Red
    $jobs | Stop-Job -ErrorAction SilentlyContinue
    $jobs | Remove-Job -Force -ErrorAction SilentlyContinue
    exit 1
}
