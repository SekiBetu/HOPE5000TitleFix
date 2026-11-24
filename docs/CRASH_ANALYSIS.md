# HOPE5000 闪退分析

## 结论

本次崩溃不是烧录器未连接造成的，也不是 `AquireAccess fail:3` 直接导致的。

崩溃发生在用户点击“自定义烧录”后。应用打开一个 WPF 子窗口，并将该窗口标题设为空字符串。窗口显示和布局过程中，WPF UI Automation 代码尝试读取标题，一条旧版 .NET Framework 代码路径把残留的 Win32 错误 `2`（`ERROR_FILE_NOT_FOUND`，中文通常显示为“系统找不到指定的文件”）包装成未处理的 `System.ComponentModel.Win32Exception`，进程随即退出。

给标题设置非空字符串即可绕开该错误处理路径。

## 现场信息

| 项目 | 内容 |
|---|---|
| 故障程序 | `HOPE5000.exe` |
| 程序集版本 | 见 [README 的“已验证版本”](../README.md#已验证版本) |
| 文件大小 | `2,423,808` 字节 |
| 原始 SHA256 | `178E7693F731896DAD905CB0ED7B9A66C710D116E87D7C855C8878EA6D37A87D` |
| 崩溃类型 | `System.ComponentModel.Win32Exception` |
| `NativeErrorCode` | `2` |
| `HResult` | `0x80004005` |
| 触发操作 | 自定义烧录 / 范围选择 |

仓库不包含 DMP、HOPE5000 本体或用户工程数据。

## 关键栈

从崩溃转储中观察到的调用方向可归纳为：

```text
System.ComponentModel.Win32Exception
  at WPF/Win32 interop window text retrieval
WindowAutomationPeer.GetNameCore()
AutomationPeer.UpdateSubtree()
ContextLayoutManager.fireAutomationEvents()
HwndSource.Process_WM_SIZE()
...
HOPE5000.ViewModels.HT8.SmartProgrammingViewModel.ClickRange()
HOPE5000.ViewModels.HT8.SmartProgrammingViewModel.SelectChangeRange()
Window.ShowDialog()
```

异常抛出点位于 WPF UI Automation 自动事件路径，不在 `ClickRange` 方法的业务代码内部。`ClickRange` 只是打开窗口并传入空标题，`WM_SIZE` 随后触发了自动化树更新。

## 空标题如何变成异常

旧版 .NET Framework 的 UI Automation 桥接代码在读取窗口文本时，没有把失败返回值和线程级 `GetLastError()` 状态完全隔离。上一个 Win32 调用留下的 `ERROR_FILE_NOT_FOUND (2)` 被错误地关联到后续标题读取，最终以 `Win32Exception` 形式抛到 UI 线程。

这类错误对调用栈具有“污染性”：

- 窗口标题为空，不代表 Win32 调用一定失败；
- 但旧运行时可能沿用线程上残留的错误码；
- 当异常穿过 WPF 消息处理代码且没有被捕获时，整个进程终止。

这是运行时兼容性缺陷，不是用户操作错误。

## 为什么标题补丁有效

原程序在 `ClickRange` 中进行动态属性赋值：

```csharp
val.Title = "";
```

工具将其改为：

```csharp
val.Title = "HOPE5000";
```

标题非空后，UI Automation 获取窗口名称时不再落入本次观察到的失败路径。补丁只改变用户字符串常量，不改变窗口内容、烧录数据或设备访问。

## `AquireAccess fail:3` 是什么

该日志来自厂商的 `Authorized.dll`，属于授权或内部访问检查输出。它与本次崩溃的异常栈没有交集：

- 崩溃异常是 `Win32Exception(2)`；
- 崩溃点在 WPF 的窗口自动化事件里；
- 当时尚未进入实际烧录或设备通信阶段；
- 即使没有连接烧录器，也不影响打补丁。

`3` 在私有 DLL 中的业务含义没有公开资料，不能仅凭日志断言为用户错误。它最多说明授权模块记录了一次访问检查失败，但不是本 DMP 的抛出源。

## 永久修复与临时方案

正规修复方式是安装对应 Windows 版本和 .NET Framework 版本的 2023 年 2 月 .NET Framework 安全与质量累积更新，或维护到更新的系统版本。

不存在适用于所有 Windows 版本的单一 KB 号。补丁号取决于：

- Windows 版本和内部版本；
- 已安装的 .NET Framework 版本；
- 系统架构；
- 更新通道（Windows Update、WSUS 或 Microsoft Update Catalog）。

如果电脑因特殊原因不能更新系统，本工具通过修改空标题来规避触发条件。它不修复 Windows/.NET 运行时本身，也不会让系统获得其他安全补丁。

## 验证标准

修复后应满足：

1. 启动 HOPE5000 正常；
2. 打开“自定义烧录/范围选择”不再闪退；
3. 子窗口标题显示为设定的文字；
4. 还原后文件 SHA256 与修改前完全一致；
5. 不出现新的未处理异常。

如果仍有崩溃，需要采集新的 DMP。不同异常栈不能套用本结论。
