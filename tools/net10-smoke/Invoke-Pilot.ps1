<#
.SYNOPSIS
Wrapper del piloto net10: convierte la salida del smoke test en PASS/BLOCKED/FAIL
y falla ante exit inesperado, results.json ausente o resultado incoherente.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$RepoRoot,
    [Parameter(Mandatory = $true)][string]$WorkDir,
    [string]$SmokeScript = (Join-Path $PSScriptRoot 'Invoke-Smoke.ps1'),
    [switch]$KeepRunDir
)

$ErrorActionPreference = 'Continue'
$started = [DateTime]::UtcNow
$code = 1
$early = ''
try {
    & $SmokeScript -RepoRoot $RepoRoot -WorkDir $WorkDir -KeepRunDir:$KeepRunDir
    if ($null -ne $LASTEXITCODE) { $code = [int]$LASTEXITCODE } else { $code = 1 }
}
catch {
    $early = $_.Exception.Message.Replace("`r", ' ').Replace("`n", ' ')
    $code = 1
}

$results = $null
if (Test-Path -LiteralPath $WorkDir) {
    $results = Get-ChildItem -LiteralPath $WorkDir -Filter results.json -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.LastWriteTimeUtc -ge $started } |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
}

$status = 'FAIL'
$reason = ''
if ($null -eq $results) {
    $reason = 'results.json no existe para esta ejecución (excepción temprana, salida inesperada o crash).'
}
else {
    $json = $null
    try { $json = Get-Content -LiteralPath $results.FullName -Raw | ConvertFrom-Json }
    catch { $reason = 'results.json ilegible: ' + $_.Exception.Message }
    if ($null -eq $json) {
        $status = 'FAIL'
    }
    elseif ($code -notin @(0, 1, 3)) {
        $status = 'FAIL'; $reason = "Exit inesperado: $code."
    }
    elseif ($code -eq 0 -and $json.status -ne 'PASS') {
        $status = 'FAIL'; $reason = "Exit 0 pero results.json.status='$($json.status)'."
    }
    elseif ($code -eq 3 -and $json.status -ne 'BLOCKED') {
        $status = 'FAIL'; $reason = "Exit 3 pero results.json.status='$($json.status)'."
    }
    elseif ($code -eq 1 -and $json.status -ne 'FAIL') {
        $status = 'FAIL'; $reason = "Exit 1 pero results.json.status='$($json.status)'."
    }
    elseif ($code -eq 0) { $status = 'PASS' }
    elseif ($code -eq 3) { $status = 'BLOCKED'; $reason = [string]$json.blockedReason }
    else { $status = 'FAIL'; $reason = 'FAIL reportado por el smoke test.' }
}

if ($early) { $reason = ($reason + ' Excepción: ' + $early).Trim() }
Write-Host "SMOKE_STATUS=$status"
Write-Host "SMOKE_EXIT=$code"
if ($reason) { Write-Host "SMOKE_REASON=$reason" }
if ($env:GITHUB_STEP_SUMMARY) {
    Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value "### net10 smoke pilot: $status (exit $code)"
    if ($reason) { Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value "Motivo: $reason" }
    if ($null -ne $results) { Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value "Evidencia: $($results.DirectoryName)" }
}
if ($env:GITHUB_ENV) { "SMOKE_STATUS=$status" | Out-File -FilePath $env:GITHUB_ENV -Append }
if ($status -eq 'FAIL') { exit 1 }
exit 0
