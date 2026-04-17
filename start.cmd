@echo off
setlocal EnableDelayedExpansion

echo ==========================================
echo    GameDB Development Launcher
echo ==========================================
echo.

:: Check if ports are in use
echo Checking ports...
netstat -ano | findstr :3000 >nul 2>&1
if %errorlevel% equ 0 (
    echo [WARNING] Port 3000 is already in use!
    echo Frontend may already be running.
    echo.
)

netstat -ano | findstr :5212 >nul 2>&1
if %errorlevel% equ 0 (
    echo [WARNING] Port 5212 is already in use!
    echo Backend may already be running.
    echo.
)

echo.
echo Choose startup mode:
echo   1. Start ALL (Docker + Backend + Frontend)
echo   2. Start Backend + Frontend only (no Docker)
echo   3. Start Backend only
echo   4. Start Frontend only
echo   5. Kill all GameDB processes and restart
echo   6. Kill all GameDB processes only (no restart)
echo.
set /p choice="Enter choice (1-6): "

if "%choice%"=="1" goto start_all
if "%choice%"=="2" goto start_no_docker
if "%choice%"=="3" goto start_backend_only
if "%choice%"=="4" goto start_frontend_only
if "%choice%"=="5" goto kill_and_restart
if "%choice%"=="6" goto kill_only

echo Invalid choice. Exiting.
goto end

:start_all
echo.
echo Starting Docker...  
powershell -Command "docker compose up -d"
if %errorlevel% neq 0 (
    echo [ERROR] Docker failed to start. Make sure Docker Desktop is running.
    pause
    goto end
)
goto start_services

:start_no_docker
goto start_services

:start_services
echo.
echo Starting Backend on http://localhost:5212...
start "GameDB Backend" powershell -NoExit -Command "dotnet watch --project GameDB.Api\GameDB.Api.csproj"

echo.
echo Starting Frontend on http://localhost:3000...
cd gamedb-client
start "GameDB Frontend" powershell -NoExit -Command "npm run dev"
cd ..

echo.
echo ==========================================
echo    All services started!
echo ==========================================
echo.
echo Frontend: http://localhost:3000
echo Backend:  http://localhost:5212
echo.
pause
goto end

:start_backend_only
echo.
echo Starting Backend on http://localhost:5212...
start "GameDB Backend" powershell -NoExit -Command "dotnet watch --project GameDB.Api\GameDB.Api.csproj"
pause
goto end

:start_frontend_only
echo.
echo Starting Frontend on http://localhost:3000...
cd gamedb-client
start "GameDB Frontend" powershell -NoExit -Command "npm run dev"
pause
goto end

:kill_and_restart
echo.
echo Killing all GameDB processes...
taskkill /F /IM "dotnet.exe" /FI "WINDOWTITLE eq GameDB Backend" >nul 2>&1
taskkill /F /IM "node.exe" /FI "WINDOWTITLE eq GameDB Frontend" >nul 2>&1
netstat -ano | findstr :3000 | findstr LISTENING > temp.txt
for /f "tokens=5" %%a in (temp.txt) do taskkill /F /PID %%a >nul 2>&1
del temp.txt 2>nul
timeout /t 2 /nobreak >nul
echo Processes killed.
echo.
goto start_all

:kill_only
echo.
echo Killing all GameDB processes...
taskkill /F /IM "dotnet.exe" 2>nul | findstr /V "not found"
taskkill /F /IM "node.exe" 2>nul | findstr /V "not found"
netstat -ano | findstr :3000 | findstr LISTENING > temp.txt 2>nul
for /f "tokens=5" %%a in (temp.txt) do taskkill /F /PID %%a >nul 2>&1
del temp.txt 2>nul
timeout /t 1 /nobreak >nul
echo.
echo All GameDB processes stopped!
pause
goto end

:end
endlocal
