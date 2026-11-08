# 数据匹配助手

一个本地运行的 Windows 表格数据匹配工具。选择包含成绩的 Excel 工作簿和需要填写的名单工作簿，软件会匹配学生，并将成绩写入名单表已有的成绩列。

## 功能

- 按姓名匹配学生；重名时结合学号、性别判断，无法确认的记录不会盲目写入。
- 支持多个工作表、多个科目或多次作业，以及数字和文字成绩。
- 保留目标表的学生顺序、格式、公式和无关工作表，不新增成绩列。
- 源成绩为空时保留目标单元格原值，公式不会被覆盖。
- 写入前列出异常记录，用户可以取消或跳过异常继续。
- 默认在覆盖目标文件前创建带时间戳的备份。
- 目标文件被占用时，可另存为“已填写”副本。
- 所有表格均在本地处理，不上传学生数据，不需要 API 密钥。

## 使用

1. 选择包含成绩的源 Excel 文件。
2. 选择需要填写的目标名单 Excel 文件。
3. 如果有多个候选工作表，手动确认源工作表和目标工作表。
4. 保持“覆盖前备份”开启，点击开始处理。
5. 如果出现异常记录，确认后再继续写入。

支持 `.xlsx` 文件，适用于 Windows 10/11 x64。

## 开发环境

- .NET SDK 10.0
- C# / WPF
- ClosedXML

还原依赖并运行测试：

```powershell
dotnet restore StudentScoreTool.slnx
dotnet test StudentScoreTool.slnx
```

运行应用：

```powershell
dotnet run --project StudentScoreTool/StudentScoreTool.csproj
```

## 发布

生成 Windows x64 自包含单文件程序：

```powershell
dotnet publish StudentScoreTool/StudentScoreTool.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -o build/publish
```

安装程序使用 Inno Setup 6。发布程序后，用 Inno Setup 打开并编译 `Installer/DataMatchingAssistant.iss`，安装包会输出到 `release` 目录。

`build`、`dist`、`release` 和各项目的 `bin`、`obj` 目录不会提交到源码仓库。

## 项目结构

```text
StudentScoreTool/          WPF 应用和 Excel 处理逻辑
StudentScoreTool.Tests/    自动化测试
Installer/                 Inno Setup 安装脚本和中文语言文件
StudentScoreTool.slnx      解决方案
```
