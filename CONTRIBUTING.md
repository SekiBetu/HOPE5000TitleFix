# Contributing

欢迎提交问题、兼容性报告和代码改进。

## 提交兼容性报告时

请提供：

- HOPE5000 程序集版本；
- `HOPE5000.exe` 文件大小和 SHA256；
- Windows 版本；
- 工具显示的完整错误信息；
- 不包含个人数据的 DMP 调用栈（仅在可公开时）。

不要提交：

- `HOPE5000.exe` 本体；
- 完整反编译源码；
- 用户烧录工程、固件或设备数据；
- 未脱敏的 DMP。

## 代码要求

- 保持 .NET Framework 4.x 与 C# 5 语法兼容；
- 不引入联网依赖；
- 修改补丁逻辑时必须增加针对失败路径的测试；
- 不允许按固定地址盲改；
- 必须保留备份验证、临时文件验证和回滚逻辑；
- 不提交 `dist/`、`release/` 以外的构建中间文件。

## 测试

```bat
build.cmd
tests\bin\PatcherSmokeTests.exe "C:\path\to\original\HOPE5000.exe"
dist\HOPE5000TitleFix.exe --smoke-test
```

只对目标文件的副本运行测试。
