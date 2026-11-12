# ADB Quick Tool

一个面向 Windows 的 Android 多设备管理工具。

它把常用的 ADB、scrcpy、文件管理、应用管理和设备调试功能集中到一个桌面界面中，适合 Android 开发、测试、售后维护和多设备管理场景。

## 下载

当前 Windows 可执行文件：

[下载 AdbQuickTool.exe](https://github.com/AnubisASN/ADBQuickTool/raw/main/bin/AdbQuickTool.exe)

也可以进入 [GitHub 仓库](https://github.com/AnubisASN/ADBQuickTool) 查看源码和更新。

## 运行环境

- Windows
- Android 设备已开启 USB 调试，或已通过网络连接 ADB
- `adb.exe`
- `scrcpy.exe`（使用投屏控制功能时需要）

首次启动后，点击左侧设置按钮，配置 `adb.exe`、`scrcpy.exe` 和 Python 路径。工具不会内置或自动获取用户设备中的账号、密码和聊天内容。

## 主要功能

### 设备管理

- 刷新并查看多个 ADB 设备
- 查看序列号、型号、系统版本、网络类型和设备地址
- 双击设备启动 scrcpy 投屏控制
- 复制 `adb connect` 和 `adb shell` 命令
- 重启设备、打开系统设置和检查设备在线状态

### ADB Shell 和 PC CMD

- 每台设备拥有独立的 ADB Shell 会话
- 内嵌 PC CMD，支持持续交互和会话状态
- 支持命令历史、输入编辑和部分命令/路径补全
- 支持从 Windows 拖入文件或文件夹填入命令行

### 文件管理

- 浏览设备文件，默认打开 `/sdcard/`
- 上传、下载、删除、重命名和新建文件/文件夹
- 拖拽上传文件
- 下载文本文件后编辑并上传回设备
- 显示文件传输进度和速度

### 应用管理

- 查看用户应用和系统应用
- 安装 APK，也支持拖拽 APK 安装
- 打开、重启和杀死应用
- 清除应用数据
- 冻结、恢复和卸载应用
- 查看包名、版本和 APK 信息

### 截图与快捷执行

- 截取设备屏幕并在工具内查看
- 复制、删除、重命名和打开截图
- 自定义 `cmd`、ADB Shell、Python 和 PowerShell 快捷命令
- 支持把“开启/关闭”命令合并为开关按钮
- 支持拖动调整快捷按钮顺序

## 使用步骤

1. 下载并运行 `AdbQuickTool.exe`。
2. 在设置中选择 `adb.exe` 和 `scrcpy.exe`。
3. 在 Android 设备上开启开发者选项和 USB 调试。
4. 通过 USB 连接设备，并在手机上确认 USB 调试授权。
5. 点击“刷新设备”。
6. 双击设备进行投屏，或使用右侧文件、应用和截图功能。

## 从源码构建

项目使用 Windows 自带的 .NET Framework C# 编译器，不要求安装 .NET SDK。

在 PowerShell 中执行：

```powershell
.\build.ps1
```

生成文件：

```text
bin\AdbQuickTool.exe
```

源码入口：

```text
src\Program.cs
```

## 数据和安全说明

- 本工具只操作用户明确连接并授权的 Android 设备。
- 工具不会保存 Android 账号密码、验证码或聊天内容。
- ADB 和 USB 调试拥有较高设备操作权限，请只连接你信任的电脑和设备。
- 执行删除、卸载、清除数据、冻结应用等操作前，请确认当前设备和目标对象。

## 当前限制

- 复杂的全屏 TUI 程序和精细终端光标控制可能无法完全模拟真实终端。
- 不同 Android 厂商对系统信息、网络信息和应用管理命令的支持存在差异。
- 应用名称在无法从设备或 APK 中解析时可能显示为包名。
- `scrcpy.exe`、`adb.exe` 需要用户自行准备，并遵守其各自的授权和许可证要求。

## 反馈与贡献

欢迎提交使用反馈、问题报告和功能建议：

[提交 Issue](https://github.com/AnubisASN/ADBQuickTool/issues)

提交问题时，建议附上 Windows 版本、Android 版本、设备型号、ADB 连接方式和复现步骤。请不要上传账号、验证码、设备隐私数据或完整运行日志。
