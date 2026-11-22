# 数据匹配助手

一个本地运行的 Windows 表格数据匹配工具。选择包含成绩的 Excel 工作簿和需要填写的名单工作簿，软件会匹配学生，并将成绩写入名单表已有的成绩列。

当前版本：1.3.0  
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

## 安装

从 [Gitee 发行版](https://gitee.com/seul-sc/DataMatchingAssistant/releases)（国内更快）或 [GitHub Releases](https://github.com/Seul-SC/DataMatchingAssistant/releases) 下载 `DataMatchingAssistant-<版本>-Setup.exe`，双击运行：

1. 开头可以选择「只为我安装」（不需要管理员权限）或「为所有用户安装」。
2. 在「选择安装位置」这一页点「浏览」，可以装到任意文件夹，例如 `D:\数据匹配助手`。
3. 可以勾选创建桌面快捷方式。安装完成后即可启动。

以后再运行新版安装包（包括软件内更新）时，会自动沿用上次选择的文件夹。卸载可在「设置 → 应用」中找到「数据匹配助手」。

## 检查更新

从 1.3.0 起，软件不再使用 Velopack，而是读取 Gitee 仓库中的 [`update/version.json`](update/version.json) 来检查更新：

`https://gitee.com/seul-sc/DataMatchingAssistant/raw/main/update/version.json`

（Gitee 无法访问时，改读 GitHub 仓库里的同一个文件。）

- 启动时只在后台检查，如果有新版本，会在「检查更新」按钮上显示小红点，不会自动弹出窗口。
- 点击「检查更新」后：如果已是最新版本，会直接提示；如果有新版本，会显示版本号和更新说明，点「更新」后软件把新的安装包下载到临时文件夹，校验 SHA-256 后打开安装向导，并关闭自己。安装向导会沿用原来的安装目录，按提示完成即可。
- 网络不通、文件校验不通过时，会给出中文提示，也可以到 Gitee 发行版页手动下载。
- 检查更新时只向 Gitee（或 GitHub）读取版本信息和安装包，不会上传学生表格。

### 从 1.1.0 / 1.2.0 升级

1.1.0 和 1.2.0 是用 Velopack 安装的，它们只能接收 Velopack 格式的更新：1.1.0 从 GitHub Releases 检查，1.2.0 从 Gitee 的 `latest` 发行版检查。为了让这些用户不用手动卸载，1.3.0 额外提供一个 **Velopack 过渡包**（用 `-p:VelopackBridge=true` 编译，保留 Velopack 钩子）：

1. 老用户在软件里点更新，收到的是 1.3.0 过渡包。
2. 过渡包第一次启动时，会提示「新版改用新的安装方式，可自选安装目录」。点「下载并安装」后，下载 Inno 安装包并打开安装向导（选「稍后」也行，「检查更新」上会一直显示小红点，随时可以再点）。
3. 安装向导在复制新文件之前，会自动卸载旧版：Setup.exe 装的旧版用旧版自带的 `Update.exe --uninstall` 删除，MSI 装的旧版用 `msiexec /x` 删除。旧版所在文件夹不能被选作新安装目录。
4. 设置会先从 `%LocalAppData%\DataMatchingAssistant\settings.json` 复制到新位置 `%AppData%\DataMatchingAssistant\settings.json`，所以主题和最近文件都会保留。
5. 万一旧版没能自动删除（例如给所有用户安装的 MSI 需要管理员权限而被取消），安装结束时会提示用户到「设置 → 应用」里手动卸载旧版。新版本不受影响。

已经安装 1.0.0（旧的 Inno 安装包）的用户，直接运行 1.3.0 安装包即可覆盖升级。

## 开发环境

- .NET SDK 10.0
- C# / WPF
- ClosedXML
- ExcelDataReader（用于读取 `.xls`）
- Inno Setup 6.5+（打安装包）
- Velopack 1.2.161（仅用于给老用户的过渡包）

还原依赖并运行测试：

```powershell
dotnet restore StudentScoreTool.slnx
dotnet test StudentScoreTool.slnx
```

运行应用：

```powershell
dotnet run --project StudentScoreTool/StudentScoreTool.csproj
```

主题、上次打开的文件路径保存在 `%AppData%\DataMatchingAssistant\settings.json`，与安装目录无关。1.2.0 及更早版本放在 `%LocalAppData%\DataMatchingAssistant\settings.json`，新版启动时会自动搬过来。

## 如何发布新版本

发布前，请把下面几处的版本号改成同一个值，例如软件版本 `1.3.0`，标签 `v1.3.0`：

1. `StudentScoreTool/StudentScoreTool.csproj` 中的 `<Version>`（以及 `AssemblyVersion`、`FileVersion`）
2. `packaging/release-notes.md` 中的更新说明（会写进 version.json 和过渡包，并显示在「检查更新」窗口）

### 打包

- **GitHub Actions 试运行**：在 Actions 里手动运行「Release」工作流（可选任意分支）。它会跑测试、打出 Inno 安装包、生成 `version.json`、打出 Velopack 过渡包，并作为 Actions 产物上传，**不会**创建或修改任何 Release。
- **GitHub Actions 正式发布**：推送 `v*` 标签。除了上面的步骤，还会把过渡包（`releases.win.json`、`full.nupkg` 等）、Inno 安装包和 `version.json` 上传到对应的 GitHub Release。
- **本机打包**（Windows，需要安装 Inno Setup 6.5 或更新版本）：

```powershell
.\scripts\Publish-Release.ps1            # 只打包，不上传
.\scripts\Publish-Release.ps1 -SkipBridge # 不再需要过渡包时
```

产物：

| 文件 | 用途 |
| --- | --- |
| `release/DataMatchingAssistant-1.3.0-Setup.exe` | 给所有用户的安装包，上传到 GitHub 和 Gitee 的版本发行版。 |
| `release/version.json` | 含版本号、更新说明、下载地址、SHA-256 和大小。安装包上传好以后，再放到仓库的 `update/version.json`。 |
| `Releases/releases.win.json`、`Releases/DataMatchingAssistant-1.3.0-full.nupkg`（以及 `RELEASES`、`assets.win.json`） | Velopack 过渡包，只给 1.1.0 / 1.2.0 老用户。 |

### 发布顺序（重要）

1. 推送 `v1.3.0` 标签，等 Actions 跑完，确认 GitHub Release 有安装包和过渡包（1.1.0 用户从这里收到过渡包）。
2. 在 Gitee 创建 `v1.3.0` 发行版，上传 `DataMatchingAssistant-1.3.0-Setup.exe`。
3. 把 Gitee `latest` 发行版里的 Velopack 文件换成 1.3.0 过渡包的 `releases.win.json`、`full.nupkg`、`RELEASES`（1.2.0 用户从这里收到过渡包）。
4. 最后把 Actions 生成的 `version.json` 提交到 `update/version.json`，并推送到 GitHub 和 Gitee 的 main。只有这一步完成后，1.3.0 及以后的用户（以及过渡包）才会看到新安装包，所以务必先上传安装包。

以后（1.3.1 起）只需要第 1、2、4 步：Gitee `latest` 里的过渡包保持 1.3.0 不动，让仍停留在旧版的用户继续能换到新安装方式。不再需要过渡包时，可以用 `-SkipBridge` 或删掉工作流里的过渡包步骤。

`build`、`dist`、`release`、`Releases`、`publish` 和各项目的 `bin`、`obj` 目录不会提交到源码仓库。

## 项目结构

```text
StudentScoreTool/          WPF 应用、主题、更新和 Excel 处理逻辑
StudentScoreTool.Tests/    自动化测试
Installer/                 Inno Setup 安装脚本（1.3.0 起的正式安装方式）
packaging/                 发给用户的更新说明
scripts/                   本机打包脚本、生成 version.json 的脚本
update/version.json        软件检查更新时读取的版本信息
.github/workflows/         打包（试运行）和发布到 GitHub Release 的工作流
StudentScoreTool.slnx      解决方案
```
