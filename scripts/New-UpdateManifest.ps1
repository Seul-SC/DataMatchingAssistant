<#
.SYNOPSIS
    根据打好的 Inno 安装包生成 update/version.json 的内容（软件检查更新时读取）。

.EXAMPLE
    .\scripts\New-UpdateManifest.ps1 -Version 1.3.0 -Installer .\release\DataMatchingAssistant-1.3.0-Setup.exe -OutFile .\release\version.json
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string]$Installer,
    [Parameter(Mandatory = $true)][string]$OutFile,
    [string]$NotesFile = (Join-Path $PSScriptRoot "..\packaging\release-notes.md"),
    [string]$GiteeRepo = "https://gitee.com/seul-sc/DataMatchingAssistant",
    [string]$GitHubRepo = "https://github.com/Seul-SC/DataMatchingAssistant"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $Installer)) {
    throw "未找到安装包：$Installer"
}

$fileName = Split-Path $Installer -Leaf
$hash = (Get-FileHash -Path $Installer -Algorithm SHA256).Hash.ToLowerInvariant()
$size = (Get-Item $Installer).Length

$notes = ""
if (Test-Path $NotesFile) {
    # 去掉 Markdown 标题行，只保留条目。
    $lines = Get-Content -Path $NotesFile -Encoding UTF8 | Where-Object { $_ -notmatch '^\s*#' }
    $notes = ($lines -join "`n").Trim()
}

$manifest = [ordered]@{
    version     = $Version
    notes       = $notes
    url         = "$GiteeRepo/releases/download/v$Version/$fileName"
    fallbackUrl = "$GitHubRepo/releases/download/v$Version/$fileName"
    sha256      = $hash
    size        = $size
}

$directory = Split-Path -Parent $OutFile
if ([string]::IsNullOrWhiteSpace($directory)) {
    $directory = (Get-Location).Path
}
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$fullPath = Join-Path (Resolve-Path -LiteralPath $directory).Path (Split-Path $OutFile -Leaf)

$json = ($manifest | ConvertTo-Json -Depth 3) + "`n"
[System.IO.File]::WriteAllText($fullPath, $json, (New-Object System.Text.UTF8Encoding $false))
Write-Host "已生成 $OutFile（sha256=$hash，size=$size）"
