# QGC → VGC 控件映射与移植状态

> 更新日期: 2026-07-02  
> 基准: 当前 VGC 源码 + QGC 控件移植文档核对  
> 结论: UI 控件外观/交互层基本完成；生产级 QGC 等价仍受地图、视频、图表、MAVLink dialect、固件真实流程验证限制。

---

## 1. 总体口径

| 口径 | 当前判断 |
|---|---:|
| QGC 控件外观/交互覆盖 | 约 90–95% |
| 页面布局与操作逻辑覆盖 | 约 85–90% |
| 可连接 MAVLink 的基础 GCS 功能 | 约 70% |
| 接近 QGC 生产级功能等价 | 约 55–65% |
| 全面移植完成 | 否 |

说明: 本文的 DONE 表示 VGC 已有对应 UI 控件或功能边界；不等同于已经完成真实地图瓦片、真实视频解码、完整 MAVLink dialect、真实 PX4/ArduPilot 硬件闭环。

---

## 2. 状态标记

| 状态 | 含义 |
|---|---|
| DONE | 已有 VGC 对应实现，外观/交互或功能边界接近 QGC |
| PARTIAL | 已实现主要结构，但交互、数据源、真实运行时或精度不足 |
| TODO | 尚无可用实现 |
| NATIVE | 由 Avalonia 原生控件替代 |
| BLOCKED | 依赖外部库、完整 dialect、平台能力或真实硬件验证 |
| SKIP | QGC 专属结构在 VGC 架构中不需要单独实现 |

---

## 3. 控件库文件清单

| 文件 | 当前行数 | 主要覆盖 |
|---|---:|---|
| `Views/Controls/SetupControls.cs` | 2560 | 校准、机架、电机、电源、PID、摇杆、参数搜索、云台 |
| `Views/Controls/MapControls.cs` | 2891 | 地图覆盖物、任务项、围栏、航迹、雷达、相机/云台叠加、命令摘要 |
| `Views/Controls/ToolbarIndicators.cs` | 3317 | Toolbar 指示器、ToolStrip、延迟按钮、信号/速度/高度/状态控件 |
| `Views/Controls/FactControls.cs` | 2230 | Fact 输入、标签、表格、单位换算、任务行、遥测格 |
| `Views/Controls/AnalyzeControls.cs` | 1360 | 参数对话框、Log、GeoTag、频谱、MAVLink 状态行 |
| `Views/Controls/AttitudeIndicator.cs` | 567 | 姿态仪、罗盘、航向、垂直仪表 |
| `Views/Controls/DialogSystem.cs` | 3249 | 弹窗、Toast、文件对话框、页签、滑动确认、KML/SHP、位置编辑 |
| `Views/Controls/SettingsControls.cs` | 619 | General、Connection、OfflineMap、NTRIP、MAVLink 设置 |
| `Views/Controls/QgcTheme.cs` | 123 | QGC 颜色和尺寸常量 |
| `Views/Controls/SetupControlModels.cs` | 235 | Setup/Checklist 数据模型 |

---

## 4. 页面级映射

| QGC 区域 | VGC 对应 | 状态 | 主要缺口 |
|---|---|---|---|
| FlyView | `FlyView.axaml`, `FlyViewModel`, 飞行仪表/Toolbar/地图覆盖层 | PARTIAL | 地图底图仍是 placeholder/local vector；视频解码为空实现 |
| PlanView | `PlanView.axaml`, `PlanViewModel`, Mission/Fence/Rally 控件 | PARTIAL | 地图瓦片未接 Mapsui；拖拽地理转换精度待提升 |
| AnalyzeView | `AnalyzeView.axaml`, `AnalyzeViewModel`, AnalyzeControls | PARTIAL | OxyPlot 图表未接；FFT 数据流未生产化 |
| SetupView | `SetupView.axaml`, `SetupViewModel`, SetupControls | PARTIAL | PX4/APM 真实校准、机架应用、电源/电机流程证据不足 |
| SettingsView | `SettingsView.axaml`, `SettingsViewModel`, SettingsControls | PARTIAL | Video codec 动态枚举、地图下载真实 provider、平台存储需验证 |
| ParameterView | `ParameterView.axaml`, `ParameterViewModel`, Facts | DONE/PARTIAL | 参数读写边界存在；完整元数据和 SITL 证据仍需补齐 |
| Shell/导航 | `MainView.axaml`, `ShellViewModel` | DONE | 架构不同于 QGC visible/Loader 模型，但可接受 |

---

## 5. 阶段映射汇总

| 范围 | 目标 | 当前状态 | 判断 |
|---|---:|---|---|
| 第 1 期 核心控件 | 52 | DONE/NATIVE | UI 层完成 |
| 第 2 期 Setup/Analyze/Settings | 35 | 33 DONE / 1 PARTIAL / 1 TODO | PID/OxyPlot 与 VideoSettings 仍缺 |
| 第 3 期 地图集成 | 12 | 11 DONE / 1 PARTIAL | 覆盖物完成，真实底图未完成 |
| 第 4 期 高级飞行控件 | 30 | DONE | UI 控件完成，视频运行时另计 |
| 第 5 期 通用/高级控件 | 100+ | 大部分 DONE | 外部库依赖项未生产化 |
| Batch 18 快速补全 | 5 | DONE | 明细已同步 |

---

## 6. Batch 18 明细同步

| # | QGC 控件 | VGC 实现 | 状态 | 文件 |
|---:|---|---|---|---|
| 244 | EditPositionDialog | `EditPositionDialog` | DONE | `DialogSystem.cs` |
| 245 | DropButton / DropPanel | `DropButton`, `DropPanel` | DONE | `ToolbarIndicators.cs` |
| 246 | QGCRoundButton | `QGCRoundButton` | DONE | `DialogSystem.cs` |
| 247 | KMLOrSHPFileDialog | `KMLOrSHPFileDialog` | DONE | `DialogSystem.cs` |
| 248 | MissionCommandSummary | `MissionCommandSummary` | DONE | `MapControls.cs` |

---

## 7. 当前关键 PARTIAL / BLOCKED 项

| 项 | 现状 | 阻塞/缺口 | 优先级 |
|---|---|---|---|
| FlightMap 底图 | Mapsui 包已接入，`MapsuiMapRenderer` 默认启用 | OSM 在线瓦片和本地文件缓存已接入；离线下载管理待接 | P0 |
| FlyView 视频 | `VideoDecodePipeline` + `NullVideoDecoder` | LibVLCSharp 未接入，无法播放 RTSP/UDP | P0 |
| PID/Telemetry 图表 | `TelemetryChartPlaceholder` | OxyPlot 未接入 | P1 |
| Vibration FFT | `FrequencyPlot` 有 UI | 缺真实 RAW_IMU→FFT 数据流 | P1 |
| VideoSettings | 有 runtime/UI | codec 枚举硬编码，需 LibVLC 支持 | P1 |
| MissionItemIndicatorDrag | 有点击/编辑基础 | 拖拽预览、Mercator 转换、完成事件不足 | P1 |
| MAVLink dialect | seed definitions + 手写服务 | full common/ardupilotmega generator blocked | P0 |
| MAVLink runtime adoption | Command/Mission/Parameter/Camera/Gimbal partial | 需 generated writer/reader 与 SITL 证据 | P0 |
| Firmware setup | UI 和状态机存在 | PX4/APM 真实校准/机架/电源/电机证据不足 | P1 |
| Vehicle fact groups | 常用 fact groups 已有 | QGC specialty fact groups、object avoidance 不完整 | P1 |
| 3D Viewer | 规划中 | 无 OpenTK/Avalonia 3D 集成 | P3 |

---

## 8. 外部库集成状态

| 库 | 当前引用 | VGC 架构准备 | 影响 |
|---|---|---|---|
| Avalonia | 已引用 | UI 主框架 | 已使用 |
| ReactiveUI.Avalonia | 已引用 | ViewModel 命令/绑定 | 已使用 |
| Mapsui / Mapsui.Avalonia | 已引用 | `MapsuiMapRenderer`, `IMapRenderer`, OSM tile download/cache, `MapProviderAdapter` | WebMercator + OSM 瓦片已接入；离线下载管理待完善 |
| OxyPlot.Avalonia | 未引用 | `TelemetryChartRuntime`, `TelemetryChartPlaceholder` | 图表 blocked |
| LibVLCSharp.Avalonia | 未引用 | `IVideoDecoder`, `VideoDecodePipeline`, `VideoSettingsRuntime` | 视频 blocked |
| OpenTK / Avalonia OpenGL | 未引用 | 路线图规划 | 3D blocked |

---

## 9. 构建验证

| 项目 | 最近验证 | 结果 |
|---|---|---|
| `VGC/VGC.csproj` | 2026-07-02 | 0 错误 / 0 警告 |
| `VGC.Desktop` | 未在本次验证 | 需单独构建 |
| `VGC.Android` | 未在本次验证 | 需单独构建 |
| Tests | 未在本次验证 | 需补测试入口与执行记录 |

---

## 10. 映射结论

VGC 已经完成大部分 QGC UI 控件的 Avalonia 化移植，尤其是 Fly/Plan/Setup/Analyze/Settings 的页面骨架、Canvas 绘制控件、Toolbar 指示器、任务规划覆盖物和弹窗系统。后续重点不是继续堆 UI 控件，而是把现有占位和抽象接入真实生产运行时: Mapsui、LibVLCSharp、OxyPlot、完整 MAVLink dialect、PX4/ArduPilot SITL/硬件验证。
