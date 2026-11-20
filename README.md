# 数据匹配助手

一个本地运行的 Windows 表格数据匹配工具。选择包含成绩的 Excel 工作簿和需要填写的名单工作簿，软件会匹配学生，并将成绩写入名单表已有的成绩列。

当前版本：1.1.0  
作者：Seul_SC_照晨  
仓库：https://github.com/Seul-SC/DataMatchingAssistant

## 功能

- 按姓名匹配学生；重名时结合学号、性别判断，无法确认的记录不会盲目写入。
- 支持多个工作表、多个科目或多次作业，以及数字和文字成绩。
- 保留目标表的学生顺序、格式、公式和无关工作表，不新增成绩列。
- 源成绩为空时保留目标单元格原值，公式不会被覆盖。
- 写入前列出异常记录，用户可以取消或跳过异常继续。
- 默认在覆盖目标文件前创建带时间戳的备份。
- 目标文件被占用时，可另存为“已填写”副本。
- 支持将 `.xlsx` 文件拖入窗口，选中后突出显示文件名，并记住上次使用的路径。
- 处理完成后可打开结果所在文件夹。
- 可在浅色、深色、薄荷绿、深薄荷四种主题之间切换，并记住上次选择。
- 「关于」中可查看作者、GitHub 链接，以及自愿的「赞赏作者」入口。不赞赏也可以使用全部功能。
- 所有表格均在本地处理，不上传学生数据，不需要 API 密钥。

## 使用

1. 选择包含成绩的源 Excel 文件，或将 `.xlsx` 文件拖到对应区域。
2. 选择需要填写的目标名单 Excel 文件。
3. 如果有多个候选工作表，手动确认源工作表和目标工作表。
4. 保持“覆盖前备份”开启，点击开始处理。
5. 如果出现异常记录，确认后再继续写入。
6. 处理完成后，可点击“打开所在文件夹”。

支持 `.xlsx` 文件，适用于 Windows 10/11 x64。

## 检查更新

从 1.1.0 起，软件使用 [Velopack](https://docs.velopack.io/) 从本仓库的 GitHub Releases 检查和安装更新。

- 启动时只在后台检查，如果有新版本，会在「检查更新」按钮上显示小红点，不会自动弹出窗口。
- 点击「检查更新」后：如果已是最新版本，会直接提示；如果有新版本，会显示更新说明，再由用户点击「立即更新」，软件会下载更新包、安装并重启。
- 检查更新时只向 GitHub 查询版本信息和安装包地址，不会上传学生表格。
- 已经安装 1.0.0（Inno Setup 安装包）的用户，需要先手动安装一次 1.1.0 的 Velopack 安装程序。之后即可在软件内一键更新。
- 1.0.0 默认装在 `%LocalAppData%\Programs\DataMatchingAssistant`，1.1.0 起 Velopack 会装到 `%LocalAppData%\DataMatchingAssistant`。装好新版本后，可在 Windows 设置中卸载旧的 1.0.0。
- 直接用 `dotnet run` 或在开发目录里运行时，不是已安装版本，无法在线更新。

## 开发环境

- .NET SDK 10.0
- C# / WPF
- ClosedXML
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

### 放入赞赏码

关于窗口已为微信、支付宝赞赏码留好位置。把图片分别保存为：

- `StudentScoreTool/Assets/tips-wechat.png`
- `StudentScoreTool/Assets/tips-alipay.png`

项目会在文件存在时自动打包进软件。请使用微信 / 支付宝的「赞赏码」，不要用普通个人收款码。文案请使用「赞赏作者」「请作者喝杯咖啡」「支持作者」，不要使用「捐款」「募捐」「公益」。

## 如何发布新版本

发布前，请把下面三处的版本号改成同一个值，例如软件版本 `1.1.0`，GitHub 标签 `v1.1.0`：

1. `StudentScoreTool/StudentScoreTool.csproj` 中的 `<Version>`（以及 `AssemblyVersion`、`FileVersion`）
2. `packaging/release-notes.md` 中的更新说明（会写入更新包，并显示在「检查更新」窗口）
3. 即将创建的 GitHub Release 标签，例如 `v1.1.0`

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

脚本会执行 `dotnet publish`（Windows x64 自包含目录，**不要**再用单文件发布），然后调用 `vpk pack`。如果 GitHub 上已有旧的 Velopack 包，还会先下载以便生成增量包。

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
  --packVersion 1.1.0 `
  --packDir .\build\publish `
  --packTitle 数据匹配助手 `
  --packAuthors Seul_SC_照晨 `
  --mainExe DataMatchingAssistant.exe `
  --icon .\StudentScoreTool\Assets\app.ico `
  --releaseNotes .\packaging\release-notes.md `
  --outputDir .\Releases `
  --channel win
```

### 必须上传到 GitHub Release 的文件

`vpk pack` 会在 `Releases` 目录中生成一组文件。请把它们上传到对应标签的 Release 中，**尤其不要漏掉更新源文件**：

| 文件 | 是否必须 | 说明 |
| --- | --- | --- |
| `releases.win.json` | 必须 | Velopack 发现新版本的唯一依据。缺少它时，用户检查更新会认为没有新版本。 |
| `DataMatchingAssistant-1.1.0-full.nupkg` | 必须 | 完整更新包。文件名中的版本号随每次发布变化。 |
| `DataMatchingAssistant-1.1.0-delta.nupkg` | 建议 | 增量更新包。只有本地同时有上一版完整包时才会生成。 |
| `DataMatchingAssistant-Setup.exe` 或 `DataMatchingAssistant-win-Setup.exe` | 必须（给新用户） | 新用户和仍在使用 1.0.0 的用户需要运行这个安装程序。实际文件名以 `vpk pack` 输出为准。 |
| `DataMatchingAssistant-Portable.zip` | 可选 | 便携版。 |
| `assets.win.json` | 建议 | 供 `vpk upload` 识别本次要上传的文件。手动上传时也可以一并带上。 |
| `RELEASES` | 可选 | 仅用于兼容旧的 Squirrel 更新源，本项目可以忽略。 |

如果使用 `.\scripts\Publish-Release.ps1 -Upload` 或下面的 GitHub Actions，这些文件会由 `vpk upload github` 自动上传并发布。请确认 Release 不是草稿，否则已安装的用户看不到更新。

上传后，已安装 1.1.0 及以后版本的用户启动软件时会在后台看到红点，点击「检查更新」即可一键更新。

### 使用 GitHub Actions

仓库包含 `.github/workflows/release.yml`。它会在推送 `v*` 标签，或在 Actions 中手动运行时：

1. 从 csproj 读取版本号（若由标签触发，还会校验标签与版本一致）
2. 发布 Windows 自包含目录
3. 尝试下载上一版 Velopack 包
4. 使用 `vpk` 1.2.161 打包
5. 用 `GITHUB_TOKEN` 上传并发布 Release

工作流已授予 `contents: write` 权限，不需要额外密钥。第一次发布 1.1.0 时，仓库里还没有旧的 Velopack 包，下载上一步可能会失败，可以忽略。

### 关于旧的 Inno Setup 安装脚本

`Installer/DataMatchingAssistant.iss` 仍保留，供参考，但 **1.1.0 起请改用 Velopack 安装程序**。用 Inno 安装的版本无法使用软件内的一键更新。

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
