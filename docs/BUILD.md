# 构建与测试

## 环境

支持以下任一环境：

- Visual Studio 2019 / 2022，包含“.NET 桌面开发”
- .NET Framework 4.8 Developer Pack
- Windows 自带的 .NET Framework 4.x C# 编译器

运行时只需要 .NET Framework 4.x。构建 `build.cmd` 会依次寻找：

```text
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
```

## 使用 build.cmd

```bat
build.cmd
```

输出：

```text
dist\HOPE5000TitleFix.exe
tests\bin\PatcherSmokeTests.exe
```

构建参数包含：

- `target:winexe`
- `platform:anycpu`
- `codepage:65001`，确保中文源码按 UTF-8 编译
- 将 `lib\dnlib.dll` 以逻辑名 `dnlib.dll` 嵌入
- 嵌入 `src\app.manifest`

## 使用 Visual Studio

打开 `src\HOPE5000TitleFix.csproj`，选择 `Release | Any CPU` 构建。

项目通过 `HintPath` 引用 `lib\dnlib.dll`，并把同一个 DLL 作为 `EmbeddedResource` 嵌入，因此发布时不需要旁置 dnlib。

## 启动自检

```bat
dist\HOPE5000TitleFix.exe --smoke-test
```

退出码 `0` 表示主程序成功从自身资源加载了 dnlib。该模式不显示界面。

注意：`/target:winexe` 程序不会把输出写到控制台。自动化环境应读取进程退出码，而不是搜索标准输出。

## 冒烟测试

必须传入原版 HOPE5000 的副本路径：

```bat
tests\bin\PatcherSmokeTests.exe "C:\path\to\HOPE5000.exe"
```

测试会创建随机临时目录，并在其中完成：

- 原始标题识别
- 无效标题拒绝
- 首次补丁与备份创建
- 中文、引号标题写入与读取
- 二次改标题且复用原备份
- 已有补丁但无备份时拒绝
- 非空备份时拒绝
- 还原后逐字节 SHA256 比对
- 非 HOPE5000 程序集拒绝

测试不会修改传入的原文件。

## 打包发布

发布 ZIP 建议包含：

```text
HOPE5000TitleFix.exe
README.md
LICENSE
THIRD-PARTY-NOTICES.txt
SHA256SUMS.txt
```

不要包含：

```text
HOPE5000.exe
*.original.bak
*.titlefix.json
*.dmp
用户工程文件
完整反编译源码
```

本地生成发布包：

```powershell
.\package.ps1 -Tag v1.0.0
```

脚本会先调用 `build.cmd`，然后生成：

```text
release\HOPE5000TitleFix-v1.0.0-win.zip
release\SHA256SUMS.txt
```

生成校验值：

```powershell
Get-FileHash .\dist\HOPE5000TitleFix.exe -Algorithm SHA256
```

PowerShell 仅用于开发打包；工具本身不依赖 PowerShell。

## 已知构建限制

- 当前仓库不依赖 NuGet 联网还原，dnlib 直接放在 `lib\`。
- `build.cmd` 使用系统安装的 C# 5 编译器，因此源码避免使用 C# 6+ 语法。
- 未做代码签名；公开发布时需要自行准备 Authenticode 证书，并在文档中如实说明。

## GitHub Actions

`build.yml` 在 `windows-latest` 上执行 `build.cmd`，随后用：

```bat
dist\HOPE5000TitleFix.exe --smoke-test
```

验证内嵌 dnlib 能从 EXE 资源中加载。构建通过后上传 `HOPE5000TitleFix-win` 产物。

`release.yml` 只由 `v*` 标签触发，例如 `v1.0.0`。它会：

- 编译 EXE；
- 组装发布目录；
- 压缩为 `HOPE5000TitleFix-v1.0.0-win.zip`；
- 计算 EXE 和 ZIP 的 SHA256；
- 调用 GitHub CLI 创建 Release 并上传附件。

工作流需要仓库的 `GITHUB_TOKEN` 具备 `contents: write` 权限；文件内已声明。若仓库组织策略禁止 Actions 创建 Release，需要管理员在仓库设置中允许。

完整回归测试需要原版 `HOPE5000.exe`，因此 CI 只执行不依赖厂商文件的启动自检。维护者仍应在本机对原版副本运行 `PatcherSmokeTests.exe`。
