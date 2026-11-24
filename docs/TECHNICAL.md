# 技术说明

## 总体思路

HOPE5000 是一个 .NET/WPF 程序。工具使用 [dnlib](https://github.com/0xd4d/dnlib) 读取目标程序集，在指定方法中找到动态设置 `Title` 属性的字符串常量，将其替换，然后由 dnlib 重新生成完整程序集。

这不是在文件中搜索几个孤立字节后直接覆盖，也不是按旧版本地址硬编码修改。

## 目标位置

```text
Type:   HOPE5000.ViewModels.HT8.SmartProgrammingViewModel
Method: ClickRange
```

目标版本中，动态绑定序列的形态如下：

```text
ldstr      "Title"
...
call       RuntimeBinder.Binder::SetMember(...)
...
ldstr      ""
callvirt   Func<...>::Invoke(...)
```

最后一个 `ldstr` 的操作数就是赋给 `val.Title` 的值。

## 特征定位

工具执行以下校验：

1. 程序集名必须是 `HOPE5000`；
2. 必须能通过 `GetTypes()` 找到完整类型名；
3. 必须存在带方法体的 `ClickRange`；
4. 在指令中寻找 `ldstr "Title"`；
5. 在后续有限窗口内寻找同时满足以下条件的 `ldstr`：
   - 紧邻下一条指令是 `callvirt`；
   - 被调用方法名为 `Invoke`；
   - 标记与候选字符串之间出现过 `SetMember`。
6. 找不到时中止，不做固定偏移回退。

对于已验证版本，目标指令偏移为 `IL_0116`。偏移只用于日志和诊断，不用于定位。

## 写入流程

```text
校验目标与标题
  → 确认 HOPE5000 未运行
  → 读取当前标题
  → 检查/创建原始备份
  → 在内存中修改 IL 操作数
  → 写入同目录临时文件
  → 重新读取临时文件并核对标题
  → 再次确认进程未运行
  → File.Replace 原子替换（不支持时回退到 Copy）
  → 再次读取目标并核对
  → 写入 .titlefix.json
```

任何一步失败，临时文件都会清理。若替换后的验证失败，工具会尝试用原始备份回滚。

## 备份策略

备份路径固定为：

```text
<目标文件>.original.bak
```

只有当备份中的标题仍为空字符串时，才认为它是可接受的原始文件。这样可以避免：

- 把已修改版本误当成原版；
- 第二次打补丁时覆盖真正的原始备份；
- 使用来路不明的文件还原。

如果当前文件已有非空标题但没有备份，工具拒绝继续。用户需要恢复厂商原版后再操作。

## 元数据

成功后写入：

```text
<目标文件>.titlefix.json
```

字段包括：

- `schema`
- `toolVersion`
- `patchedAtUtc`
- `target`
- `windowTitle`
- `backup`
- `originalSha256`
- `patchedSha256`

元数据不参与补丁定位，只用于审计。写入失败不会撤销已经成功的补丁，但会在界面中显示警告。

## 还原策略

1. 备份必须存在且程序集名为 `HOPE5000`；
2. 备份标题必须为空；
3. 复制到临时文件并核对 SHA256；
4. 再次读取临时文件的 IL 标题；
5. 原子替换目标文件；
6. 目标文件 SHA256 必须与备份完全一致；
7. 目标标题必须为空；
8. 删除 `.titlefix.json`，保留 `.original.bak`。

因此还原结果与原始文件逐字节一致，而不只是“看起来恢复了”。

## 单文件打包

dnlib 以托管资源 `dnlib.dll` 嵌入主 EXE。程序启动后最先注册 `AppDomain.AssemblyResolve`，在需要时从资源流加载 dnlib。这样发布目录只需要一个 `HOPE5000TitleFix.exe`。

EXE 不依赖 PowerShell，不启动外部进程，不写入 GAC 或注册表。

## 兼容性与边界

- 目标必须是托管 .NET 程序集，程序集名必须为 `HOPE5000`。
- 目标版本必须包含上述动态绑定序列。
- 已验证版本未强命名（`PublicKeyToken=null`）。
- 若厂商后续版本改为直接设置 `Window.Title`、更换类型名或移除动态绑定，本工具会拒绝修改。
- 工具不处理其他 WPF 异常、授权错误、驱动错误或烧录失败。
- 工具不校验 Authenticode 签名。未签名版本可能触发 SmartScreen。

## 安全不变量

- 不修改原始备份；
- 不在 HOPE5000 运行时替换文件；
- 不接受非 HOPE5000 程序集；
- 不在结构不匹配时猜测补丁位置；
- 不联网；
- 不包含或再分发 HOPE5000 本体。
