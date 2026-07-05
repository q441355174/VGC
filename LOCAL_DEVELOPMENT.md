# 本机开发环境说明

> 本文档仅记录当前本机/局域网开发环境信息，不应作为通用项目配置提交到共享环境。

## 路径与工具

| 项目 | 路径/值 | 说明 |
|---|---|---|
| SSH JDK bin | `/usr/lib/jvm/java-21-openjdk-amd64/bin` | 远程 Linux JDK 21 可执行文件目录 |
| Android SDK | `/home/a/Android/Sdk` | 远程 Linux Android SDK 目录 |
| Linux 映射项目路径 | `/mnt/hgfs/Code/VGC/VGC` | 对应 Windows `E:\Code\VGC\VGC` |
| ADB 工具目录 | `E:\z_z-gaoji\搞机工具箱9.92` | Windows 本机 ADB 所在目录 |
| QGC 源码目录 | `E:\Code\VGC\qgroundcontrol` | Windows 本机 QGroundControl 源码路径 |

## 设备与仿真

| 项目 | 地址/值 | 说明 |
|---|---|---|
| SITL 仿真飞机 | `100.83.181.91:6276` | 仿真飞行器连接地址 |
| Android 模拟器 | `emulator-5554` | 本机 Android 模拟器设备 ID |

## SSH 环境

| 项目 | 值 |
|---|---|
| 默认连接 | `default` |
| 用户与主机 | `a@192.168.52.131:22` |
| 远程工作目录 | `/home/a` |

## 使用提示

- 在 SSH/Linux 环境构建 Android 相关内容时，优先使用上面的 JDK 与 Android SDK 路径。
- 需要从 Linux 访问当前项目时，使用 `/mnt/hgfs/Code/VGC/VGC`。
- 需要对比或移植 QGC 代码时，Windows 本机源码位于 `E:\Code\VGC\qgroundcontrol`。
- 连接 Android 模拟器时，设备 ID 使用 `emulator-5554`。
