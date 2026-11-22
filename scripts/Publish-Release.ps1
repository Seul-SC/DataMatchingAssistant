<#
.SYNOPSIS
    打包数据匹配助手的 Velopack 发布文件，可选上传到 GitHub Releases。

.DESCRIPTION
    在仓库根目录执行。脚本会：
    1. 从 csproj 读取版本号；
    2. 发布 Windows x64 自包含目录（不是单文件，供 Velopack 打包）；
    3. 尝试下载已有 GitHub Release 中的 Velopack 包，以便生成增量包；
    4. 使用 vpk pack --msi --instLocation Either 生成 MSI 安装包、更新包和 releases.win.json；
    5. 从待上传目录中去掉 Setup.exe 和 Portable.zip（对外只提供 MSI 安装程序）；
    6. 若指定 -Upload，则使用 vpk upload github 发布。

    软件内更新源从 1.2.0 起改为 Gitee。本脚本仍把更新包上传到 GitHub，
    以便已安装 1.1.0 的用户能检查到 1.2.0。请在打包完成后，再把同样的
    更新文件（至少 releases.win.json 与 full/delta nupkg）以及 MSI
    发布到 Gitee 仓库 https://gitee.com/seul-sc/DataMatchingAssistant ：
    既要有版本标签（如 v1.2.0），也要维护名为 latest 的 Release，
    因为应用会请求
    https://gitee.com/seul-sc/DataMatchingAssistant/releases/download/latest/releases.win.json
    本脚本不会使用或写入任何 Gitee 令牌。

    首次把 1.0.0 的 Inno 安装用户升级到 Velopack 时，还没有旧的 Velopack 包，
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

function Prepare-ReleaseAssets {
    param(
        [Parameter(Mandatory = $true)][string]$OutputDir,
        [Parameter(Mandatory = $true)][string]$PackId,
        [Parameter(Mandatory = $true)][string]$Version
    )

    $unpublishedDir = Join-Path $OutputDir "unpublished-installers"
    New-Item -ItemType Directory -Path $unpublishedDir -Force | Out-Null

    Get-ChildItem -Path $OutputDir -File | Where-Object {
        $_.Name -like "*Setup.exe" -or $_.Name -like "*Portable.zip"
    } | ForEach-Object {
        Write-Host "不作为对外安装程序发布，已移走：$($_.Name)"
        Move-Item -Path $_.FullName -Destination (Join-Path $unpublishedDir $_.Name) -Force
    }

    $msi = Get-ChildItem -Path $OutputDir -File -Filter "*.msi" | Select-Object -First 1
    $advertisedName = "$PackId-$Version-Setup.msi"
    if ($msi -and $msi.Name -ne $advertisedName) {
        $destination = Join-Path $OutputDir $advertisedName
        Copy-Item -Path $msi.FullName -Destination $destination -Force
        Write-Host "已复制 MSI 为 $advertisedName"
    }

    $assetsPath = Join-Path $OutputDir "assets.win.json"
    if (Test-Path $assetsPath) {
        $raw = Get-Content -Path $assetsPath -Raw -Encoding UTF8
        foreach ($name in @("Setup.exe", "Portable.zip")) {
            $raw = [regex]::Replace(
                $raw,
                '(?s),\s*\{[^{}]*' + [regex]::Escape($name) + '[^{}]*\}',
                "")
            $raw = [regex]::Replace(
                $raw,
                '(?s)\{\s*[^{}]*' + [regex]::Escape($name) + '[^{}]*\}\s*,',
                "")
        }
        Set-Content -Path $assetsPath -Value $raw -Encoding UTF8
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
    "--channel", "win",
    "--msi",
    "--instLocation", "Either"
)

if (Test-Path $notesPath) {
    $packArgs += @("--releaseNotes", $notesPath)
}

Write-Host "正在使用 Velopack 打包（含 MSI）..."
& vpk @packArgs
if ($LASTEXITCODE -ne 0) {
    throw "vpk pack 失败。"
}

Prepare-ReleaseAssets -OutputDir $outputDir -PackId $PackId -Version $version

Write-Host ""
Write-Host "打包完成。请把 Releases 目录中的这些文件上传到 GitHub Release $tag（1.1.0 用户仍从 GitHub 更新）："
Write-Host "  - releases.win.json                 （必须，更新检查依赖此文件）"
Write-Host "  - $PackId-$version-full.nupkg       （必须，完整更新包）"
Write-Host "  - $PackId-$version-delta.nupkg      （如有，增量更新包）"
Write-Host "  - $PackId-$version-Setup.msi        （必须，给新用户的安装程序；不要上传 Setup.exe）"
Write-Host "  - RELEASES                          （如有，建议一并上传）"
Write-Host "  - assets.win.json                   （建议上传，vpk upload 会用到）"
Write-Host ""
Write-Host "请同时把上述更新文件和 MSI 发布到 Gitee 的版本 Release，以及名为 latest 的 Release。"
Write-Host "不要上传 Setup.exe 或 Portable.zip 作为对外安装程序。"
Write-Host "版本号、安装包和 GitHub / Gitee tag 必须一致，例如 $version / $tag 。"
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

    Write-Host "上传完成。请在 GitHub 上确认 Release 已公开，并且包含 releases.win.json 和 MSI，且没有把 Setup.exe / Portable.zip 作为安装程序。"
}
