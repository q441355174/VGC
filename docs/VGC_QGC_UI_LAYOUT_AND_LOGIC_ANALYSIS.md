# VGC vs QGC UI 布局、操作逻辑与页面关联分析

> 更新日期: 2026-07-02  
> 目标: 评估 VGC 使用 C# / Avalonia 复刻 QGC UI、交互逻辑和页面数据流的真实完成度。

---

## 1. 总体结论

| 维度 | 完成度 | 结论 |
|---|---:|---|
| UI 控件外观/交互 | 90–95% | 大部分 QGC 控件已有 Avalonia 实现 |
| 页面布局 | 85–90% | Fly/Plan/Setup/Analyze/Settings 页面完整 |
| 页面数据流 | 75–85% | ViewModel 与 runtime 基础已接通 |
| 真实地图/视频/图表 | 20–40% | 抽象就绪，外部库未接入 |
| QGC 生产级等价 | 55–65% | MAVLink、固件、硬件闭环仍需补齐 |

---

## 2. 导航架构

| 维度 | QGC | VGC | 状态 |
|---|---|---|---|
| 主视图模型 | Fly/Plan 常驻，visible 切换 | `ContentControl` 切换 ViewModel | 架构不同，可接受 |
| Tool drawer | Loader 覆盖页面 | Shell 顶栏 + 右侧抽屉 | 已实现 |
| 导航保护 | `allowViewSwitch()` | `NavigationGuard` / ViewModel 状态 | 已实现 |
| Toast | validation toast | `ToastNotification` | 已实现 |
| 关闭检查 | 未保存任务/参数/连接 | Close warnings | 已实现 |
| 首次引导 | first run prompt | FirstRunPromptService | 已实现 |

---

## 3. 页面布局状态

| 页面 | VGC 文件 | 当前状态 | 主要缺口 |
|---|---|---|---|
| Fly | `FlyView.axaml` 729 行 | 固定工具栏、地图层、ToolStrip、HUD、PiP、虚拟摇杆、告警、飞前检查齐全 | 地图底图和视频是占位/空实现 |
| Plan | `PlanView.axaml` 322 行 | Mission/Fence/Rally、右侧编辑面板、上传下载确认、地图覆盖物齐全 | 地图瓦片未接，拖拽精度待补 |
| Setup | `SetupView.axaml` 539 行 | Safety/Sensors/Radio/Power/Airframe/Motors/PID/Joystick UI 齐全 | 真实 PX4/APM 流程证据不足 |
| Analyze | `AnalyzeView.axaml` 310 行 | Inspector、Console、Replay、Log、GeoTag、Vibration 基础齐全 | OxyPlot 图表和 FFT 数据流未完成 |
| Settings | `SettingsView.axaml` 100 行 | General、Connection、OfflineMap、NTRIP、VideoSettings 框架存在 | 视频 codec、地图 provider、平台存储需接真实库 |
| Parameters | `ParameterView.axaml` 113 行 | 搜索、列表、编辑状态存在 | 完整元数据和 SITL 写入验证不足 |
| Shell | `MainView.axaml` 241 行 | 导航、状态、抽屉、日志入口 | 与 QGC 交互模型不同但可用 |

---

## 4. FlyView 操作逻辑

| 操作 | QGC 流程 | VGC 状态 | 缺口 |
|---|---|---|---|
| 起飞/降落/RTL/暂停 | ToolStrip → 确认 → MAVLink command | GuidedAction + QgcDelayButton 已有 | 需 SITL/实机命令证据 |
| 飞前检查 | PreFlight popup | FlyView 弹窗 + Checklist model | 需按机型/固件细化 |
| 高度输入 | GuidedValueSlider | 已有 | 需命令闭环验证 |
| 地图/视频 PiP | PipView 交换 | UI 已有 | 视频源未真实渲染 |
| 告警消息 | VehicleWarnings | 横幅/消息行已实现 | 健康聚合仍不完整 |
| 虚拟摇杆 | On-screen joystick | Runtime + UI 已有 | 需实机控制和安全门限验证 |
| 云台/相机 | Gimbal/camera controls | UI + command boundary 已有 | camera/gimbal MAVLink 状态 partial |

---

## 5. PlanView 操作逻辑

| 区域 | 已实现 | 剩余 |
|---|---|---|
| Mission item | 航点添加、排序、编辑、摘要行、统计条 | 复杂任务和 QGC 全参数编辑仍需扩展 |
| GeoFence | polygon/circle 覆盖物和传输边界 | 地图 provider 与真实坐标交互需验证 |
| Rally | rally point 编辑/传输边界 | SITL/实机证据 |
| 文件互通 | Plan JSON import/export | QGC 全格式兼容需持续测试 |
| 地图交互 | overlay、fit、click tools | Mercator 转换、拖拽预览、真实瓦片 |

---

## 6. SetupView 操作逻辑

| 区域 | UI 状态 | 生产级状态 |
|---|---|---|
| Airframe | 机架卡片和选择 UI 已有 | PX4/ArduPilot apply transcript 缺失 |
| Sensors | 校准按钮、6 面引导、进度状态已实现 | 真实 accel/gyro/level/compass transcript 缺失 |
| Radio | PWM 条形图、通道表已实现 | 物理遥控器校准证据缺失 |
| Power | 电池/电压/电流 UI 已实现 | 实际 battery monitor 验证缺失 |
| Motors/Safety | 电机测试和安全开关 UI 已实现 | ArduPilot actuator parity 和 live motor evidence blocked |
| PID | 分组滑块/数值 UI 已实现 | 实时图表和参数写入证据不足 |
| Joystick | 轴映射/按钮/死区 UI 已实现 | 真实输入设备覆盖有限 |

---

## 7. AnalyzeView 操作逻辑

| 功能 | 当前状态 | 缺口 |
|---|---|---|
| MAVLink Inspector | 消息过滤、列表、包表存在 | 完整 dialect 字段覆盖不足 |
| MAVLink Console | NSH 输入/输出 UI 存在 | 真实 shell 会话 transcript 待验证 |
| Log Replay | 播放/暂停/步进/速度/seek 存在 | 更多日志格式和边界测试 |
| Telemetry Chart | 数据系列管理存在 | `TelemetryChartPlaceholder` 仍需 OxyPlot |
| Vibration | 3 轴条形图、频谱控件存在 | RAW_IMU→FFT 数据流未生产化 |
| GeoTag | 文件选择/偏移 UI 存在 | 真实图片写入和格式覆盖需验证 |

---

## 8. 页面间数据流

| 数据源 | 流向 | 状态 |
|---|---|---|
| `LinkManager` | Shell、Fly、Plan、Overview、MAVLink | 已接通 |
| `MavlinkProtocol` | Shell、Fly、Analyze、Vehicle | 已接通 |
| `MultiVehicleManager` | Shell、Fly、Plan、Setup、Parameter | 已接通 |
| `GuidedActionController` | Fly command flow | 已接通，需实机证据 |
| `MissionTransferManager` | Plan upload/download/clear | 已接通，需 SITL 覆盖 |
| `ParameterManager` | ParameterView/Setup facts | 已接通，metadata 不完整 |
| `VideoDecodePipeline` | Payload/Fly video | 抽象存在，默认 `NullVideoDecoder` |
| `MapProviderHost` | Fly/Plan map runtime | local fallback 存在，真实 provider 未接 |

---

## 9. 自定义控件与运行时规模

| 模块 | .cs 文件 | 代码行 | 判断 |
|---|---:|---:|---|
| Views | 19 | 17652 | UI 控件主力，完成度高 |
| ViewModels | 13 | 4916 | 页面状态和命令基本完整 |
| Mission | 36 | 3699 | Mission/Fence/Rally 结构完整 |
| Mavlink | 30 | 3557 | 基础协议强，完整 dialect blocked |
| Vehicles | 24 | 2795 | 多机/状态/fact 基础完整，specialty fact 缺 |
| Comms | 29 | 2534 | UDP/TCP/Serial/Bluetooth/Mock/Replay/NTRIP 结构齐全 |
| Payload | 16 | 2074 | Camera/Gimbal 边界有，视频解码缺 |
| Analyze | 14 | 2620 | 日志/回放/Inspector 基础齐全 |
| Maps | 18 | 1906 | provider/cache/adapter 有，真实渲染缺 |
| Setup | 9 | 1386 | 设置 runtime 有，生产证据不足 |
| Firmware | 14 | 904 | PX4/APM profile 有，metadata drift blocked |

---

## 10. 剩余差距

| 优先级 | 差距 | 证据/位置 | 影响 |
|---|---|---|---|
| P0 | 真实地图瓦片 | Mapsui WebMercator renderer、OSM 在线瓦片、本地文件缓存已接入 | 离线下载管理和 UI 运行验证仍待完善 |
| P0 | 视频流播放 | `NullVideoDecoder` | FlyView 视频不可用 |
| P0 | MAVLink full dialect | generator/dialect blocked | 无法宣称完整 QGC 协议覆盖 |
| P0 | MAVLink runtime adoption | command/mission/param/camera/gimbal partial | 真实飞控闭环风险 |
| P1 | OxyPlot 图表 | `TelemetryChartPlaceholder` | Analyze/PID 图表不可用 |
| P1 | Setup 真实流程 | Firmware setup production partial/blocked | Setup 只能算 UI+边界完成 |
| P1 | Mission drag 精度 | linear screen/geo 转换 | 地图编辑体验偏离 QGC |
| P1 | Vehicle fact groups | specialty fact groups missing | 健康/状态细节不足 |
| P2 | Android/Desktop 平台验证 | 本次仅 Core build | 跨平台交付风险 |
| P3 | 3D Viewer | 未接 3D 库 | QGC Viewer3D 缺失 |

---

## 11. 构建状态

| 项目 | 本次状态 |
|---|---|
| `VGC/VGC.csproj` | 0 错误 / 0 警告 |
| `VGC.Desktop` | 未验证 |
| `VGC.Android` | 未验证 |
| Tests | 未验证 |

---

## 12. UI 分析结论

VGC 的 UI 层已进入“补真实运行时”阶段。继续开发应优先接入 Mapsui、LibVLCSharp、OxyPlot，并用 SITL/实机 transcript 证明 MAVLink、Setup、Mission、Parameter 的闭环，而不是继续把控件完成率写成接近 100%。
