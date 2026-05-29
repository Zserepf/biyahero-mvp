#!/usr/bin/env pwsh
# ============================================================
#  BiyaHero — Local Dev Stop Script
#  Usage: .\stop.ps1
# ============================================================

$root = $PSScriptRoot

function Write-Step($msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Write-Ok($msg)   { Write-Host "    [OK] $msg" -ForegroundColor Green }
function Write-Warn($msg) { Write-Host "    [!!] $msg" -ForegroundColor Yellow }

# ── 1. Stop API process ──────────────────────────────────────
Write-Step "Stopping .NET API..."

$apiPidFile = Join-Path $root ".api.pid"
if (Test-Path $apiPidFile) {
    $savedPid = (Get-Content $apiPidFile -Raw).Trim()
    try {
        Stop-Process -Id $savedPid -Force -ErrorAction Stop
        Write-Ok "API process $savedPid stopped"
    } catch {
        Write-Warn "API process $savedPid was already gone"
    }
    Remove-Item $apiPidFile -Force
} else {
    # Fallback: kill dotnet processes that look like the API
    $procs = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue
    if ($procs) {
        $procs | Stop-Process -Force
        Write-Ok "Stopped $($procs.Count) dotnet process(es)"
    } else {
        Write-Warn "No .api.pid file and no dotnet processes found — nothing to stop"
    }
}

# ── 2. Stop frontend process ─────────────────────────────────
Write-Step "Stopping Next.js frontend..."

$webPidFile = Join-Path $root ".web.pid"
if (Test-Path $webPidFile) {
    $savedPid = (Get-Content $webPidFile -Raw).Trim()
    try {
        Stop-Process -Id $savedPid -Force -ErrorAction Stop
        Write-Ok "Frontend process $savedPid stopped"
    } catch {
        Write-Warn "Frontend process $savedPid was already gone"
    }
    Remove-Item $webPidFile -Force
} else {
    Write-Warn "No .web.pid file found — nothing to stop"
}

# ── 3. Stop Docker infrastructure ────────────────────────────
Write-Step "Stopping Docker infrastructure..."

if (Get-Command docker -ErrorAction SilentlyContinue) {
    Set-Location $root
    docker compose -f docker-compose.test.yml down
    Write-Ok "Docker containers stopped"
} else {
    Write-Warn "Docker not found — skipping container teardown"
}

Write-Host ""
Write-Host "  BiyaHero stopped." -ForegroundColor Magenta
Write-Host ""
