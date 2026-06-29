param(
    [string]$SourceApk = "MobileApp/StreetFoodNarrator.App/bin/Release/net10.0-android36.0/com.streetfood.narrator-Signed.apk",
    [string]$PublicBaseUrl = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $repoRoot

if (-not (Test-Path $SourceApk)) {
    throw "Source APK not found: $SourceApk"
}

$canonicalUploadsDir = "API/StreetFoodNarrator.API/Uploads"
$legacyMirrorDir = "API/StreetFoodNarrator.API/wwwroot/uploads"
$publishDir = "publish/apk"

New-Item -ItemType Directory -Path $canonicalUploadsDir -Force | Out-Null
New-Item -ItemType Directory -Path $legacyMirrorDir -Force | Out-Null
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

$canonicalApk = Join-Path $canonicalUploadsDir "streetfood-narrator.apk"
$legacyMirrorApk = Join-Path $legacyMirrorDir "streetfood-narrator.apk"
$timestamp = Get-Date -Format "yyyyMMdd-HHmm"
$timestampedApkName = "streetfood-latest-$timestamp.apk"
$timestampedUploadsApk = Join-Path $canonicalUploadsDir $timestampedApkName
$timestampedPublishApk = Join-Path $publishDir $timestampedApkName

Copy-Item $SourceApk $canonicalApk -Force
Copy-Item $SourceApk $legacyMirrorApk -Force
Copy-Item $SourceApk $timestampedUploadsApk -Force
Copy-Item $SourceApk $timestampedPublishApk -Force

$hash = (Get-FileHash $canonicalApk -Algorithm SHA256).Hash
$size = (Get-Item $canonicalApk).Length

Write-Output "PUBLISHED_CANONICAL_APK=$canonicalApk"
Write-Output "PUBLISHED_TIMESTAMPED_APK=$timestampedPublishApk"
Write-Output "APK_SIZE=$size"
Write-Output "APK_SHA256=$hash"

if (-not [string]::IsNullOrWhiteSpace($PublicBaseUrl)) {
    $base = $PublicBaseUrl.TrimEnd('/')
    Write-Output "PUBLIC_CANONICAL_URL=$base/uploads/streetfood-narrator.apk"
    Write-Output "PUBLIC_TIMESTAMPED_URL=$base/uploads/$timestampedApkName"
}
