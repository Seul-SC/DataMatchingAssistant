<#
.SYNOPSIS
    打包数据匹配助手的 Velopack 发布文件，可选上传到 GitHub Releases。

.DESCRIPTION
    在仓库根目录执行。脚本会：
    1. 从 csproj 读取版本号；
    2. 发布 Windows x64 自包含目录（不是单文件，供 Velopack 打包）；
    3. 尝试下载已有 GitHub Release 中的 Velopack 包，以便生成增量包；
    4. 使用 vpk pack 生成安装包、更新包和 releases.win.json；
    5. 若指定 -Upload，则使用 vpk upload github 发布。

    首次把 1.0.0 的 Inno 安装用户升级到 1.1.0 时，还没有旧的 Velopack 包，
    下载步骤可能会失败，这是正常的，脚本会继续打出完整包。

.EXAMPLE
    .\scripts\Publish-Release.ps1

.EXAMPLE
    .\scripts\Publish-Release.ps1 -Upload -GitHubToken $env:GITHUB_TOKEN
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$PackId = "DataMatchingAssistant",
    [string]$RepoUrl = "https://github.com/Seul-SC/DataMatchingAssistant",
    [string]$GitHubToken,
    [switch]$Upload,
    [switch]$SkipDownload
)

$ErrorActionPreference = "Stop"

function Assert-Command($Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "未找到命令：$Name"
    }
}

Assert-Command dotnet

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $repoRoot

$csproj = Join-Path $repoRoot "StudentScoreTool\StudentScoreTool.csproj"
if (-not (Test-Path $csproj)) {
    throw "未找到项目文件：$csproj"
}

[xml]$project = Get-Content $csproj
$version = $project.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "无法从 csproj 读取 <Version>。"
}

$tag = "v$version"
$publishDir = Join-Path $repoRoot "build\publish"
$outputDir = Join-Path $repoRoot "Releases"
$iconPath = Join-Path $repoRoot "StudentScoreTool\Assets\app.ico"
$notesPath = Join-Path $repoRoot "packaging\release-notes.md"
$mainExe = "DataMatchingAssistant.exe"

if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) {
    Write-Host "正在安装与项目一致的 vpk 1.2.161 ..."
    dotnet tool install -g vpk --version 1.2.161
}

$toolRoot = Join-Path $env:USERPROFILE ".dotnet\tools"
if (Test-Path $toolRoot) {
    $env:PATH = "$toolRoot;$env:PATH"
}

Assert-Command vpk

if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}

Write-Host "发布 $PackId $version 到 $publishDir"
dotnet publish $csproj `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish 失败。"
}

New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

if (-not $SkipDownload) {
    Write-Host "尝试从 GitHub Releases 下载已有 Velopack 包，以便生成增量更新..."
    $downloadArgs = @(
        "download", "github",
        "--repoUrl", $RepoUrl,
        "--outputDir", $outputDir,
        "--channel", "win"
    )
    if (-not [string]::IsNullOrWhiteSpace($GitHubToken)) {
        $downloadArgs += @("--token", $GitHubToken)
    }

    & vpk @downloadArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "下载已有 Release 失败（若这是第一个 Velopack 版本，可忽略）。将只生成完整更新包。"
    }
}

$packArgs = @(
    "pack",
    "--packId", $PackId,
    "--packVersion", $version,
    "--packDir", $publishDir,
    "--packTitle", "数据匹配助手",
    "--packAuthors", "Seul_SC_照晨",
    "--mainExe", $mainExe,
    "--icon", $iconPath,
    "--outputDir", $outputDir,
    "--channel", "win"
)

if (Test-Path $notesPath) {
    $packArgs += @("--releaseNotes", $notesPath)
}

Write-Host "正在使用 Velopack 打包..."
& vpk @packArgs
if ($LASTEXITCODE -ne 0) {
    throw "vpk pack 失败。"
}

Write-Host ""
Write-Host "打包完成。请把 Releases 目录中的这些文件上传到 GitHub Release $tag ："
Write-Host "  - releases.win.json            （必须，更新检查依赖此文件）"
Write-Host "  - $PackId-$version-full.nupkg  （必须，完整更新包）"
Write-Host "  - $PackId-$version-delta.nupkg （如有，增量更新包）"
Write-Host "  - $PackId-Setup.exe 或 $PackId-win-Setup.exe （新用户或 1.0.0 用户的安装程序）"
Write-Host "  - $PackId-Portable.zip         （可选，便携版）"
Write-Host "  - assets.win.json              （建议上传，vpk upload 会用到）"
Write-Host "  - RELEASES                     （可选，仅兼容旧版 Squirrel）"
Write-Host ""
Write-Host "版本号、安装包和 GitHub tag 必须一致，例如 $version / $tag 。"
Get-ChildItem $outputDir | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize

if ($Upload) {
    if ([string]::IsNullOrWhiteSpace($GitHubToken)) {
        throw "使用 -Upload 时必须提供 -GitHubToken，或先设置环境变量后再传入。"
    }

    Write-Host "正在上传到 GitHub Releases（$tag）..."
    & vpk upload github `
        --outputDir $outputDir `
        --channel win `
        --repoUrl $RepoUrl `
        --token $GitHubToken `
        --publish `
        --releaseName "数据匹配助手 $version" `
        --tag $tag

    if ($LASTEXITCODE -ne 0) {
        throw "vpk upload github 失败。"
    }

    Write-Host "上传完成。请在 GitHub 上确认 Release 已公开，并且包含 releases.win.json。"
}
