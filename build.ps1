param(
    [string]$GameDir = "D:\steam\steamapps\common\WitchSpring R",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path (Join-Path $GameDir "BepInEx\core\BepInEx.Core.dll"))) {
    Write-Error "找不到 BepInEx：$GameDir\BepInEx\core\BepInEx.Core.dll"
}

Write-Host "游戏目录: $GameDir"

dotnet build (Join-Path $PSScriptRoot "src\WSRLetterBoxFix.csproj") `
    -c $Configuration `
    -p:GameDir="$GameDir"

if ($LASTEXITCODE -eq 0) {
    $out = Join-Path $GameDir "BepInEx\plugins\WSRLetterBoxFix\WSRLetterBoxFix.dll"
    if (Test-Path $out) {
        Write-Host "已输出: $out"
    }
}
