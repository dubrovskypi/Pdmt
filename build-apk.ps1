$envFile = Join-Path $PSScriptRoot ".build.local.env"
if (-not (Test-Path $envFile)) {
    Write-Error ".build.local.env not found. Copy .build.local.env.example and fill in values."
    exit 1
}

foreach ($line in Get-Content $envFile) {
    if ($line -match '^\s*#' -or $line -notmatch '=') { continue }
    $key, $value = $line -split '=', 2
    Set-Variable -Name $key.Trim() -Value $value.Trim()
}

if (-not $MAUI_API_URL)   { Write-Error "MAUI_API_URL is not set in .build.local.env";   exit 1 }
if (-not $STORE_PASS)     { Write-Error "STORE_PASS is not set in .build.local.env";     exit 1 }
if (-not $KEYSTORE_PATH)  { Write-Error "KEYSTORE_PATH is not set in .build.local.env";  exit 1 }

$appSettingsPath = Join-Path $PSScriptRoot "Pdmt.Maui/appsettings.json"
$original = [System.IO.File]::ReadAllText($appSettingsPath)
$patched = @{ PdmtApi = @{ BaseUrl = $MAUI_API_URL } } | ConvertTo-Json
[System.IO.File]::WriteAllText($appSettingsPath, $patched)

try {
    Write-Host "Building APK with API URL: $MAUI_API_URL" -ForegroundColor Green

    dotnet publish Pdmt.Maui/Pdmt.Maui.csproj `
        -f net8.0-android `
        -c Release `
        -p:AndroidSigningKeyStore=$KEYSTORE_PATH `
        -p:AndroidSigningKeyAlias=pdmt `
        -p:AndroidSigningStorePass=$STORE_PASS `
        -p:AndroidSigningKeyPass=$STORE_PASS

    if ($LASTEXITCODE -eq 0) {
        Write-Host "APK built successfully" -ForegroundColor Green
    } else {
        Write-Host "Build failed" -ForegroundColor Red
        exit 1
    }
} finally {
    [System.IO.File]::WriteAllText($appSettingsPath, $original)
}
