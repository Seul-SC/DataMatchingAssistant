# 数据匹配助手

一个本地运行的 Windows 表格数据匹配工具。选择包含成绩的 Excel 工作簿和需要填写的名单工作簿，软件会匹配学生，并将成绩写入名单表已有的成绩列。

当前版本：1.2.0  
作者：Seul_SC_照晨  
仓库：https://github.com/Seul-SC/DataMatchingAssistant  
Gitee：https://gitee.com/seul-sc/DataMatchingAssistant

## 功能

- 两边都有学号时优先按学号匹配；某边没有学号时再按姓名。重名时结合学号、性别判断，无法确认的记录不会盲目写入。
- 支持多个工作表、多个科目或多次作业，以及数字和文字成绩。数字成绩按 0～150 检查，超出范围仍写入并在问题窗列出。
- 保留目标表的学生顺序、格式、公式和无关工作表，不新增成绩列。
- 源成绩为空时保留目标单元格原值，不填入。只有同一作业既有成绩又有空格时，才在问题窗列出；若该作业所有学生都为空，则不列为问题。公式不会被覆盖。
- 名单里已有不同成绩时，写入前先确认是否覆盖。学号相同但姓名不同时仍按学号写入，并在结束后列出差异。
- 默认在覆盖目标文件前创建带时间戳的备份。
- 目标文件被占用时，可另存为“已填写”副本。
- 支持将 `.xlsx`、`.xls` 或 `.csv` 文件拖入窗口，选中后突出显示文件名，并记住上次使用的路径。名单若是 `.xls` 或 `.csv`，结果会另存为 `.xlsx`，不改动原文件。
- 处理顺利时弹出成功提示；有问题时列出清单，关闭后可在主界面再次打开。处理完成后也可打开结果所在文件夹。
- 可在浅色、薄荷绿、同行、涟漪、深色五种主题之间切换（鼠标悬停「外观」即可挑选），并记住上次选择。
- 「关于」中可查看作者和 GitHub 链接。有问题或建议，可发邮件至 1753534364@qq.com，或在 GitHub Issues 留言。
- 所有表格均在本地处理，不上传学生数据，不需要 API 密钥。

## 使用

1. 选择包含成绩的源表格文件，或将 `.xlsx`、`.xls`、`.csv` 文件拖到对应区域。
2. 选择需要填写的目标名单文件。
3. 如果有多个候选工作表，手动确认源工作表和目标工作表。
4. 保持“覆盖前备份”开启，点击开始处理。
5. 如果名单里已有不同成绩，先确认是否覆盖；处理结束后如有问题，可在弹窗中查看，关闭后也能再次打开。
6. 处理完成后，可点击“打开所在文件夹”。

支持 `.xlsx`、`.xls` 和 `.csv` 文件，适用于 Windows 10/11 x64。

## 检查更新

从 1.2.0 起，软件使用 [Velopack](https://docs.velopack.io/) 从 Gitee Releases 检查和安装后续更新。更新源为：

`https://gitee.com/seul-sc/DataMatchingAssistant/releases/download/latest`

请在该地址对应的 `latest` Release 中放好 `releases.win.json` 和更新包。已安装 1.1.0 的用户仍然从本仓库的 GitHub Releases 检查更新，因此每次发版也要把同样的更新文件发到 GitHub。

- 启动时只在后台检查，如果有新版本，会在「检查更新」按钮上显示小红点，不会自动弹出窗口。
- 点击「检查更新」后：如果已是最新版本，会直接提示；如果有新版本，会显示更新说明，再由用户点击「立即更新」，软件会下载更新包、安装并重启。
- 检查更新时只向 Gitee 查询版本信息和安装包地址，不会上传学生表格。
- 已经安装 1.0.0（Inno Setup 安装包）的用户，需要先手动安装一次 1.1.0 或 1.2.0 的 Velopack 安装程序。之后即可在软件内一键更新。
- 已经安装 1.1.0 的用户，在软件里更新到 1.2.0 后，仍会留在原来的文件夹（通常是 `%LocalAppData%\DataMatchingAssistant`）。如果要换到其他磁盘，需要再运行一次 1.2.0 的 MSI。
- 直接用 `dotnet run` 或在开发目录里运行时，不是已安装版本，无法在线更新。

## 开发环境

- .NET SDK 10.0
- C# / WPF
- ClosedXML
- ExcelDataReader（用于读取 `.xls`）
- Velopack 1.2.161

还原依赖并运行测试：

```powershell
dotnet restore StudentScoreTool.slnx
dotnet test StudentScoreTool.slnx
```

运行应用：

```powershell
dotnet run --project StudentScoreTool/StudentScoreTool.csproj
```

主题、上次打开的文件路径保存在 `%LocalAppData%\DataMatchingAssistant\settings.json`，不会放在安装目录的 `current` 文件夹里，以免更新时被覆盖。

## 如何发布新版本

发布前，请把下面三处的版本号改成同一个值，例如软件版本 `1.2.0`，GitHub / Gitee 标签 `v1.2.0`：

1. `StudentScoreTool/StudentScoreTool.csproj` 中的 `<Version>`（以及 `AssemblyVersion`、`FileVersion`）
2. `packaging/release-notes.md` 中的更新说明（会写入更新包，并显示在「检查更新」窗口）
3. 即将创建的 GitHub Release 标签，例如 `v1.2.0`；同时在 Gitee 创建对应版本 Release，并更新名为 `latest` 的 Release

### 本机打包

在 Windows 上、仓库根目录执行：

```powershell
# 如尚未安装与项目一致的命令行工具
dotnet tool install -g vpk --version 1.2.161

# 只打包，稍后手动上传
.\scripts\Publish-Release.ps1

# 或打包并直接发布到 GitHub（需要有仓库写入权限的 token）
.\scripts\Publish-Release.ps1 -Upload -GitHubToken $env:GITHUB_TOKEN
```

脚本会执行 `dotnet publish`（Windows x64 自包含目录，**不要**再用单文件发布），然后调用 `vpk pack --msi --instLocation Either`。如果 GitHub 上已有旧的 Velopack 包，还会先下载以便生成增量包。脚本不会上传 Setup.exe 或便携版 zip。Gitee 发布需要另行手动完成，不要把 Gitee 令牌写进仓库。

也可以手工执行同样的步骤：

```powershell
dotnet publish StudentScoreTool/StudentScoreTool.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=false `
  -o build/publish

vpk pack `
  --packId DataMatchingAssistant `
  --packVersion 1.2.0 `
  --packDir .\build\publish `
  --packTitle 数据匹配助手 `
  --packAuthors Seul_SC_照晨 `
  --mainExe DataMatchingAssistant.exe `
  --icon .\StudentScoreTool\Assets\app.ico `
  --releaseNotes .\packaging\release-notes.md `
  --outputDir .\Releases `
  --channel win `
  --msi `
  --instLocation Either
```

### 必须上传的文件

`vpk pack` 会在 `Releases` 目录中生成一组文件。请把它们上传到 GitHub 对应标签的 Release，**尤其不要漏掉更新源文件**；同时把同样的更新文件和 MSI 发布到 Gitee 的版本 Release 以及 `latest` Release：

| 文件 | 是否必须 | 说明 |
| --- | --- | --- |
| `releases.win.json` | 必须 | Velopack 发现新版本的唯一依据。缺少它时，用户检查更新会认为没有新版本。 |
| `DataMatchingAssistant-1.2.0-full.nupkg` | 必须 | 完整更新包。文件名中的版本号随每次发布变化。 |
| `DataMatchingAssistant-1.2.0-delta.nupkg` | 建议 | 增量更新包。只有本地同时有上一版完整包时才会生成。 |
| `DataMatchingAssistant-1.2.0-Setup.msi` | 必须（给新用户） | 对外安装程序。请使用 MSI，不要再把 Setup.exe 或 Portable.zip 作为安装程序发布。 |
| `assets.win.json` | 建议 | 供 `vpk upload` 识别本次要上传的文件。手动上传时也可以一并带上。 |
| `RELEASES` | 建议 | 部分更新源仍会用到，建议一并上传。 |

如果使用 `.\scripts\Publish-Release.ps1 -Upload` 或下面的 GitHub Actions，这些文件会由 `vpk upload github` 自动上传并发布。请确认 Release 不是草稿，否则已安装的用户看不到更新。不要把 Setup.exe 或 Portable.zip 作为对外安装程序上传。

上传后，已安装 1.1.0 的用户可从 GitHub 检查到 1.2.0；从 1.2.0 起，软件会改向 Gitee 检查后续版本。

### 使用 GitHub Actions

仓库包含 `.github/workflows/release.yml`。它会在推送 `v*` 标签，或在 Actions 中手动运行时：

1. 从 csproj 读取版本号（若由标签触发，还会校验标签与版本一致）
2. 发布 Windows 自包含目录
3. 尝试下载上一版 Velopack 包
4. 使用 `vpk` 1.2.161 打包，并加上 `--msi --instLocation Either`
5. 移走 Setup.exe 和 Portable.zip 后，用 `GITHUB_TOKEN` 上传并发布 GitHub Release

工作流已授予 `contents: write` 权限，不需要额外密钥。Gitee 发布需要另外手动完成。

### 关于旧的 Inno Setup 安装脚本

`Installer/DataMatchingAssistant.iss` 仍保留，供参考，但 **已不再作为受支持的安装方式**。1.2.0 起请改用 Velopack MSI。用 Inno 安装的版本无法使用软件内的一键更新。

`build`、`dist`、`release`、`Releases`、`publish` 和各项目的 `bin`、`obj` 目录不会提交到源码仓库。

## 项目结构

```text
StudentScoreTool/          WPF 应用、主题、更新和 Excel 处理逻辑
StudentScoreTool.Tests/    自动化测试
Installer/                 旧的 Inno Setup 安装脚本（已不再作为推荐发布方式）
packaging/                 发给用户的更新说明
scripts/                   本机打包脚本
.github/workflows/         使用 GITHUB_TOKEN 发布 Velopack 资源的工作流
StudentScoreTool.slnx      解决方案
```
