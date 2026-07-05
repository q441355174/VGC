# QGC → VGC 移植进度清单

> 更新日期: 2026-07-05  
> 基准: `E:/Code/VGC/qgroundcontrol/src` 与当前 VGC 源码  
> 权威状态源: `VGC/Release/QgcSourcePortInventory.cs`, `VGC/ViewModels/QgcQmlParityCatalog.cs`, `VGC/Release/QgcReplacementAudit.cs`, `VGC/Release/FullPortAudit.cs`

## 结论

| 项 | 当前状态 |
|---|---|
| QGC 源码级 parity | 未完成 |
| QGC replacement | 未完成 |
| Release packaging | 已通过 |
| Final replacement acceptance | 未完成 |
| 桌面端核心 UI | 基本可运行 |
| Fly/Plan 地图 | 已有在线 raster 底图、基础交互和 overlay；离线/Android/SITL 仍缺 |
| 主要阻塞 | Android workload/device、SITL、真实硬件、视频播放、完整 MAVLink dialect、release artifacts |

当前不能声明“完整移植”。进度应以源码级 inventory 和测试中的防过度声明为准。

## 源码覆盖统计

| QGC source | Count | VGC 状态 |
|---|---:|---|
| `src/**/*.qml` | 442 | 已按 QML 模块建账 |
| `src/**/*.cc` | 497 | 已纳入源码级清单，后端多数为 partial |
| `src/**/*.h` | 571 | 已纳入源码级清单，后端多数为 partial |

| VGC area | `.cs` count | 说明 |
|---|---:|---|
| `Mission` | 36 | Plan、Mission/Fence/Rally、transfer、complex preview |
| `Comms` | 31 | Serial/TCP/UDP/mock/replay、Android 边界 |
| `Mavlink` | 30 | parser/writer/CRC/部分 message/service |
| `Vehicles` | 24 | 多机、状态、fact group、命令边界 |
| `Maps` | 18 | provider、tile source、cache/offline 框架 |
| `Payload` | 16 | video/camera/gimbal 状态与命令边界 |
| `Analyze` | 14 | inspector、replay、logs、geotag、chart runtime |
| `Firmware` | 14 | PX4/APM profiles、命令能力、setup parity |
| `Facts` | 13 | metadata、validation、parameter edit |
| `Views` | 11 | Avalonia 页面 |
| `Setup` | 9 | calibration/setup runtime |
| `VGC.UI/Controls` | 9 | QGC 风格控件库 |

## QGC 源码区域对照

| QGC area | QML | C++ | H | VGC target | Status | Coverage | Remaining work |
|---|---:|---:|---:|---|---|---:|---|
| QmlControls | 98 | 32 | 33 | `VGC.UI/Controls` | Partial | 78% | screenshot parity、完整控件行为证据 |
| FlyView | 59 | 0 | 0 | `FlyView.axaml`, `FlyViewModel.cs` | Partial | 72% | SITL/实机、截图、完整 action menu 行为 |
| FlightMap | 29 | 0 | 0 | `MapControls.cs`, `VGC/Maps` | Partial | 68% | offline regions、Android map lifecycle、provider settings、SITL |
| PlanView | 43 | 0 | 0 | `PlanView.axaml`, `PlanViewModel.cs` | Partial | 72% | complex item authoring、terrain workflow、SITL transfer transcript |
| AnalyzeView | 18 | 22 | 24 | `VGC/Analyze`, `AnalyzeViewModel.cs` | Partial | 70% | real log packs、screenshot parity |
| AppSettings | 29 | 0 | 0 | `SettingsView.axaml`, `Core/Settings` | Partial | 55% | full settings pages、map/offline settings、signing UI、device evidence |
| Settings | 0 | 27 | 27 | `Core/Settings`, `Comms` | Partial | 55% | complete QGC settings backend parity |
| AutoPilotPlugins | 84 | 55 | 55 | `VGC/Firmware`, `VGC/Setup` | Partial | 58% | airframe/calibration depth、live firmware behavior、hardware evidence |
| FirmwarePlugin | 7 | 14 | 16 | `VGC/Firmware` | Partial | 62% | full plugin behavior、SITL evidence |
| FactSystem | 18 | 10 | 10 | `VGC/Facts`, `FactControls.cs` | Partial | 70% | full metadata、edge-case validation |
| Vehicle | 12 | 62 | 64 | `VGC/Vehicles`, `VGC/Mavlink` | Partial | 60% | full QGC Vehicle、failsafe、terrain、avoidance、signing、hardware evidence |
| MissionManager | 0 | 37 | 39 | `VGC/Mission` | Partial | 72% | protocol completeness、SITL upload/download evidence |
| Comms | 0 | 24 | 24 | `VGC/Comms` | Partial | 68% | Bluetooth、Android USB/device、field validation、link recovery parity |
| MAVLink | 0 | 14 | 19 | `VGC/Mavlink` | Partial | 65% | full dialect coverage、signing、edge cases |
| GPS | 3 | 17 | 25 | `VGC/Positioning` | Partial | 55% | real GPS、permissions、mobile field evidence |
| VideoManager | 0 | 46 | 55 | `VGC/Payload` | Partial | 45% | real stream pipeline、decoder/rendering、payload evidence |
| Camera | 0 | 7 | 7 | `VGC/Payload` | Partial | 55% | real camera capability、media workflow evidence |
| Terrain | 0 | 6 | 7 | `VGC/Terrain` | Partial | 45% | real terrain service、full QGC terrain workflow |
| Viewer3D | 8 | 11 | 13 | Deferred | Deferred | 0% | 3D viewer not migrated |
| Android | 0 | 4 | 10 | `VGC.Android`, `Comms/Android*` | Blocked | 30% | workload/device validation、native serial parity |
| ADSB | 0 | 3 | 4 | `VGC/Traffic` | Partial | 45% | live ADSB source、map/runtime evidence |
| LogManager | 1 | 5 | 5 | `VGC/Analyze` | Partial | 65% | real log corpus、UI runtime evidence |
| Utilities | 0 | 88 | 95 | `Core`, `Validation`, `Release` | Partial | 50% | selective replacement only, not one-to-one mapped |
| QtLocationPlugin | 0 | 0 | 26 | `VGC/Maps` | Partial | 45% | intentionally replaced by Avalonia/Mapsui-style rendering |
| API | 0 | 3 | 3 | `Composition`, `ViewModels` | Mapped | 40% | public plugin API parity not claimed |
| ReleasePackaging | 0 | 0 | 0 | `VGC/Release` | Blocked | 25% | signed Android package、desktop artifact、final evidence pack |

## 页面与 UI 状态

| Area | Current state | Remaining work |
|---|---|---|
| Shell/navigation | Avalonia shell、toolbar、drawer、logs 入口已实现 | QGC screenshot/resize/platform shell parity |
| Fly | toolbar、ToolStrip、HUD、PiP、虚拟摇杆、告警、飞前检查、地图接线已实现；二级/三级入口基本正确 | SITL/实机命令证据、视频真实播放、截图 parity、更多动作矩阵 |
| Plan | mission/fence/rally、地图点击、overlay、transfer、import/export 已实现；二级/三级编辑入口基本正确 | complex item 全量 authoring、terrain、SITL upload/download |
| Setup | safety/sensors/radio/power/airframe/motors/PID/joystick UI 与状态机存在；二级/三级分流基本正确 | PX4/APM 真实校准、机架、电源、电机证据 |
| Analyze | inspector、console、replay、logs、geotag、vibration 基础存在；二级/三级调用正确 | OxyPlot 图表、FFT 数据流、真实日志 corpus |
| Settings | grouped settings、link config、NTRIP/offline map/video settings 框架存在 | 完整 QGC settings runtime、signing UI、平台存储/device evidence |
| Parameters | 搜索、编辑、写入边界存在 | 完整 metadata、SITL/实机参数写入证据 |

## 生产级阻塞项

| Priority | Blocker | Current state | Required evidence |
|---|---|---|---|
| P0 | Android build/device | Android workload build、APK install、emulator lifecycle 已验证 | physical USB/serial skipped this pass |
| P0 | SITL validation | TCP SITL heartbeat/telemetry transcript 已采集 | active command transcript blocked pending explicit scenario authorization |
| P0 | Real hardware | Skipped this pass | out of current scope |
| P0 | Release artifacts | desktop publish + Android Release signed APK present | final acceptance still needs parity/runtime evidence |
| P0 | MAVLink full dialect | parser/writer、seed coverage、manifest、common/ardupilotmega seed fixture 存在 | full upstream common/ardupilotmega generated coverage |
| P0 | Video playback | Skipped real stream; synthetic decoder/frame health test present | out of current scope |
| P1 | Map offline/provider UI | online raster、offline planning/policy、download queue exists | Android map lifecycle/provider UI evidence |
| P1 | Analyze charts | chart runtime/placeholder exists | OxyPlot rendering + data stream evidence |
| P1 | Setup production workflows | UI/state machines exist | PX4/APM calibration/airframe/power/motor transcripts |
| P3 | Viewer3D | deferred | selected 3D stack + minimum viewer implementation |

## 验证状态

| Check | Latest result |
|---|---|
| `dotnet test "E:/Code/VGC/VGC/VGC.Tests/VGC.Tests.csproj" --no-restore` | Pass |
| `dotnet build "E:/Code/VGC/VGC/VGC.Desktop/VGC.Desktop.csproj" --no-restore` | Pass, 0 warnings/errors |
| Android build | Pass: SSH/Linux build + emulator install/launch transcript captured |
| SITL | Partial: TCP heartbeat/telemetry transcript captured; ArduPilot/command evidence missing |
| Real hardware | Skipped: out of current scope |
| Release candidate | Packaging pass: desktop publish + Android Release signed APK present; final replacement still blocked by runtime/parity evidence |
| Evidence index | `docs/RELEASE_EVIDENCE_INDEX.md` |

## 文档维护规则

| Rule | Apply |
|---|---|
| 进度以代码审计模型为准 | 更新 `QgcSourcePortInventory.cs` / `QgcQmlParityCatalog.cs` 后再更新本文 |
| 不用 DONE 表示生产级完成 | 没有 runtime/SITL/device/release evidence 时保持 Partial/Blocked |
| 删除重复路线图 | 本文保留源码对照、页面状态、阻塞项；路线图由代码 audit/tests 承载 |
| 新增功能后补测试 | 防止 `CanClaim*` 被误改为 true |
