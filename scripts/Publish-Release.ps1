<#
.SYNOPSIS
    在本机（Windows）打包数据匹配助手 1.3.0 起的发布文件，不上传任何东西。

.DESCRIPTION
    在仓库根目录执行。脚本会：
    1. 从 csproj 读取版本号；
    2. 发布 Windows x64 自包含目录（build\publish），用 Inno Setup 打出
       release\DataMatchingAssistant-<版本>-Setup.exe（安装时可点「浏览」自选目录）；
    3. 生成 release\version.json（软件检查更新时读取，含 sha256 和大小）；
    4. 除非指定 -SkipBridge，再用 -p:VelopackBridge=true 发布一份「过渡包」并用 vpk 打成
       Releases\releases.win.json + Releases\DataMatchingAssistant-<版本>-full.nupkg，
       只给 1.1.0 / 1.2.0（Velopack 安装）的老用户做最后一次软件内更新，引导他们改装 Inno 版本。

    上传由人工或 GitHub Actions（推送 v* 标签）完成，见 README「如何发布新版本」。
    本脚本不会使用任何 GitHub / Gitee 令牌。

.EXAMPLE
    .\scripts\Publish-Release.ps1

.EXAMPLE
    .\scripts\Publish-Release.ps1 -SkipBridge -IsccPath "D:\Tools\Inno Setup 6\ISCC.exe"
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$PackId = "DataMatchingAssistant",
    [string]$IsccPath,
    [switch]$SkipTests,
    [switch]$SkipBridge
)

$ErrorActionPreference = "Stop"

function Assert-Command($Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "未找到命令：$Name"
    }
}

function Invoke-Publish([string]$OutputDir, [string[]]$ExtraArgs) {
    if (Test-Path $OutputDir) {
        Remove-Item $OutputDir -Recurse -Force
    }
    & dotnet publish $csproj -c $Configuration -r $Runtime --self-contained true -p:PublishSingleFile=false -o $OutputDir @ExtraArgs
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish 失败。"
    }
}

Assert-Command dotnet

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $repoRoot

$csproj = Join-Path $repoRoot "StudentScoreTool\StudentScoreTool.csproj"
[xml]$project = Get-Content $csproj
$version = $project.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "无法从 csproj 读取 <Version>。"
}

$publishDir = Join-Path $repoRoot "build\publish"
$bridgePublishDir = Join-Path $repoRoot "build\publish-bridge"
$releaseDir = Join-Path $repoRoot "release"
$bridgeOutputDir = Join-Path $repoRoot "Releases"
$installer = Join-Path $releaseDir "$PackId-$version-Setup.exe"

if (-not $SkipTests) {
    Write-Host "运行测试..."
    & dotnet test (Join-Path $repoRoot "StudentScoreTool.slnx") -c $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "测试没有通过。"
    }
}

if ([string]::IsNullOrWhiteSpace($IsccPath)) {
    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
    )
    $IsccPath = $candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
}
if ([string]::IsNullOrWhiteSpace($IsccPath) -or -not (Test-Path $IsccPath)) {
    throw "未找到 Inno Setup 6 的 ISCC.exe。请先安装 Inno Setup 6.5 或更新版本（https://jrsoftware.org/isdl.php），或用 -IsccPath 指定。"
}

Write-Host "发布 $PackId $version 到 $publishDir"
Invoke-Publish $publishDir @()

New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
Write-Host "使用 Inno Setup 打包..."
& $IsccPath "/DAppVersion=$version" "/DPublishDir=$publishDir" "/DOutputDir=$releaseDir" (Join-Path $repoRoot "Installer\DataMatchingAssistant.iss")
if ($LASTEXITCODE -ne 0) {
    throw "ISCC 编译失败。"
}

& (Join-Path $PSScriptRoot "New-UpdateManifest.ps1") -Version $version -Installer $installer -OutFile (Join-Path $releaseDir "version.json")

if (-not $SkipBridge) {
    if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) {
        Write-Host "正在安装与项目一致的 vpk 1.2.161 ..."
        dotnet tool install -g vpk --version 1.2.161
        $toolRoot = Join-Path $env:USERPROFILE ".dotnet\tools"
        if (Test-Path $toolRoot) {
            $env:PATH = "$toolRoot;$env:PATH"
        }
    }
    Assert-Command vpk

    Write-Host "发布 Velopack 过渡包到 $bridgePublishDir"
    Invoke-Publish $bridgePublishDir @("-p:VelopackBridge=true")

    if (Test-Path $bridgeOutputDir) {
        Remove-Item $bridgeOutputDir -Recurse -Force
    }
    # 关闭增量包：zstd 在处理中文文件名（如 数据匹配助手_ExecutionStub.exe）时会失败。
    & vpk pack `
        --packId $PackId `
        --packVersion $version `
        --packDir $bridgePublishDir `
        --packTitle 数据匹配助手 `
        --packAuthors Seul_SC_照晨 `
        --mainExe DataMatchingAssistant.exe `
        --icon (Join-Path $repoRoot "StudentScoreTool\Assets\app.ico") `
        --releaseNotes (Join-Path $repoRoot "packaging\release-notes.md") `
        --outputDir $bridgeOutputDir `
        --channel win `
        --delta none
    if ($LASTEXITCODE -ne 0) {
        throw "vpk pack 失败。"
    }

    # 过渡包只用于软件内更新，不对外提供 Setup.exe / Portable.zip。
    Get-ChildItem -Path $bridgeOutputDir -File | Where-Object {
        $_.Name -like "*Setup.exe" -or $_.Name -like "*Portable.zip"
    } | Remove-Item -Force
}

Write-Host ""
Write-Host "打包完成："
Write-Host "  release\$PackId-$version-Setup.exe   （给所有用户的安装包）"
Write-Host "  release\version.json                 （发版时放到仓库 update\version.json）"
if (-not $SkipBridge) {
    Write-Host "  Releases\releases.win.json           （过渡包，给 1.1.0 / 1.2.0 老用户）"
    Write-Host "  Releases\$PackId-$version-full.nupkg"
}
Get-ChildItem $releaseDir | Select-Object Name, Length | Format-Table -AutoSize
