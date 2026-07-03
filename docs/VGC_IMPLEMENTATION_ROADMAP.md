# VGC 实现路线图与开发计划

> 版本: 2.0  
> 日期: 2026-07-02  
> 基准: 当前 VGC 源码、QGC 控件映射、运行时 parity 自评  
> 当前状态: UI 控件移植基本完成；生产级等价重点转向地图、视频、图表、MAVLink dialect、PX4/ArduPilot 验证。

---

## 1. 当前完成度

| 口径 | 完成度 | 说明 |
|---|---:|---|
| UI 控件外观/交互 | 90–95% | 大部分 QGC 控件已有 Avalonia 对应实现 |
| 页面布局/操作逻辑 | 85–90% | Fly/Plan/Setup/Analyze/Settings 已成型 |
| 基础 GCS 运行时 | 65–75% | Link/MAVLink/Vehicle/Mission/Parameter 基础存在 |
| 外部库能力 | 20–30% | Mapsui/OxyPlot/LibVLC/OpenTK 均未接入 |
| QGC 生产级等价 | 55–65% | 仍缺完整协议、地图、视频、真实固件闭环 |

---

## 2. 已完成范围

| 范围 | 状态 | 说明 |
|---|---|---|
| Batch 18 快速补全 | 完成 | `EditPositionDialog`, `DropButton`, `QGCRoundButton`, `KMLOrSHPFileDialog`, `MissionCommandSummary` 已实现 |
| FlyView UI | 基本完成 | Toolbar、ToolStrip、HUD、PiP、虚拟摇杆、告警、飞前检查已实现 |
| PlanView UI | 基本完成 | Mission/Fence/Rally、右面板、传输确认、地图覆盖物已实现 |
| Setup UI | 基本完成 | Airframe/Sensors/Radio/Power/Safety/Motors/PID/Joystick 已实现 |
| Analyze UI | 部分完成 | Inspector/Console/Replay/Log/GeoTag/Vibration 已实现；图表占位 |
| Settings UI | 部分完成 | General/Connection/OfflineMap/NTRIP/VideoSettings 框架存在 |
| MAVLink 基础 | 部分完成 | parser/writer/router/FTP/基础 command/mission/param 存在 |
| Mission 系统 | 部分完成 | Plan JSON、Mission/Fence/Rally transfer 边界存在 |

---

## 3. 阻塞项总表

| 优先级 | 阻塞项 | 当前状态 | 目标 |
|---|---|---|---|
| P0 | Mapsui 地图瓦片 | Mapsui 包、`MapsuiMapRenderer`、OSM 在线瓦片下载和本地文件缓存已接入 | Fly/Plan 显示真实地图，支持缓存和离线 |
| P0 | LibVLC 视频播放 | `NullVideoDecoder` | RTSP/UDP 视频可播放、可停止、可显示状态 |
| P0 | MAVLink full dialect | seed definitions | common/ardupilotmega 生成器与 CRC 表完整化 |
| P0 | MAVLink runtime adoption | 多服务 partial | Command/Mission/Parameter/Camera/Gimbal 使用更完整消息覆盖 |
| P1 | OxyPlot 图表 | `TelemetryChartPlaceholder` | PID/Telemetry/FFT 图表真实可用 |
| P1 | Setup 生产验证 | PX4/APM partial | SITL/实机 transcript 覆盖校准/机架/电源/电机 |
| P1 | Mission 拖拽完善 | drag partial | Mercator 转换、拖拽预览、完成事件 |
| P1 | VideoSettings codec | 硬编码 | 从 LibVLC 或平台能力动态枚举 |
| P2 | 平台构建验证 | Core 已验证 | Desktop/Android 独立 build 通过 |
| P3 | 3D Viewer | 未开始 | Viewer3D 最小可用版本 |

---

## 4. 开发计划

### Phase A — 文档口径修正

| 项 | 内容 | 状态 |
|---|---|---|
| A1 | 更新控件映射，取消“全面完成”误导 | 已完成 |
| A2 | 更新 UI/逻辑分析，标明真实缺口 | 已完成 |
| A3 | 更新路线图，生成后续开发计划 | 已完成 |
| A4 | Core build 验证 | 已完成 |

### Phase B — 地图生产化

| 项 | 内容 | 涉及文件 | 验收 |
|---|---|---|---|
| B1 | 添加 Mapsui / Mapsui.Avalonia 引用 | `VGC.csproj`, `Directory.Packages.props` | 已完成；restore/build 通过 |
| B2 | 实现 `MapsuiMapRenderer : IMapRenderer` | `Views/Controls/MapControls.cs` | 已完成；`RenderTiles`, `GeoToScreen`, `ScreenToGeo` 可用 |
| B3 | 将 Fly/Plan map runtime 注入真实 renderer | `FlightMapControl` 默认 renderer | 已完成；默认不再使用 placeholder renderer |
| B4 | 接入真实在线 tile 绘制/cache/offline provider | `MapsuiMapRenderer`, local app-data cache | 部分完成；OSM 在线瓦片下载和本地文件缓存已接入，离线下载管理待接 |
| B5 | 验证地图交互 | Fly/Plan | 部分完成；Core build 通过，UI 交互待运行验证 |

### Phase C — 视频生产化

| 项 | 内容 | 涉及文件 | 验收 |
|---|---|---|---|
| C1 | 添加 LibVLCSharp 平台包 | `VGC.csproj`, Desktop/Android project | Windows build 通过 |
| C2 | 实现 `LibVlcVideoDecoder : IVideoDecoder` | `Payload/` | `StartAsync/StopAsync` 控制真实流 |
| C3 | FlyView 视频控件替换占位 | `FlyView.axaml`, `FlyViewModel` | RTSP/UDP 流可见 |
| C4 | 绑定 `VideoStreamIndicator` | `ToolbarIndicators.cs`, payload runtime | ON/OFF、分辨率、错误状态正确 |
| C5 | VideoSettings codec 动态枚举 | `VideoSettingsRuntime.cs` | codec 列表不再硬编码 |

### Phase D — 图表与分析生产化

| 项 | 内容 | 涉及文件 | 验收 |
|---|---|---|---|
| D1 | 添加 OxyPlot.Avalonia | `VGC.csproj` | build 通过 |
| D2 | 替换 `TelemetryChartPlaceholder` | `AnalyzeControls.cs`, `AnalyzeView.axaml` | 折线图可显示 |
| D3 | PID tuning chart | `SetupControls.cs`, `AnalyzeControls.cs` | PID_TUNING 数据进入图表 |
| D4 | 通用 telemetry chart | `AnalyzeViewModel`, `Analyze/TelemetryChartRuntime.cs` | 多系列滚动窗口 |
| D5 | FFT 数据流 | `Analyze/`, `FrequencyPlot` | RAW_IMU→FFT→频谱更新 |

### Phase E — MAVLink 完整化

| 项 | 内容 | 涉及文件 | 验收 |
|---|---|---|---|
| E1 | 引入 full common.xml / ardupilotmega.xml fixtures | `Mavlink/Definitions` | fixtures 可加载 |
| E2 | 实现 dialect generator | `tools/` 或 `Mavlink/Generated` | strong records + CRC extra 自动生成 |
| E3 | 替换 seed-only registry | `MavlinkCrcExtraRegistry` | 全消息 CRC 覆盖 |
| E4 | runtime adoption | Command/Mission/Parameter/Camera/Gimbal services | partial 降为 complete |
| E5 | SITL transcript | 测试/日志文档 | PX4/APM 基础任务和参数闭环 |

### Phase F — Setup/固件生产验证

| 项 | 内容 | 涉及文件 | 验收 |
|---|---|---|---|
| F1 | Airframe apply transcript | `Firmware/`, `Setup/` | PX4/APM 机架设置可验证 |
| F2 | Sensor calibration transcript | `Setup/SensorCalibrationWorkflow.cs` | accel/gyro/level/compass 流程完整 |
| F3 | Radio calibration transcript | `Setup/` | min/max/trim/channel mapping 正确 |
| F4 | Power/Battery monitor | `Setup/`, `Facts/` | 真实 battery monitor 参数验证 |
| F5 | Motor safety parity | `Setup/`, `Firmware/` | 安全确认和 actuator 映射明确 |

### Phase G — 交互完善与平台验证

| 项 | 内容 | 涉及文件 | 验收 |
|---|---|---|---|
| G1 | Mission drag 完善 | `MapControls.cs`, `PlanViewModel.cs` | 拖拽预览、Mercator 转换、完成事件 |
| G2 | Desktop build | `VGC.Desktop` | 0 错误 / 0 警告 |
| G3 | Android build | `VGC.Android` | 0 错误 / 0 警告 |
| G4 | UI smoke test | Fly/Plan/Setup/Analyze/Settings | 页面可打开，主路径可操作 |
| G5 | 文档回写 | `docs/` | 状态与代码一致 |

### Phase H — 3D Viewer

| 项 | 内容 | 验收 |
|---|---|---|
| H1 | 选择 OpenGL 技术栈 | Windows/Android 均可构建 |
| H2 | Attitude3D 最小实现 | 姿态随 vehicle attitude 更新 |
| H3 | FlightPath3D | 历史轨迹可渲染 |
| H4 | Vehicle3DModel | 内置模型可显示 |
| H5 | TerrainMesh3D | 地形数据源明确 |

---

## 5. 推荐执行顺序

| 顺序 | Phase | 原因 |
|---:|---|---|
| 1 | Phase B 地图 | Fly/Plan 最大可见缺口，且已有 `IMapRenderer` 抽象 |
| 2 | Phase C 视频 | FlyView 核心能力，已有 `IVideoDecoder` 抽象 |
| 3 | Phase D 图表 | Analyze/PID 现有 placeholder 可直接替换 |
| 4 | Phase E MAVLink | 决定生产级兼容性上限 |
| 5 | Phase F Setup 验证 | 把 UI 完成转为真实 PX4/APM 可用 |
| 6 | Phase G 平台/交互 | 交付前必须验证 Desktop/Android |
| 7 | Phase H 3D | 优先级最低、技术风险最高 |

---

## 6. 当前验收记录

| 项 | 状态 |
|---|---|
| `dotnet build E:/android/gcs/VGC/VGC/VGC.csproj` | 0 错误 / 0 警告 |
| 控件映射文档口径 | 已更新 |
| UI/逻辑分析文档口径 | 已更新 |
| 路线图与开发计划 | 已更新 |
| Desktop build | 待验证 |
| Android build | 待验证 |

---

## 7. 文档维护规则

| 触发 | 需要更新 |
|---|---|
| 接入外部库 | 更新依赖状态、路线图 Phase 状态、映射文档缺口 |
| 完成真实运行时 | 把 PARTIAL/BLOCKED 改为 DONE，并写明验证证据 |
| 完成构建验证 | 记录项目、命令、结果 |
| 发现文档乐观或过期 | 以源码和构建结果为准修正文档 |
