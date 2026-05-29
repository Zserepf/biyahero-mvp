# ============================================================
#  BiyaHero -- Local Dev Startup Script
#  Usage:    .\start.ps1
#  Stop with: .\stop.ps1
# ============================================================

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

function Write-Step($msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Write-Ok($msg)   { Write-Host "    [OK] $msg" -ForegroundColor Green }
function Write-Warn($msg) { Write-Host "    [!!] $msg" -ForegroundColor Yellow }
function Write-Fail($msg) { Write-Host "    [XX] $msg" -ForegroundColor Red }

# --- 1. Prerequisite checks ---
Write-Step "Checking prerequisites..."

$missing = @()
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { $missing += ".NET SDK 8+" }
if (-not (Get-Command node   -ErrorAction SilentlyContinue)) { $missing += "Node.js 18+" }
if (-not (Get-Command npm    -ErrorAction SilentlyContinue)) { $missing += "npm 9+" }

if ($missing.Count -gt 0) {
    Write-Fail "Missing required tools:"
    $missing | ForEach-Object { Write-Host "      - $_" -ForegroundColor Red }
    Write-Host ""
    Write-Host "  Install .NET SDK 8  : https://dotnet.microsoft.com/download" -ForegroundColor Yellow
    Write-Host "  Install Node.js 18+ : https://nodejs.org" -ForegroundColor Yellow
    exit 1
}

Write-Ok ".NET  $(dotnet --version)"
Write-Ok "Node  $(node --version)"
Write-Ok "npm   $(npm --version)"

# --- 2. Infrastructure (PostgreSQL + DynamoDB Local) ---
$dockerAvailable = [bool](Get-Command docker -ErrorAction SilentlyContinue)

if ($dockerAvailable) {
    Write-Step "Starting infrastructure via Docker (PostgreSQL + DynamoDB Local)..."
    Set-Location $root
    docker compose -f docker-compose.test.yml up -d

    Write-Host "    Waiting for PostgreSQL to be ready..." -ForegroundColor DarkCyan
    $retries = 0
    do {
        Start-Sleep -Seconds 3
        $retries++
        $health = docker inspect --format "{{.State.Health.Status}}" biyahero-test-postgres 2>$null
    } while ($health -ne "healthy" -and $retries -lt 20)

    if ($health -ne "healthy") {
        Write-Warn "PostgreSQL health check timed out -- it may still be starting."
    } else {
        Write-Ok "PostgreSQL is healthy"
    }

    $env:ConnectionStrings__Postgres = "Host=localhost;Database=biyahero_test;Username=biyahero_test;Password=test_password"
    $env:ConnectionStrings__DynamoDB = "http://localhost:8000"
} else {
    Write-Warn "Docker not found -- skipping container startup."
    Write-Host ""
    Write-Host "  The API and frontend will still start, but database-backed features" -ForegroundColor Yellow
    Write-Host "  (login, register, routes, heatmap) will not work until you either:" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "    A) Install Docker Desktop : https://www.docker.com/products/docker-desktop" -ForegroundColor Yellow
    Write-Host "       Then re-run .\start.ps1" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "    B) Install PostgreSQL natively : https://www.postgresql.org/download/windows/" -ForegroundColor Yellow
    Write-Host "       Create a database named 'biyahero' with user postgres / postgres" -ForegroundColor Yellow
    Write-Host ""

    $env:ConnectionStrings__Postgres = "Host=localhost;Database=biyahero;Username=postgres;Password=postgres"
    $env:ConnectionStrings__DynamoDB = "http://localhost:8000"
}

$env:ASPNETCORE_ENVIRONMENT = "Development"

# --- 3. Backend API ---
Write-Step "Starting .NET 8 API (http://localhost:5000)..."

$apiDir = Join-Path $root "apps\api"

$apiProcess = Start-Process -FilePath "cmd.exe" `
    -ArgumentList "/c", "dotnet run --project BiyaHero.Api --no-launch-profile" `
    -WorkingDirectory $apiDir `
    -PassThru `
    -WindowStyle Normal

Write-Ok "API process started (PID $($apiProcess.Id))"
$apiProcess.Id | Out-File (Join-Path $root ".api.pid") -Encoding ascii

# --- 4. Frontend PWA ---
Write-Step "Checking frontend dependencies..."

$webDir = Join-Path $root "apps\web"
if (-not (Test-Path (Join-Path $webDir "node_modules"))) {
    Write-Host "    Running npm install..." -ForegroundColor DarkCyan
    npm install --prefix $webDir
} else {
    Write-Ok "node_modules already present -- skipping install"
}

Write-Step "Starting Next.js PWA (http://localhost:3000)..."

$webProcess = Start-Process -FilePath "cmd.exe" `
    -ArgumentList "/c", "npm run dev" `
    -WorkingDirectory $webDir `
    -PassThru `
    -WindowStyle Normal

Write-Ok "Frontend process started (PID $($webProcess.Id))"
$webProcess.Id | Out-File (Join-Path $root ".web.pid") -Encoding ascii

# --- 5. Summary ---
Write-Host ""
Write-Host "============================================" -ForegroundColor Magenta
Write-Host "  BiyaHero is starting up!" -ForegroundColor Magenta
Write-Host "============================================" -ForegroundColor Magenta
Write-Host ""
Write-Host "  Frontend  ->  http://localhost:3000" -ForegroundColor White
Write-Host "  API       ->  http://localhost:5000" -ForegroundColor White
if ($dockerAvailable) {
    Write-Host "  DynamoDB  ->  http://localhost:8000" -ForegroundColor White
    Write-Host "  Postgres  ->  localhost:5432" -ForegroundColor White
}
Write-Host ""
Write-Host "  Allow ~15 seconds for both servers to finish compiling." -ForegroundColor DarkGray
Write-Host "  Run .\stop.ps1 to shut everything down cleanly." -ForegroundColor DarkGray
Write-Host ""
