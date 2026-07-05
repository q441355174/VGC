using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using VGC.Analyze;
using VGC.Comms;
using VGC.Core.Settings;
using VGC.Core.Application;
using VGC.Core.Logging;
using VGC.Core.Localization;
using VGC.Input;
using VGC.Mavlink;
using VGC.Mavlink.Generated;
using VGC.Vehicles;
using VGC.Facts;
using VGC.Firmware;
using VGC.Maps;
using VGC.Mission;
using VGC.Payload;
using VGC.Positioning;
using VGC.Release;
using VGC.Setup;
using VGC.Terrain;
using VGC.Traffic;
using VGC.Validation;
using VGC.ViewModels;

var tests = new (string Name, Action Test)[]
{
    ("Parse MAVLink v1 heartbeat", ParseV1Heartbeat),
    ("Reject invalid MAVLink v1 heartbeat CRC", RejectInvalidV1HeartbeatCrc),
    ("Recover after invalid MAVLink v1 CRC", RecoverAfterInvalidV1Crc),
    ("Keep unsupported MAVLink v1 messages parseable", KeepUnsupportedV1MessagesParseable),
    ("Parse split MAVLink v1 heartbeat", ParseSplitV1Heartbeat),
    ("Recover after invalid prefix bytes", RecoverAfterInvalidPrefixBytes),
    ("Buffer incomplete frame", BufferIncompleteFrame),
    ("Parse MAVLink v2 heartbeat shape", ParseV2Heartbeat),
    ("Create GCS heartbeat frame", CreateGcsHeartbeatFrame),
    ("Create COMMAND_LONG frame", CreateCommandLongFrame),
    ("Parse COMMAND_ACK", ParseCommandAck),
    ("Send vehicle arm and disarm commands", SendVehicleArmDisarmCommands),
    ("Track command queue ack state", TrackCommandQueueAckState),
    ("Reject duplicate pending vehicle command", RejectDuplicatePendingVehicleCommand),
    ("Track guided action command states", TrackGuidedActionCommandStates),
    ("Create PARAM_REQUEST_LIST frame", CreateParamRequestListFrame),
    ("Create PARAM_REQUEST_READ frame by index", CreateParamRequestReadFrameByIndex),
    ("Create PARAM_REQUEST_READ frame by name", CreateParamRequestReadFrameByName),
    ("Send PARAM_REQUEST_READ frame", SendParamRequestReadFrame),
    ("Create PARAM_SET frame from Fact", CreateParamSetFrameFromFact),
    ("Create MISSION_REQUEST_LIST frame", CreateMissionRequestListFrame),
    ("Create and parse MISSION_COUNT frame", CreateAndParseMissionCountFrame),
    ("Create and parse MISSION_REQUEST_INT frame", CreateAndParseMissionRequestIntFrame),
    ("Create and parse MISSION_ITEM_INT frame", CreateAndParseMissionItemIntFrame),
    ("Create and parse MISSION_ACK frame", CreateAndParseMissionAckFrame),
    ("Create MISSION_CLEAR_ALL frame", CreateMissionClearAllFrame),
    ("Create and parse mission type frames", CreateAndParseMissionTypeFrames),
    ("Send mission frame", SendMissionFrame),
    ("Track ParameterManager request and write state", TrackParameterManagerState),
    ("Track named parameter writes", TrackNamedParameterWrites),
    ("Clear pending parameter write from matching PARAM_VALUE", ClearPendingParameterWriteFromMatchingParamValue),
    ("Keep unrelated parameter writes pending", KeepUnrelatedParameterWritesPending),
    ("Track parameter download progress", TrackParameterDownloadProgress),
    ("Mark parameter download ready when complete", MarkParameterDownloadReadyWhenComplete),
    ("Report missing parameter indexes", ReportMissingParameterIndexes),
    ("Parameter view lists active vehicle parameters", ParameterViewListsActiveVehicleParameters),
    ("Parameter view filters parameter rows", ParameterViewFiltersParameterRows),
    ("Parameter view reports pending writes", ParameterViewReportsPendingWrites),
    ("Parameter projection keeps rows without metadata", ParameterProjectionKeepsRowsWithoutMetadata),
    ("Parameter projection merges metadata", ParameterProjectionMergesMetadata),
    ("Load parameter metadata from JSON", LoadParameterMetadataFromJson),
    ("Parameter view uses runtime metadata catalog", ParameterViewUsesRuntimeMetadataCatalog),
    ("Validate parameter edit before commit", ValidateParameterEditBeforeCommit),
    ("Project parameter write retry and failure state", ProjectParameterWriteRetryAndFailureState),
    ("Send parameter write and readback from parameter view", SendParameterWriteAndReadbackFromParameterView),
    ("Round-trip parameter cache snapshot", RoundTripParameterCacheSnapshot),
    ("Project parameter cache states", ProjectParameterCacheStates),
    ("Parse STATUSTEXT", ParseStatusText),
    ("Reject invalid STATUSTEXT CRC", RejectInvalidStatusTextCrc),
    ("Raise STATUSTEXT protocol event", RaiseStatusTextProtocolEvent),
    ("Aggregate MAVLink inspector packets", AggregateMavlinkInspectorPackets),
    ("Decode MAVLink inspector packets", DecodeMavlinkInspectorPackets),
    ("Filter MAVLink inspector rows", FilterMavlinkInspectorRows),
    ("Feed MAVLink inspector from protocol", FeedMavlinkInspectorFromProtocol),
    ("Analyze view hosts MAVLink inspector", AnalyzeViewHostsMavlinkInspector),
    ("Analyze view filters MAVLink inspector rows", AnalyzeViewFiltersMavlinkInspectorRows),
    ("Analyze view switches second-level tabs", AnalyzeViewSwitchesSecondLevelTabs),
    ("Analyze view exposes tab and action commands", AnalyzeViewExposesTabAndActionCommands),
    ("Apply vehicle status messages", ApplyVehicleStatusMessages),
    ("Track heartbeat flight mode state", TrackHeartbeatFlightModeState),
    ("Create SET_MODE frame", CreateSetModeFrame),
    ("Apply vehicle telemetry packets", ApplyVehicleTelemetryPackets),
    ("Apply vehicle battery status payload", ApplyVehicleBatteryStatusPayload),
    ("Validate Fact metadata range", ValidateFactMetadataRange),
    ("Apply PARAM_VALUE to ParameterManager", ApplyParamValueToParameterManager),
    ("Convert plan item to MISSION_ITEM_INT", ConvertPlanItemToMissionItemInt),
    ("Convert MISSION_ITEM_INT to plan item", ConvertMissionItemIntToPlanItem),
    ("Round-trip plan item mission conversion", RoundTripPlanItemMissionConversion),
    ("Track mission read transaction state", TrackMissionReadTransactionState),
    ("Track mission write transaction state", TrackMissionWriteTransactionState),
    ("Fail mission transfer on sequence mismatch", FailMissionTransferOnSequenceMismatch),
    ("Reject overlapping mission transactions", RejectOverlappingMissionTransactions),
    ("Route mission read packets through Vehicle", RouteMissionReadPacketsThroughVehicle),
    ("Route mission write packets through Vehicle", RouteMissionWritePacketsThroughVehicle),
    ("Route geofence read packets through Vehicle", RouteGeoFenceReadPacketsThroughVehicle),
    ("Route rally read packets through Vehicle", RouteRallyReadPacketsThroughVehicle),
    ("Ignore cross-type mission packets", IgnoreCrossTypeMissionPackets),
    ("Send mission transfer request-list action", SendMissionTransferRequestListAction),
    ("Send mission transfer count action", SendMissionTransferCountAction),
    ("Send mission transfer item action", SendMissionTransferItemAction),
    ("Send mission transfer actions with mission type", SendMissionTransferActionsWithMissionType),
    ("Ignore empty mission transfer action", IgnoreEmptyMissionTransferAction),
    ("Mission transfer service begins read", MissionTransferServiceBeginsRead),
    ("Mission transfer service handles read packet", MissionTransferServiceHandlesReadPacket),
    ("Mission transfer service completes write", MissionTransferServiceCompletesWrite),
    ("Mission transfer timeout resends last action", MissionTransferTimeoutResendsLastAction),
    ("Mission transfer packet resets retry count", MissionTransferPacketResetsRetryCount),
    ("Mission transfer retry exhaustion fails transfer", MissionTransferRetryExhaustionFailsTransfer),
    ("Plan view reflects active mission data", PlanViewReflectsActiveMissionData),
    ("Plan view edits waypoint mission items", PlanViewEditsWaypointMissionItems),
    ("Plan view switches plan sections", PlanViewSwitchesPlanSections),
    ("Plan view navigation keeps section and map tool independent", PlanViewNavigationKeepsSectionAndMapToolIndependent),
    ("Plan view edits geofence and rally sections", PlanViewEditsGeoFenceAndRallySections),
    ("Plan view adds waypoint from map click", PlanViewAddsWaypointFromMapClick),
    ("Plan view moves selected waypoint from map click", PlanViewMovesSelectedWaypointFromMapClick),
    ("Plan view adds geofence and rally from map clicks", PlanViewAddsGeoFenceAndRallyFromMapClicks),
    ("Plan view reflects mission transfer progress", PlanViewReflectsMissionTransferProgress),
    ("Plan view reflects mission transfer errors", PlanViewReflectsMissionTransferErrors),
    ("Plan view uploads geofence transfer", PlanViewUploadsGeoFenceTransfer),
    ("Plan view uploads rally transfer", PlanViewUploadsRallyTransfer),
    ("Plan view reports section transfer blockers", PlanViewReportsSectionTransferBlockers),
    ("Plan view exposes section transfer availability", PlanViewExposesSectionTransferAvailability),
    ("Plan view exposes transfer command model", PlanViewExposesTransferCommandModel),
    ("Plan view confirms pending mission transfer", PlanViewConfirmsPendingMissionTransfer),
    ("Plan view confirms pending section transfer", PlanViewConfirmsPendingSectionTransfer),
    ("Plan view cancels pending section transfer", PlanViewCancelsPendingSectionTransfer),
    ("Plan view emits transfer notifications", PlanViewEmitsTransferNotifications),
    ("Plan view bounds transfer notifications", PlanViewBoundsTransferNotifications),
    ("Round-trip persisted link configuration", RoundTripPersistedLinkConfiguration),
    ("Select active link by deterministic policy", SelectActiveLinkByDeterministicPolicy),
    ("Capture link configuration state", CaptureLinkConfigurationState),
    ("Create future link runtime configurations", CreateFutureLinkRuntimeConfigurations),
    ("Track link diagnostics projection", TrackLinkDiagnosticsProjection),
    ("Auto-connect saved link policy", AutoConnectSavedLinkPolicy),
    ("Initialize default Comm Link in shell", InitializeDefaultCommLinkInShell),
    ("Connect selected UDP Comm Link from shell", ConnectSelectedUdpCommLinkFromShell),
    ("Reject invalid selected Comm Link port", RejectInvalidSelectedCommLinkPort),
    ("Connect selected serial Comm Link from shell", ConnectSelectedSerialCommLinkFromShell),
    ("Edit link configuration view model", EditLinkConfigurationViewModel),
    ("Forward MAVLink packets across links", ForwardMavlinkPacketsAcrossLinks),
    ("Model Bluetooth link boundary", ModelBluetoothLinkBoundary),
    ("Model Android USB serial lifecycle", ModelAndroidUsbSerialLifecycle),
    ("Project link error recovery", ProjectLinkErrorRecovery),
    ("Catalog link runtime evidence", CatalogLinkRuntimeEvidence),
    ("TCP client link exchanges bytes", TcpClientLinkExchangesBytes),
    ("TCP server link accepts client bytes", TcpServerLinkAcceptsClientBytes),
    ("Serial link routes bytes through adapter", SerialLinkRoutesBytesThroughAdapter),
    ("Serial link validates configuration", SerialLinkValidatesConfiguration),
    ("Scale log replay timing", ScaleLogReplayTiming),
    ("Replay log packets in timestamp order", ReplayLogPacketsInTimestampOrder),
    ("Parse QGC telemetry log replay source", ParseQgcTelemetryLogReplaySource),
    ("Parse durable synthetic replay fixture", ParseDurableSyntheticReplayFixture),
    ("Control TLog replay playback session", ControlTLogReplayPlaybackSession),
    ("Build replay timeline and packet index", BuildReplayTimelineAndPacketIndex),
    ("Analyze view exposes replay playback controls", AnalyzeViewExposesReplayPlaybackControls),
    ("Analyze view exposes replay timeline projection", AnalyzeViewExposesReplayTimelineProjection),
    ("Analyze view exposes replay workflow state", AnalyzeViewExposesReplayWorkflowState),
    ("Manage telemetry chart runtime", ManageTelemetryChartRuntime),
    ("Analyze view exposes telemetry chart snapshot", AnalyzeViewExposesTelemetryChartSnapshot),
    ("Analyze view feeds telemetry chart from MAVLink", AnalyzeViewFeedsTelemetryChartFromMavlink),
    ("Analyze view summarizes durable replay diagnostics", AnalyzeViewSummarizesDurableReplayDiagnostics),
    ("Select ULog and DataFlash parser boundaries", SelectULogAndDataFlashParserBoundaries),
    ("Project parser diagnostics into flight log summary", ProjectParserDiagnosticsIntoFlightLogSummary),
    ("Model flight log download workflow", ModelFlightLogDownloadWorkflow),
    ("Model flight log download cancellation and retry", ModelFlightLogDownloadCancellationAndRetry),
    ("Match GeoTag images to log track points", MatchGeoTagImagesToLogTrackPoints),
    ("Apply GeoTag offset tolerance and deterministic ties", ApplyGeoTagOffsetToleranceAndDeterministicTies),
    ("Run MAVLink console runtime", RunMavlinkConsoleRuntime),
    ("Project flight log download panel", ProjectFlightLogDownloadPanel),
    ("Manage PX4 log metadata", ManagePx4LogMetadata),
    ("Inspect ULog parser runtime", InspectULogParserRuntime),
    ("Inspect DataFlash parser runtime", InspectDataFlashParserRuntime),
    ("Project GeoTag runtime UI", ProjectGeoTagRuntimeUi),
    ("Project file log viewer rows", ProjectFileLogViewerRows),
    ("Project replay workflow detail", ProjectReplayWorkflowDetail),
    ("Catalog analyze runtime evidence", CatalogAnalyzeRuntimeEvidence),
    ("Audit analyze runtime parity gaps", AuditAnalyzeRuntimeParityGaps),
    ("Map AnalyzeView QGC QML parity without runtime overclaim", MapAnalyzeViewQgcQmlParityWithoutRuntimeOverclaim),
    ("Parse external GPS NMEA fix", ParseExternalGpsNmeaFix),
    ("Select positioning source by fix quality", SelectPositioningSourceByFixQuality),
    ("Run RTK correction session", RunRtkCorrectionSession),
    ("Project platform positioning permissions", ProjectPlatformPositioningPermissions),
    ("Send FollowMe target command", SendFollowMeTargetCommand),
    ("Catalog positioning runtime evidence", CatalogPositioningRuntimeEvidence),
    ("Audit positioning runtime parity gaps", AuditPositioningRuntimeParityGaps),
    ("Catalog Gate 10 QGC QML remaining modules", CatalogGate10QgcQmlRemainingModules),
    ("Scan joystick devices", ScanJoystickDevices),
    ("Calibrate joystick axes", CalibrateJoystickAxes),
    ("Project joystick manual control", ProjectJoystickManualControl),
    ("Project ADSB traffic alerts and overlays", ProjectAdsbTrafficAlertsAndOverlays),
    ("Project RemoteID state and overlay", ProjectRemoteIdStateAndOverlay),
    ("Catalog input traffic runtime evidence", CatalogInputTrafficRuntimeEvidence),
    ("Audit input traffic runtime parity gaps", AuditInputTrafficRuntimeParityGaps),
    ("Navigate shell settings workspace", NavigateShellSettingsWorkspace),
    ("Open shell indicator drawers", OpenShellIndicatorDrawers),
    ("Open shell tool drawer navigation actions", OpenShellToolDrawerNavigationActions),
    ("Auto-connect shell TCP endpoint from environment", AutoConnectShellTcpEndpointFromEnvironment),
    ("Auto-connect shell TCP endpoint from startup intent", AutoConnectShellTcpEndpointFromStartupIntent),
    ("Catalog UI parity workflows", CatalogUiParityWorkflows),
    ("Catalog QGC QML module inventory", CatalogQgcQmlModuleInventory),
    ("Catalog QGC public component migration", CatalogQgcPublicComponentMigration),
    ("Audit QGC QML parity blockers", AuditQgcQmlParityBlockers),
    ("Prevent QGC QML migration overclaim", PreventQgcQmlMigrationOverclaim),
    ("Build desktop android usability matrix", BuildDesktopAndroidUsabilityMatrix),
    ("Audit UI parity residual gaps", AuditUiParityResidualGaps),
    ("Catalog SITL hardware validation scenarios", CatalogSitlHardwareValidationScenarios),
    ("Build guarded SITL command transcript plan", BuildGuardedSitlCommandTranscriptPlan),
    ("Gate SITL command authorization", GateSitlCommandAuthorization),
    ("Record SITL environment blockers", RecordSitlEnvironmentBlockers),
    ("Audit validation closure claims", AuditValidationClosureClaims),
    ("Catalog SITL hardware evidence", CatalogSitlHardwareEvidence),
    ("Catalog v1.54 release readiness", CatalogV154ReleaseReadiness),
    ("Audit release license inventory", AuditReleaseLicenseInventory),
    ("Plan desktop and Android release packages", PlanDesktopAndAndroidReleasePackages),
    ("Build Android release device matrix", BuildAndroidReleaseDeviceMatrix),
    ("Decide Browser and iOS release scope", DecideBrowserAndIosReleaseScope),
    ("Audit release closure blockers", AuditReleaseClosureBlockers),
    ("Catalog release evidence", CatalogReleaseEvidence),
    ("Prevent release candidate overclaim", PreventReleaseCandidateOverclaim),
    ("Catalog final full port module matrix", CatalogFinalFullPortModuleMatrix),
    ("Catalog final full port evidence gates", CatalogFinalFullPortEvidenceGates),
    ("Record deferred and not applicable decisions", RecordDeferredAndNotApplicableDecisions),
    ("Catalog release candidate blockers", CatalogReleaseCandidateBlockers),
    ("Audit final full port completion claims", AuditFinalFullPortCompletionClaims),
    ("Catalog final full port evidence", CatalogFinalFullPortEvidence),
    ("Verify full port risk register", VerifyFullPortRiskRegister),
    ("Prevent QGC full port overclaim", PreventQgcFullPortOverclaim),
    ("Catalog QGC replacement phase evidence", CatalogQgcReplacementPhaseEvidence),
    ("Audit QGC replacement acceptance blockers", AuditQgcReplacementAcceptanceBlockers),
    ("Plan QGC replacement evidence pack", PlanQgcReplacementEvidencePack),
    ("Audit QGC replacement final state", AuditQgcReplacementFinalState),
    ("Catalog screenshot parity evidence", CatalogScreenshotParityEvidence),
    ("Catalog QML parity sub evidence", CatalogQmlParitySubEvidence),
    ("Catalog QGC source port inventory", CatalogQgcSourcePortInventory),
    ("Audit QGC source port progress", AuditQgcSourcePortProgress),
    ("Prevent QGC source port overclaim", PreventQgcSourcePortOverclaim),
    ("Match QGC source inventory with QML catalog", MatchQgcSourceInventoryWithQmlCatalog),
    ("Catalog vehicle sub-manager parity", CatalogVehicleSubManagerParity),
    ("Catalog MAVLink dialect generation closure", CatalogMavlinkDialectGenerationClosure),
    ("Catalog MAVLink runtime adoption closure", CatalogMavlinkRuntimeAdoptionClosure),
    ("Catalog firmware setup production parity", CatalogFirmwareSetupProductionParity),
    ("Catalog desktop UI runtime evidence", CatalogDesktopUiRuntimeEvidence),
    ("Catalog Gate 11 runtime evidence without parity overclaim", CatalogGate11RuntimeEvidenceWithoutParityOverclaim),
    ("Catalog Android native integration closure", CatalogAndroidNativeIntegrationClosure),
    ("Catalog map production runtime closure", CatalogMapProductionRuntimeClosure),
    ("Catalog video payload production pipeline", CatalogVideoPayloadProductionPipeline),
    ("Catalog analyze logs utilities completion", CatalogAnalyzeLogsUtilitiesCompletion),
    ("Mock payload service boundaries", MockPayloadServiceBoundaries),
    ("Select video stream runtime state", SelectVideoStreamRuntimeState),
    ("Track camera runtime commands", TrackCameraRuntimeCommands),
    ("Track gimbal runtime commands", TrackGimbalRuntimeCommands),
    ("Select video backend decision", SelectVideoBackendDecision),
    ("Model video decode pipeline", ModelVideoDecodePipeline),
    ("Decode synthetic video frames", DecodeSyntheticVideoFrames),
    ("Model UVC device runtime", ModelUvcDeviceRuntime),
    ("Project video display layout", ProjectVideoDisplayLayout),
    ("Plan payload media output", PlanPayloadMediaOutput),
    ("Project thermal stream metadata", ProjectThermalStreamMetadata),
    ("Validate camera definition settings", ValidateCameraDefinitionSettings),
    ("Link gimbal ROI target", LinkGimbalRoiTarget),
    ("Catalog payload runtime evidence", CatalogPayloadRuntimeEvidence),
    ("Audit payload runtime parity gaps", AuditPayloadRuntimeParityGaps),
    ("Fly view reports no active vehicle", FlyViewReportsNoActiveVehicle),
    ("Fly view projects active vehicle indicators", FlyViewProjectsActiveVehicleIndicators),
    ("Fly view exposes operator layout contract", FlyViewExposesOperatorLayoutContract),
    ("Fly view exposes guided action command surface", FlyViewExposesGuidedActionCommandSurface),
    ("Build vehicle map overlays", BuildVehicleMapOverlays),
    ("Project local map display frame", ProjectLocalMapDisplayFrame),
    ("Fly view exposes local map display state", FlyViewExposesLocalMapDisplayState),
    ("Fly view exposes map host binding state", FlyViewExposesMapHostBindingState),
    ("Fly view projects home and trajectory state", FlyViewProjectsHomeAndTrajectoryState),
    ("Skip vehicle map overlay without coordinate", SkipVehicleMapOverlayWithoutCoordinate),
    ("Catalog FlyView QML runtime parity", CatalogFlyViewQmlRuntimeParity),
    ("Catalog FlightMap QML runtime parity", CatalogFlightMapQmlRuntimeParity),
    ("Keep QGC UI parity blocked after FlyView and FlightMap migration", KeepQgcUiParityBlockedAfterFlyViewAndFlightMapMapping),
    ("Catalog PlanView QML runtime parity", CatalogPlanViewQmlRuntimeParity),
    ("Keep QGC UI parity blocked after PlanView migration", KeepQgcUiParityBlockedAfterPlanViewMapping),
    ("Overview view reflects vehicle status", OverviewViewReflectsVehicleStatus),
    ("Round-trip edited plan document", RoundTripEditedPlanDocument),
    ("Round-trip geofence plan document", RoundTripGeoFencePlanDocument),
    ("Validate geofence plan document", ValidateGeoFencePlanDocument),
    ("Convert geofence plan mission items", ConvertGeoFencePlanMissionItems),
    ("Round-trip rally points plan document", RoundTripRallyPointsPlanDocument),
    ("Validate rally points plan document", ValidateRallyPointsPlanDocument),
    ("Convert rally point mission items", ConvertRallyPointMissionItems),
    ("Coordinate plan document sections", CoordinatePlanDocumentSections),
    ("Round-trip all plan document sections", RoundTripAllPlanDocumentSections),
    ("Build read-only plan map overlays", BuildReadOnlyPlanMapOverlays),
    ("Project plan map preview overlays", ProjectPlanMapPreviewOverlays),
    ("Track map follow and recenter state", TrackMapFollowAndRecenterState),
    ("Fly view exposes map follow state", FlyViewExposesMapFollowState),
    ("Fly view exposes payload control state", FlyViewExposesPayloadControlState),
    ("Track payload protocol command boundary", TrackPayloadProtocolCommandBoundary),
    ("Plan payload media storage", PlanPayloadMediaStorage),
    ("Map provider catalog exposes production candidates", MapProviderCatalogExposesProductionCandidates),
    ("Local map runtime implements provider adapter boundary", LocalMapRuntimeImplementsProviderAdapterBoundary),
    ("Map provider host selects local fallback", MapProviderHostSelectsLocalFallback),
    ("Map provider host exposes active raster tiles", MapProviderHostExposesActiveRasterTiles),
    ("Fly view exposes provider host state", FlyViewExposesProviderHostState),
    ("Bridge vehicle overlays to provider commands", BridgeVehicleOverlaysToProviderCommands),
    ("Bridge plan overlays to provider commands", BridgePlanOverlaysToProviderCommands),
    ("Create stable map tile cache keys", CreateStableMapTileCacheKeys),
    ("Create provider tile cache policies", CreateProviderTileCachePolicies),
    ("Track map interaction selection runtime", TrackMapInteractionSelectionRuntime),
    ("Project map provider attribution policy", ProjectMapProviderAttributionPolicy),
    ("Estimate offline map region tiles", EstimateOfflineMapRegionTiles),
    ("Plan offline map provider policy", PlanOfflineMapProviderPolicy),
    ("Run offline map download queue", RunOfflineMapDownloadQueue),
    ("Evict map tile cache entries", EvictMapTileCacheEntries),
    ("Model Android map lifecycle risks", ModelAndroidMapLifecycleRisks),
    ("Catalog map runtime evidence", CatalogMapRuntimeEvidence),
    ("Audit map offline parity gaps", AuditMapOfflineParityGaps),
    ("Apply section scoped plan map edits", ApplySectionScopedPlanMapEdits),
    ("Reject invalid plan map edits", RejectInvalidPlanMapEdits),
    ("Round-trip complex mission models", RoundTripComplexMissionModels),
    ("Calculate complex mission previews", CalculateComplexMissionPreviews),
    ("Calculate complex authoring previews from settings", CalculateComplexAuthoringPreviewsFromSettings),
    ("Query terrain through cache boundary", QueryTerrainThroughCacheBoundary),
    ("Plan terrain adjusted mission altitudes", PlanTerrainAdjustedMissionAltitudes),
    ("Preview terrain backed mission altitudes", PreviewTerrainBackedMissionAltitudes),
    ("Preview mission altitudes without terrain", PreviewMissionAltitudesWithoutTerrain),
    ("Lookup mission command metadata", LookupMissionCommandMetadata),
    ("Load QGC mission command metadata", LoadQgcMissionCommandMetadata),
    ("Gate loaded mission command metadata by firmware", GateLoadedMissionCommandMetadataByFirmware),
    ("Gate mission command availability by firmware", GateMissionCommandAvailabilityByFirmware),
    ("Track geofence transfer state boundary", TrackGeoFenceTransferStateBoundary),
    ("Keep geofence transfer separate from mission transfer", KeepGeoFenceTransferSeparateFromMissionTransfer),
    ("GeoFence transfer service writes fence items", GeoFenceTransferServiceWritesFenceItems),
    ("GeoFence transfer service reads fence plan", GeoFenceTransferServiceReadsFencePlan),
    ("GeoFence transfer service maps timeout", GeoFenceTransferServiceMapsTimeout),
    ("Track rally transfer state boundary", TrackRallyTransferStateBoundary),
    ("Keep rally transfer separate from mission and geofence transfer", KeepRallyTransferSeparateFromMissionAndGeoFenceTransfer),
    ("Rally transfer service writes rally items", RallyTransferServiceWritesRallyItems),
    ("Rally transfer service reads rally plan", RallyTransferServiceReadsRallyPlan),
    ("Rally transfer service maps timeout", RallyTransferServiceMapsTimeout),
    ("Select firmware plugins by autopilot", SelectFirmwarePluginsByAutopilot),
    ("Expose firmware vehicle support profiles", ExposeFirmwareVehicleSupportProfiles),
    ("Resolve firmware specific flight modes", ResolveFirmwareSpecificFlightModes),
    ("Expose firmware command capability tables", ExposeFirmwareCommandCapabilityTables),
    ("Select vehicle setup components by firmware", SelectVehicleSetupComponentsByFirmware),
    ("Project read-only setup summary", ProjectReadOnlySetupSummary),
    ("Project setup component status from parameters", ProjectSetupComponentStatusFromParameters),
    ("Run sensor calibration workflow states", RunSensorCalibrationWorkflowStates),
    ("Cancel and fail sensor calibration workflow", CancelAndFailSensorCalibrationWorkflow),
    ("Project radio and power setup boundaries", ProjectRadioAndPowerSetupBoundaries),
    ("Run motor safety workflow states", RunMotorSafetyWorkflowStates),
    ("Project motor safety setup boundaries", ProjectMotorSafetySetupBoundaries),
    ("Derive plan transfer support from vehicle firmware", DerivePlanTransferSupportFromVehicleFirmware),
    ("Gate plan transfer support", GatePlanTransferSupport),
    ("Round-trip minimal plan document", RoundTripMinimalPlanDocument),
    ("Parse QGC plan fixture with sections", ParseQgcPlanFixtureWithSections),
    ("Export QGC compatible plan shape", ExportQgcCompatiblePlanShape),
    ("Report structured plan import errors", ReportStructuredPlanImportErrors),
    ("Round-trip plan import export service", RoundTripPlanImportExportService),
    ("Plan view exposes import export status", PlanViewExposesImportExportStatus),
    ("Plan view exposes authoring preview state", PlanViewExposesAuthoringPreviewState),
    ("Plan view exposes workflow panel state", PlanViewExposesWorkflowPanelState),
    ("Plan view exposes file workflow state", PlanViewExposesFileWorkflowState),
    ("Load Tianditu API key from settings store", LoadTiandituApiKeyFromSettings),
    ("Load Tianditu API key from environment fallback", LoadTiandituApiKeyFromEnvironmentFallback),
    ("Disable Tianditu provider when key is missing", DisableTiandituProviderWhenKeyMissing),
    ("Generate Tianditu tile URL with key injection", GenerateTiandituTileUrlWithKeyInjection),
    ("Add and discover configured video streams", AddAndDiscoverConfiguredVideoStreams),
    ("Start and stop video stream state machine", StartAndStopVideoStreamStateMachine),
    ("Reject start stream when already active", RejectStartVideoStreamWhenAlreadyActive),
    ("Send camera image capture command", SendCameraImageCaptureCommand),
    ("Send camera video record commands", SendCameraVideoRecordCommands),
    ("Send gimbal pitch yaw command", SendGimbalPitchYawCommand),
    ("Create raster tile adapter with OSM descriptor", CreateRasterTileAdapterWithOsmDescriptor),
    ("Fetch raster tile through adapter", FetchRasterTileThroughAdapter),
    ("Store and reload map tile cache entry", StoreAndReloadMapTileCacheEntry),
    ("Create Tianditu raster adapter with key injection", CreateTiandituRasterAdapter),
    ("Fetch Tianditu raster tile through adapter", FetchTiandituRasterTileThroughAdapter),
    ("Validate CRC extra registry coverage", ValidateCrcExtraRegistryCoverage),
    ("Apply vehicle attitude telemetry", ApplyVehicleAttitudeTelemetry),
    ("Serialize QGC compatible plan with coordinate field", SerializeQgcCompatiblePlanWithCoordinate),
    ("Evaluate preflight checklist with telemetry", EvaluatePreflightChecklistWithTelemetry),
    ("Detect communication lost on heartbeat timeout", DetectCommunicationLostOnHeartbeatTimeout),
    ("Ignore non vehicle heartbeats", IgnoreNonVehicleHeartbeats),
    ("Track vehicle link manager bytes and errors", TrackVehicleLinkManagerBytesAndErrors),
    ("Enqueue and acknowledge vehicle command", EnqueueAndAcknowledgeVehicleCommand),
    ("Retry expired vehicle command", RetryExpiredVehicleCommand),
    ("Fail vehicle command after max retries", FailVehicleCommandAfterMaxRetries),
    ("Get vehicle capabilities from firmware plugin", GetVehicleCapabilitiesFromFirmwarePlugin),
    ("Set message interval via command long", SetMessageIntervalViaCommandLong),
    ("Apply message interval runtime policies", ApplyMessageIntervalRuntimePolicies),
    ("Retry and acknowledge message interval requests", RetryAndAcknowledgeMessageIntervalRequests),
    ("Expose MAVLink stream config profiles", ExposeMavlinkStreamConfigProfiles),
    ("Apply stream config profile through vehicle manager", ApplyStreamConfigProfileThroughVehicleManager),
    ("Track initial connect state machine", TrackInitialConnectStateMachine),
    ("Request streams and parameters after initial heartbeat", RequestStreamsAndParametersAfterInitialHeartbeat),
    ("Update battery fact group from vehicle telemetry", UpdateBatteryFactGroupFromVehicleTelemetry),
    ("Update vehicle baseline fact groups", UpdateVehicleBaselineFactGroups),
    ("Catalog vehicle core parity gaps", CatalogVehicleCoreParityGaps),
    ("Resolve vehicle standard modes", ResolveVehicleStandardModes),
    ("Track component information request state", TrackComponentInformationRequestState),
    ("Record vehicle trajectory points", RecordVehicleTrajectoryPoints),
    ("Audit vehicle core parity blockers", AuditVehicleCoreParityBlockers),
    ("Track MAVLink message statistics per type", TrackMavlinkMessageStatistics),
    ("Expose MAVLink parser frame sequence", ExposeMavlinkParserFrameSequence),
    ("Track MAVLink sequence loss per link", TrackMavlinkSequenceLossPerLink),
    ("Record MAVLink protocol per-link statistics", RecordMavlinkProtocolPerLinkStatistics),
    ("Catalog MAVLink full protocol coverage gaps", CatalogMavlinkFullProtocolCoverageGaps),
    ("Audit MAVLink full protocol coverage blockers", AuditMavlinkFullProtocolCoverageBlockers),
    ("Select MAVLink generator decision", SelectMavlinkGeneratorDecision),
    ("Expose MAVLink seed message definitions", ExposeMavlinkSeedMessageDefinitions),
    ("Load MAVLink ArduPilotMega seed fixture", LoadMavlinkArduPilotMegaSeedFixture),
    ("Load MAVLink dialect manifest", LoadMavlinkDialectManifest),
    ("Audit MAVLink dialect ingestion plan", AuditMavlinkDialectIngestionPlan),
    ("Align MAVLink seed definitions with CRC registry", AlignMavlinkSeedDefinitionsWithCrcRegistry),
    ("Audit generated MAVLink CRC registry", AuditGeneratedMavlinkCrcRegistry),
    ("Keep legacy MAVLink CRC entries available", KeepLegacyMavlinkCrcEntriesAvailable),
    ("Generate MAVLink definitions from dialect XML", GenerateMavlinkDefinitionsFromDialectXml),
    ("Build MAVLink CRC registry from dialect XML", BuildMavlinkCrcRegistryFromDialectXml),
    ("Round-trip generated MAVLink parser writer frames", RoundTripGeneratedMavlinkParserWriterFrames),
    ("Sign and validate MAVLink v2 frame", SignAndValidateMavlinkV2Frame),
    ("Reject tampered MAVLink v2 signature", RejectTamperedMavlinkV2Signature),
    ("Parse signed MAVLink v2 frame", ParseSignedMavlinkV2Frame),
    ("Round-trip MAVLink FTP payload", RoundTripMavlinkFtpPayload),
    ("List MAVLink FTP directory", ListMavlinkFtpDirectory),
    ("Download MAVLink FTP file chunks", DownloadMavlinkFtpFileChunks),
    ("Handle MAVLink FTP NAK and retry", HandleMavlinkFtpNakAndRetry),
    ("Define MAVLink missing strong message records", DefineMavlinkMissingStrongMessageRecords),
    ("Define and read settings group facts", DefineAndReadSettingsGroupFacts),
    ("Validate mission items with rules", ValidateMissionItemsWithRules),
    ("Calculate camera GSD from sensor specs", CalculateCameraGsdFromSensorSpecs),
    ("Apply home position telemetry", ApplyHomePositionTelemetry),
    ("Request message and acknowledge response", RequestMessageAndAcknowledgeResponse),
    ("Route outbound command through MAVLink router", RouteOutboundCommandThroughMavlinkRouter),
    ("Route outbound service families through MAVLink router", RouteOutboundServiceFamiliesThroughMavlinkRouter),
    ("Keep ViewModels out of MAVLink payload writing", KeepViewModelsOutOfMavlinkPayloadWriting),
    ("Expose MAVLink protocol evidence catalog", ExposeMavlinkProtocolEvidenceCatalog),
    ("Verify MAVLink protocol evidence sources and tests", VerifyMavlinkProtocolEvidenceSourcesAndTests),
    ("Register and persist settings manager defaults", RegisterAndPersistSettingsManagerDefaults),
    ("Settings view model edits grouped facts", SettingsViewModelEditsGroupedFacts),
    ("Reactive fact group tracks lifecycle", ReactiveFactGroupTracksLifecycle),
    ("Coordinate app lifecycle and close guards", CoordinateAppLifecycleAndCloseGuards),
    ("Rotate file logs and project viewer rows", RotateFileLogsAndProjectViewerRows),
    ("Resolve localization boundary keys", ResolveLocalizationBoundaryKeys),
    ("Catalog settings lifecycle evidence", CatalogSettingsLifecycleEvidence),
    ("Load firmware metadata packages", LoadFirmwareMetadataPackages),
    ("Project firmware command UI metadata", ProjectFirmwareCommandUiMetadata),
    ("Project autopilot setup components", ProjectAutoPilotSetupComponents),
    ("Setup view switches component navigation", SetupViewSwitchesComponentNavigation),
    ("Shell tool drawer switches setup parameters analyze", ShellToolDrawerSwitchesSetupParametersAnalyze),
    ("Plan view exposes deep workflow states", PlanViewExposesDeepWorkflowStates),
    ("Analyze view remains partial chart implementation", AnalyzeViewRemainsPartialChartImplementation),
    ("Setup view exposes partial deep component coverage", SetupViewExposesPartialDeepComponentCoverage),
    ("Create calibration command boundaries", CreateCalibrationCommandBoundaries),
    ("Project radio calibration and manual control", ProjectRadioCalibrationAndManualControl),
    ("Project power battery setup metadata", ProjectPowerBatterySetupMetadata),
    ("Create safety motor command boundary", CreateSafetyMotorCommandBoundary),
    ("Project parameter setup rows", ProjectParameterSetupRows),
    ("Catalog firmware setup evidence", CatalogFirmwareSetupEvidence),
    ("Review firmware setup runtime gaps", ReviewFirmwareSetupRuntimeGaps),
    ("Catalog firmware setup parity flows", CatalogFirmwareSetupParityFlows),
    ("Project firmware setup runtime flow", ProjectFirmwareSetupRuntimeFlow),
    ("Audit firmware setup parity blockers", AuditFirmwareSetupParityBlockers),
    ("Catalog Gate 9 setup parameter settings QML parity", CatalogGate9SetupParameterSettingsQmlParity),
    ("Track complex item dirty serialization", TrackComplexItemDirtySerialization),
    ("Plan survey grid camera spacing", PlanSurveyGridCameraSpacing),
    ("Plan corridor scan expansion", PlanCorridorScanExpansion),
    ("Plan structure scan layers", PlanStructureScanLayers),
    ("Validate fixed wing landing pattern", ValidateFixedWingLandingPattern),
    ("Create VTOL landing boundary", CreateVtolLandingBoundary),
    ("Model camera and speed sections", ModelCameraAndSpeedSections),
    ("Round-trip KML shape boundary", RoundTripKmlShapeBoundary),
    ("Project terrain plan UI rows", ProjectTerrainPlanUiRows),
    ("Project advanced plan UI panels", ProjectAdvancedPlanUiPanels),
    ("Catalog plan advanced evidence", CatalogPlanAdvancedEvidence),
    ("Audit plan advanced parity gaps", AuditPlanAdvancedParityGaps)
};

var failed = 0;
foreach (var (name, test) in tests)
{
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {name}: {ex.Message}");
    }
}

return failed == 0 ? 0 : 1;

static void ParseV1Heartbeat()
{
    var parser = new MavlinkFrameParser();
    var frames = parser.Parse(MavlinkTestFrames.HeartbeatV1(systemId: 7, componentId: 2, autopilot: MavAutopilot.ArduPilotMega, vehicleType: MavType.FixedWing));
    Require(frames.Count == 1, "Expected one frame.");
    var frame = frames[0];
    Require(frame.Version == 1, "Expected MAVLink v1.");
    Require(frame.SystemId == 7, "Expected system id 7.");
    Require(frame.ComponentId == 2, "Expected component id 2.");
    Require(frame.MessageId == 0, "Expected HEARTBEAT message id.");
    Require(frame.Payload[4] == (byte)MavType.FixedWing, "Expected fixed wing type.");
    Require(frame.Payload[5] == (byte)MavAutopilot.ArduPilotMega, "Expected ArduPilot autopilot.");
}

static void RejectInvalidV1HeartbeatCrc()
{
    var parser = new MavlinkFrameParser();
    var frame = MavlinkTestFrames.HeartbeatV1();
    frame[^1] ^= 0xFF;

    var frames = parser.Parse(frame);
    Require(frames.Count == 0, "Expected invalid CRC frame to be rejected.");
    Require(parser.BufferedByteCount == 0, "Expected rejected frame to drain from buffer.");
}

static void RecoverAfterInvalidV1Crc()
{
    var parser = new MavlinkFrameParser();
    var invalid = MavlinkTestFrames.HeartbeatV1(systemId: 2);
    invalid[^2] ^= 0xFF;
    var valid = MavlinkTestFrames.HeartbeatV1(systemId: 42);

    var frames = parser.Parse(invalid.Concat(valid).ToArray());
    Require(frames.Count == 1, "Expected parser to continue after rejected CRC.");
    Require(frames[0].SystemId == 42, "Expected valid frame after rejected CRC.");
}

static void KeepUnsupportedV1MessagesParseable()
{
    var parser = new MavlinkFrameParser();
    var unsupported = new byte[]
    {
        0xFE,
        0x00,
        0x00,
        0x11,
        0x22,
        0xC8,
        0x00,
        0x00
    };

    var frames = parser.Parse(unsupported);
    Require(frames.Count == 1, "Expected unsupported message to keep existing parse behavior.");
    Require(frames[0].SystemId == 0x11, "Expected unsupported frame system id.");
    Require(frames[0].ComponentId == 0x22, "Expected unsupported frame component id.");
    Require(frames[0].MessageId == 0xC8, "Expected unsupported message id.");
}

static void ParseSplitV1Heartbeat()
{
    var parser = new MavlinkFrameParser();
    var frame = MavlinkTestFrames.HeartbeatV1();
    var first = parser.Parse(frame.AsSpan(0, 5));
    Require(first.Count == 0, "Expected no frame from partial header.");
    Require(parser.BufferedByteCount == 5, "Expected buffered partial bytes.");

    var second = parser.Parse(frame.AsSpan(5));
    Require(second.Count == 1, "Expected one frame after completing split packet.");
    Require(parser.BufferedByteCount == 0, "Expected buffer to drain.");
}

static void RecoverAfterInvalidPrefixBytes()
{
    var parser = new MavlinkFrameParser();
    var frame = MavlinkTestFrames.HeartbeatV1(systemId: 3);
    var bytes = new byte[] { 0x00, 0x01, 0x02 }.Concat(frame).ToArray();
    var frames = parser.Parse(bytes);
    Require(frames.Count == 1, "Expected parser to skip invalid prefix bytes.");
    Require(frames[0].SystemId == 3, "Expected recovered heartbeat system id.");
}

static void BufferIncompleteFrame()
{
    var parser = new MavlinkFrameParser();
    var frames = parser.Parse(MavlinkTestFrames.HeartbeatV1().AsSpan(0, 8));
    Require(frames.Count == 0, "Expected incomplete frame to produce no frames.");
    Require(parser.BufferedByteCount == 8, "Expected incomplete frame to remain buffered.");
}

static void ParseV2Heartbeat()
{
    var parser = new MavlinkFrameParser();
    var frames = parser.Parse(MavlinkTestFrames.HeartbeatV2(systemId: 9, componentId: 4));
    Require(frames.Count == 1, "Expected one MAVLink v2 frame.");
    var frame = frames[0];
    Require(frame.Version == 2, "Expected MAVLink v2.");
    Require(frame.SystemId == 9, "Expected system id 9.");
    Require(frame.ComponentId == 4, "Expected component id 4.");
    Require(frame.MessageId == 0, "Expected HEARTBEAT message id.");
}

static void CreateGcsHeartbeatFrame()
{
    var parser = new MavlinkFrameParser();
    var frames = parser.Parse(MavlinkTestFrames.GcsHeartbeat());
    Require(frames.Count == 1, "Expected one GCS heartbeat frame.");
    var frame = frames[0];
    Require(frame.Version == 1, "Expected MAVLink v1 GCS heartbeat.");
    Require(frame.SystemId == 255, "Expected default GCS system id.");
    Require(frame.ComponentId == 190, "Expected default GCS component id.");
    Require(frame.Payload[4] == (byte)MavType.Gcs, "Expected GCS MAV_TYPE.");
    Require(frame.Payload[5] == (byte)MavAutopilot.Invalid, "Expected invalid autopilot for GCS.");
}

static void CreateCommandLongFrame()
{
    var service = new MavlinkCommandService(systemId: 255, componentId: 190);
    var command = new MavlinkCommandLong(
        TargetSystemId: 7,
        TargetComponentId: 1,
        Command: MavlinkCommandIds.ComponentArmDisarm,
        Confirmation: 2,
        Param1: 1.0f,
        Param2: 2989.0f,
        Param3: 3.0f,
        Param4: 4.0f,
        Param5: 5.0f,
        Param6: 6.0f,
        Param7: 7.0f);

    var parser = new MavlinkFrameParser();
    var frames = parser.Parse(service.CreateCommandLongFrame(command));
    Require(frames.Count == 1, "Expected COMMAND_LONG frame.");
    var frame = frames[0];
    Require(frame.MessageId == 76, "Expected COMMAND_LONG message id.");
    Require(Math.Abs(BitConverter.ToSingle(frame.Payload, 0) - 1.0f) < 0.001f, "Expected param1.");
    Require(Math.Abs(BitConverter.ToSingle(frame.Payload, 4) - 2989.0f) < 0.001f, "Expected param2.");
    Require(BitConverter.ToUInt16(frame.Payload, 28) == MavlinkCommandIds.ComponentArmDisarm, "Expected command id.");
    Require(frame.Payload[30] == 7, "Expected target system.");
    Require(frame.Payload[31] == 1, "Expected target component.");
    Require(frame.Payload[32] == 2, "Expected confirmation.");
}

static void ParseCommandAck()
{
    var parser = new MavlinkFrameParser();
    var frames = parser.Parse(MavlinkTestFrames.CommandAck(systemId: 3, componentId: 4, result: MavlinkCommandResult.Accepted));
    Require(frames.Count == 1, "Expected COMMAND_ACK frame.");
    var frame = frames[0];
    var packet = new MavlinkPacket(new MockLinkTransport(), frame.Version, frame.SystemId, frame.ComponentId, frame.MessageId, frame.Payload);
    Require(MavlinkCommandService.TryReadCommandAck(packet, out var ack), "Expected COMMAND_ACK parser to succeed.");
    Require(ack.SystemId == 3, "Expected ACK system id.");
    Require(ack.ComponentId == 4, "Expected ACK component id.");
    Require(ack.Command == MavlinkCommandIds.ComponentArmDisarm, "Expected ACK command.");
    Require(ack.Result == MavlinkCommandResult.Accepted, "Expected ACK result.");
}

static void SendVehicleArmDisarmCommands()
{
    var link = new MockLinkTransport();
    var sent = new List<byte[]>();
    link.BytesSent += (_, args) => sent.Add(args.Bytes);
    link.ConnectAsync().GetAwaiter().GetResult();

    var vehicle = new Vehicle(id: 9, componentId: 1, autopilot: MavAutopilot.Px4, vehicleType: MavType.Quadrotor);
    var commandService = vehicle.CreateCommandService(link);
    var armResult = commandService.ArmAsync().GetAwaiter().GetResult();
    commandService.HandleCommandAck(new MavlinkCommandAck(vehicle.Id, vehicle.ComponentId, MavlinkCommandIds.ComponentArmDisarm, MavlinkCommandResult.Accepted));
    var disarmResult = commandService.DisarmAsync().GetAwaiter().GetResult();

    Require(armResult.Sent, "Expected arm command to send.");
    Require(disarmResult.Sent, "Expected disarm command to send.");
    Require(sent.Count == 2, "Expected arm and disarm frames.");
    var parser = new MavlinkFrameParser();
    var frames = parser.Parse(sent[0].Concat(sent[1]).ToArray());
    Require(frames.Count == 2, "Expected two COMMAND_LONG frames.");
    Require(frames[0].MessageId == 76, "Expected arm COMMAND_LONG.");
    Require(BitConverter.ToUInt16(frames[0].Payload, 28) == MavlinkCommandIds.ComponentArmDisarm, "Expected arm command id.");
    Require(Math.Abs(BitConverter.ToSingle(frames[0].Payload, 0) - 1.0f) < 0.001f, "Expected arm param1.");
    Require(frames[0].Payload[30] == 9, "Expected arm target system.");
    Require(frames[0].Payload[31] == 1, "Expected arm target component.");
    Require(Math.Abs(BitConverter.ToSingle(frames[1].Payload, 0)) < 0.001f, "Expected disarm param1.");
}

static void TrackCommandQueueAckState()
{
    var queue = new VehicleCommandQueue();
    var begin = queue.TryEnqueue(targetComponentId: 1, command: MavlinkCommandIds.ComponentArmDisarm);
    Require(begin.Sent, "Expected pending command to begin.");
    Require(queue.PendingCount == 1, "Expected command pending.");

    var inProgressMatched = queue.TryAcknowledge(1, MavlinkCommandIds.ComponentArmDisarm);
    Require(inProgressMatched, "Expected ACK to match.");
    Require(queue.PendingCount == 0, "Expected ACK to clear pending.");
}

static void RejectDuplicatePendingVehicleCommand()
{
    var link = new MockLinkTransport();
    var sent = new List<byte[]>();
    link.BytesSent += (_, args) => sent.Add(args.Bytes);
    link.ConnectAsync().GetAwaiter().GetResult();

    var vehicle = new Vehicle(id: 9, componentId: 1, autopilot: MavAutopilot.Px4, vehicleType: MavType.Quadrotor);
    var commandService = vehicle.CreateCommandService(link);
    var first = commandService.ArmAsync().GetAwaiter().GetResult();
    Require(first.Sent, "Expected first command to send.");

    try
    {
        commandService.ArmAsync().GetAwaiter().GetResult();
    }
    catch (InvalidOperationException)
    {
    }

    Require(sent.Count == 1, "Expected duplicate command not to write a frame.");
}

static void TrackGuidedActionCommandStates()
{
    var link = new MockLinkTransport();
    var sent = new List<byte[]>();
    link.BytesSent += (_, args) => sent.Add(args.Bytes);
    link.ConnectAsync().GetAwaiter().GetResult();
    var vehicle = new Vehicle(id: 9, componentId: 1, autopilot: MavAutopilot.Px4, vehicleType: MavType.Quadrotor);
    var guided = new GuidedActionController();

    var ready = guided.Capture(vehicle, link).First(action => action.Kind == GuidedActionKind.Takeoff);
    Require(ready.State == GuidedActionState.Ready, "Expected takeoff action to be ready.");
    Require(ready.IsEnabled, "Expected takeoff action to be enabled.");

    var confirmation = guided.RequestConfirmation(GuidedActionKind.Takeoff, vehicle, link);
    Require(confirmation.State == GuidedActionState.ConfirmationRequired, "Expected takeoff confirmation state.");
    Require(guided.PendingConfirmation == GuidedActionKind.Takeoff, "Expected pending takeoff confirmation.");

    var pending = guided.ConfirmAsync(vehicle, link).GetAwaiter().GetResult();
    Require(pending.State == GuidedActionState.Pending, "Expected takeoff pending state.");
    Require(sent.Count == 1, "Expected guided action command frame.");
    var frames = new MavlinkFrameParser().Parse(sent[0]);
    Require(frames.Count == 1 && frames[0].MessageId == 76, "Expected guided action COMMAND_LONG.");
    Require(BitConverter.ToUInt16(frames[0].Payload, 28) == MavlinkCommandIds.NavTakeoff, "Expected takeoff command id.");

    var inProgress = guided.HandleCommandAck(new MavlinkCommandAck(vehicle.Id, vehicle.ComponentId, MavlinkCommandIds.NavTakeoff, MavlinkCommandResult.InProgress));
    Require(inProgress, "Expected in-progress takeoff ACK to match.");
    Require(guided.Capture(vehicle, link).First(action => action.Kind == GuidedActionKind.Takeoff).State == GuidedActionState.Pending, "Expected in-progress ACK to keep takeoff pending.");

    var accepted = guided.HandleCommandAck(new MavlinkCommandAck(vehicle.Id, vehicle.ComponentId, MavlinkCommandIds.NavTakeoff, MavlinkCommandResult.Accepted));
    Require(accepted, "Expected accepted takeoff ACK to match.");
    var acceptedStatus = guided.Capture(vehicle, link).First(action => action.Kind == GuidedActionKind.Takeoff);
    Require(acceptedStatus.State == GuidedActionState.Accepted, "Expected accepted takeoff state.");
    Require(acceptedStatus.AckResult == MavlinkCommandResult.Accepted, "Expected accepted takeoff ACK result.");

    guided.RequestConfirmation(GuidedActionKind.Land, vehicle, link);
    guided.ConfirmAsync(vehicle, link).GetAwaiter().GetResult();
    var timedOut = guided.MarkTimeouts(TimeSpan.Zero).First(action => action.Kind == GuidedActionKind.Land);
    Require(timedOut.State == GuidedActionState.Timeout, "Expected land timeout state.");

    guided.RequestConfirmation(GuidedActionKind.ReturnToLaunch, vehicle, link);
    guided.ConfirmAsync(vehicle, link).GetAwaiter().GetResult();
    guided.HandleCommandAck(new MavlinkCommandAck(vehicle.Id, vehicle.ComponentId, MavlinkCommandIds.NavReturnToLaunch, MavlinkCommandResult.Denied));
    var rejected = guided.Capture(vehicle, link).First(action => action.Kind == GuidedActionKind.ReturnToLaunch);
    Require(rejected.State == GuidedActionState.Rejected, "Expected RTL rejected state.");
    Require(rejected.AckResult == MavlinkCommandResult.Denied, "Expected RTL denied ACK result.");
}

static void CreateParamRequestListFrame()
{
    var service = new MavlinkParameterService();
    var parser = new MavlinkFrameParser();
    var frames = parser.Parse(service.CreateParamRequestListFrame(new MavlinkParameterRequestList(9, 1)));
    Require(frames.Count == 1, "Expected PARAM_REQUEST_LIST frame.");
    var frame = frames[0];
    Require(frame.MessageId == 21, "Expected PARAM_REQUEST_LIST message id.");
    Require(frame.Payload.Length == 2, "Expected PARAM_REQUEST_LIST payload length.");
    Require(frame.Payload[0] == 9, "Expected target system.");
    Require(frame.Payload[1] == 1, "Expected target component.");
}

static void CreateParamRequestReadFrameByIndex()
{
    var service = new MavlinkParameterService();
    var parser = new MavlinkFrameParser();
    var frames = parser.Parse(service.CreateParamRequestReadFrame(MavlinkParameterRequestRead.ByIndex(9, 1, 42)));
    Require(frames.Count == 1, "Expected PARAM_REQUEST_READ frame.");
    var frame = frames[0];
    Require(frame.MessageId == 20, "Expected PARAM_REQUEST_READ message id.");
    Require(frame.Payload.Length == 20, "Expected PARAM_REQUEST_READ payload length.");
    Require(BitConverter.ToInt16(frame.Payload, 0) == 42, "Expected request index.");
    Require(frame.Payload[2] == 9, "Expected target system.");
    Require(frame.Payload[3] == 1, "Expected target component.");
    Require(frame.Payload.AsSpan(4, 16).ToArray().All(static b => b == 0), "Expected empty parameter name for index request.");
}

static void CreateParamRequestReadFrameByName()
{
    var service = new MavlinkParameterService();
    var parser = new MavlinkFrameParser();
    var frames = parser.Parse(service.CreateParamRequestReadFrame(MavlinkParameterRequestRead.ByName(9, 1, "MPC_XY_VEL")));
    Require(frames.Count == 1, "Expected PARAM_REQUEST_READ frame.");
    var frame = frames[0];
    Require(frame.MessageId == 20, "Expected PARAM_REQUEST_READ message id.");
    Require(BitConverter.ToInt16(frame.Payload, 0) == -1, "Expected name request index sentinel.");
    Require(frame.Payload[2] == 9, "Expected target system.");
    Require(frame.Payload[3] == 1, "Expected target component.");

    var nameBytes = frame.Payload.AsSpan(4, 16);
    var terminator = nameBytes.IndexOf((byte)0);
    var name = System.Text.Encoding.ASCII.GetString(terminator >= 0 ? nameBytes[..terminator] : nameBytes);
    Require(name == "MPC_XY_VEL", "Expected PARAM_REQUEST_READ name.");
}

static void SendParamRequestReadFrame()
{
    var link = new MockLinkTransport();
    var sent = new List<byte[]>();
    link.BytesSent += (_, args) => sent.Add(args.Bytes);
    link.ConnectAsync().GetAwaiter().GetResult();

    var service = new MavlinkParameterService();
    service.SendParamRequestReadAsync(link, MavlinkParameterRequestRead.ByIndex(9, 1, 7)).GetAwaiter().GetResult();

    Require(sent.Count == 1, "Expected one PARAM_REQUEST_READ frame sent.");
    var parser = new MavlinkFrameParser();
    var frames = parser.Parse(sent[0]);
    Require(frames.Count == 1, "Expected sent PARAM_REQUEST_READ to parse.");
    Require(frames[0].MessageId == 20, "Expected sent PARAM_REQUEST_READ message id.");
}

static void CreateParamSetFrameFromFact()
{
    var fact = new Fact(componentId: 1, "MPC_XY_VEL", new FactMetaData("MPC_XY_VEL", FactValueType.Float), 3.5f);
    var service = new MavlinkParameterService();
    var parser = new MavlinkFrameParser();
    var frames = parser.Parse(service.CreateParamSetFrame(targetSystemId: 9, targetComponentId: 1, fact));
    Require(frames.Count == 1, "Expected PARAM_SET frame.");
    var frame = frames[0];
    Require(frame.MessageId == 23, "Expected PARAM_SET message id.");
    Require(Math.Abs(BitConverter.ToSingle(frame.Payload, 0) - 3.5f) < 0.001f, "Expected PARAM_SET value.");
    Require(frame.Payload[4] == 9, "Expected PARAM_SET target system.");
    Require(frame.Payload[5] == 1, "Expected PARAM_SET target component.");

    var nameBytes = frame.Payload.AsSpan(6, 16);
    var terminator = nameBytes.IndexOf((byte)0);
    var name = System.Text.Encoding.ASCII.GetString(terminator >= 0 ? nameBytes[..terminator] : nameBytes);
    Require(name == "MPC_XY_VEL", "Expected PARAM_SET name.");
    Require(frame.Payload[22] == (byte)MavlinkParamType.Real32, "Expected PARAM_SET type.");
}

static void CreateMissionRequestListFrame()
{
    var service = new MavlinkMissionService();
    var parser = new MavlinkFrameParser();
    var frames = parser.Parse(service.CreateMissionRequestListFrame(new MavlinkMissionRequestList(9, 1)));
    Require(frames.Count == 1, "Expected MISSION_REQUEST_LIST frame.");
    var frame = frames[0];
    Require(frame.MessageId == 43, "Expected MISSION_REQUEST_LIST message id.");
    Require(frame.Payload.Length == 2, "Expected MISSION_REQUEST_LIST payload length.");
    Require(frame.Payload[0] == 9, "Expected target system.");
    Require(frame.Payload[1] == 1, "Expected target component.");
}

static void CreateAndParseMissionCountFrame()
{
    var service = new MavlinkMissionService();
    var parser = new MavlinkFrameParser();
    var frames = parser.Parse(service.CreateMissionCountFrame(new MavlinkMissionCount(9, 1, 3)));
    Require(frames.Count == 1, "Expected MISSION_COUNT frame.");
    var frame = frames[0];
    Require(frame.MessageId == 44, "Expected MISSION_COUNT message id.");
    Require(frame.Payload.Length == 4, "Expected MISSION_COUNT payload length.");
    var packet = new MavlinkPacket(new MockLinkTransport(), frame.Version, frame.SystemId, frame.ComponentId, frame.MessageId, frame.Payload);
    Require(MavlinkMissionService.TryReadMissionCount(packet, out var count), "Expected MISSION_COUNT parser to succeed.");
    Require(count.TargetSystemId == 9, "Expected target system.");
    Require(count.TargetComponentId == 1, "Expected target component.");
    Require(count.Count == 3, "Expected mission count.");
}

static void CreateAndParseMissionRequestIntFrame()
{
    var service = new MavlinkMissionService();
    var parser = new MavlinkFrameParser();
    var frames = parser.Parse(service.CreateMissionRequestIntFrame(new MavlinkMissionRequestInt(9, 1, 2)));
    Require(frames.Count == 1, "Expected MISSION_REQUEST_INT frame.");
    var frame = frames[0];
    Require(frame.MessageId == 51, "Expected MISSION_REQUEST_INT message id.");
    Require(frame.Payload.Length == 4, "Expected MISSION_REQUEST_INT payload length.");
    var packet = new MavlinkPacket(new MockLinkTransport(), frame.Version, frame.SystemId, frame.ComponentId, frame.MessageId, frame.Payload);
    Require(MavlinkMissionService.TryReadMissionRequestInt(packet, out var request), "Expected MISSION_REQUEST_INT parser to succeed.");
    Require(request.TargetSystemId == 9, "Expected target system.");
    Require(request.TargetComponentId == 1, "Expected target component.");
    Require(request.Sequence == 2, "Expected requested sequence.");
}

static void CreateAndParseMissionItemIntFrame()
{
    var item = new MavlinkMissionItemInt(
        TargetSystemId: 9,
        TargetComponentId: 1,
        Sequence: 2,
        Command: 16,
        Frame: 3,
        Current: 0,
        AutoContinue: 1,
        Param1: 10,
        Param2: 20,
        Param3: 30,
        Param4: 40,
        X: 473977420,
        Y: 85455940,
        Z: 30);
    var service = new MavlinkMissionService();
    var parser = new MavlinkFrameParser();
    var frames = parser.Parse(service.CreateMissionItemIntFrame(item));
    Require(frames.Count == 1, "Expected MISSION_ITEM_INT frame.");
    var frame = frames[0];
    Require(frame.MessageId == 73, "Expected MISSION_ITEM_INT message id.");
    Require(frame.Payload.Length == 37, "Expected MISSION_ITEM_INT payload length.");
    Require(Math.Abs(BitConverter.ToSingle(frame.Payload, 0) - 10) < 0.001, "Expected param1.");
    Require(BitConverter.ToInt32(frame.Payload, 16) == 473977420, "Expected scaled latitude.");
    Require(BitConverter.ToInt32(frame.Payload, 20) == 85455940, "Expected scaled longitude.");
    Require(BitConverter.ToUInt16(frame.Payload, 28) == 2, "Expected sequence.");
    Require(BitConverter.ToUInt16(frame.Payload, 30) == 16, "Expected command.");
    Require(frame.Payload[32] == 9, "Expected target system.");
    Require(frame.Payload[33] == 1, "Expected target component.");
    Require(frame.Payload[34] == 3, "Expected frame.");
    Require(frame.Payload[36] == 1, "Expected auto continue.");

    var packet = new MavlinkPacket(new MockLinkTransport(), frame.Version, frame.SystemId, frame.ComponentId, frame.MessageId, frame.Payload);
    Require(MavlinkMissionService.TryReadMissionItemInt(packet, out var parsed), "Expected MISSION_ITEM_INT parser to succeed.");
    Require(parsed == item, "Expected parsed mission item to match original.");
}

static void CreateAndParseMissionAckFrame()
{
    var service = new MavlinkMissionService();
    var parser = new MavlinkFrameParser();
    var frames = parser.Parse(service.CreateMissionAckFrame(new MavlinkMissionAck(9, 1, MavlinkMissionResult.Accepted)));
    Require(frames.Count == 1, "Expected MISSION_ACK frame.");
    var frame = frames[0];
    Require(frame.MessageId == 47, "Expected MISSION_ACK message id.");
    Require(frame.Payload.Length == 3, "Expected MISSION_ACK payload length.");
    var packet = new MavlinkPacket(new MockLinkTransport(), frame.Version, frame.SystemId, frame.ComponentId, frame.MessageId, frame.Payload);
    Require(MavlinkMissionService.TryReadMissionAck(packet, out var ack), "Expected MISSION_ACK parser to succeed.");
    Require(ack.TargetSystemId == 9, "Expected target system.");
    Require(ack.TargetComponentId == 1, "Expected target component.");
    Require(ack.Result == MavlinkMissionResult.Accepted, "Expected accepted result.");
}

static void CreateMissionClearAllFrame()
{
    var service = new MavlinkMissionService();
    var parser = new MavlinkFrameParser();
    var frames = parser.Parse(service.CreateMissionClearAllFrame(new MavlinkMissionClearAll(9, 1)));
    Require(frames.Count == 1, "Expected MISSION_CLEAR_ALL frame.");
    var frame = frames[0];
    Require(frame.MessageId == 45, "Expected MISSION_CLEAR_ALL message id.");
    Require(frame.Payload.Length == 2, "Expected MISSION_CLEAR_ALL payload length.");
    Require(frame.Payload[0] == 9, "Expected target system.");
    Require(frame.Payload[1] == 1, "Expected target component.");
}

static void CreateAndParseMissionTypeFrames()
{
    var service = new MavlinkMissionService();

    var requestListFrame = ParseSingleFrame(service.CreateMissionRequestListFrame(new MavlinkMissionRequestList(9, 1, MavMissionType.Fence)));
    Require(requestListFrame.Version == 2, "Expected MAVLink v2 request-list for non-default mission type.");
    Require(requestListFrame.Payload.Length == 3, "Expected mission_type extension in request-list.");
    Require(requestListFrame.Payload[2] == (byte)MavMissionType.Fence, "Expected fence request-list mission type.");
    var requestListPacket = new MavlinkPacket(new MockLinkTransport(), requestListFrame.Version, requestListFrame.SystemId, requestListFrame.ComponentId, requestListFrame.MessageId, requestListFrame.Payload);
    Require(MavlinkMissionService.TryReadMissionRequestList(requestListPacket, out var requestList), "Expected request-list parser.");
    Require(requestList.MissionType == MavMissionType.Fence, "Expected fence request-list to round-trip.");

    var countFrame = ParseSingleFrame(service.CreateMissionCountFrame(new MavlinkMissionCount(9, 1, 3, MavMissionType.Rally)));
    Require(countFrame.Version == 2, "Expected MAVLink v2 count for non-default mission type.");
    Require(countFrame.Payload.Length == 5, "Expected mission_type extension in count.");
    Require(countFrame.Payload[4] == (byte)MavMissionType.Rally, "Expected rally count mission type.");
    var countPacket = new MavlinkPacket(new MockLinkTransport(), countFrame.Version, countFrame.SystemId, countFrame.ComponentId, countFrame.MessageId, countFrame.Payload);
    Require(MavlinkMissionService.TryReadMissionCount(countPacket, out var count), "Expected mission count parser.");
    Require(count.MissionType == MavMissionType.Rally, "Expected rally count to round-trip.");

    var item = CreateMissionItem(sequence: 2, missionType: MavMissionType.Fence);
    var itemFrame = ParseSingleFrame(service.CreateMissionItemIntFrame(item));
    Require(itemFrame.Version == 2, "Expected MAVLink v2 item for non-default mission type.");
    Require(itemFrame.Payload.Length == 38, "Expected mission_type extension in item.");
    Require(itemFrame.Payload[37] == (byte)MavMissionType.Fence, "Expected fence item mission type.");
    var itemPacket = new MavlinkPacket(new MockLinkTransport(), itemFrame.Version, itemFrame.SystemId, itemFrame.ComponentId, itemFrame.MessageId, itemFrame.Payload);
    Require(MavlinkMissionService.TryReadMissionItemInt(itemPacket, out var parsedItem), "Expected mission item parser.");
    Require(parsedItem == item, "Expected fence item to round-trip.");

    var ackFrame = ParseSingleFrame(service.CreateMissionAckFrame(new MavlinkMissionAck(9, 1, MavlinkMissionResult.Accepted, MavMissionType.Fence)));
    Require(ackFrame.Version == 2, "Expected MAVLink v2 ACK for non-default mission type.");
    Require(ackFrame.Payload.Length == 4, "Expected mission_type extension in ACK.");
    Require(ackFrame.Payload[3] == (byte)MavMissionType.Fence, "Expected fence ACK mission type.");

    var clearFrame = ParseSingleFrame(service.CreateMissionClearAllFrame(new MavlinkMissionClearAll(9, 1, MavMissionType.All)));
    Require(clearFrame.Version == 2, "Expected MAVLink v2 clear-all for explicit all mission type.");
    Require(clearFrame.Payload.Length == 3, "Expected mission_type extension in clear-all.");
    Require(clearFrame.Payload[2] == (byte)MavMissionType.All, "Expected all mission type.");
}

static void SendMissionFrame()
{
    var link = new MockLinkTransport();
    var sent = new List<byte[]>();
    link.BytesSent += (_, args) => sent.Add(args.Bytes);
    link.ConnectAsync().GetAwaiter().GetResult();

    var service = new MavlinkMissionService();
    service.SendMissionRequestListAsync(link, new MavlinkMissionRequestList(9, 1)).GetAwaiter().GetResult();

    Require(sent.Count == 1, "Expected one mission frame sent.");
    var parser = new MavlinkFrameParser();
    var frames = parser.Parse(sent[0]);
    Require(frames.Count == 1, "Expected sent mission frame to parse.");
    Require(frames[0].MessageId == 43, "Expected sent MISSION_REQUEST_LIST message id.");
}

static void TrackParameterManagerState()
{
    var manager = new ParameterManager();
    var requestEvents = 0;
    var writeEvents = 0;
    manager.RequestStateChanged += (_, _) => requestEvents++;
    manager.WriteStateChanged += (_, _) => writeEvents++;

    manager.BeginParameterRequest();
    Require(manager.IsParameterRequestActive, "Expected request active.");
    Require(manager.LastParameterRequestStartedAt is not null, "Expected request start timestamp.");
    manager.CompleteParameterRequest();
    Require(!manager.IsParameterRequestActive, "Expected request inactive.");
    Require(manager.LastParameterRequestCompletedAt is not null, "Expected request completion timestamp.");
    Require(requestEvents == 2, "Expected request state events.");

    manager.BeginParameterWrite();
    manager.BeginParameterWrite();
    Require(manager.HasPendingWrites, "Expected pending writes.");
    Require(manager.PendingWriteCount == 2, "Expected pending write count.");
    manager.CompleteParameterWrite();
    manager.CompleteParameterWrite();
    Require(!manager.HasPendingWrites, "Expected no pending writes.");
    Require(manager.PendingWriteCount == 0, "Expected pending write count to drain.");
    Require(manager.LastParameterWriteCompletedAt is not null, "Expected write completion timestamp.");
    Require(writeEvents == 4, "Expected write state events.");
}

static void TrackNamedParameterWrites()
{
    var manager = new ParameterManager();
    var writeEvents = 0;
    manager.WriteStateChanged += (_, _) => writeEvents++;

    manager.BeginParameterWrite(componentId: 1, name: "MPC_XY_VEL");
    manager.BeginParameterWrite(componentId: 1, name: "SYS_ID");
    manager.BeginParameterWrite(componentId: 1, name: "SYS_ID");

    Require(manager.PendingWriteCount == 2, "Expected duplicate named write not to increase count.");
    Require(manager.HasPendingWrites, "Expected named writes pending.");
    Require(manager.IsParameterWritePending(1, "MPC_XY_VEL"), "Expected MPC_XY_VEL pending.");
    Require(manager.IsParameterWritePending(1, "SYS_ID"), "Expected SYS_ID pending.");
    Require(manager.GetPendingWriteNames(1).SequenceEqual(["MPC_XY_VEL", "SYS_ID"]), "Expected sorted pending write names.");
    Require(writeEvents == 2, "Expected write events only for new named writes.");
}

static void ClearPendingParameterWriteFromMatchingParamValue()
{
    var manager = new ParameterManager();
    manager.BeginParameterWrite(componentId: 1, name: "MPC_XY_VEL");

    ApplyParamValue(manager, componentId: 1, name: "MPC_XY_VEL", count: 1, index: 0);

    Require(!manager.IsParameterWritePending(1, "MPC_XY_VEL"), "Expected matching PARAM_VALUE to clear pending write.");
    Require(!manager.HasPendingWrites, "Expected no pending writes after ACK.");
    Require(manager.PendingWriteCount == 0, "Expected pending write count to drain.");
    Require(manager.LastParameterWriteCompletedAt is not null, "Expected write completion timestamp.");
}

static void KeepUnrelatedParameterWritesPending()
{
    var manager = new ParameterManager();
    manager.BeginParameterWrite(componentId: 1, name: "MPC_XY_VEL");
    manager.BeginParameterWrite(componentId: 2, name: "SYS_ID");

    ApplyParamValue(manager, componentId: 1, name: "SYS_ID", count: 1, index: 0);

    Require(manager.IsParameterWritePending(1, "MPC_XY_VEL"), "Expected unrelated component/name write to stay pending.");
    Require(manager.IsParameterWritePending(2, "SYS_ID"), "Expected unrelated component write to stay pending.");
    Require(manager.PendingWriteCount == 2, "Expected unrelated PARAM_VALUE not to clear pending write count.");
}

static void TrackParameterDownloadProgress()
{
    var manager = new ParameterManager();
    var downloadEvents = 0;
    manager.DownloadStateChanged += (_, _) => downloadEvents++;

    manager.BeginParameterRequest();
    ApplyParamValue(manager, componentId: 1, name: "SYS_ID", count: 4, index: 0);
    ApplyParamValue(manager, componentId: 1, name: "MPC_XY_VEL", count: 4, index: 2);

    Require(manager.ExpectedParameterCount == 4, "Expected parameter count from PARAM_VALUE.");
    Require(manager.ReceivedParameterCount == 2, "Expected unique received parameter index count.");
    Require(Math.Abs(manager.LoadProgress - 0.5) < 0.001, "Expected half progress.");
    Require(manager.MissingParameters, "Expected missing parameters after partial download.");
    Require(!manager.ParametersReady, "Expected parameters not ready while incomplete.");
    Require(manager.IsParameterRequestActive, "Expected request to remain active while incomplete.");
    Require(downloadEvents == 3, "Expected begin plus two download events.");
}

static void MarkParameterDownloadReadyWhenComplete()
{
    var manager = new ParameterManager();
    manager.BeginParameterRequest();

    ApplyParamValue(manager, componentId: 1, name: "P0", count: 2, index: 0);
    ApplyParamValue(manager, componentId: 1, name: "P1", count: 2, index: 1);

    Require(manager.ExpectedParameterCount == 2, "Expected two parameters.");
    Require(manager.ReceivedParameterCount == 2, "Expected two received indexes.");
    Require(Math.Abs(manager.LoadProgress - 1.0) < 0.001, "Expected full progress.");
    Require(manager.ParametersReady, "Expected parameters ready after full download.");
    Require(!manager.MissingParameters, "Expected no missing parameters after full download.");
    Require(!manager.IsParameterRequestActive, "Expected request to auto-complete after full download.");
    Require(manager.LastParameterRequestCompletedAt is not null, "Expected completion timestamp after full download.");
}

static void ReportMissingParameterIndexes()
{
    var manager = new ParameterManager();
    manager.BeginParameterRequest();

    ApplyParamValue(manager, componentId: 1, name: "P0", count: 5, index: 0);
    ApplyParamValue(manager, componentId: 1, name: "P2", count: 5, index: 2);
    ApplyParamValue(manager, componentId: 1, name: "P4", count: 5, index: 4);

    var missing = manager.GetMissingParameterIndexes(componentId: 1);
    Require(missing.SequenceEqual(new ushort[] { 1, 3 }), "Expected missing parameter indexes 1 and 3.");

    manager.CompleteParameterRequest();
    Require(!manager.ParametersReady, "Expected partial download not to be marked ready on completion.");
    Require(manager.MissingParameters, "Expected missing parameters after completing partial request.");
}

static void ParameterViewListsActiveVehicleParameters()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var linkManager = new LinkManager(logger);
    var vehicles = new MultiVehicleManager(protocol, logger);
    var viewModel = new ParameterViewModel(vehicles);
    protocol.Attach(linkManager);
    var link = linkManager.CreateConnectedMockLinkAsync().GetAwaiter().GetResult();
    link.EmitIncoming(MavlinkTestFrames.HeartbeatV1(systemId: 9, componentId: 1, autopilot: MavAutopilot.Px4, vehicleType: MavType.Quadrotor));
    link.EmitIncoming(MavlinkTestFrames.ParamValue(systemId: 9, componentId: 1, name: "SYS_ID", value: 1, count: 2, index: 0));
    link.EmitIncoming(MavlinkTestFrames.ParamValue(systemId: 9, componentId: 1, name: "MPC_XY_VEL", value: 3.5f, count: 2, index: 1));

    Require(viewModel.HasParameters, "Expected Parameter view to see active vehicle parameters.");
    Require(viewModel.ParameterRows.Count == 2, "Expected two parameter rows.");
    Require(viewModel.ParameterRows[0].Name == "MPC_XY_VEL", "Expected rows sorted by name.");
    Require(viewModel.ParameterRows[1].Name == "SYS_ID", "Expected SYS_ID row.");
    Require(viewModel.ParameterSummary.Contains("Params 2"), "Expected parameter count summary.");
    Require(viewModel.ParameterDownloadState.Contains("Ready 2/2"), "Expected ready download state.");
}

static void ParameterViewFiltersParameterRows()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var linkManager = new LinkManager(logger);
    var vehicles = new MultiVehicleManager(protocol, logger);
    var viewModel = new ParameterViewModel(vehicles);
    protocol.Attach(linkManager);
    var link = linkManager.CreateConnectedMockLinkAsync().GetAwaiter().GetResult();
    link.EmitIncoming(MavlinkTestFrames.HeartbeatV1(systemId: 9, componentId: 1, autopilot: MavAutopilot.Px4, vehicleType: MavType.Quadrotor));
    link.EmitIncoming(MavlinkTestFrames.ParamValue(systemId: 9, componentId: 1, name: "SYS_ID", value: 1, count: 3, index: 0));
    link.EmitIncoming(MavlinkTestFrames.ParamValue(systemId: 9, componentId: 1, name: "MPC_XY_VEL", value: 3.5f, count: 3, index: 1));
    link.EmitIncoming(MavlinkTestFrames.ParamValue(systemId: 9, componentId: 1, name: "BAT_LOW_THR", value: 14.2f, count: 3, index: 2));

    viewModel.SearchText = "mpc";

    Require(viewModel.ParameterRows.Count == 1, "Expected search to filter to one row.");
    Require(viewModel.ParameterRows[0].Name == "MPC_XY_VEL", "Expected case-insensitive name search.");

    viewModel.SearchText = "14.2";

    Require(viewModel.ParameterRows.Count == 1, "Expected value search to filter to one row.");
    Require(viewModel.ParameterRows[0].Name == "BAT_LOW_THR", "Expected value search result.");
}

static void ParameterViewReportsPendingWrites()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var linkManager = new LinkManager(logger);
    var vehicles = new MultiVehicleManager(protocol, logger);
    var viewModel = new ParameterViewModel(vehicles);
    protocol.Attach(linkManager);
    var link = linkManager.CreateConnectedMockLinkAsync().GetAwaiter().GetResult();
    link.EmitIncoming(MavlinkTestFrames.HeartbeatV1(systemId: 9, componentId: 1, autopilot: MavAutopilot.Px4, vehicleType: MavType.Quadrotor));
    link.EmitIncoming(MavlinkTestFrames.ParamValue(systemId: 9, componentId: 1, name: "SYS_ID", value: 1, count: 1, index: 0));

    var vehicle = vehicles.ActiveVehicle ?? throw new InvalidOperationException("Expected active vehicle.");
    vehicle.ParameterManager.BeginParameterWrite(componentId: 1, name: "SYS_ID");

    Require(viewModel.ParameterRows.Count == 1, "Expected one parameter row.");
    Require(viewModel.ParameterRows[0].IsPendingWrite, "Expected pending write projection.");
    Require(viewModel.ParameterSummary.Contains("Pending writes 1"), "Expected pending write summary.");
}

static void ParameterProjectionKeepsRowsWithoutMetadata()
{
    var manager = new ParameterManager();
    ApplyParamValue(manager, componentId: 1, name: "SYS_ID", count: 1, index: 0);

    var rows = ParameterProjection.BuildRows(manager);

    Require(rows.Count == 1, "Expected one projected row.");
    Require(rows[0].Name == "SYS_ID", "Expected raw parameter name.");
    Require(rows[0].Label == "SYS_ID", "Expected label to fall back to parameter name.");
    Require(!rows[0].HasMetadata, "Expected row to report missing metadata.");
    Require(rows[0].Group == string.Empty, "Expected empty group without metadata.");
}

static void ParameterProjectionMergesMetadata()
{
    var manager = new ParameterManager();
    ApplyParamValue(manager, componentId: 1, name: "MPC_XY_VEL", count: 1, index: 0);
    var catalog = new InMemoryParameterMetadataCatalog([
        new ParameterMetadata(
            "MPC_XY_VEL",
            Group: "Multicopter Position Control",
            Label: "XY velocity",
            Description: "Maximum horizontal velocity",
            Units: "m/s",
            Min: 0,
            Max: 20,
            RebootRequired: true)
    ]);

    var rows = ParameterProjection.BuildRows(manager, catalog, "horizontal");

    Require(rows.Count == 1, "Expected metadata description to participate in search.");
    Require(rows[0].Name == "MPC_XY_VEL", "Expected metadata row.");
    Require(rows[0].HasMetadata, "Expected row to report metadata match.");
    Require(rows[0].Label == "XY velocity", "Expected metadata label.");
    Require(rows[0].Group == "Multicopter Position Control", "Expected metadata group.");
    Require(rows[0].Units == "m/s", "Expected metadata units.");
    Require(rows[0].Range == "0..20", "Expected metadata range.");
    Require(rows[0].RebootRequired, "Expected reboot-required flag.");
}

static void LoadParameterMetadataFromJson()
{
    var runtime = new ParameterMetadataRuntime();
    var source = new JsonParameterMetadataSource(ReadFixture("qgc-parameter-metadata.json"));
    var catalog = runtime.LoadAsync(
        source,
        new ParameterMetadataSourceContext("PX4", "Quadrotor", "test")).GetAwaiter().GetResult();

    var match = catalog.Find(1, "MPC_XY_VEL");
    Require(match is not null, "Expected PX4 parameter metadata.");
    var metadata = match ?? throw new InvalidOperationException("Expected PX4 parameter metadata.");
    Require(metadata.Label == "XY velocity", "Expected metadata label.");
    Require(metadata.Group == "Multicopter Position Control", "Expected metadata group.");
    Require(metadata.EnumValues?.Count == 2, "Expected enum values.");
    Require(metadata.RebootRequired, "Expected reboot-required metadata.");
    Require(runtime.LastContext?.FirmwareId == "PX4", "Expected runtime to record load context.");
    Require(catalog.Find(1, "PSC_VELXY_MAX") is null, "Expected non-matching firmware package to be filtered.");
}

static void ParameterViewUsesRuntimeMetadataCatalog()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var linkManager = new LinkManager(logger);
    var vehicles = new MultiVehicleManager(protocol, logger);
    var runtime = new ParameterMetadataRuntime();
    var catalog = runtime.LoadAsync(
        new JsonParameterMetadataSource(ReadFixture("qgc-parameter-metadata.json")),
        new ParameterMetadataSourceContext("PX4", "Quadrotor", "test")).GetAwaiter().GetResult();
    var viewModel = new ParameterViewModel(vehicles, catalog);
    protocol.Attach(linkManager);
    var link = linkManager.CreateConnectedMockLinkAsync().GetAwaiter().GetResult();
    link.EmitIncoming(MavlinkTestFrames.HeartbeatV1(systemId: 9, componentId: 1, autopilot: MavAutopilot.Px4, vehicleType: MavType.Quadrotor));
    link.EmitIncoming(MavlinkTestFrames.ParamValue(systemId: 9, componentId: 1, name: "MPC_XY_VEL", value: 3.5f, count: 1, index: 0));

    Require(viewModel.ParameterRows.Count == 1, "Expected one parameter row.");
    Require(viewModel.ParameterRows[0].HasMetadata, "Expected metadata-backed row.");
    Require(viewModel.ParameterRows[0].Range == "0..20", "Expected range projection.");
    Require(viewModel.ParameterRows[0].EnumValues.Contains("1=Enabled"), "Expected enum projection.");
    Require(viewModel.ParameterSummary.Contains("Metadata 1/1"), "Expected metadata summary.");
    Require(viewModel.ParameterMetadataState.Contains("Metadata matched 1/1"), "Expected metadata state.");
}

static void ValidateParameterEditBeforeCommit()
{
    var manager = new ParameterManager();
    ApplyParamValue(manager, componentId: 1, name: "SYS_ID", count: 1, index: 0);
    var catalog = new InMemoryParameterMetadataCatalog([
        new ParameterMetadata("SYS_ID", Min: 1, Max: 255)
    ]);
    var service = new ParameterEditService();

    var accepted = service.Commit(manager, 1, "SYS_ID", "200", catalog);

    Require(accepted.Accepted, "Expected valid parameter edit to be accepted.");
    Require(manager.IsParameterWritePending(1, "SYS_ID"), "Expected accepted edit to create pending write.");
    Require(manager.GetParameterWriteState(1, "SYS_ID").Status == ParameterWriteStatus.Pending, "Expected pending write state.");

    var rejected = service.Commit(manager, 1, "SYS_ID", "300", catalog);

    Require(!rejected.Accepted, "Expected out-of-range edit to be rejected.");
    Require(!manager.IsParameterWritePending(1, "SYS_ID"), "Expected rejected edit to clear pending state.");
    Require(manager.GetParameterWriteState(1, "SYS_ID").Status == ParameterWriteStatus.Failed, "Expected failed write state.");
    Require(manager.GetParameterWriteState(1, "SYS_ID").LastError?.Contains("<= 255") == true, "Expected range error.");
}

static void ProjectParameterWriteRetryAndFailureState()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var linkManager = new LinkManager(logger);
    var vehicles = new MultiVehicleManager(protocol, logger);
    var catalog = new InMemoryParameterMetadataCatalog([
        new ParameterMetadata("SYS_ID", Min: 1, Max: 255)
    ]);
    var viewModel = new ParameterViewModel(vehicles, catalog);
    protocol.Attach(linkManager);
    var link = linkManager.CreateConnectedMockLinkAsync().GetAwaiter().GetResult();
    link.EmitIncoming(MavlinkTestFrames.HeartbeatV1(systemId: 9, componentId: 1, autopilot: MavAutopilot.Px4, vehicleType: MavType.Quadrotor));
    link.EmitIncoming(MavlinkTestFrames.ParamValue(systemId: 9, componentId: 1, name: "SYS_ID", value: 1, count: 1, index: 0));

    var accepted = viewModel.CommitParameterEdit(1, "SYS_ID", "42");
    var vehicle = vehicles.ActiveVehicle ?? throw new InvalidOperationException("Expected active vehicle.");
    vehicle.ParameterManager.RecordParameterWriteRetry(1, "SYS_ID");

    Require(accepted.Accepted, "Expected ViewModel commit to be accepted.");
    Require(viewModel.ParameterRows[0].WriteStatus == nameof(ParameterWriteStatus.Pending), "Expected pending write status projection.");
    Require(viewModel.ParameterRows[0].WriteRetryCount == 1, "Expected retry count projection.");
    Require(viewModel.LastParameterEditStatusText.Contains("Pending write SYS_ID"), "Expected edit status text.");

    var rejected = viewModel.CommitParameterEdit(1, "SYS_ID", "300");

    Require(!rejected.Accepted, "Expected invalid ViewModel commit to be rejected.");
    Require(viewModel.ParameterRows[0].WriteStatus == nameof(ParameterWriteStatus.Failed), "Expected failed write status projection.");
    Require(viewModel.ParameterRows[0].WriteError.Contains("<= 255"), "Expected write error projection.");
}

static void SendParameterWriteAndReadbackFromParameterView()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var linkManager = new LinkManager(logger);
    var vehicles = new MultiVehicleManager(protocol, logger);
    var viewModel = new ParameterViewModel(vehicles);
    protocol.Attach(linkManager);
    var link = linkManager.CreateConnectedMockLinkAsync().GetAwaiter().GetResult();
    var sent = new List<byte[]>();
    link.BytesSent += (_, args) => sent.Add(args.Bytes.ToArray());
    link.EmitIncoming(MavlinkTestFrames.HeartbeatV1(systemId: 9, componentId: 1, autopilot: MavAutopilot.Px4, vehicleType: MavType.Quadrotor));
    link.EmitIncoming(MavlinkTestFrames.ParamValue(systemId: 9, componentId: 1, name: "SYS_ID", value: 1, count: 1, index: 0));
    sent.Clear();

    var accepted = viewModel.CommitParameterEdit(1, "SYS_ID", "42");

    Require(accepted.Accepted, "Expected edit accepted.");
    Require(sent.Count == 2, "Expected PARAM_SET and PARAM_REQUEST_READ frames.");
    var frames = new MavlinkFrameParser().Parse(sent.SelectMany(static bytes => bytes).ToArray());
    Require(frames.Any(static frame => frame.MessageId == MavlinkMessageIds.ParamSet), "Expected PARAM_SET frame.");
    Require(frames.Any(static frame => frame.MessageId == MavlinkMessageIds.ParamRequestRead), "Expected PARAM_REQUEST_READ frame.");
    Require(viewModel.ParameterRows[0].Value == "42", "Expected local row value updated while write pending.");
}

static void RoundTripParameterCacheSnapshot()
{
    var manager = new ParameterManager();
    ApplyParamValue(manager, componentId: 1, name: "SYS_ID", count: 2, index: 0);
    ApplyParamValue(manager, componentId: 1, name: "MPC_XY_VEL", count: 2, index: 1);
    var runtime = new ParameterCacheRuntime();
    var now = new DateTimeOffset(2026, 6, 25, 12, 0, 0, TimeSpan.Zero);
    var snapshot = runtime.CreateSnapshot(manager, "vehicle-9", "PX4", "Quadrotor", now);
    var store = new InMemoryParameterCacheStore();

    runtime.SaveAsync(store, snapshot).GetAwaiter().GetResult();
    var loaded = runtime.LoadAsync(store, "vehicle-9").GetAwaiter().GetResult();

    Require(loaded is not null, "Expected cached snapshot.");
    var loadedSnapshot = loaded ?? throw new InvalidOperationException("Expected cached snapshot.");
    Require(loadedSnapshot.VehicleIdentity == "vehicle-9", "Expected vehicle identity.");
    Require(loadedSnapshot.FirmwareId == "PX4", "Expected firmware identity.");
    Require(loadedSnapshot.VehicleType == "Quadrotor", "Expected vehicle type.");
    Require(loadedSnapshot.Parameters.Count == 2, "Expected cached parameters.");
    Require(!loadedSnapshot.IsStale(now.AddMinutes(5), TimeSpan.FromHours(1)), "Expected fresh cache.");
    Require(loadedSnapshot.IsStale(now.AddHours(2), TimeSpan.FromHours(1)), "Expected stale cache.");
}

static void ProjectParameterCacheStates()
{
    var manager = new ParameterManager();
    ApplyParamValue(manager, componentId: 1, name: "SYS_ID", count: 1, index: 0);
    var catalog = new InMemoryParameterMetadataCatalog([
        new ParameterMetadata("SYS_ID", ComponentId: 1, Label: "System ID"),
        new ParameterMetadata("MPC_XY_VEL", ComponentId: 1, Label: "XY velocity"),
        new ParameterMetadata("BAT_LOW_THR", ComponentId: 1, Label: "Battery low")
    ]);
    var now = new DateTimeOffset(2026, 6, 25, 12, 0, 0, TimeSpan.Zero);
    var freshSnapshot = new ParameterCacheSnapshot(
        "vehicle-9",
        "PX4",
        "Quadrotor",
        now.AddMinutes(-5),
        [
            new ParameterCacheEntry(1, "MPC_XY_VEL", "3.5", FactValueType.Float, now.AddMinutes(-5))
        ]);

    var freshRows = ParameterProjection.BuildRows(
        manager,
        catalog,
        cacheSnapshot: freshSnapshot,
        now: now,
        staleAfter: TimeSpan.FromHours(1),
        includeMissingMetadata: true);

    Require(freshRows.Single(row => row.Name == "SYS_ID").CacheState == "Live", "Expected live cache state.");
    Require(freshRows.Single(row => row.Name == "MPC_XY_VEL").CacheState == "Cached", "Expected cached state.");
    Require(freshRows.Single(row => row.Name == "BAT_LOW_THR").CacheState == "Missing", "Expected missing state.");

    var staleSnapshot = freshSnapshot with { UpdatedAt = now.AddHours(-2) };
    var staleRows = ParameterProjection.BuildRows(
        manager,
        catalog,
        cacheSnapshot: staleSnapshot,
        now: now,
        staleAfter: TimeSpan.FromHours(1),
        includeMissingMetadata: true);

    Require(staleRows.Single(row => row.Name == "MPC_XY_VEL").CacheState == "Stale", "Expected stale cache state.");
}

static void ParseStatusText()
{
    var parser = new MavlinkFrameParser();
    var frames = parser.Parse(MavlinkTestFrames.StatusText(systemId: 5, componentId: 1, severity: MavlinkSeverity.Critical, text: "PreArm: GPS fix required"));
    Require(frames.Count == 1, "Expected STATUSTEXT frame.");
    var frame = frames[0];
    var packet = new MavlinkPacket(new MockLinkTransport(), frame.Version, frame.SystemId, frame.ComponentId, frame.MessageId, frame.Payload);
    Require(MavlinkStatusTextParser.TryRead(packet, out var statusText), "Expected STATUSTEXT parser to succeed.");
    Require(statusText.SystemId == 5, "Expected STATUSTEXT system id.");
    Require(statusText.ComponentId == 1, "Expected STATUSTEXT component id.");
    Require(statusText.Severity == MavlinkSeverity.Critical, "Expected STATUSTEXT severity.");
    Require(statusText.Text == "PreArm: GPS fix required", "Expected STATUSTEXT text.");
}

static void RejectInvalidStatusTextCrc()
{
    var parser = new MavlinkFrameParser();
    var frame = MavlinkTestFrames.StatusText();
    frame[^1] ^= 0xFF;

    var frames = parser.Parse(frame);
    Require(frames.Count == 0, "Expected invalid STATUSTEXT CRC to be rejected.");
}

static void RaiseStatusTextProtocolEvent()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var linkManager = new LinkManager(logger);
    var protocol = new MavlinkProtocol();
    MavlinkStatusText? received = null;
    protocol.StatusTextReceived += (_, statusText) => received = statusText;
    protocol.Attach(linkManager);

    var link = linkManager.CreateConnectedMockLinkAsync().GetAwaiter().GetResult();
    link.EmitIncoming(MavlinkTestFrames.StatusText(severity: MavlinkSeverity.Warning, text: "Battery low"));

    Require(received is not null, "Expected STATUSTEXT event.");
    Require(received!.Severity == MavlinkSeverity.Warning, "Expected event severity.");
    Require(received.Text == "Battery low", "Expected event text.");
}

static void AggregateMavlinkInspectorPackets()
{
    var now = new DateTimeOffset(2026, 6, 25, 12, 0, 0, TimeSpan.Zero);
    var inspector = new MavlinkInspector(() => now);
    var link = new MockLinkTransport();

    inspector.Observe(new MavlinkPacket(link, Version: 2, SystemId: 1, ComponentId: 1, MessageId: 0, Payload: []));
    now = now.AddSeconds(2);
    inspector.Observe(new MavlinkPacket(link, Version: 2, SystemId: 1, ComponentId: 1, MessageId: 0, Payload: []));
    inspector.Observe(new MavlinkPacket(link, Version: 2, SystemId: 1, ComponentId: 1, MessageId: 1, Payload: []));

    Require(inspector.Rows.Count == 2, "Expected two inspector rows.");
    var heartbeat = inspector.Rows.First(row => row.MessageId == 0);
    Require(heartbeat.Count == 2, "Expected heartbeat count.");
    Require(Math.Abs(heartbeat.RateHz - 1) < 0.001, "Expected heartbeat rate.");
    Require(heartbeat.LastSeenAt == now, "Expected last seen timestamp.");
}

static void DecodeMavlinkInspectorPackets()
{
    var inspector = new MavlinkInspector();
    var missionService = new MavlinkMissionService();

    inspector.Observe(CreatePacket(MavlinkTestFrames.HeartbeatV1(systemId: 9, componentId: 1)));
    inspector.Observe(CreatePacket(MavlinkTestFrames.ParamValue(systemId: 9, componentId: 1, name: "BAT_LOW_THR", value: 14.2f, count: 3, index: 2)));
    inspector.Observe(CreatePacket(MavlinkTestFrames.CommandAck(systemId: 9, componentId: 1, result: MavlinkCommandResult.Denied)));
    inspector.Observe(CreatePacket(MavlinkTestFrames.StatusText(systemId: 9, componentId: 1, severity: MavlinkSeverity.Warning, text: "Battery low")));
    inspector.Observe(CreatePacket(missionService.CreateMissionCountFrame(new MavlinkMissionCount(9, 1, 2, MavMissionType.Fence))));

    var rows = inspector.Rows;
    Require(rows.Any(static row => row.MessageName == "HEARTBEAT" && row.FieldSummary.Contains("autopilot")), "Expected decoded HEARTBEAT row.");
    Require(rows.Any(static row => row.MessageName == "PARAM_VALUE" && row.FieldSummary.Contains("BAT_LOW_THR")), "Expected decoded PARAM_VALUE row.");
    Require(rows.Any(static row => row.MessageName == "COMMAND_ACK" && row.FieldSummary.Contains("Denied")), "Expected decoded COMMAND_ACK row.");
    Require(rows.Any(static row => row.MessageName == "STATUSTEXT" && row.Severity == "Warning" && row.Text == "Battery low"), "Expected decoded STATUSTEXT row.");
    Require(rows.Any(static row => row.MessageName == "MISSION_COUNT" && row.FieldSummary.Contains("Fence")), "Expected decoded mission row.");
}

static void FilterMavlinkInspectorRows()
{
    var inspector = new MavlinkInspector();

    inspector.Observe(CreatePacket(MavlinkTestFrames.HeartbeatV1(systemId: 1, componentId: 1)));
    inspector.Observe(CreatePacket(MavlinkTestFrames.StatusText(systemId: 9, componentId: 42, severity: MavlinkSeverity.Warning, text: "Battery low")));
    inspector.Observe(CreatePacket(MavlinkTestFrames.ParamValue(systemId: 9, componentId: 1, name: "MPC_XY_VEL", value: 3.5f)));

    Require(inspector.GetRows(new MavlinkInspectorFilter(MessageId: 253)).Single().MessageName == "STATUSTEXT", "Expected message id filter.");
    Require(inspector.GetRows(new MavlinkInspectorFilter(MessageName: "heart")).Single().MessageName == "HEARTBEAT", "Expected message name filter.");
    Require(inspector.GetRows(new MavlinkInspectorFilter(SystemId: 9)).Count == 2, "Expected system filter.");
    Require(inspector.GetRows(new MavlinkInspectorFilter(ComponentId: 42)).Single().MessageName == "STATUSTEXT", "Expected component filter.");
    Require(inspector.GetRows(new MavlinkInspectorFilter(Severity: "warn")).Single().Text == "Battery low", "Expected severity filter.");
    Require(inspector.GetRows(new MavlinkInspectorFilter(Text: "xy_vel")).Single().MessageName == "PARAM_VALUE", "Expected decoded text filter.");
}

static void FeedMavlinkInspectorFromProtocol()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var linkManager = new LinkManager(logger);
    var protocol = new MavlinkProtocol();
    var inspector = new MavlinkInspector();
    protocol.Attach(linkManager);
    inspector.Attach(protocol);

    var link = linkManager.CreateConnectedMockLinkAsync().GetAwaiter().GetResult();
    link.EmitIncoming(MavlinkTestFrames.HeartbeatV1(systemId: 9, componentId: 1));
    link.EmitIncoming(MavlinkTestFrames.StatusText(systemId: 9, componentId: 1, text: "Inspector"));

    Require(inspector.Rows.Count == 2, "Expected heartbeat and STATUSTEXT rows.");
    Require(inspector.Rows.Any(static row => row is { SystemId: 9, ComponentId: 1, MessageId: 0, Count: 1 }), "Expected heartbeat row.");
    Require(inspector.Rows.Any(static row => row is { SystemId: 9, ComponentId: 1, MessageId: 253, Count: 1 }), "Expected STATUSTEXT row.");
}

static void AnalyzeViewHostsMavlinkInspector()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var linkManager = new LinkManager(logger);
    var protocol = new MavlinkProtocol();
    var analyze = new AnalyzeViewModel(protocol);
    protocol.Attach(linkManager);

    var link = linkManager.CreateConnectedMockLinkAsync().GetAwaiter().GetResult();
    link.EmitIncoming(MavlinkTestFrames.HeartbeatV1(systemId: 9, componentId: 1));

    Require(analyze.Title == "Analyze", "Expected Analyze title.");
    Require(analyze.InspectorRows.Count == 1, "Expected Analyze inspector row.");
    Require(analyze.Summary.Contains("1"), "Expected Analyze summary count.");
    Require(analyze.InspectorRows[0].MessageName == "HEARTBEAT", "Expected decoded Analyze inspector row.");
}

static void AnalyzeViewFiltersMavlinkInspectorRows()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var linkManager = new LinkManager(logger);
    var protocol = new MavlinkProtocol();
    var analyze = new AnalyzeViewModel(protocol);
    protocol.Attach(linkManager);

    var link = linkManager.CreateConnectedMockLinkAsync().GetAwaiter().GetResult();
    link.EmitIncoming(MavlinkTestFrames.HeartbeatV1(systemId: 9, componentId: 1));
    link.EmitIncoming(MavlinkTestFrames.StatusText(systemId: 9, componentId: 42, severity: MavlinkSeverity.Warning, text: "Battery low"));

    Require(analyze.InspectorRows.Count == 2, "Expected unfiltered Analyze rows.");
    analyze.FilterText = "severity:warning";
    Require(analyze.InspectorRows.Count == 1, "Expected filtered Analyze row.");
    Require(analyze.InspectorRows[0].MessageName == "STATUSTEXT", "Expected filtered STATUSTEXT row.");
    Require(analyze.Summary.Contains("filtered"), "Expected filtered summary.");
}

static void AnalyzeViewSwitchesSecondLevelTabs()
{
    var analyze = new AnalyzeViewModel(new MavlinkProtocol());

    Require(analyze.ActivePage == AnalyzePage.Inspector, "Expected default analyze page to be inspector.");
    Require(analyze.IsInspectorTab, "Expected inspector tab true by default.");
    analyze.SelectAnalyzePage(AnalyzePage.Console);
    Require(analyze.ActivePage == AnalyzePage.Console, "Expected console page after enum switch.");
    Require(analyze.IsConsoleTab && !analyze.IsInspectorTab && !analyze.IsChartTab, "Expected console tab exclusivity.");
    analyze.SelectAnalyzeTab("chart");
    Require(analyze.ActivePage == AnalyzePage.Chart, "Expected chart page after legacy string shim.");
    Require(analyze.IsChartTab && !analyze.IsInspectorTab && !analyze.IsConsoleTab, "Expected chart tab exclusivity.");
    Require(analyze.SelectedAnalyzeTab == "chart", "Expected selected tab shim to track active page.");
}

static void AnalyzeViewExposesTabAndActionCommands()
{
    var analyze = new AnalyzeViewModel(new MavlinkProtocol());

    analyze.ShowConsoleTabCommand.Execute().Subscribe();
    Require(analyze.ActivePage == AnalyzePage.Console, "Expected command-driven switch to console tab.");
    analyze.ShowChartTabCommand.Execute().Subscribe();
    Require(analyze.ActivePage == AnalyzePage.Chart, "Expected command-driven switch to chart tab.");
    analyze.ShowInspectorTabCommand.Execute().Subscribe();
    Require(analyze.ActivePage == AnalyzePage.Inspector, "Expected command-driven switch to inspector tab.");

    analyze.ConsoleInput = "status";
    analyze.SendConsoleCommandAction.Execute().Subscribe();
    Require(analyze.ConsoleLines.Count == 1, "Expected console command to append line.");
    analyze.ClearConsoleCommand.Execute().Subscribe();
    Require(analyze.ConsoleLines.Count == 0, "Expected clear console command to clear lines.");

    analyze.AddChartDataPoint("Altitude", 1, 10);
    analyze.ClearChartCommand.Execute().Subscribe();
    Require(analyze.ChartSnapshot.Series.Count > 0, "Expected chart clear command to re-seed default series.");
}

static void ApplyVehicleStatusMessages()
{
    var vehicle = new Vehicle(id: 1, componentId: 1, autopilot: MavAutopilot.Px4, vehicleType: MavType.Quadrotor);
    var parser = new MavlinkFrameParser();
    var frames = parser.Parse(MavlinkTestFrames.StatusText(componentId: 42, severity: MavlinkSeverity.Error, text: "Compass not calibrated")
        .Concat(MavlinkTestFrames.StatusText(componentId: 42, severity: MavlinkSeverity.Info, text: "Ready to arm"))
        .ToArray());
    Require(frames.Count == 2, "Expected two STATUSTEXT frames.");

    foreach (var frame in frames)
    {
        vehicle.ApplyPacket(new MavlinkPacket(new MockLinkTransport(), frame.Version, frame.SystemId, frame.ComponentId, frame.MessageId, frame.Payload));
    }

    Require(vehicle.StatusMessages.Count == 2, "Expected status message history.");
    Require(vehicle.StatusMessages[0].ComponentId == 42, "Expected message component.");
    Require(vehicle.StatusMessages[0].Severity == MavlinkSeverity.Error, "Expected first severity.");
    Require(vehicle.StatusMessages[0].Text == "Compass not calibrated", "Expected first text.");
    Require(vehicle.StatusMessages[1].Severity == MavlinkSeverity.Info, "Expected second severity.");
    Require(vehicle.StatusMessages[1].Text == "Ready to arm", "Expected second text.");
}

static void TrackHeartbeatFlightModeState()
{
    var vehicle = new Vehicle(id: 1, componentId: 1, autopilot: MavAutopilot.Px4, vehicleType: MavType.Quadrotor);
    Require(vehicle.FlightModeName == "PreFlight", "Expected default PreFlight mode.");

    vehicle.MarkHeartbeat(systemStatus: 4, baseMode: 0x01, customMode: 0x1234);
    Require(vehicle.BaseMode == 0x01, "Expected base mode.");
    Require(vehicle.CustomMode == 0x1234, "Expected custom mode.");
    Require(vehicle.FlightModeName == "Custom:0x1234", "Expected custom flight mode name.");

    vehicle.MarkHeartbeat(systemStatus: 4, baseMode: 0x40 | 0x10, customMode: 0);
    Require(vehicle.FlightModeName == "Manual Stabilize", "Expected base-mode bit name.");
}

static void CreateSetModeFrame()
{
    var service = new MavlinkModeService();
    var parser = new MavlinkFrameParser();
    var frames = parser.Parse(service.CreateSetModeFrame(new MavlinkSetMode(TargetSystemId: 9, BaseMode: 0x01, CustomMode: 0x12345678)));
    Require(frames.Count == 1, "Expected SET_MODE frame.");
    var frame = frames[0];
    Require(frame.MessageId == 11, "Expected SET_MODE message id.");
    Require(BitConverter.ToUInt32(frame.Payload, 0) == 0x12345678, "Expected SET_MODE custom mode.");
    Require(frame.Payload[4] == 9, "Expected SET_MODE target system.");
    Require(frame.Payload[5] == 0x01, "Expected SET_MODE base mode.");
}

static void ApplyVehicleTelemetryPackets()
{
    var vehicle = new Vehicle(id: 1, componentId: 1, autopilot: MavAutopilot.Px4, vehicleType: MavType.Quadrotor);
    var parser = new MavlinkFrameParser();
    var frames = parser.Parse(MavlinkTestFrames.GlobalPositionInt().Concat(MavlinkTestFrames.SysStatus()).Concat(MavlinkTestFrames.GpsRawInt()).ToArray());
    Require(frames.Count == 3, "Expected three telemetry frames.");

    foreach (var frame in frames)
    {
        vehicle.ApplyPacket(new MavlinkPacket(new VGC.Comms.MockLinkTransport(), frame.Version, frame.SystemId, frame.ComponentId, frame.MessageId, frame.Payload));
    }

    var coordinate = vehicle.Coordinate;
    if (coordinate is null)
    {
        throw new InvalidOperationException("Expected coordinate.");
    }

    Require(Math.Abs(coordinate.Latitude - 47.397742) < 0.000001, "Expected latitude.");
    Require(Math.Abs(coordinate.Longitude - 8.545594) < 0.000001, "Expected longitude.");
    Require(Math.Abs(vehicle.RelativeAltitudeMeters!.Value - 12.3) < 0.1, "Expected relative altitude.");
    Require(Math.Abs(vehicle.BatteryVoltage!.Value - 12.0) < 0.01, "Expected battery voltage.");
    Require(vehicle.BatteryRemainingPercent == 87, "Expected battery remaining.");
    Require(vehicle.GpsFixType == 3, "Expected GPS fix type.");
    Require(vehicle.SatelliteCount == 14, "Expected satellite count.");
}

static void ApplyVehicleBatteryStatusPayload()
{
    var vehicle = new Vehicle(id: 1, componentId: 1, autopilot: MavAutopilot.ArduPilotMega, vehicleType: MavType.Quadrotor);
    var payload = new byte[36];
    BitConverter.GetBytes((ushort)11875).CopyTo(payload, 10);
    for (var i = 1; i < 10; i++)
    {
        BitConverter.GetBytes(ushort.MaxValue).CopyTo(payload, 10 + i * 2);
    }

    payload[35] = 73;

    var updated = vehicle.ApplyPacket(new MavlinkPacket(
        new VGC.Comms.MockLinkTransport(),
        Version: 2,
        SystemId: 1,
        ComponentId: 1,
        MessageId: 147,
        Payload: payload));

    Require(updated, "Expected BATTERY_STATUS payload to update vehicle.");
    Require(Math.Abs(vehicle.BatteryVoltage!.Value - 11.875) < 0.001, "Expected BATTERY_STATUS voltage.");
    Require(vehicle.BatteryRemainingPercent == 73, "Expected BATTERY_STATUS remaining percent.");
}

static void ValidateFactMetadataRange()
{
    var metaData = new FactMetaData("TEST", FactValueType.Float)
    {
        Units = "m",
        Min = 0,
        Max = 10
    };

    var fact = new Fact(componentId: 1, "TEST", metaData, 5);
    Require(fact.DisplayValue == "5 m", "Expected display value with units.");
    Require(fact.Validate(11).IsValid == false, "Expected max validation failure.");
    Require(fact.Validate(6).IsValid, "Expected valid value.");
}

static void ApplyParamValueToParameterManager()
{
    var vehicle = new Vehicle(id: 1, componentId: 1, autopilot: MavAutopilot.Px4, vehicleType: MavType.Quadrotor);
    var parser = new MavlinkFrameParser();
    var frames = parser.Parse(MavlinkTestFrames.ParamValue(name: "MPC_XY_VEL", value: 3.5f));
    Require(frames.Count == 1, "Expected PARAM_VALUE frame.");
    var frame = frames[0];
    vehicle.ApplyPacket(new MavlinkPacket(new VGC.Comms.MockLinkTransport(), frame.Version, frame.SystemId, frame.ComponentId, frame.MessageId, frame.Payload));
    Require(vehicle.ParameterManager.Count == 1, "Expected one parameter.");
    Require(vehicle.ParameterManager.TryGetParameter(1, "MPC_XY_VEL", out var fact), "Expected parameter fact.");
    Require(fact is not null && Math.Abs(Convert.ToSingle(fact.RawValue) - 3.5f) < 0.001f, "Expected parameter value.");
}

static void ApplyParamValue(ParameterManager manager, int componentId, string name, ushort count, ushort index)
{
    var parser = new MavlinkFrameParser();
    var frames = parser.Parse(MavlinkTestFrames.ParamValue(componentId: (byte)componentId, name: name, count: count, index: index));
    Require(frames.Count == 1, "Expected PARAM_VALUE frame.");
    Require(manager.ApplyParamValuePayload(frames[0].ComponentId, frames[0].Payload), "Expected PARAM_VALUE to apply.");
}

static void ConvertPlanItemToMissionItemInt()
{
    var planItem = new MissionPlanItem
    {
        Command = 16,
        Frame = 3,
        Params = [10, 20, 30, 40, 47.397742, 8.545594, 30],
        AutoContinue = true,
        DoJumpId = 7
    };

    var item = MissionItemConverter.ToMavlinkMissionItemInt(planItem, targetSystemId: 9, targetComponentId: 1);
    Require(item.TargetSystemId == 9, "Expected target system.");
    Require(item.TargetComponentId == 1, "Expected target component.");
    Require(item.Sequence == 7, "Expected sequence from doJumpId.");
    Require(item.Command == 16, "Expected command.");
    Require(item.Frame == 3, "Expected frame.");
    Require(item.AutoContinue == 1, "Expected auto continue.");
    Require(Math.Abs(item.Param1 - 10) < 0.001, "Expected param1.");
    Require(item.X == 473977420, "Expected scaled latitude.");
    Require(item.Y == 85455940, "Expected scaled longitude.");
    Require(Math.Abs(item.Z - 30) < 0.001, "Expected altitude.");
}

static void ConvertMissionItemIntToPlanItem()
{
    var item = new MavlinkMissionItemInt(
        TargetSystemId: 9,
        TargetComponentId: 1,
        Sequence: 3,
        Command: 16,
        Frame: 3,
        Current: 0,
        AutoContinue: 1,
        Param1: 10,
        Param2: 20,
        Param3: 30,
        Param4: 40,
        X: 473977420,
        Y: 85455940,
        Z: 30);

    var planItem = MissionItemConverter.ToMissionPlanItem(item);
    Require(planItem.Type == "SimpleItem", "Expected simple item type.");
    Require(planItem.Command == 16, "Expected command.");
    Require(planItem.Frame == 3, "Expected frame.");
    Require(planItem.DoJumpId == 3, "Expected doJumpId from sequence.");
    Require(planItem.AutoContinue, "Expected auto continue.");
    Require(Math.Abs(planItem.Params[0] - 10) < 0.001, "Expected param1.");
    Require(Math.Abs(planItem.Params[4] - 47.397742) < 0.0000001, "Expected latitude.");
    Require(Math.Abs(planItem.Params[5] - 8.545594) < 0.0000001, "Expected longitude.");
    Require(Math.Abs(planItem.Params[6] - 30) < 0.001, "Expected altitude.");
}

static void RoundTripPlanItemMissionConversion()
{
    var source = new MissionPlanItem
    {
        Command = 16,
        Frame = 3,
        Params = [1.5, 2.5, 3.5, 4.5, -10.1234567, 20.7654321, 100.5],
        AutoContinue = false,
        DoJumpId = 12
    };

    var missionItem = MissionItemConverter.ToMavlinkMissionItemInt(source, targetSystemId: 9, targetComponentId: 1);
    var roundTripped = MissionItemConverter.ToMissionPlanItem(missionItem);

    Require(roundTripped.Command == source.Command, "Expected command to round-trip.");
    Require(roundTripped.Frame == source.Frame, "Expected frame to round-trip.");
    Require(roundTripped.AutoContinue == source.AutoContinue, "Expected autoContinue to round-trip.");
    Require(roundTripped.DoJumpId == source.DoJumpId, "Expected doJumpId to round-trip.");
    for (var i = 0; i < 4; i++)
    {
        Require(Math.Abs(roundTripped.Params[i] - source.Params[i]) < 0.000001, $"Expected param {i + 1} to round-trip.");
    }

    Require(Math.Abs(roundTripped.Params[4] - source.Params[4]) < 0.0000001, "Expected latitude to round-trip.");
    Require(Math.Abs(roundTripped.Params[5] - source.Params[5]) < 0.0000001, "Expected longitude to round-trip.");
    Require(Math.Abs(roundTripped.Params[6] - source.Params[6]) < 0.001, "Expected altitude to round-trip.");
}

static void TrackMissionReadTransactionState()
{
    var manager = new MissionTransferManager();
    var beginAction = manager.BeginRead();
    Require(beginAction.Type == MissionTransferActionType.SendMissionRequestList, "Expected request-list action.");
    Require(manager.TransactionType == MissionTransactionType.Read, "Expected read transaction.");
    Require(manager.ExpectedMessage == MissionExpectedMessage.MissionCount, "Expected mission count.");
    Require(manager.InProgress, "Expected transaction in progress.");

    var request0 = manager.HandleMissionCount(new MavlinkMissionCount(255, 190, 2));
    Require(request0.Type == MissionTransferActionType.SendMissionRequestInt, "Expected request item action.");
    Require(request0.Sequence == 0, "Expected item 0 request.");
    Require(manager.ExpectedItemCount == 2, "Expected item count.");

    var request1 = manager.HandleMissionItemInt(CreateMissionItem(sequence: 0));
    Require(request1.Type == MissionTransferActionType.SendMissionRequestInt, "Expected next item request.");
    Require(request1.Sequence == 1, "Expected item 1 request.");
    Require(manager.ReceivedItemCount == 1, "Expected one received item.");
    Require(Math.Abs(manager.Progress - 0.5) < 0.001, "Expected half progress.");

    var done = manager.HandleMissionItemInt(CreateMissionItem(sequence: 1));
    Require(done.Type == MissionTransferActionType.None, "Expected no action after read completion.");
    Require(!manager.InProgress, "Expected read transaction complete.");
    Require(manager.MissionItems.Count == 2, "Expected stored mission items.");
    Require(manager.LastError == MissionTransferError.None, "Expected no error.");
}

static void TrackMissionWriteTransactionState()
{
    var manager = new MissionTransferManager();
    var items = new[] { CreateMissionItem(sequence: 0), CreateMissionItem(sequence: 1) };
    var beginAction = manager.BeginWrite(items);
    Require(beginAction.Type == MissionTransferActionType.SendMissionCount, "Expected mission-count action.");
    Require(manager.TransactionType == MissionTransactionType.Write, "Expected write transaction.");
    Require(manager.ExpectedMessage == MissionExpectedMessage.MissionRequestInt, "Expected mission request.");
    Require(manager.PendingWriteRequestCount == 2, "Expected two pending write requests.");

    var send0 = manager.HandleMissionRequestInt(new MavlinkMissionRequestInt(255, 190, 0));
    Require(send0.Type == MissionTransferActionType.SendMissionItemInt, "Expected send mission item action.");
    Require(send0.Item == items[0], "Expected item 0.");
    Require(manager.ExpectedMessage == MissionExpectedMessage.MissionRequestInt, "Expected more mission requests.");
    Require(manager.PendingWriteRequestCount == 1, "Expected one pending write request.");

    var send1 = manager.HandleMissionRequestInt(new MavlinkMissionRequestInt(255, 190, 1));
    Require(send1.Type == MissionTransferActionType.SendMissionItemInt, "Expected send mission item action.");
    Require(send1.Item == items[1], "Expected item 1.");
    Require(manager.ExpectedMessage == MissionExpectedMessage.MissionAck, "Expected final mission ACK.");
    Require(manager.PendingWriteRequestCount == 0, "Expected no pending write requests.");

    var done = manager.HandleMissionAck(new MavlinkMissionAck(255, 190, MavlinkMissionResult.Accepted));
    Require(done.Type == MissionTransferActionType.None, "Expected no action after ACK.");
    Require(!manager.InProgress, "Expected write transaction complete.");
    Require(manager.LastError == MissionTransferError.None, "Expected no error.");
}

static void FailMissionTransferOnSequenceMismatch()
{
    var manager = new MissionTransferManager();
    manager.BeginRead();
    manager.HandleMissionCount(new MavlinkMissionCount(255, 190, 2));

    var action = manager.HandleMissionItemInt(CreateMissionItem(sequence: 1));
    Require(action.Type == MissionTransferActionType.None, "Expected no action on failure.");
    Require(!manager.InProgress, "Expected transaction to stop on sequence mismatch.");
    Require(manager.LastError == MissionTransferError.SequenceMismatch, "Expected sequence mismatch error.");
    Require(manager.LastErrorMessage is not null && manager.LastErrorMessage.Contains("Expected mission item 0"), "Expected useful error message.");
}

static void RejectOverlappingMissionTransactions()
{
    var manager = new MissionTransferManager();
    var first = manager.BeginRead();
    var second = manager.BeginClear();

    Require(first.Type == MissionTransferActionType.SendMissionRequestList, "Expected first transaction to start.");
    Require(second.Type == MissionTransferActionType.None, "Expected overlapping transaction to be rejected.");
    Require(manager.InProgress, "Expected original transaction to stay active.");
    Require(manager.TransactionType == MissionTransactionType.Read, "Expected original read transaction.");
    Require(manager.LastError == MissionTransferError.Busy, "Expected busy error.");
}

static void RouteMissionReadPacketsThroughVehicle()
{
    var vehicle = new Vehicle(id: 9, componentId: 1, autopilot: MavAutopilot.Px4, vehicleType: MavType.Quadrotor);
    var service = new MavlinkMissionService(systemId: 1, componentId: 1);
    var parser = new MavlinkFrameParser();

    var begin = vehicle.MissionTransferManager.BeginRead();
    Require(begin.Type == MissionTransferActionType.SendMissionRequestList, "Expected request-list action.");

    ApplyVehicleFrame(vehicle, parser, service.CreateMissionCountFrame(new MavlinkMissionCount(255, 190, 1)));
    Require(vehicle.MissionTransferManager.LastAction.Type == MissionTransferActionType.SendMissionRequestInt, "Expected mission item request action.");
    Require(vehicle.MissionTransferManager.LastAction.Sequence == 0, "Expected sequence 0 request.");

    ApplyVehicleFrame(vehicle, parser, service.CreateMissionItemIntFrame(CreateMissionItem(sequence: 0)));
    Require(!vehicle.MissionTransferManager.InProgress, "Expected read transaction complete.");
    Require(vehicle.MissionTransferManager.MissionItems.Count == 1, "Expected one mission item.");
    Require(vehicle.MissionTransferManager.LastError == MissionTransferError.None, "Expected no mission error.");
}

static void RouteMissionWritePacketsThroughVehicle()
{
    var vehicle = new Vehicle(id: 9, componentId: 1, autopilot: MavAutopilot.Px4, vehicleType: MavType.Quadrotor);
    var service = new MavlinkMissionService(systemId: 1, componentId: 1);
    var parser = new MavlinkFrameParser();
    var item = CreateMissionItem(sequence: 0);

    var begin = vehicle.MissionTransferManager.BeginWrite([item]);
    Require(begin.Type == MissionTransferActionType.SendMissionCount, "Expected mission-count action.");

    ApplyVehicleFrame(vehicle, parser, service.CreateMissionRequestIntFrame(new MavlinkMissionRequestInt(255, 190, 0)));
    Require(vehicle.MissionTransferManager.LastAction.Type == MissionTransferActionType.SendMissionItemInt, "Expected mission item send action.");
    Require(vehicle.MissionTransferManager.LastAction.Item == item, "Expected mission item action payload.");
    Require(vehicle.MissionTransferManager.ExpectedMessage == MissionExpectedMessage.MissionAck, "Expected final ACK.");

    ApplyVehicleFrame(vehicle, parser, service.CreateMissionAckFrame(new MavlinkMissionAck(255, 190, MavlinkMissionResult.Accepted)));
    Require(!vehicle.MissionTransferManager.InProgress, "Expected write transaction complete.");
    Require(vehicle.MissionTransferManager.LastError == MissionTransferError.None, "Expected no mission error.");
}

static void RouteGeoFenceReadPacketsThroughVehicle()
{
    var link = new MockLinkTransport();
    var sent = new List<byte[]>();
    link.BytesSent += (_, args) => sent.Add(args.Bytes);
    link.ConnectAsync().GetAwaiter().GetResult();

    var vehicle = new Vehicle(id: 9, componentId: 1, autopilot: MavAutopilot.Px4, vehicleType: MavType.Quadrotor);
    vehicle.AttachPlanTransferLink(link);
    var missionFrames = new MavlinkMissionService(systemId: 9, componentId: 1);
    var parser = new MavlinkFrameParser();
    var geoFence = new GeoFencePlan();
    geoFence.Circles.Add(new GeoFenceCircle
    {
        Circle = new GeoFenceCircleShape
        {
            Center = new PlanCoordinate(47.397742, 8.545594),
            Radius = 80
        }
    });
    var items = GeoFenceMissionItemConverter.ToMissionItems(geoFence, targetSystemId: 9, targetComponentId: 1);

    vehicle.GeoFenceTransferService!.BeginReadAsync().GetAwaiter().GetResult();
    ApplyVehicleFrame(vehicle, parser, missionFrames.CreateMissionCountFrame(new MavlinkMissionCount(255, 190, checked((ushort)items.Count), MavMissionType.Fence)));
    Require(sent.Count == 2, "Expected GeoFence request-list and request-int frames.");
    Require(MavlinkMissionService.TryReadMissionRequestInt(CreatePacket(sent[1]), out var request), "Expected GeoFence request-int frame.");
    Require(request.MissionType == MavMissionType.Fence, "Expected Fence request type.");

    foreach (var item in items)
    {
        ApplyVehicleFrame(vehicle, parser, missionFrames.CreateMissionItemIntFrame(item));
    }

    Require(!vehicle.GeoFenceTransferManager.InProgress, "Expected GeoFence read complete.");
    Require(vehicle.LastGeoFencePlan.Circles.Count == 1, "Expected vehicle GeoFence plan cache.");
    Require(!vehicle.MissionTransferManager.InProgress, "Expected mission manager to stay idle.");
}

static void RouteRallyReadPacketsThroughVehicle()
{
    var link = new MockLinkTransport();
    var sent = new List<byte[]>();
    link.BytesSent += (_, args) => sent.Add(args.Bytes);
    link.ConnectAsync().GetAwaiter().GetResult();

    var vehicle = new Vehicle(id: 9, componentId: 1, autopilot: MavAutopilot.Px4, vehicleType: MavType.Quadrotor);
    vehicle.AttachPlanTransferLink(link);
    var missionFrames = new MavlinkMissionService(systemId: 9, componentId: 1);
    var parser = new MavlinkFrameParser();
    var rallyPoints = new RallyPointsPlan();
    rallyPoints.Points.Add(new PlanCoordinate(47.39760401, 8.5509154, 50));
    var items = RallyPointMissionItemConverter.ToMissionItems(rallyPoints, targetSystemId: 9, targetComponentId: 1);

    vehicle.RallyPointTransferService!.BeginReadAsync().GetAwaiter().GetResult();
    ApplyVehicleFrame(vehicle, parser, missionFrames.CreateMissionCountFrame(new MavlinkMissionCount(255, 190, checked((ushort)items.Count), MavMissionType.Rally)));
    Require(sent.Count == 2, "Expected Rally request-list and request-int frames.");
    Require(MavlinkMissionService.TryReadMissionRequestInt(CreatePacket(sent[1]), out var request), "Expected Rally request-int frame.");
    Require(request.MissionType == MavMissionType.Rally, "Expected Rally request type.");

    foreach (var item in items)
    {
        ApplyVehicleFrame(vehicle, parser, missionFrames.CreateMissionItemIntFrame(item));
    }

    Require(!vehicle.RallyPointTransferManager.InProgress, "Expected Rally read complete.");
    Require(vehicle.LastRallyPointsPlan.Points.Count == 1, "Expected vehicle Rally plan cache.");
    Require(!vehicle.MissionTransferManager.InProgress, "Expected mission manager to stay idle.");
}

static void IgnoreCrossTypeMissionPackets()
{
    var manager = new MissionTransferManager(MavMissionType.Mission);
    manager.BeginRead();

    var service = new MavlinkMissionService();
    var staleFrame = ParseSingleFrame(service.CreateMissionCountFrame(new MavlinkMissionCount(255, 190, 2, MavMissionType.Fence)));
    var stalePacket = new MavlinkPacket(new MockLinkTransport(), staleFrame.Version, staleFrame.SystemId, staleFrame.ComponentId, staleFrame.MessageId, staleFrame.Payload);

    Require(!manager.ApplyPacket(stalePacket), "Expected stale fence mission count to be ignored.");
    Require(manager.InProgress, "Expected mission transfer to stay active.");
    Require(manager.ExpectedMessage == MissionExpectedMessage.MissionCount, "Expected mission count to remain expected.");
    Require(manager.LastError == MissionTransferError.None, "Expected no error from stale mission type.");

    var action = manager.HandleMissionCount(new MavlinkMissionCount(255, 190, 1, MavMissionType.Mission));
    Require(action.Type == MissionTransferActionType.SendMissionRequestInt, "Expected matching mission count to advance.");
}

static void SendMissionTransferRequestListAction()
{
    var manager = new MissionTransferManager();
    var action = manager.BeginRead();
    var sent = SendMissionTransferAction(manager, action);

    Require(sent.Count == 1, "Expected one mission request-list frame.");
    var frame = ParseSingleFrame(sent[0]);
    Require(frame.MessageId == 43, "Expected MISSION_REQUEST_LIST.");
    Require(frame.Payload[0] == 9, "Expected target system.");
    Require(frame.Payload[1] == 1, "Expected target component.");
}

static void SendMissionTransferCountAction()
{
    var manager = new MissionTransferManager();
    var action = manager.BeginWrite([CreateMissionItem(sequence: 0), CreateMissionItem(sequence: 1)]);
    var sent = SendMissionTransferAction(manager, action);

    Require(sent.Count == 1, "Expected one mission count frame.");
    var frame = ParseSingleFrame(sent[0]);
    Require(frame.MessageId == 44, "Expected MISSION_COUNT.");
    Require(BitConverter.ToUInt16(frame.Payload, 0) == 2, "Expected mission count.");
    Require(frame.Payload[2] == 9, "Expected target system.");
    Require(frame.Payload[3] == 1, "Expected target component.");
}

static void SendMissionTransferItemAction()
{
    var manager = new MissionTransferManager();
    var item = CreateMissionItem(sequence: 0);
    manager.BeginWrite([item]);
    var action = manager.HandleMissionRequestInt(new MavlinkMissionRequestInt(255, 190, 0));
    var sent = SendMissionTransferAction(manager, action);

    Require(sent.Count == 1, "Expected one mission item frame.");
    var frame = ParseSingleFrame(sent[0]);
    Require(frame.MessageId == 73, "Expected MISSION_ITEM_INT.");
    var packet = new MavlinkPacket(new MockLinkTransport(), frame.Version, frame.SystemId, frame.ComponentId, frame.MessageId, frame.Payload);
    Require(MavlinkMissionService.TryReadMissionItemInt(packet, out var parsed), "Expected item parser.");
    Require(parsed == item, "Expected sent item payload.");
}

static void SendMissionTransferActionsWithMissionType()
{
    var manager = new MissionTransferManager(MavMissionType.Rally);
    var action = manager.BeginRead();
    var sent = SendMissionTransferAction(manager, action);

    Require(sent.Count == 1, "Expected one rally request-list frame.");
    var frame = ParseSingleFrame(sent[0]);
    Require(frame.Version == 2, "Expected MAVLink v2 rally request-list frame.");
    Require(frame.MessageId == 43, "Expected MISSION_REQUEST_LIST.");
    Require(frame.Payload.Length == 3, "Expected rally request-list mission_type payload.");
    Require(frame.Payload[2] == (byte)MavMissionType.Rally, "Expected rally mission type.");
}

static void IgnoreEmptyMissionTransferAction()
{
    var manager = new MissionTransferManager();
    var sent = SendMissionTransferAction(manager, MissionTransferAction.None);
    Require(sent.Count == 0, "Expected no frame for empty action.");
}

static void MissionTransferServiceBeginsRead()
{
    var (service, sent) = CreateMissionTransferService();
    var action = service.BeginReadAsync().GetAwaiter().GetResult();

    Require(action.Type == MissionTransferActionType.SendMissionRequestList, "Expected request-list action.");
    Require(sent.Count == 1, "Expected one initial read frame.");
    Require(ParseSingleFrame(sent[0]).MessageId == 43, "Expected MISSION_REQUEST_LIST.");
}

static void MissionTransferServiceHandlesReadPacket()
{
    var (service, sent) = CreateMissionTransferService();
    var missionFrames = new MavlinkMissionService(systemId: 1, componentId: 1);
    service.BeginReadAsync().GetAwaiter().GetResult();

    var countPacket = CreatePacket(missionFrames.CreateMissionCountFrame(new MavlinkMissionCount(255, 190, 1)));
    var requestAction = service.HandlePacketAsync(countPacket).GetAwaiter().GetResult();
    Require(requestAction.Type == MissionTransferActionType.SendMissionRequestInt, "Expected item request action.");
    Require(sent.Count == 2, "Expected request-list and request-int frames.");
    Require(ParseSingleFrame(sent[1]).MessageId == 51, "Expected MISSION_REQUEST_INT.");

    var itemPacket = CreatePacket(missionFrames.CreateMissionItemIntFrame(CreateMissionItem(sequence: 0)));
    var doneAction = service.HandlePacketAsync(itemPacket).GetAwaiter().GetResult();
    Require(doneAction.Type == MissionTransferActionType.None, "Expected no action after read completion.");
    Require(sent.Count == 2, "Expected no final read frame.");
    Require(!service.Manager.InProgress, "Expected read to complete.");
    Require(service.Manager.MissionItems.Count == 1, "Expected one mission item.");
}

static void MissionTransferServiceCompletesWrite()
{
    var (service, sent) = CreateMissionTransferService();
    var missionFrames = new MavlinkMissionService(systemId: 1, componentId: 1);
    var item = CreateMissionItem(sequence: 0);
    var begin = service.BeginWriteAsync([item]).GetAwaiter().GetResult();
    Require(begin.Type == MissionTransferActionType.SendMissionCount, "Expected mission count action.");
    Require(sent.Count == 1, "Expected initial mission count frame.");
    Require(ParseSingleFrame(sent[0]).MessageId == 44, "Expected MISSION_COUNT.");

    var requestPacket = CreatePacket(missionFrames.CreateMissionRequestIntFrame(new MavlinkMissionRequestInt(255, 190, 0)));
    var itemAction = service.HandlePacketAsync(requestPacket).GetAwaiter().GetResult();
    Require(itemAction.Type == MissionTransferActionType.SendMissionItemInt, "Expected send item action.");
    Require(sent.Count == 2, "Expected count and item frames.");
    Require(ParseSingleFrame(sent[1]).MessageId == 73, "Expected MISSION_ITEM_INT.");

    var ackPacket = CreatePacket(missionFrames.CreateMissionAckFrame(new MavlinkMissionAck(255, 190, MavlinkMissionResult.Accepted)));
    var done = service.HandlePacketAsync(ackPacket).GetAwaiter().GetResult();
    Require(done.Type == MissionTransferActionType.None, "Expected no action after ACK.");
    Require(sent.Count == 2, "Expected no final write frame.");
    Require(!service.Manager.InProgress, "Expected write to complete.");
}

static void MissionTransferTimeoutResendsLastAction()
{
    var (service, sent) = CreateMissionTransferService();
    service.MaxRetryCount = 2;
    service.BeginReadAsync().GetAwaiter().GetResult();

    var retry = service.HandleTimeoutAsync().GetAwaiter().GetResult();

    Require(retry.Type == MissionTransferActionType.SendMissionRequestList, "Expected retry to resend request-list action.");
    Require(service.RetryCount == 1, "Expected retry count to increment.");
    Require(sent.Count == 2, "Expected initial frame and retry frame.");
    Require(ParseSingleFrame(sent[1]).MessageId == 43, "Expected retried MISSION_REQUEST_LIST.");
    Require(service.LastSentAction.Type == MissionTransferActionType.SendMissionRequestList, "Expected last sent action to remain request-list.");
}

static void MissionTransferPacketResetsRetryCount()
{
    var (service, sent) = CreateMissionTransferService();
    var missionFrames = new MavlinkMissionService(systemId: 1, componentId: 1);
    service.BeginReadAsync().GetAwaiter().GetResult();
    service.HandleTimeoutAsync().GetAwaiter().GetResult();
    Require(service.RetryCount == 1, "Expected retry count before packet.");

    var countPacket = CreatePacket(missionFrames.CreateMissionCountFrame(new MavlinkMissionCount(255, 190, 1)));
    var request = service.HandlePacketAsync(countPacket).GetAwaiter().GetResult();

    Require(request.Type == MissionTransferActionType.SendMissionRequestInt, "Expected packet to advance read transfer.");
    Require(service.RetryCount == 0, "Expected valid packet to reset retry count.");
    Require(sent.Count == 3, "Expected initial, retry, and next action frames.");
    Require(ParseSingleFrame(sent[2]).MessageId == 51, "Expected next request-int frame.");
}

static void MissionTransferRetryExhaustionFailsTransfer()
{
    var (service, sent) = CreateMissionTransferService();
    service.MaxRetryCount = 1;
    service.BeginReadAsync().GetAwaiter().GetResult();

    var firstRetry = service.HandleTimeoutAsync().GetAwaiter().GetResult();
    var exhausted = service.HandleTimeoutAsync().GetAwaiter().GetResult();

    Require(firstRetry.Type == MissionTransferActionType.SendMissionRequestList, "Expected first timeout to retry.");
    Require(exhausted.Type == MissionTransferActionType.None, "Expected exhausted timeout to stop sending.");
    Require(sent.Count == 2, "Expected no frame after retry exhaustion.");
    Require(!service.Manager.InProgress, "Expected transfer to fail closed.");
    Require(service.Manager.LastError == MissionTransferError.MaxRetryExceeded, "Expected max retry error.");
    Require(service.LastSentAction.Type == MissionTransferActionType.None, "Expected last sent action to clear after exhaustion.");
}

static void PlanViewReflectsActiveMissionData()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var linkManager = new LinkManager(logger);
    var vehicles = new MultiVehicleManager(protocol, logger);

    var planViewModel = new PlanViewModel(vehicles);
    protocol.Attach(linkManager);
    var link = linkManager.CreateConnectedMockLinkAsync().GetAwaiter().GetResult();
    link.EmitIncoming(MavlinkTestFrames.HeartbeatV1(systemId: 9, componentId: 1, autopilot: MavAutopilot.Px4, vehicleType: MavType.Quadrotor));

    Require(planViewModel.Title == "Plan", "Expected plan title.");
    Require(planViewModel.MissionSummary.Contains("Mission v"), "Expected mission summary text.");
    Require(planViewModel.EmptyStateText.Contains("Plan View"), "Expected empty state text.");
    Require(planViewModel.MissionSummary.Contains("Items 1"), "Expected one default mission item.");
}

static void PlanViewEditsWaypointMissionItems()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var vehicles = new MultiVehicleManager(protocol, logger);
    var planViewModel = new PlanViewModel(vehicles);

    Require(planViewModel.MissionItems.Count == 1, "Expected default blank waypoint.");
    Require(planViewModel.SelectedItem is not null, "Expected default selection.");

    var added = planViewModel.AddWaypoint();
    Require(planViewModel.MissionItems.Count == 2, "Expected added waypoint.");
    Require(ReferenceEquals(planViewModel.SelectedItem, added), "Expected added waypoint to be selected.");
    Require(added.DoJumpId == 2, "Expected added waypoint order.");

    planViewModel.SelectedCommand = 22;
    planViewModel.SelectedFrame = 3;
    planViewModel.UpdateSelectedWaypoint(47.397742, 8.545594, 35);
    Require(added.Command == 22, "Expected selected command edit.");
    Require(Math.Abs(added.Params[4] - 47.397742) < 0.000001, "Expected latitude edit.");
    Require(Math.Abs(added.Params[5] - 8.545594) < 0.000001, "Expected longitude edit.");
    Require(Math.Abs(added.Params[6] - 35) < 0.001, "Expected altitude edit.");

    planViewModel.MoveSelectedItemUp();
    Require(planViewModel.SelectedIndex == 0, "Expected selected waypoint to move up.");
    Require(added.DoJumpId == 1, "Expected order to normalize after move up.");

    planViewModel.MoveSelectedItemDown();
    Require(planViewModel.SelectedIndex == 1, "Expected selected waypoint to move down.");
    Require(added.DoJumpId == 2, "Expected order to normalize after move down.");

    planViewModel.RemoveSelectedItem();
    Require(planViewModel.MissionItems.Count == 1, "Expected selected waypoint removal.");
    Require(planViewModel.MissionItems[0].DoJumpId == 1, "Expected remaining waypoint order to normalize.");
}

static void PlanViewSwitchesPlanSections()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var vehicles = new MultiVehicleManager(protocol, logger);
    var planViewModel = new PlanViewModel(vehicles);

    Require(planViewModel.ActiveSection == PlanSection.Mission, "Expected mission section by default.");
    planViewModel.ShowGeoFenceSectionCommand.Execute().Subscribe();
    Require(planViewModel.ActiveSection == PlanSection.GeoFence, "Expected GeoFence section.");
    planViewModel.ShowRallySectionCommand.Execute().Subscribe();
    Require(planViewModel.ActiveSection == PlanSection.Rally, "Expected Rally section.");
    planViewModel.ShowMissionSectionCommand.Execute().Subscribe();
    Require(planViewModel.ActiveSection == PlanSection.Mission, "Expected Mission section.");
}

static void PlanViewNavigationKeepsSectionAndMapToolIndependent()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var vehicles = new MultiVehicleManager(protocol, logger);
    var planViewModel = new PlanViewModel(vehicles);

    planViewModel.SelectFencePolygonMapToolCommand.Execute().Subscribe();
    Require(planViewModel.ActiveMapClickTool == PlanMapClickTool.FencePolygon, "Expected fence polygon tool.");
    Require(planViewModel.ActiveSection == PlanSection.Mission, "Tool selection must not change section.");

    planViewModel.ShowRallySectionCommand.Execute().Subscribe();
    Require(planViewModel.ActiveSection == PlanSection.Rally, "Expected rally section.");
    Require(planViewModel.ActiveMapClickTool == PlanMapClickTool.FencePolygon, "Section navigation must not change tool.");

    planViewModel.SelectWaypointMapToolCommand.Execute().Subscribe();
    Require(planViewModel.ActiveMapClickTool == PlanMapClickTool.Waypoint, "Expected waypoint tool.");
    Require(planViewModel.ActiveSection == PlanSection.Rally, "Tool selection must preserve current section.");

    planViewModel.ShowGeoFenceSectionCommand.Execute().Subscribe();
    Require(planViewModel.ActiveSection == PlanSection.GeoFence, "Expected geofence section.");
    Require(planViewModel.ActiveMapClickTool == PlanMapClickTool.Waypoint, "Section navigation must preserve current tool.");
}

static void PlanViewEditsGeoFenceAndRallySections()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var vehicles = new MultiVehicleManager(protocol, logger);
    var planViewModel = new PlanViewModel(vehicles);

    Require(planViewModel.GeoFencePolygons.Count == 0, "Expected no GeoFence polygons by default.");
    Require(planViewModel.GeoFenceCircles.Count == 0, "Expected no GeoFence circles by default.");
    Require(planViewModel.RallyPoints.Count == 0, "Expected no Rally points by default.");

    planViewModel.AddGeoFencePolygon();
    planViewModel.AddGeoFenceCircle();
    planViewModel.AddRallyPoint();

    Require(planViewModel.GeoFencePolygons.Count == 1, "Expected added GeoFence polygon.");
    Require(planViewModel.GeoFenceCircles.Count == 1, "Expected added GeoFence circle.");
    Require(planViewModel.RallyPoints.Count == 1, "Expected added Rally point.");
    Require(planViewModel.GeoFenceValidationText == "GeoFence valid", "Expected valid GeoFence text.");
    Require(planViewModel.RallyValidationText == "Rally valid", "Expected valid Rally text.");

    planViewModel.RemoveLastGeoFencePolygon();
    planViewModel.RemoveLastGeoFenceCircle();
    planViewModel.RemoveLastRallyPoint();

    Require(planViewModel.GeoFencePolygons.Count == 0, "Expected removed GeoFence polygon.");
    Require(planViewModel.GeoFenceCircles.Count == 0, "Expected removed GeoFence circle.");
    Require(planViewModel.RallyPoints.Count == 0, "Expected removed Rally point.");
}

static void PlanViewAddsWaypointFromMapClick()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var vehicles = new MultiVehicleManager(protocol, logger);
    var planViewModel = new PlanViewModel(vehicles);

    planViewModel.SelectWaypointMapToolCommand.Execute().Subscribe();
    var coordinate = planViewModel.ApplyMapClick(0.75, 0.25);

    Require(planViewModel.ActiveMapClickTool == PlanMapClickTool.Waypoint, "Expected waypoint map click tool.");
    Require(planViewModel.ActiveSection == PlanSection.Mission, "Expected mission section after waypoint map click.");
    Require(planViewModel.MissionItems.Count == 2, "Expected waypoint created from map click.");
    Require(planViewModel.SelectedItem is not null, "Expected clicked waypoint selection.");
    Require(Math.Abs(planViewModel.SelectedLatitude - coordinate.Latitude) < 0.000001, "Expected clicked waypoint latitude.");
    Require(Math.Abs(planViewModel.SelectedLongitude - coordinate.Longitude) < 0.000001, "Expected clicked waypoint longitude.");
    Require(Math.Abs(planViewModel.SelectedAltitude - 50) < 0.001, "Expected clicked waypoint altitude.");
    Require(planViewModel.IsPlanDirty, "Expected map click to mark plan dirty.");
    Require(planViewModel.PlanMapClickModeText.Contains("waypoint", StringComparison.OrdinalIgnoreCase), "Expected waypoint map click mode text.");
}

static void PlanViewMovesSelectedWaypointFromMapClick()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var vehicles = new MultiVehicleManager(protocol, logger);
    var planViewModel = new PlanViewModel(vehicles);

    var added = planViewModel.AddWaypointAt(new PlanCoordinate(47.397742, 8.545594, 35));
    planViewModel.SelectedItem = added;
    planViewModel.SelectMoveWaypointMapToolCommand.Execute().Subscribe();
    var coordinate = planViewModel.ApplyMapClick(0.20, 0.80);

    Require(planViewModel.ActiveMapClickTool == PlanMapClickTool.MoveSelectedWaypoint, "Expected move waypoint map click tool.");
    Require(planViewModel.ActiveSection == PlanSection.Mission, "Expected mission section after moving selected waypoint.");
    Require(planViewModel.MissionItems.Count == 2, "Expected move click not to create a new waypoint.");
    Require(ReferenceEquals(planViewModel.SelectedItem, added), "Expected moved waypoint to remain selected.");
    Require(Math.Abs(planViewModel.SelectedLatitude - coordinate.Latitude) < 0.000001, "Expected moved waypoint latitude.");
    Require(Math.Abs(planViewModel.SelectedLongitude - coordinate.Longitude) < 0.000001, "Expected moved waypoint longitude.");
    Require(Math.Abs(planViewModel.SelectedAltitude - 50) < 0.001, "Expected moved waypoint altitude from map projection.");
    Require(planViewModel.MissionMapMarkers.Count == 2, "Expected marker projection for both waypoints.");
    Require(planViewModel.MissionMapMarkers.Count(static marker => marker.IsSelected) == 1, "Expected exactly one selected marker.");
    Require(planViewModel.MissionMapMarkers[1].IsSelected, "Expected moved waypoint marker to be selected.");
    Require(planViewModel.MissionMapMarkers[1].MarkerText.EndsWith("*", StringComparison.Ordinal), "Expected selected marker text indicator.");
    Require(planViewModel.MissionMapMarkers[1].MarkerFill == "#ffffff", "Expected selected marker fill.");
    Require(planViewModel.PlanMapClickModeText.Contains("move selected", StringComparison.OrdinalIgnoreCase), "Expected move mode text.");
}

static void PlanViewAddsGeoFenceAndRallyFromMapClicks()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var vehicles = new MultiVehicleManager(protocol, logger);
    var planViewModel = new PlanViewModel(vehicles);

    planViewModel.SelectFencePolygonMapToolCommand.Execute().Subscribe();
    var first = planViewModel.ApplyMapClick(0.10, 0.20);
    var second = planViewModel.ApplyMapClick(0.20, 0.30);
    var third = planViewModel.ApplyMapClick(0.30, 0.40);

    Require(planViewModel.ActiveMapClickTool == PlanMapClickTool.FencePolygon, "Expected fence polygon map click tool.");
    Require(planViewModel.ActiveSection == PlanSection.GeoFence, "Expected geofence section after polygon clicks.");
    Require(planViewModel.GeoFencePolygons.Count == 1, "Expected one map-created geofence polygon.");
    Require(planViewModel.GeoFencePolygons[0].Polygon.Count == 3, "Expected three polygon vertices from map clicks.");
    Require(Math.Abs(planViewModel.GeoFencePolygons[0].Polygon[0].Latitude - first.Latitude) < 0.000001, "Expected first polygon click latitude.");
    Require(Math.Abs(planViewModel.GeoFencePolygons[0].Polygon[1].Longitude - second.Longitude) < 0.000001, "Expected second polygon click longitude.");
    Require(Math.Abs(planViewModel.GeoFencePolygons[0].Polygon[2].Latitude - third.Latitude) < 0.000001, "Expected third polygon click latitude.");

    planViewModel.SelectFenceCircleMapToolCommand.Execute().Subscribe();
    var circleCenter = planViewModel.ApplyMapClick(0.50, 0.50);
    Require(planViewModel.GeoFenceCircles.Count == 1, "Expected one map-created geofence circle.");
    Require(Math.Abs(planViewModel.GeoFenceCircles[0].Circle.Center.Latitude - circleCenter.Latitude) < 0.000001, "Expected circle center latitude.");

    planViewModel.SelectRallyMapToolCommand.Execute().Subscribe();
    var rally = planViewModel.ApplyMapClick(0.90, 0.80);
    Require(planViewModel.ActiveSection == PlanSection.Rally, "Expected rally section after rally click.");
    Require(planViewModel.RallyPoints.Count == 1, "Expected one map-created rally point.");
    Require(Math.Abs(planViewModel.RallyPoints[0].Longitude - rally.Longitude) < 0.000001, "Expected rally longitude.");
}

static void PlanViewReflectsMissionTransferProgress()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var linkManager = new LinkManager(logger);
    var vehicles = new MultiVehicleManager(protocol, logger);
    var planViewModel = new PlanViewModel(vehicles);
    protocol.Attach(linkManager);
    var link = linkManager.CreateConnectedMockLinkAsync().GetAwaiter().GetResult();
    link.EmitIncoming(MavlinkTestFrames.HeartbeatV1(systemId: 9, componentId: 1, autopilot: MavAutopilot.Px4, vehicleType: MavType.Quadrotor));

    var vehicle = vehicles.ActiveVehicle ?? throw new InvalidOperationException("Expected active vehicle.");
    var missionFrames = new MavlinkMissionService(systemId: 9, componentId: 1);
    vehicle.MissionTransferManager.BeginRead();
    link.EmitIncoming(missionFrames.CreateMissionCountFrame(new MavlinkMissionCount(255, 190, 2)));
    link.EmitIncoming(missionFrames.CreateMissionItemIntFrame(CreateMissionItem(sequence: 0)));

    Require(planViewModel.VehicleMissionState.Contains("Vehicle 9"), "Expected active vehicle mission state.");
    Require(planViewModel.MissionTransferSummary.Contains("Reading mission 1/2"), "Expected read progress summary.");
    Require(Math.Abs(planViewModel.MissionTransferProgressPercent - 50) < 0.001, "Expected 50 percent mission progress.");
}

static void PlanViewReflectsMissionTransferErrors()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var linkManager = new LinkManager(logger);
    var vehicles = new MultiVehicleManager(protocol, logger);
    var planViewModel = new PlanViewModel(vehicles);
    protocol.Attach(linkManager);
    var link = linkManager.CreateConnectedMockLinkAsync().GetAwaiter().GetResult();
    link.EmitIncoming(MavlinkTestFrames.HeartbeatV1(systemId: 9, componentId: 1, autopilot: MavAutopilot.Px4, vehicleType: MavType.Quadrotor));

    var vehicle = vehicles.ActiveVehicle ?? throw new InvalidOperationException("Expected active vehicle.");
    var missionFrames = new MavlinkMissionService(systemId: 9, componentId: 1);
    vehicle.MissionTransferManager.BeginRead();
    link.EmitIncoming(missionFrames.CreateMissionCountFrame(new MavlinkMissionCount(255, 190, 2)));
    link.EmitIncoming(missionFrames.CreateMissionItemIntFrame(CreateMissionItem(sequence: 1)));

    Require(planViewModel.MissionTransferErrorText.Contains("SequenceMismatch"), "Expected mission sequence error.");
    Require(planViewModel.MissionTransferErrorText.Contains("Expected mission item 0"), "Expected mission error detail.");
}

static void PlanViewUploadsGeoFenceTransfer()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var linkManager = new LinkManager(logger);
    var vehicles = new MultiVehicleManager(protocol, logger);
    var planViewModel = new PlanViewModel(vehicles, linkManager);
    protocol.Attach(linkManager);
    var link = linkManager.CreateConnectedMockLinkAsync().GetAwaiter().GetResult();
    var sent = new List<byte[]>();
    link.BytesSent += (_, args) => sent.Add(args.Bytes);
    link.EmitIncoming(MavlinkTestFrames.HeartbeatV1(systemId: 9, componentId: 1, autopilot: MavAutopilot.Px4, vehicleType: MavType.Quadrotor));
    WaitFor(() => sent.Count >= 6);
    sent.Clear();

    planViewModel.AddGeoFenceCircle();
    planViewModel.UploadGeoFenceAsync().GetAwaiter().GetResult();

    Require(sent.Count == 1, "Expected GeoFence upload to send initial count.");
    Require(MavlinkMissionService.TryReadMissionCount(CreatePacket(sent[0]), out var count), "Expected GeoFence upload count.");
    Require(count.MissionType == MavMissionType.Fence, "Expected Fence upload mission type.");
    Require(planViewModel.GeoFenceTransferSummary.Contains("Writing GeoFence"), "Expected GeoFence write summary.");
}

static void PlanViewUploadsRallyTransfer()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var linkManager = new LinkManager(logger);
    var vehicles = new MultiVehicleManager(protocol, logger);
    var planViewModel = new PlanViewModel(vehicles, linkManager);
    protocol.Attach(linkManager);
    var link = linkManager.CreateConnectedMockLinkAsync().GetAwaiter().GetResult();
    var sent = new List<byte[]>();
    link.BytesSent += (_, args) => sent.Add(args.Bytes);
    link.EmitIncoming(MavlinkTestFrames.HeartbeatV1(systemId: 9, componentId: 1, autopilot: MavAutopilot.Px4, vehicleType: MavType.Quadrotor));
    WaitFor(() => sent.Count >= 6);
    sent.Clear();

    planViewModel.AddRallyPoint();
    planViewModel.UploadRallyAsync().GetAwaiter().GetResult();

    Require(sent.Count == 1, "Expected Rally upload to send initial count.");
    Require(MavlinkMissionService.TryReadMissionCount(CreatePacket(sent[0]), out var count), "Expected Rally upload count.");
    Require(count.MissionType == MavMissionType.Rally, "Expected Rally upload mission type.");
    Require(planViewModel.RallyTransferSummary.Contains("Writing Rally"), "Expected Rally write summary.");
}

static void PlanViewReportsSectionTransferBlockers()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var vehicles = new MultiVehicleManager(protocol, logger);
    var planViewModel = new PlanViewModel(vehicles);

    planViewModel.DownloadGeoFenceAsync().GetAwaiter().GetResult();
    planViewModel.DownloadRallyAsync().GetAwaiter().GetResult();

    Require(planViewModel.GeoFenceTransferErrorText.Contains("active vehicle"), "Expected GeoFence active vehicle error.");
    Require(planViewModel.RallyTransferErrorText.Contains("active vehicle"), "Expected Rally active vehicle error.");
}

static void PlanViewExposesSectionTransferAvailability()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var linkManager = new LinkManager(logger);
    var vehicles = new MultiVehicleManager(protocol, logger);
    var planViewModel = new PlanViewModel(vehicles, linkManager);
    protocol.Attach(linkManager);

    Require(!planViewModel.CanRequestGeoFenceTransfer, "Expected GeoFence transfer unavailable without vehicle/link.");
    Require(!planViewModel.CanRequestRallyTransfer, "Expected Rally transfer unavailable without vehicle/link.");
    Require(planViewModel.PlanTransferLinkText.Contains("No send-capable link"), "Expected no-link text.");

    var link = linkManager.CreateConnectedMockLinkAsync().GetAwaiter().GetResult();
    link.EmitIncoming(MavlinkTestFrames.HeartbeatV1(systemId: 9, componentId: 1, autopilot: MavAutopilot.Px4, vehicleType: MavType.Quadrotor));

    Require(planViewModel.CanRequestGeoFenceTransfer, "Expected GeoFence transfer available with supported vehicle/link.");
    Require(planViewModel.CanRequestRallyTransfer, "Expected Rally transfer available with supported vehicle/link.");
    Require(planViewModel.PlanTransferLinkText.Contains("Mock Link"), "Expected active transfer link text.");
}

static void PlanViewExposesTransferCommandModel()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var linkManager = new LinkManager(logger);
    var vehicles = new MultiVehicleManager(protocol, logger);
    var planViewModel = new PlanViewModel(vehicles, linkManager);
    protocol.Attach(linkManager);

    Require(planViewModel.PlanTransferCommands.Count == 9, "Expected Mission/GeoFence/Rally upload/download/clear commands.");
    Require(planViewModel.PlanTransferCommands[0].Section == PlanSection.Mission, "Expected Mission command first.");
    Require(planViewModel.PlanTransferCommands[0].Verb == PlanTransferVerb.Upload, "Expected Mission upload first.");
    Require(planViewModel.PlanTransferCommands[0].Label == "Mission Upload", "Expected Mission upload label.");
    Require(planViewModel.PlanTransferCommands[3].Label == "GeoFence Upload", "Expected GeoFence upload label.");
    Require(planViewModel.PlanTransferCommands[6].Label == "Rally Upload", "Expected Rally upload label.");
    Require(planViewModel.PlanTransferCommands.All(static command => !command.IsEnabled), "Expected commands disabled without vehicle/link.");

    var link = linkManager.CreateConnectedMockLinkAsync().GetAwaiter().GetResult();
    link.EmitIncoming(MavlinkTestFrames.HeartbeatV1(systemId: 9, componentId: 1, autopilot: MavAutopilot.Px4, vehicleType: MavType.Quadrotor));

    Require(planViewModel.PlanTransferCommands.All(static command => command.IsEnabled), "Expected all transfer commands enabled with supported vehicle/link.");

    planViewModel.RequestMissionUpload();

    Require(planViewModel.HasPendingTransfer, "Expected pending mission transfer.");
    Require(planViewModel.PendingTransferText.Contains("Mission upload"), "Expected Mission pending text.");
    Require(planViewModel.PlanTransferCommands.All(static command => !command.IsEnabled), "Expected commands disabled while pending.");
}

static void PlanViewConfirmsPendingMissionTransfer()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var linkManager = new LinkManager(logger);
    var vehicles = new MultiVehicleManager(protocol, logger);
    var planViewModel = new PlanViewModel(vehicles, linkManager);
    protocol.Attach(linkManager);
    var link = linkManager.CreateConnectedMockLinkAsync().GetAwaiter().GetResult();
    var sent = new List<byte[]>();
    link.BytesSent += (_, args) => sent.Add(args.Bytes);
    link.EmitIncoming(MavlinkTestFrames.HeartbeatV1(systemId: 9, componentId: 1, autopilot: MavAutopilot.Px4, vehicleType: MavType.Quadrotor));
    WaitFor(() => sent.Count >= 6);
    sent.Clear();

    planViewModel.RequestMissionUpload();

    Require(planViewModel.HasPendingTransfer, "Expected pending Mission upload.");
    Require(sent.Count == 0, "Expected Mission request to wait for confirmation.");

    planViewModel.ConfirmPendingTransferAsync().GetAwaiter().GetResult();

    Require(!planViewModel.HasPendingTransfer, "Expected pending Mission transfer to clear after confirm.");
    Require(sent.Count == 1, "Expected confirmed Mission transfer to send.");
    Require(MavlinkMissionService.TryReadMissionCount(CreatePacket(sent[0]), out var count), "Expected confirmed Mission count.");
    Require(count.MissionType == MavMissionType.Mission, "Expected confirmed Mission transfer.");
    Require(count.Count == planViewModel.MissionItems.Count, "Expected Mission count to match Plan items.");
}

static void PlanViewConfirmsPendingSectionTransfer()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var linkManager = new LinkManager(logger);
    var vehicles = new MultiVehicleManager(protocol, logger);
    var planViewModel = new PlanViewModel(vehicles, linkManager);
    protocol.Attach(linkManager);
    var link = linkManager.CreateConnectedMockLinkAsync().GetAwaiter().GetResult();
    var sent = new List<byte[]>();
    link.BytesSent += (_, args) => sent.Add(args.Bytes);
    link.EmitIncoming(MavlinkTestFrames.HeartbeatV1(systemId: 9, componentId: 1, autopilot: MavAutopilot.Px4, vehicleType: MavType.Quadrotor));
    WaitFor(() => sent.Count >= 6);
    sent.Clear();

    planViewModel.AddGeoFenceCircle();
    planViewModel.RequestGeoFenceUpload();

    Require(planViewModel.HasPendingTransfer, "Expected pending GeoFence upload.");
    Require(planViewModel.PendingTransferText.Contains("GeoFence upload"), "Expected pending transfer text.");
    Require(sent.Count == 0, "Expected request to wait for confirmation.");
    Require(!planViewModel.CanRequestGeoFenceTransfer, "Expected transfer requests disabled while pending.");

    planViewModel.ConfirmPendingTransferAsync().GetAwaiter().GetResult();

    Require(!planViewModel.HasPendingTransfer, "Expected pending transfer to clear after confirm.");
    Require(sent.Count == 1, "Expected confirmed transfer to send.");
    Require(MavlinkMissionService.TryReadMissionCount(CreatePacket(sent[0]), out var count), "Expected confirmed GeoFence count.");
    Require(count.MissionType == MavMissionType.Fence, "Expected confirmed GeoFence transfer.");
}

static void PlanViewCancelsPendingSectionTransfer()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var linkManager = new LinkManager(logger);
    var vehicles = new MultiVehicleManager(protocol, logger);
    var planViewModel = new PlanViewModel(vehicles, linkManager);
    protocol.Attach(linkManager);
    var link = linkManager.CreateConnectedMockLinkAsync().GetAwaiter().GetResult();
    var sent = new List<byte[]>();
    link.BytesSent += (_, args) => sent.Add(args.Bytes);
    link.EmitIncoming(MavlinkTestFrames.HeartbeatV1(systemId: 9, componentId: 1, autopilot: MavAutopilot.Px4, vehicleType: MavType.Quadrotor));
    WaitFor(() => sent.Count >= 6);
    sent.Clear();

    planViewModel.AddRallyPoint();
    planViewModel.RequestRallyUpload();
    planViewModel.CancelPendingTransfer();

    Require(!planViewModel.HasPendingTransfer, "Expected pending transfer to clear after cancel.");
    Require(sent.Count == 0, "Expected cancelled transfer not to send.");
    Require(planViewModel.CanRequestRallyTransfer, "Expected Rally transfer available after cancel.");
    Require(!vehicles.ActiveVehicle!.RallyPointTransferManager.InProgress, "Expected local cancel not to start Rally protocol transfer.");
    Require(planViewModel.TransferNotifications.Count == 1, "Expected local cancel notification.");
    Require(planViewModel.TransferNotifications[0].Severity == PlanTransferNotificationSeverity.Warning, "Expected cancel warning notification.");
    Require(planViewModel.TransferNotifications[0].Message.Contains("canceled locally"), "Expected local cancel boundary text.");
}

static void PlanViewEmitsTransferNotifications()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var linkManager = new LinkManager(logger);
    var vehicles = new MultiVehicleManager(protocol, logger);
    var planViewModel = new PlanViewModel(vehicles, linkManager);
    protocol.Attach(linkManager);
    var link = linkManager.CreateConnectedMockLinkAsync().GetAwaiter().GetResult();
    link.EmitIncoming(MavlinkTestFrames.HeartbeatV1(systemId: 9, componentId: 1, autopilot: MavAutopilot.Px4, vehicleType: MavType.Quadrotor));

    planViewModel.RequestMissionDownload();
    planViewModel.ConfirmPendingTransferAsync().GetAwaiter().GetResult();

    Require(planViewModel.TransferNotifications.Count == 1, "Expected Mission download notification.");
    Require(planViewModel.TransferNotifications[0].Section == PlanSection.Mission, "Expected Mission notification section.");
    Require(planViewModel.TransferNotifications[0].Verb == PlanTransferVerb.Download, "Expected Mission download notification verb.");
    Require(planViewModel.TransferNotifications[0].Severity == PlanTransferNotificationSeverity.Success, "Expected successful transfer notification.");
    Require(planViewModel.LatestTransferNotificationText.Contains("Mission download started"), "Expected latest notification text.");

    var offlinePlan = new PlanViewModel(new MultiVehicleManager(new MavlinkProtocol(), logger));
    offlinePlan.DownloadGeoFenceAsync().GetAwaiter().GetResult();

    Require(offlinePlan.TransferNotifications.Count == 1, "Expected failed transfer notification.");
    Require(offlinePlan.TransferNotifications[0].Severity == PlanTransferNotificationSeverity.Error, "Expected failure notification.");
    Require(offlinePlan.TransferNotifications[0].Message.Contains("active vehicle"), "Expected failure reason in notification.");
}

static void PlanViewBoundsTransferNotifications()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var planViewModel = new PlanViewModel(new MultiVehicleManager(new MavlinkProtocol(), logger));

    for (var i = 0; i < 25; i++)
    {
        planViewModel.DownloadGeoFenceAsync().GetAwaiter().GetResult();
    }

    Require(planViewModel.TransferNotifications.Count == 20, "Expected transfer notification history to be bounded.");
    Require(planViewModel.TransferNotifications.All(static notification => notification.Severity == PlanTransferNotificationSeverity.Error), "Expected all bounded notifications to be errors.");
}

static void RoundTripPersistedLinkConfiguration()
{
    var snapshot = new AppSettingsSnapshot
    {
        LinkConfiguration = new LinkConfigurationState
        {
            PreferredActiveLinkName = "UDP Primary",
            Links =
            [
                new PersistedLinkConfiguration("UDP Primary", LinkType.Udp, 14550, "127.0.0.1", 14555),
                new PersistedLinkConfiguration("Mock Backup", LinkType.Mock)
            ]
        }
    };

    var json = JsonSerializer.Serialize(snapshot);
    var roundTripped = JsonSerializer.Deserialize<AppSettingsSnapshot>(json)
        ?? throw new InvalidOperationException("Expected settings snapshot to deserialize.");

    Require(roundTripped.LinkConfiguration.PreferredActiveLinkName == "UDP Primary", "Expected preferred active link to round-trip.");
    Require(roundTripped.LinkConfiguration.Links.Count == 2, "Expected two persisted links.");
    Require(roundTripped.LinkConfiguration.Links[0].Type == LinkType.Udp, "Expected UDP link type.");
    Require(roundTripped.LinkConfiguration.Links[0].LocalPort == 14550, "Expected UDP local port.");
    Require(roundTripped.LinkConfiguration.Links[0].TargetHost == "127.0.0.1", "Expected UDP target host.");
    Require(roundTripped.LinkConfiguration.Links[0].TargetPort == 14555, "Expected UDP target port.");
}

static void SelectActiveLinkByDeterministicPolicy()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var linkManager = new LinkManager(logger);
    linkManager.CreateConnectedMockLinkAsync("Mock A").GetAwaiter().GetResult();
    linkManager.CreateConnectedMockLinkAsync("Mock B").GetAwaiter().GetResult();

    Require(linkManager.ActiveLink?.Configuration.Name == "Mock A", "Expected first send-capable link to be active by default.");

    linkManager.SetPreferredActiveLink("Mock B");

    Require(linkManager.ActiveLink?.Configuration.Name == "Mock B", "Expected preferred send-capable link to be active.");

    linkManager.SetPreferredActiveLink("Missing");

    Require(linkManager.ActiveLink?.Configuration.Name == "Mock A", "Expected missing preference to fall back to first send-capable link.");
}

static void CaptureLinkConfigurationState()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var linkManager = new LinkManager(logger);
    linkManager.CreateConnectedMockLinkAsync("Mock Capture").GetAwaiter().GetResult();
    linkManager.SetPreferredActiveLink("Mock Capture");

    var state = linkManager.CaptureConfigurationState();

    Require(state.PreferredActiveLinkName == "Mock Capture", "Expected captured preferred active link.");
    Require(state.Links.Count == 1, "Expected one captured link.");
    Require(state.Links[0].Name == "Mock Capture", "Expected captured link name.");
    Require(state.Links[0].Type == LinkType.Mock, "Expected captured mock link type.");
}

static void CreateFutureLinkRuntimeConfigurations()
{
    var serial = new PersistedLinkConfiguration("Serial", LinkType.Serial, SerialPortName: "COM3", BaudRate: 115200)
        .ToRuntimeConfiguration();
    var tcp = new PersistedLinkConfiguration("TCP", LinkType.Tcp, Host: "192.168.1.10", Port: 5760, IsServer: false)
        .ToRuntimeConfiguration();
    var replay = new PersistedLinkConfiguration("Replay", LinkType.LogReplay, FilePath: "flight.tlog", ReplaySpeed: 2.0, LoopReplay: true)
        .ToRuntimeConfiguration();

    Require(serial is SerialLinkConfiguration, "Expected serial runtime configuration.");
    Require(((SerialLinkConfiguration)serial).PortName == "COM3", "Expected serial port name.");
    Require(((SerialLinkConfiguration)serial).BaudRate == 115200, "Expected serial baud rate.");
    Require(tcp is TcpLinkConfiguration, "Expected TCP runtime configuration.");
    Require(((TcpLinkConfiguration)tcp).Host == "192.168.1.10", "Expected TCP host.");
    Require(((TcpLinkConfiguration)tcp).Port == 5760, "Expected TCP port.");
    Require(replay is LogReplayLinkConfiguration, "Expected log replay runtime configuration.");
    Require(((LogReplayLinkConfiguration)replay).FilePath == "flight.tlog", "Expected replay file path.");
    Require(Math.Abs(((LogReplayLinkConfiguration)replay).Speed - 2.0) < 0.000001, "Expected replay speed.");
    Require(((LogReplayLinkConfiguration)replay).Loop, "Expected replay loop flag.");
}

static void TrackLinkDiagnosticsProjection()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var linkManager = new LinkManager(logger);
    var link = linkManager.CreateConnectedMockLinkAsync("Diagnostics").GetAwaiter().GetResult();

    link.EmitIncoming([1, 2, 3]);
    link.WriteAsync(new byte[] { 4, 5 }).GetAwaiter().GetResult();

    var snapshot = linkManager.GetDiagnostics().Single();
    Require(snapshot.Name == "Diagnostics", "Expected diagnostics link name.");
    Require(snapshot.Type == LinkType.Mock, "Expected diagnostics link type.");
    Require(snapshot.ConnectionState == "Send-capable", "Expected send-capable diagnostics state.");
    Require(snapshot.BytesReceived == 3, "Expected received byte count.");
    Require(snapshot.BytesSent == 2, "Expected sent byte count.");
    Require(snapshot.LastReceivedAt is not null, "Expected last receive timestamp.");
    Require(snapshot.LastSentAt is not null, "Expected last sent timestamp.");

    link.DisconnectAsync().GetAwaiter().GetResult();
    link.WriteAsync(new byte[] { 9 }).GetAwaiter().GetResult();

    snapshot = linkManager.GetDiagnostics().Single();
    Require(snapshot.ConnectionState == "Disconnected", "Expected disconnected diagnostics state.");
    Require(snapshot.LastError?.Contains("not connected", StringComparison.OrdinalIgnoreCase) == true, "Expected last link error.");
}

static void AutoConnectSavedLinkPolicy()
{
    var settings = new FakeSettingsStore();
    settings.Current.LinkConfiguration = new LinkConfigurationState
    {
        PreferredActiveLinkName = "Mock Preferred",
        Links =
        [
            new PersistedLinkConfiguration("Mock Preferred", LinkType.Mock),
            new PersistedLinkConfiguration("Serial Deferred", LinkType.Serial, SerialPortName: "COM7", BaudRate: 57600)
        ]
    };

    var store = new AppSettingsLinkConfigurationStore(settings);
    var logger = new VGC.Core.Logging.AppLogger();
    var linkManager = new LinkManager(logger, store);

    var result = linkManager.AutoConnectSavedLinksAsync().GetAwaiter().GetResult();

    Require(result.ConnectedCount == 1, "Expected one auto-connected link.");
    Require(result.UnsupportedCount == 1, "Expected unsupported platform link to be reported.");
    Require(linkManager.ActiveLink?.Configuration.Name == "Mock Preferred", "Expected preferred link to be active.");
    Require(result.SelectFallback(linkManager.Links)?.Configuration.Name == "Mock Preferred", "Expected fallback to select connected preferred link.");

    result = linkManager.AutoConnectSavedLinksAsync().GetAwaiter().GetResult();
    Require(result.Links[0].Outcome == AutoConnectOutcome.AlreadyConnected, "Expected second auto-connect to detect existing link.");
}

static void InitializeDefaultCommLinkInShell()
{
    var store = new FakeSettingsStore();
    var logger = new AppLogger();
    var linkManager = new LinkManager(logger, new AppSettingsLinkConfigurationStore(store));
    var protocol = new MavlinkProtocol();
    protocol.Attach(linkManager);
    var heartbeat = new GcsHeartbeatService(linkManager, new MavlinkFrameWriter(), logger);
    var shell = new ShellViewModel(
        new AppLifecycleService(store, logger),
        new AppCloseCoordinator(),
        logger,
        linkManager,
        protocol,
        heartbeat,
        new MultiVehicleManager(protocol, logger),
        linkConfigurationStore: new AppSettingsLinkConfigurationStore(store));

    shell.EnsureCommLinksLoadedAsync().GetAwaiter().GetResult();

    Require(shell.CommLinks.Count == 1, "Expected default Comm Link.");
    Require(shell.SelectedCommLink?.Type == LinkType.Udp, "Expected default UDP Comm Link.");
    Require(shell.SelectedCommLink?.LocalPort == 14550, "Expected QGC-compatible UDP default port.");
}

static void OpenConnectDrawerUsesSettingsWorkspace()
{
    var store = new FakeSettingsStore();
    var logger = new AppLogger();
    var linkManager = new LinkManager(logger, new AppSettingsLinkConfigurationStore(store));
    var protocol = new MavlinkProtocol();
    protocol.Attach(linkManager);
    var heartbeat = new GcsHeartbeatService(linkManager, new MavlinkFrameWriter(), logger);
    var shell = new ShellViewModel(
        new AppLifecycleService(store, logger),
        new AppCloseCoordinator(),
        logger,
        linkManager,
        protocol,
        heartbeat,
        new MultiVehicleManager(protocol, logger),
        linkConfigurationStore: new AppSettingsLinkConfigurationStore(store));

    shell.ShowConnectDrawerCommand.Execute().Subscribe();
    Require(shell.IsToolDrawerActive, "Expected connect drawer command to open tool drawer.");
    Require(shell.ToolDrawerTitle == "Comm Link Settings", "Expected connect flow to expose comm-link semantics.");
}

static void ConnectSelectedUdpCommLinkFromShell()
{
    var store = new FakeSettingsStore();
    store.Current.LinkConfiguration.Links = [new PersistedLinkConfiguration("Manual UDP", LinkType.Udp, 14551)];
    var logger = new AppLogger();
    var linkManager = new LinkManager(logger, new AppSettingsLinkConfigurationStore(store));
    var protocol = new MavlinkProtocol();
    protocol.Attach(linkManager);
    var heartbeat = new GcsHeartbeatService(linkManager, new MavlinkFrameWriter(), logger);
    var shell = new ShellViewModel(
        new AppLifecycleService(store, logger),
        new AppCloseCoordinator(),
        logger,
        linkManager,
        protocol,
        heartbeat,
        new MultiVehicleManager(protocol, logger),
        linkConfigurationStore: new AppSettingsLinkConfigurationStore(store));

    shell.EnsureCommLinksLoadedAsync().GetAwaiter().GetResult();
    shell.ConnectSelectedCommLinkAsync().GetAwaiter().GetResult();

    Require(linkManager.Links.Count == 1, "Expected one UDP Comm Link.");
    Require(linkManager.ActiveLink?.Configuration is UdpLinkConfiguration udp && udp.LocalPort == 14551, "Expected configured UDP port.");
    Require(heartbeat.IsRunning, "Expected heartbeat after Comm Link connect.");
    Require(shell.CommLinkStatus.Contains("Manual UDP", StringComparison.Ordinal), "Expected Comm Link status.");

    linkManager.DisconnectAllAsync().GetAwaiter().GetResult();
    heartbeat.StopAsync().GetAwaiter().GetResult();
}

static void RejectInvalidSelectedCommLinkPort()
{
    var store = new FakeSettingsStore();
    store.Current.LinkConfiguration.Links = [new PersistedLinkConfiguration("Bad UDP", LinkType.Udp, 0)];
    var logger = new AppLogger();
    var linkManager = new LinkManager(logger, new AppSettingsLinkConfigurationStore(store));
    var protocol = new MavlinkProtocol();
    protocol.Attach(linkManager);
    var heartbeat = new GcsHeartbeatService(linkManager, new MavlinkFrameWriter(), logger);
    var shell = new ShellViewModel(
        new AppLifecycleService(store, logger),
        new AppCloseCoordinator(),
        logger,
        linkManager,
        protocol,
        heartbeat,
        new MultiVehicleManager(protocol, logger),
        linkConfigurationStore: new AppSettingsLinkConfigurationStore(store));

    shell.EnsureCommLinksLoadedAsync().GetAwaiter().GetResult();
    shell.ConnectSelectedCommLinkAsync().GetAwaiter().GetResult();

    Require(linkManager.Links.Count == 0, "Expected invalid selected link to create no link.");
    Require(shell.CommLinkStatus.Contains("1-65535", StringComparison.Ordinal), "Expected validation status.");
    Require(!heartbeat.IsRunning, "Expected heartbeat not to start after failed Comm Link connect.");
}

static void ConnectSelectedSerialCommLinkFromShell()
{
    var store = new FakeSettingsStore();
    store.Current.LinkConfiguration.Links = [new PersistedLinkConfiguration("Manual Serial", LinkType.Serial, SerialPortName: "COM7", BaudRate: 57600)];
    var logger = new AppLogger();
    var linkManager = new LinkManager(logger, new AppSettingsLinkConfigurationStore(store));
    var protocol = new MavlinkProtocol();
    protocol.Attach(linkManager);
    var heartbeat = new GcsHeartbeatService(linkManager, new MavlinkFrameWriter(), logger);
    var adapter = new FakeSerialPortAdapter();
    var shell = new ShellViewModel(
        new AppLifecycleService(store, logger),
        new AppCloseCoordinator(),
        logger,
        linkManager,
        protocol,
        heartbeat,
        new MultiVehicleManager(protocol, logger),
        serialPortAdapterFactory: () => adapter,
        serialPortEnumerator: new FakeSerialPortEnumerator(["COM7"]),
        linkConfigurationStore: new AppSettingsLinkConfigurationStore(store));

    shell.EnsureCommLinksLoadedAsync().GetAwaiter().GetResult();
    shell.RefreshSerialPortsAsync().GetAwaiter().GetResult();
    shell.ConnectSelectedCommLinkAsync().GetAwaiter().GetResult();

    Require(shell.AvailableSerialPorts.SequenceEqual(["COM7"]), "Expected fake serial port to be listed.");
    Require(adapter.OpenedWith?.PortName == "COM7", "Expected selected serial port.");
    Require(adapter.OpenedWith?.BaudRate == 57600, "Expected selected serial baud.");
    Require(linkManager.ActiveLink?.Configuration.Type == LinkType.Serial, "Expected active serial link.");
    Require(heartbeat.IsRunning, "Expected heartbeat after serial Comm Link connect.");

    heartbeat.StopAsync().GetAwaiter().GetResult();
    linkManager.DisconnectAllAsync().GetAwaiter().GetResult();
}

static void EditLinkConfigurationViewModel()
{
    var settings = new FakeSettingsStore();
    settings.Current.LinkConfiguration = new LinkConfigurationState
    {
        PreferredActiveLinkName = "UDP",
        Links = [new PersistedLinkConfiguration("UDP", LinkType.Udp, 14550)]
    };
    var viewModel = new LinkConfigurationViewModel(new AppSettingsLinkConfigurationStore(settings));

    viewModel.LoadAsync().GetAwaiter().GetResult();
    Require(viewModel.Links.Count == 1, "Expected initial link row.");
    Require(viewModel.SelectedLink?.Name == "UDP", "Expected selected persisted row.");

    var backup = viewModel.Add(new PersistedLinkConfiguration("Mock Backup", LinkType.Mock));
    viewModel.SelectPreferred(backup);
    backup.Name = "Mock Preferred";
    viewModel.SaveAsync().GetAwaiter().GetResult();

    Require(settings.Current.LinkConfiguration.Links.Count == 2, "Expected two saved links.");
    Require(settings.Current.LinkConfiguration.PreferredActiveLinkName == "Mock Preferred", "Expected preferred saved link.");
    Require(settings.Current.LinkConfiguration.Links[1].Type == LinkType.Mock, "Expected saved mock link.");

    viewModel.DeleteSelected();
    viewModel.SaveAsync().GetAwaiter().GetResult();
    Require(settings.Current.LinkConfiguration.Links.Count == 1, "Expected delete to persist one link.");
}

static void ForwardMavlinkPacketsAcrossLinks()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var linkManager = new LinkManager(logger);
    var source = linkManager.CreateConnectedMockLinkAsync("Source").GetAwaiter().GetResult();
    var destination = linkManager.CreateConnectedMockLinkAsync("Destination").GetAwaiter().GetResult();
    byte[]? forwarded = null;
    destination.BytesSent += (_, args) => forwarded = args.Bytes;

    using var forwarding = new MavlinkForwardingService(linkManager);
    forwarding.Configure(["Source", "Destination"], true);

    source.EmitIncoming([1, 2, 3, 4]);
    SpinWait.SpinUntil(() => forwarded is not null, TimeSpan.FromSeconds(1));

    Require(forwarded?.SequenceEqual(new byte[] { 1, 2, 3, 4 }) == true, "Expected packet forwarded to destination.");
    Require(forwarding.ForwardedPackets == 1, "Expected forwarded packet count.");
    Require(forwarding.ForwardedBytes == 4, "Expected forwarded byte count.");
}

static void ModelBluetoothLinkBoundary()
{
    var platform = new UnavailableBluetoothLinkPlatform("No Bluetooth adapter bound.");
    var devices = platform.DiscoverAsync().GetAwaiter().GetResult();

    Require(platform.Capabilities.Desktop == BluetoothPlatformSupport.Unsupported, "Expected desktop unsupported state.");
    Require(platform.Capabilities.Android == BluetoothPlatformSupport.Unsupported, "Expected Android unsupported state.");
    Require(devices.Count == 0, "Expected no devices from unavailable boundary.");

    var descriptor = new BluetoothDeviceDescriptor("bt-1", "Telemetry Radio", "00:11:22:33:44:55", true, true);
    Require(descriptor.CanConnect && descriptor.IsPaired, "Expected Bluetooth descriptor to model paired connectable device.");
}

static void ModelAndroidUsbSerialLifecycle()
{
    var platform = new FakeAndroidUsbSerialPlatform(
        [new AndroidUsbSerialDevice("usb-1", "PX4 USB", 0x26AC, 0x0011, false)]);
    var runtime = new AndroidUsbSerialRuntime(platform);

    runtime.DiscoverAsync().GetAwaiter().GetResult();
    Require(runtime.State == AndroidUsbSerialState.PermissionRequired, "Expected permission required after discovery.");
    Require(runtime.ConnectAsync().GetAwaiter().GetResult() == false, "Expected connect blocked before permission.");

    Require(runtime.RequestPermissionAsync().GetAwaiter().GetResult(), "Expected permission grant.");
    Require(runtime.ConnectAsync().GetAwaiter().GetResult(), "Expected USB serial connect.");
    Require(runtime.Capture().State == AndroidUsbSerialState.Connected, "Expected connected snapshot.");

    runtime.DisconnectAsync().GetAwaiter().GetResult();
    Require(runtime.State == AndroidUsbSerialState.Disconnected, "Expected disconnected state.");

    platform.FailConnect = true;
    runtime.ConnectAsync().GetAwaiter().GetResult();
    Require(runtime.State == AndroidUsbSerialState.Failed, "Expected failed state after platform exception.");
}

static void ProjectLinkErrorRecovery()
{
    var projector = new LinkErrorRecoveryProjector(new LinkErrorRecoveryPolicy(2, TimeSpan.FromMilliseconds(50)));
    var failed = new LinkDiagnosticsSnapshot(
        "Telemetry",
        LinkType.Mock,
        "Disconnected",
        false,
        false,
        0,
        0,
        null,
        null,
        "Port closed",
        "Not configured");

    var first = projector.Project(failed);
    var second = projector.Project(failed);
    var third = projector.Project(failed);

    Require(first.State == LinkRecoveryState.Failed, "Expected first failure projection.");
    Require(second.RetryAfter == TimeSpan.FromMilliseconds(100), "Expected second retry backoff.");
    Require(third.State == LinkRecoveryState.ManualInterventionRequired, "Expected manual intervention after retries.");
}

static void CatalogLinkRuntimeEvidence()
{
    var checklist = LinkRuntimeEvidenceCatalog.CreateV143Checklist();

    Require(checklist.Count == 3, "Expected v1.43 evidence checklist entries.");
    Require(checklist.Any(static item => item.Name.Contains("Desktop", StringComparison.Ordinal)), "Expected desktop workflow evidence.");
    Require(checklist.Any(static item => item.Name.Contains("Android", StringComparison.Ordinal)), "Expected Android checklist evidence.");
    Require(checklist.All(static item => item.IsComplete), "Expected checklist entries complete for shared-core boundary.");
}

static void TcpClientLinkExchangesBytes()
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var endpoint = (IPEndPoint)listener.LocalEndpoint;
    var serverReceived = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
    var serverTask = Task.Run(async () =>
    {
        using var client = await listener.AcceptTcpClientAsync();
        await using var stream = client.GetStream();
        var buffer = new byte[3];
        var read = await stream.ReadAsync(buffer);
        serverReceived.SetResult(buffer[..read]);
        await stream.WriteAsync(new byte[] { 9, 8, 7 });
        await stream.FlushAsync();
    });

    var logger = new VGC.Core.Logging.AppLogger();
    var manager = new LinkManager(logger);
    var received = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
    var sent = new List<byte[]>();
    var link = manager.CreateConnectedTcpClientLinkAsync(
        new TcpLinkConfiguration("TCP Client", IPAddress.Loopback.ToString(), endpoint.Port)).GetAwaiter().GetResult();
    link.BytesReceived += (_, args) => received.TrySetResult(args.Bytes);
    link.BytesSent += (_, args) => sent.Add(args.Bytes);

    Require(link.IsConnected, "Expected TCP client to connect.");
    Require(link.CanSend, "Expected TCP client to send.");
    Require(manager.ActiveLink == link, "Expected TCP client to be active link.");

    link.WriteAsync(new byte[] { 1, 2, 3 }).GetAwaiter().GetResult();

    var serverBytes = WaitForResult(serverReceived.Task, "Expected server to receive TCP bytes.");
    var clientBytes = WaitForResult(received.Task, "Expected client to receive TCP bytes.");

    Require(serverBytes.SequenceEqual(new byte[] { 1, 2, 3 }), "Expected server to receive client bytes.");
    Require(clientBytes.SequenceEqual(new byte[] { 9, 8, 7 }), "Expected client to receive server bytes.");
    Require(sent.Count == 1 && sent[0].SequenceEqual(new byte[] { 1, 2, 3 }), "Expected BytesSent event.");

    manager.DisconnectAllAsync().GetAwaiter().GetResult();
    listener.Stop();
    serverTask.GetAwaiter().GetResult();
}

static void TcpServerLinkAcceptsClientBytes()
{
    var server = new TcpServerLinkTransport(new TcpLinkConfiguration("TCP Server", IPAddress.Loopback.ToString(), 0, isServer: true));
    var received = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
    var sent = new List<byte[]>();
    server.BytesReceived += (_, args) => received.TrySetResult(args.Bytes);
    server.BytesSent += (_, args) => sent.Add(args.Bytes);
    server.ConnectAsync().GetAwaiter().GetResult();

    using var client = new TcpClient();
    client.Connect(IPAddress.Loopback, server.BoundPort);
    using var stream = client.GetStream();

    stream.Write(new byte[] { 4, 5, 6 });
    stream.Flush();
    var serverBytes = WaitForResult(received.Task, "Expected server to receive TCP client bytes.");

    server.WriteAsync(new byte[] { 7, 8, 9 }).GetAwaiter().GetResult();
    var buffer = new byte[3];
    var read = stream.Read(buffer);

    Require(server.IsConnected, "Expected TCP server to remain listening.");
    Require(server.CanSend, "Expected TCP server to send after accepting client.");
    Require(serverBytes.SequenceEqual(new byte[] { 4, 5, 6 }), "Expected TCP server receive bytes.");
    Require(buffer[..read].SequenceEqual(new byte[] { 7, 8, 9 }), "Expected TCP client receive server bytes.");
    Require(sent.Count == 1 && sent[0].SequenceEqual(new byte[] { 7, 8, 9 }), "Expected server BytesSent event.");

    server.DisconnectAsync().GetAwaiter().GetResult();
}

static void SerialLinkRoutesBytesThroughAdapter()
{
    var adapter = new FakeSerialPortAdapter();
    var transport = new SerialLinkTransport(new SerialLinkConfiguration("Serial", "COM9", 115200), adapter);
    var received = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
    var sent = new List<byte[]>();
    transport.BytesReceived += (_, args) => received.TrySetResult(args.Bytes);
    transport.BytesSent += (_, args) => sent.Add(args.Bytes);

    transport.ConnectAsync().GetAwaiter().GetResult();
    Require(transport.IsConnected, "Expected serial transport to connect.");
    Require(transport.CanSend, "Expected serial transport to be send-capable.");
    Require(adapter.OpenedWith?.PortName == "COM9", "Expected adapter to receive serial configuration.");

    transport.WriteAsync(new byte[] { 1, 2, 3 }).GetAwaiter().GetResult();
    adapter.EmitIncoming([4, 5, 6]);
    var receivedBytes = WaitForResult(received.Task, "Expected serial incoming bytes.");

    Require(adapter.Written.Count == 1 && adapter.Written[0].SequenceEqual(new byte[] { 1, 2, 3 }), "Expected adapter write bytes.");
    Require(sent.Count == 1 && sent[0].SequenceEqual(new byte[] { 1, 2, 3 }), "Expected serial BytesSent event.");
    Require(receivedBytes.SequenceEqual(new byte[] { 4, 5, 6 }), "Expected serial BytesReceived event.");

    transport.DisconnectAsync().GetAwaiter().GetResult();
    Require(!transport.IsConnected, "Expected serial transport to disconnect.");
}

static void SerialLinkValidatesConfiguration()
{
    var adapter = new FakeSerialPortAdapter();

    RequireThrows<ArgumentException>(
        () => new SerialLinkTransport(new SerialLinkConfiguration("Serial", "", 115200), adapter),
        "Expected empty serial port to be rejected.");
    RequireThrows<ArgumentOutOfRangeException>(
        () => new SerialLinkTransport(new SerialLinkConfiguration("Serial", "COM9", 0), adapter),
        "Expected invalid baud rate to be rejected.");
    RequireThrows<ArgumentException>(
        () => new SerialLinkTransport(new SerialLinkConfiguration("Serial", "COM9", 115200, parity: "Bad"), adapter),
        "Expected invalid parity to be rejected.");
}

static void ScaleLogReplayTiming()
{
    var policy = new DefaultLogReplayTimingPolicy();

    Require(policy.ScaleDelay(TimeSpan.FromSeconds(2), 2.0) == TimeSpan.FromSeconds(1), "Expected 2x replay to halve delay.");
    Require(policy.ScaleDelay(TimeSpan.FromSeconds(2), 0.5) == TimeSpan.FromSeconds(4), "Expected 0.5x replay to double delay.");
}

static void ReplayLogPacketsInTimestampOrder()
{
    var configuration = new LogReplayLinkConfiguration("Replay", "memory.tlog", speed: 10);
    var replay = new LogReplayLinkTransport(
        configuration,
        new InMemoryLogReplaySource([
            new LogReplayPacket(TimeSpan.FromSeconds(2), [2]),
            new LogReplayPacket(TimeSpan.FromSeconds(1), [1])
        ]),
        delayScheduler: new ImmediateLogReplayDelayScheduler());
    var received = new List<byte>();
    replay.BytesReceived += (_, args) => received.Add(args.Bytes[0]);

    replay.ConnectAsync().GetAwaiter().GetResult();
    replay.ReplayOnceAsync().GetAwaiter().GetResult();

    Require(!replay.CanSend, "Expected log replay to be read-only.");
    Require(received.SequenceEqual(new byte[] { 1, 2 }), "Expected replay packets ordered by timestamp.");
}

static void ParseQgcTelemetryLogReplaySource()
{
    var firstFrame = MavlinkTestFrames.HeartbeatV1(systemId: 1, componentId: 1);
    var secondFrame = MavlinkTestFrames.HeartbeatV1(systemId: 2, componentId: 1);
    var path = Path.Combine(Path.GetTempPath(), $"vgc-replay-{Guid.NewGuid():N}.tlog");

    try
    {
        File.WriteAllBytes(path, BuildQgcTelemetryLog(
            (1_000_000UL, firstFrame),
            (1_250_000UL, secondFrame)));

        var source = new TelemetryLogReplaySource(path);
        var packets = source.ReadPacketsAsync().GetAwaiter().GetResult();
        Require(packets.Count == 2, "Expected two replay packets from telemetry log.");
        Require(packets[0].Timestamp == TimeSpan.Zero, "Expected first replay timestamp to be normalized to zero.");
        Require(packets[1].Timestamp == TimeSpan.FromMilliseconds(250), "Expected relative replay timestamp.");
        Require(packets[0].Bytes.SequenceEqual(firstFrame), "Expected first replay frame bytes.");
        Require(packets[1].Bytes.SequenceEqual(secondFrame), "Expected second replay frame bytes.");

        var replay = new LogReplayLinkTransport(
            new LogReplayLinkConfiguration("Replay", path),
            source,
            delayScheduler: new ImmediateLogReplayDelayScheduler());
        var emitted = new List<byte[]>();
        replay.BytesReceived += (_, args) => emitted.Add(args.Bytes);

        replay.ConnectAsync().GetAwaiter().GetResult();
        replay.ReplayOnceAsync().GetAwaiter().GetResult();

        Require(emitted.Count == 2, "Expected replay transport to emit parsed log frames.");
        Require(emitted[0].SequenceEqual(firstFrame), "Expected replay transport first frame.");
        Require(emitted[1].SequenceEqual(secondFrame), "Expected replay transport second frame.");
    }
    finally
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

static void ParseDurableSyntheticReplayFixture()
{
    var path = FindFixturePath(Path.Combine("replay", "synthetic-heartbeat-minimal.tlog"));
    var metadata = JsonDocument.Parse(ReadFixture(Path.Combine("replay", "synthetic-heartbeat-minimal.json")));
    var source = new TelemetryLogReplaySource(path);
    var packets = source.ReadPacketsAsync().GetAwaiter().GetResult();

    Require(packets.Count == 2, "Expected two packets from durable synthetic replay fixture.");
    Require(packets[0].Timestamp == TimeSpan.Zero, "Expected first durable fixture packet to start at zero.");
    Require(packets[1].Timestamp == TimeSpan.FromMilliseconds(250), "Expected durable fixture timestamp delta.");

    var parser = new MavlinkFrameParser();
    var firstFrames = parser.Parse(packets[0].Bytes);
    var secondFrames = parser.Parse(packets[1].Bytes);
    Require(firstFrames.Count == 1, "Expected first durable fixture packet to contain one MAVLink frame.");
    Require(secondFrames.Count == 1, "Expected second durable fixture packet to contain one MAVLink frame.");
    Require(firstFrames[0].Version == 1, "Expected durable fixture to use MAVLink v1.");
    Require(firstFrames[0].MessageId == 0, "Expected first durable fixture packet to be HEARTBEAT.");
    Require(secondFrames[0].MessageId == 0, "Expected second durable fixture packet to be HEARTBEAT.");
    Require(firstFrames[0].SystemId == 9, "Expected first durable fixture heartbeat system id.");
    Require(secondFrames[0].SystemId == 10, "Expected second durable fixture heartbeat system id.");

    var root = metadata.RootElement;
    Require(root.GetProperty("file").GetString() == "synthetic-heartbeat-minimal.tlog", "Expected metadata to identify fixture file.");
    Require(root.GetProperty("source").GetString() == "Synthetic VGC-generated MAVLink frames", "Expected synthetic source metadata.");
    Require(root.GetProperty("privacy").GetString() == "No real flight, vehicle, operator, or location data", "Expected privacy metadata.");
    Require(root.GetProperty("messages")[0].GetProperty("name").GetString() == "HEARTBEAT", "Expected HEARTBEAT metadata.");
}

static void ControlTLogReplayPlaybackSession()
{
    var session = new ReplayPlaybackSession();
    var source = new InMemoryLogReplaySource([
        new LogReplayPacket(TimeSpan.FromSeconds(2), [2]),
        new LogReplayPacket(TimeSpan.FromSeconds(1), [1]),
        new LogReplayPacket(TimeSpan.FromSeconds(5), [5])
    ]);

    session.OpenAsync(source).GetAwaiter().GetResult();
    Require(session.Snapshot.State == ReplayPlaybackState.Ready, "Expected replay to be ready.");
    Require(session.Snapshot.PacketCount == 3, "Expected packet count.");
    Require(session.Snapshot.Duration == TimeSpan.FromSeconds(5), "Expected replay duration.");

    session.SetSpeed(2);
    Require(Math.Abs(session.Snapshot.Speed - 2) < 0.000001, "Expected replay speed.");

    session.Seek(TimeSpan.FromSeconds(3));
    Require(session.Snapshot.PacketIndex == 2, "Expected seek to select next packet.");
    Require(session.Snapshot.State == ReplayPlaybackState.Ready, "Expected seek to keep ready state.");

    session.Play();
    Require(session.Snapshot.State == ReplayPlaybackState.Playing, "Expected replay playing state.");

    var packet = session.AdvanceToNextPacket();
    Require(packet is not null && packet.Bytes[0] == 5, "Expected next packet after seek.");
    Require(session.Snapshot.State == ReplayPlaybackState.Ended, "Expected replay ended after final packet.");

    session.Seek(TimeSpan.FromSeconds(1));
    Require(session.Snapshot.State == ReplayPlaybackState.Paused, "Expected seek from ended to pause at valid packet.");
    session.Play();
    packet = session.AdvanceToNextPacket();
    Require(packet is not null && packet.Bytes[0] == 1, "Expected replay restart from seek position.");

    session.Pause();
    Require(session.Snapshot.State == ReplayPlaybackState.Paused, "Expected replay paused state.");

    RequireThrows<ArgumentOutOfRangeException>(() => session.SetSpeed(0), "Expected invalid speed rejection.");
}

static void BuildReplayTimelineAndPacketIndex()
{
    var parser = new QgcTelemetryLogReplayParser();
    var packets = parser.Parse(BuildQgcTelemetryLog(
        (1_000_000UL, MavlinkTestFrames.HeartbeatV1(systemId: 9, componentId: 1)),
        (1_500_000UL, MavlinkTestFrames.StatusText(systemId: 9, componentId: 42, severity: MavlinkSeverity.Warning, text: "Battery low")),
        (4_500_000UL, MavlinkTestFrames.ParamValue(systemId: 9, componentId: 1, name: "BAT_LOW_THR", value: 14.2f))));

    var timeline = new ReplayTimelineBuilder(gapThreshold: TimeSpan.FromSeconds(1)).Build(packets);

    Require(timeline.PacketCount == 3, "Expected timeline packet count.");
    Require(timeline.Duration == TimeSpan.FromSeconds(3.5), "Expected timeline duration.");
    Require(Math.Abs(timeline.AverageRateHz - (3 / 3.5)) < 0.000001, "Expected average packet rate.");
    Require(timeline.Packets[0] is { Sequence: 0, Timestamp: var timestamp } && timestamp == TimeSpan.Zero, "Expected first packet index.");
    Require(timeline.Packets[0].MessageName == "HEARTBEAT", "Expected decoded heartbeat row.");
    Require(timeline.Packets[1].MessageId == 253, "Expected STATUSTEXT message id.");
    Require(timeline.Packets[1].FieldSummary == "Battery low", "Expected STATUSTEXT summary.");
    Require(timeline.Packets[2].MessageName == "PARAM_VALUE", "Expected PARAM_VALUE message.");
    Require(timeline.MessageRates.Any(static row => row.MessageId == 0 && row.Count == 1), "Expected heartbeat rate row.");
    Require(timeline.MessageRates.Any(static row => row.MessageId == 253 && row.MessageName == "STATUSTEXT"), "Expected STATUSTEXT rate row.");
    Require(timeline.Gaps.Count == 1, "Expected one replay gap.");
    Require(timeline.Gaps[0].Duration == TimeSpan.FromSeconds(3), "Expected gap duration.");
}

static void AnalyzeViewExposesReplayPlaybackControls()
{
    var protocol = new MavlinkProtocol();
    var analyze = new AnalyzeViewModel(protocol);
    var source = new InMemoryLogReplaySource([
        new LogReplayPacket(TimeSpan.Zero, [1]),
        new LogReplayPacket(TimeSpan.FromSeconds(1), [2])
    ]);

    analyze.OpenReplayAsync(source).GetAwaiter().GetResult();
    Require(analyze.Replay.State == ReplayPlaybackState.Ready, "Expected Analyze replay ready state.");
    Require(analyze.ReplayProgressText.Contains("0/2"), "Expected Analyze replay progress.");

    analyze.PlayReplay();
    Require(analyze.Replay.State == ReplayPlaybackState.Playing, "Expected Analyze replay playing state.");

    var packet = analyze.AdvanceReplayToNextPacket();
    Require(packet is not null && packet.Bytes[0] == 1, "Expected first Analyze replay packet.");

    analyze.SetReplaySpeed(2);
    Require(Math.Abs(analyze.Replay.Speed - 2) < 0.000001, "Expected Analyze replay speed.");

    analyze.PauseReplay();
    Require(analyze.Replay.State == ReplayPlaybackState.Paused, "Expected Analyze replay paused state.");
}

static void AnalyzeViewExposesReplayTimelineProjection()
{
    var protocol = new MavlinkProtocol();
    var analyze = new AnalyzeViewModel(protocol);
    var source = new InMemoryLogReplaySource([
        new LogReplayPacket(TimeSpan.Zero, MavlinkTestFrames.HeartbeatV1(systemId: 9, componentId: 1)),
        new LogReplayPacket(TimeSpan.FromSeconds(1), MavlinkTestFrames.StatusText(systemId: 9, componentId: 42, text: "Ready")),
        new LogReplayPacket(TimeSpan.FromSeconds(4), MavlinkTestFrames.ParamValue(systemId: 9, componentId: 1, name: "SYS_ID", value: 1))
    ]);

    analyze.OpenReplayAsync(source).GetAwaiter().GetResult();

    Require(analyze.ReplayPacketRows.Count == 3, "Expected Analyze packet index rows.");
    Require(analyze.ReplayPacketRows[0].MessageName == "HEARTBEAT", "Expected Analyze timeline decode.");
    Require(analyze.ReplayMessageRates.Count == 3, "Expected Analyze message rate rows.");
    Require(analyze.ReplayGaps.Count == 1, "Expected Analyze gap rows.");
    Require(analyze.ReplayTimelineSummary.Contains("3 packets"), "Expected Analyze timeline summary.");
}

static void AnalyzeViewExposesReplayWorkflowState()
{
    var protocol = new MavlinkProtocol();
    var analyze = new AnalyzeViewModel(protocol);
    var source = new InMemoryLogReplaySource([
        new LogReplayPacket(TimeSpan.Zero, MavlinkTestFrames.HeartbeatV1(systemId: 9, componentId: 1)),
        new LogReplayPacket(TimeSpan.FromSeconds(1), MavlinkTestFrames.StatusText(systemId: 9, componentId: 42, text: "Ready"))
    ]);

    Require(analyze.ReplayWorkflowState.CanOpen, "Expected replay open workflow to be available.");
    Require(!analyze.ReplayWorkflowState.CanPlay, "Expected closed replay not to be playable.");

    analyze.OpenReplayAsync(source).GetAwaiter().GetResult();
    var ready = analyze.ReplayWorkflowState;
    Require(ready.CanPlay, "Expected ready replay to be playable.");
    Require(ready.CanSeek, "Expected ready replay to be seekable.");
    Require(ready.CanFilter, "Expected Analyze filter workflow.");
    Require(ready.CanShowDetail, "Expected selected replay packet detail after open.");
    Require(ready.SelectedDetailText.Contains("HEARTBEAT"), "Expected replay detail to include decoded message.");

    analyze.PlayReplay();
    Require(analyze.ReplayWorkflowState.CanPause, "Expected playing replay to be pausable.");
    Require(analyze.ReplayWorkflowState.CanStep, "Expected playing replay to be steppable.");

    analyze.FilterText = "name:STATUSTEXT";
    Require(analyze.ReplayWorkflowState.FilterText == "name:STATUSTEXT", "Expected workflow filter text.");

    analyze.SelectedReplayPacketRow = null;
    analyze.SelectedInspectorRow = new MavlinkInspectorRow(9, 42, 253, "STATUSTEXT", "Ready", "Info", "Ready", 1, 0, DateTimeOffset.Now);
    Require(analyze.ReplayWorkflowState.SelectedDetailText.Contains("Ready"), "Expected inspector detail text.");
}

static void ManageTelemetryChartRuntime()
{
    var chart = new TelemetryChartRuntime();

    chart.AddSeries("ALT", "m");
    chart.AddDataPoint("ALT", 1, 10);
    chart.AddDataPoint("ALT", 2, 12);

    var snapshot = chart.Snapshot;
    Require(snapshot.Series.Count == 1, "Expected one telemetry series.");
    Require(snapshot.Series[0].DataPoints.Count == 2, "Expected telemetry samples.");
    Require(snapshot.StatusText == "1 series, 2 data points", "Expected telemetry chart status.");

    snapshot = chart.Clear();
    Require(snapshot.Series.Count == 0, "Expected cleared telemetry series.");
    Require(snapshot.StatusText == "0 series, 0 data points", "Expected cleared telemetry chart status.");
}

static void AnalyzeViewExposesTelemetryChartSnapshot()
{
    var protocol = new MavlinkProtocol();
    var analyze = new AnalyzeViewModel(protocol);

    analyze.AddChartSeries("ALT", "m");
    analyze.AddChartDataPoint("ALT", 1, 10);

    Require(analyze.ChartSnapshot.Series.Count == 6, "Expected Analyze chart series.");
    Require(analyze.ChartSnapshot.Series.Single(static series => series.Name == "ALT").DataPoints.Count == 1, "Expected Analyze chart data point.");
    Require(analyze.ChartSnapshot.StatusText == "6 series, 1 data points", "Expected Analyze chart status.");

    analyze.ClearChart();
    Require(analyze.ChartSnapshot.Series.Count == 5, "Expected Analyze chart defaults after clear.");
    Require(analyze.ChartSnapshot.Series.All(static series => series.DataPoints.Count == 0), "Expected Analyze chart clear.");
}

static void AnalyzeViewFeedsTelemetryChartFromMavlink()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var linkManager = new LinkManager(logger);
    var protocol = new MavlinkProtocol();
    var analyze = new AnalyzeViewModel(protocol);
    protocol.Attach(linkManager);

    var link = linkManager.CreateConnectedMockLinkAsync().GetAwaiter().GetResult();
    link.EmitIncoming(MavlinkTestFrames.Attitude(roll: 0.1f, pitch: 0.2f, yaw: 0.3f));
    link.EmitIncoming(MavlinkTestFrames.GlobalPositionInt(relativeAltitudeMeters: 12.3));

    var snapshot = analyze.ChartSnapshot;
    Require(snapshot.Series.Single(static series => series.Name == "Roll").DataPoints.Count == 1, "Expected roll chart sample.");
    Require(snapshot.Series.Single(static series => series.Name == "Pitch").DataPoints.Count == 1, "Expected pitch chart sample.");
    Require(snapshot.Series.Single(static series => series.Name == "Heading").DataPoints.Count == 1, "Expected heading chart sample.");
    Require(snapshot.Series.Single(static series => series.Name == "Altitude").DataPoints.Single().Value == 12.3, "Expected altitude chart sample.");
}

static void AnalyzeViewSummarizesDurableReplayDiagnostics()
{
    var protocol = new MavlinkProtocol();
    var analyze = new AnalyzeViewModel(protocol);
    var path = FindFixturePath(Path.Combine("replay", "synthetic-heartbeat-minimal.tlog"));
    var source = new TelemetryLogReplaySource(path);

    analyze.OpenReplayAsync(source).GetAwaiter().GetResult();

    var diagnostic = analyze.DiagnosticSummary;
    Require(diagnostic.ReplayPacketCount > 0, "Expected durable replay packets.");
    Require(diagnostic.ReplayMessageTypeCount > 0, "Expected replay message type count.");
    Require(diagnostic.TopReplayMessage.Contains("HEARTBEAT"), "Expected heartbeat top replay message.");
    Require(diagnostic.SummaryText.Contains("Replay"), "Expected replay diagnostic summary text.");
    Require(analyze.ReplayWorkflowState.SelectedDetailText.Contains("HEARTBEAT"), "Expected durable replay selected detail.");
}

static void SelectULogAndDataFlashParserBoundaries()
{
    var catalog = new FlightLogParserCatalog();
    var ulogHeader = new byte[] { 0x55, 0x4c, 0x6f, 0x67, 0x01, 0x12, 0x35, 0x00 };
    var dataFlashHeader = new byte[] { 0xa3, 0x95, 0x80 };
    var unknown = new byte[] { 0x01, 0x02, 0x03 };

    var ulogParser = catalog.SelectParser(ulogHeader);
    Require(ulogParser is not null && ulogParser.Format == FlightLogFormat.Px4ULog, "Expected ULog parser selection.");
    var ulogResult = catalog.Parse(ulogHeader);
    Require(!ulogResult.Success, "Expected boundary ULog parser to report unsupported.");
    Require(ulogResult.Diagnostics.Any(static diagnostic => diagnostic.Code == "Px4ULog.Unsupported"), "Expected ULog unsupported diagnostic.");

    var dataFlashParser = catalog.SelectParser(dataFlashHeader);
    Require(dataFlashParser is not null && dataFlashParser.Format == FlightLogFormat.ArduPilotDataFlash, "Expected DataFlash parser selection.");
    var dataFlashResult = catalog.Parse(dataFlashHeader);
    Require(!dataFlashResult.Success, "Expected boundary DataFlash parser to report unsupported.");
    Require(dataFlashResult.Diagnostics.Any(static diagnostic => diagnostic.Code == "ArduPilotDataFlash.Unsupported"), "Expected DataFlash unsupported diagnostic.");

    Require(catalog.SelectParser(unknown) is null, "Expected no parser for unknown bytes.");
    var unknownResult = catalog.Parse(unknown);
    Require(unknownResult.Diagnostics.Any(static diagnostic => diagnostic.Code == "Unknown.Unsupported"), "Expected unknown unsupported diagnostic.");
}

static void ProjectParserDiagnosticsIntoFlightLogSummary()
{
    var diagnostics = new[]
    {
        new FlightLogDiagnostic(FlightLogDiagnosticSeverity.Warning, "Fixture.Source", "Synthetic fixture only")
    };
    var parsed = new ParsedFlightLog(
        FlightLogFormat.Px4ULog,
        TimeSpan.FromSeconds(12),
        [new FlightLogMessageDefinition("vehicle_attitude", ["roll", "pitch", "yaw"], 50)],
        [new FlightLogSeriesSample("vehicle_attitude", TimeSpan.FromSeconds(1), new Dictionary<string, double> { ["roll"] = 0.1 })],
        [new KeyValuePair<string, string>("SYS_AUTOSTART", "4001")],
        [new FlightLogEvent(TimeSpan.FromSeconds(2), "info", "armed")],
        diagnostics);

    var success = new FlightLogParserResult(true, parsed, diagnostics);
    var summary = FlightLogSummaryProjection.FromResult(success);
    Require(summary.Format == FlightLogFormat.Px4ULog, "Expected summary format.");
    Require(summary.MessageCount == 1, "Expected summary message count.");
    Require(summary.SampleCount == 1, "Expected summary sample count.");
    Require(summary.ParameterCount == 1, "Expected summary parameter count.");
    Require(summary.Duration == TimeSpan.FromSeconds(12), "Expected summary duration.");

    var failure = FlightLogParserResult.Unsupported(FlightLogFormat.ArduPilotDataFlash, "DataFlash parser not available.");
    var failureSummary = FlightLogSummaryProjection.FromResult(failure);
    Require(failureSummary.Format == FlightLogFormat.Unknown, "Expected unsupported summary format.");
    Require(failureSummary.StatusText.Contains("not available"), "Expected unsupported status text.");
}

static void ModelFlightLogDownloadWorkflow()
{
    var workflow = new FlightLogDownloadWorkflow();

    var listAction = workflow.RequestList();
    Require(listAction.Type == FlightLogDownloadActionType.RequestList, "Expected list request action.");
    Require(workflow.Snapshot.State == FlightLogDownloadState.Listing, "Expected listing state.");

    var logs = new[]
    {
        new FlightLogDescriptor(1, "older", new DateTimeOffset(2026, 6, 24, 12, 0, 0, TimeSpan.Zero), 10, FlightLogFormat.Px4ULog),
        new FlightLogDescriptor(2, "latest", new DateTimeOffset(2026, 6, 25, 12, 0, 0, TimeSpan.Zero), 20, FlightLogFormat.ArduPilotDataFlash)
    };
    workflow.ApplyList(logs);
    Require(workflow.Snapshot.State == FlightLogDownloadState.ListReady, "Expected list ready state.");
    Require(workflow.Snapshot.Logs[0].Id == 2, "Expected logs ordered newest first.");

    var download = workflow.RequestDownload(2, overwriteExisting: true);
    Require(download.Type == FlightLogDownloadActionType.RequestDownload, "Expected download action.");
    var storageRequest = download.StorageRequest ?? throw new InvalidOperationException("Expected storage request.");
    Require(storageRequest.SuggestedFileName == "latest.bin", "Expected DataFlash filename.");
    Require(storageRequest.OverwriteExisting, "Expected overwrite flag.");
    Require(workflow.Snapshot.State == FlightLogDownloadState.Downloading, "Expected downloading state.");

    workflow.ApplyProgress(5);
    Require(Math.Abs(workflow.Snapshot.Progress - 0.25) < 0.000001, "Expected download progress.");
    workflow.Complete(FlightLogStorageResult.Stored("logs/latest.bin"));
    Require(workflow.Snapshot.State == FlightLogDownloadState.Completed, "Expected completed download.");
    Require(workflow.Snapshot.StoredPath == "logs/latest.bin", "Expected stored path.");
}

static void ModelFlightLogDownloadCancellationAndRetry()
{
    var workflow = new FlightLogDownloadWorkflow(maxRetryCount: 2);
    workflow.RequestList();
    workflow.ApplyList([
        new FlightLogDescriptor(3, "flight", DateTimeOffset.UtcNow, 100, FlightLogFormat.Px4ULog)
    ]);

    workflow.RequestDownload(3);
    var cancel = workflow.Cancel();
    Require(cancel.Type == FlightLogDownloadActionType.CancelDownload, "Expected cancel action.");
    Require(workflow.Snapshot.State == FlightLogDownloadState.Cancelled, "Expected cancelled state.");

    workflow.RequestDownload(3);
    workflow.Fail("timeout");
    Require(workflow.Snapshot.CanRetry, "Expected retry to be available.");
    var retry = workflow.Retry();
    Require(retry.Type == FlightLogDownloadActionType.RetryDownload, "Expected retry action.");
    Require(workflow.Snapshot.State == FlightLogDownloadState.Downloading, "Expected retry downloading state.");
    Require(workflow.Snapshot.RetryCount == 1, "Expected retry count.");

    workflow.Fail("timeout again");
    workflow.Retry();
    workflow.Fail("final timeout");
    Require(!workflow.Snapshot.CanRetry, "Expected retry limit to be reached.");
}

static void MatchGeoTagImagesToLogTrackPoints()
{
    var matcher = new GeoTagMatcher();
    var baseTime = new DateTimeOffset(2026, 6, 25, 12, 0, 0, TimeSpan.Zero);
    var images = new[]
    {
        new GeoTagImageDescriptor("exact.jpg", baseTime),
        new GeoTagImageDescriptor("near.jpg", baseTime.AddMilliseconds(500)),
        new GeoTagImageDescriptor("far.jpg", baseTime.AddSeconds(10))
    };
    var track = new[]
    {
        new GeoTagTrackPoint(baseTime, new MapCoordinate(47.0, 8.0, 500)),
        new GeoTagTrackPoint(baseTime.AddSeconds(1), new MapCoordinate(47.1, 8.1, 510))
    };

    var summary = matcher.Match(images, track, new GeoTagMatchPolicy(TimeSpan.FromSeconds(2), TimeSpan.Zero));

    Require(summary.ImageCount == 3, "Expected geotag image count.");
    Require(summary.MatchedCount == 2, "Expected matched count.");
    Require(summary.UnmatchedCount == 1, "Expected unmatched count.");
    Require(summary.Results[0].State == GeoTagMatchState.Exact, "Expected exact geotag match.");
    Require(summary.Results[1].State == GeoTagMatchState.Nearest, "Expected nearest geotag match.");
    Require(summary.Results[1].Delta == TimeSpan.FromMilliseconds(500), "Expected nearest delta.");
    Require(summary.Results[2].State == GeoTagMatchState.Unmatched, "Expected unmatched geotag result.");
}

static void ApplyGeoTagOffsetToleranceAndDeterministicTies()
{
    var matcher = new GeoTagMatcher();
    var baseTime = new DateTimeOffset(2026, 6, 25, 12, 0, 0, TimeSpan.Zero);
    var images = new[]
    {
        new GeoTagImageDescriptor("offset.jpg", baseTime),
        new GeoTagImageDescriptor("tie.jpg", baseTime.AddSeconds(5))
    };
    var track = new[]
    {
        new GeoTagTrackPoint(baseTime.AddSeconds(2), new MapCoordinate(47.2, 8.2)),
        new GeoTagTrackPoint(baseTime.AddSeconds(4), new MapCoordinate(47.4, 8.4)),
        new GeoTagTrackPoint(baseTime.AddSeconds(6), new MapCoordinate(47.6, 8.6))
    };

    var offsetSummary = matcher.Match(
        images.Take(1),
        track,
        new GeoTagMatchPolicy(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(2)));
    Require(offsetSummary.Results[0].State == GeoTagMatchState.Exact, "Expected offset exact match.");
    Require(Math.Abs(offsetSummary.Results[0].TrackPoint!.Coordinate.Latitude - 47.2) < 0.000001, "Expected offset coordinate.");

    var tieSummary = matcher.Match(
        images.Skip(1),
        track,
        new GeoTagMatchPolicy(TimeSpan.FromSeconds(2), TimeSpan.Zero));
    Require(tieSummary.Results[0].State == GeoTagMatchState.Nearest, "Expected tie nearest match.");
    Require(Math.Abs(tieSummary.Results[0].TrackPoint!.Coordinate.Latitude - 47.4) < 0.000001, "Expected earlier tie coordinate.");

    var unmatched = matcher.Match(
        images,
        [],
        GeoTagMatchPolicy.Default);
    Require(unmatched.UnmatchedCount == 2, "Expected unmatched without track points.");
}

static void RunMavlinkConsoleRuntime()
{
    var now = new DateTimeOffset(2026, 6, 29, 10, 0, 0, TimeSpan.Zero);
    var console = new MavlinkConsoleRuntime();

    var pending = console.SubmitCommand("status", now);
    Require(pending.HasPendingCommand, "Expected pending console command.");
    Require(pending.PendingCommand == "status", "Expected trimmed pending command.");
    Require(pending.Lines[0].Kind == MavlinkConsoleLineKind.Command, "Expected command line.");

    var response = console.ReceiveLine("system ok", now.AddSeconds(1));
    Require(!response.HasPendingCommand, "Expected response to clear pending command.");
    Require(response.Lines.Count == 2, "Expected command and response lines.");
    Require(response.Lines[1].Kind == MavlinkConsoleLineKind.Response, "Expected response line.");

    var failed = console.SubmitCommand("", now.AddSeconds(2));
    Require(failed.Lines.Last().Kind == MavlinkConsoleLineKind.Error, "Expected empty command error line.");
}

static void ProjectFlightLogDownloadPanel()
{
    var workflow = new FlightLogDownloadWorkflow(maxRetryCount: 2);
    var projector = new FlightLogDownloadPanelProjector();
    workflow.RequestList();
    workflow.ApplyList([
        new FlightLogDescriptor(2, "log-2", new DateTimeOffset(2026, 6, 29, 9, 0, 0, TimeSpan.Zero), 4096, FlightLogFormat.Px4ULog)
    ]);

    var ready = projector.Project(workflow.Snapshot);
    Require(ready.CanRequestList, "Expected list refresh to be available.");
    Require(ready.StatusText.Contains("ready"), "Expected ready status.");

    workflow.RequestDownload(2);
    workflow.ApplyProgress(1024);
    var downloading = projector.Project(workflow.Snapshot);
    Require(downloading.CanCancel, "Expected active download to be cancellable.");
    Require(Math.Abs(downloading.Progress - 0.25) < 0.000001, "Expected download progress.");
    Require(downloading.ActiveLogName == "log-2", "Expected active log name.");

    workflow.Fail("radio lost");
    var failed = projector.Project(workflow.Snapshot);
    Require(failed.CanRetry, "Expected retry after failure.");
    Require(failed.StatusText == "radio lost", "Expected failure message.");
}

static void ManagePx4LogMetadata()
{
    var manager = new Px4LogManager();
    var newer = new Px4LogMetadata(
        2,
        "log_2.ulg",
        2 * 1024 * 1024,
        new DateTimeOffset(2026, 6, 29, 11, 0, 0, TimeSpan.Zero),
        TimeSpan.FromMinutes(4),
        "PX4 Quad",
        IsUploaded: false);
    var older = newer with
    {
        Id = 1,
        FileName = "log_1.ulg",
        SizeBytes = 1024 * 1024,
        Timestamp = new DateTimeOffset(2026, 6, 29, 10, 0, 0, TimeSpan.Zero)
    };

    var state = manager.Load([older, newer]);
    Require(state.Logs[0].Id == 2, "Expected newest PX4 log first.");
    Require(state.TotalBytes == 3 * 1024 * 1024, "Expected total PX4 log bytes.");
    Require(state.Summary.Contains("3.0 MB"), "Expected formatted PX4 log size.");

    var completed = manager.ApplyDownloadComplete(2, "logs/log_2.ulg");
    Require(completed.Logs.First(log => log.Id == 2).IsUploaded, "Expected completed PX4 log flag.");
    Require(completed.Logs.First(log => log.Id == 2).FileName == "logs/log_2.ulg", "Expected stored PX4 path.");
}

static void InspectULogParserRuntime()
{
    var runtime = new FlightLogParserRuntime();
    var state = runtime.Inspect([0x55, 0x4c, 0x6f, 0x67, 0x01, 0x12, 0x35, 0x00]);

    Require(state.Format == FlightLogFormat.Px4ULog, "Expected PX4 ULog format.");
    Require(state.Recognized, "Expected ULog header recognition.");
    Require(!state.Parsed, "Expected full ULog parsing to remain boundary-only.");
    Require(state.StatusText.Contains("planned"), "Expected parser boundary status text.");
}

static void InspectDataFlashParserRuntime()
{
    var runtime = new FlightLogParserRuntime();
    var state = runtime.Inspect([0xa3, 0x95, 0x80, 0x01]);

    Require(state.Format == FlightLogFormat.ArduPilotDataFlash, "Expected DataFlash format.");
    Require(state.Recognized, "Expected DataFlash header recognition.");
    Require(!state.Parsed, "Expected full DataFlash parsing to remain boundary-only.");
    Require(state.Diagnostics.Any(static diagnostic => diagnostic.Code.Contains("ArduPilotDataFlash")), "Expected DataFlash diagnostic code.");
}

static void ProjectGeoTagRuntimeUi()
{
    var images = new[]
    {
        new GeoTagImageDescriptor("img001.jpg", new DateTimeOffset(2026, 6, 29, 10, 0, 0, TimeSpan.Zero)),
        new GeoTagImageDescriptor("img002.jpg", new DateTimeOffset(2026, 6, 29, 10, 0, 5, TimeSpan.Zero))
    };
    var track = new[]
    {
        new GeoTagTrackPoint(new DateTimeOffset(2026, 6, 29, 10, 0, 0, TimeSpan.Zero), new MapCoordinate(47.397742, 8.545594, 488))
    };
    var summary = new GeoTagMatcher().Match(images, track, GeoTagMatchPolicy.Default);
    var ui = new GeoTagRuntimeProjector().Project(summary);

    Require(ui.MatchedCount == 1, "Expected one geotag match.");
    Require(ui.UnmatchedCount == 1, "Expected one unmatched image.");
    Require(ui.Rows[0].CoordinateText == "47.397742,8.545594", "Expected coordinate projection.");
    Require(ui.Summary == "1 matched, 1 unmatched", "Expected geotag summary.");
}

static void ProjectFileLogViewerRows()
{
    var rows = new[]
    {
        new LogViewerRow(new DateTimeOffset(2026, 6, 29, 10, 0, 0, TimeSpan.Zero), "Info", "Map", "ready"),
        new LogViewerRow(new DateTimeOffset(2026, 6, 29, 10, 1, 0, TimeSpan.Zero), "Error", "Link", "lost"),
        new LogViewerRow(new DateTimeOffset(2026, 6, 29, 10, 2, 0, TimeSpan.Zero), "Error", "Map", "tile failed")
    };
    var projector = new FileLogViewerProjector();
    var state = projector.Project(rows, levelFilter: "Error", categoryFilter: "Map");
    var export = projector.ExportText(state);

    Require(state.Rows.Count == 1, "Expected filtered app log row.");
    Require(state.Rows[0].Message == "tile failed", "Expected filtered log message.");
    Require(state.Summary == "1 app log rows", "Expected app log summary.");
    Require(export.Contains("tile failed"), "Expected exported log text.");
}

static void ProjectReplayWorkflowDetail()
{
    var source = new InMemoryLogReplaySource([
        new LogReplayPacket(TimeSpan.Zero, MavlinkTestFrames.HeartbeatV1(systemId: 9, componentId: 1)),
        new LogReplayPacket(TimeSpan.FromSeconds(3), MavlinkTestFrames.StatusText(systemId: 9, componentId: 42, text: "hello"))
    ]);
    var session = new ReplayPlaybackSession();
    session.OpenAsync(source).GetAwaiter().GetResult();
    session.Play();
    var timeline = session.Timeline;
    var selected = timeline.Packets[1];
    var ui = new ReplayWorkflowProjector().Project(session.Snapshot, timeline, selected);

    Require(ui.CanPause, "Expected replay pause control while playing.");
    Require(ui.CanSeek, "Expected replay seek control.");
    Require(ui.CanStep, "Expected replay step control while playing.");
    Require(ui.PacketCount == 2, "Expected replay packet count.");
    Require(ui.GapCount == 1, "Expected replay gap count.");
    Require(ui.SelectedDetail.Contains("STATUSTEXT"), "Expected selected packet detail.");
}

static void CatalogAnalyzeRuntimeEvidence()
{
    var evidence = new AnalyzeRuntimeEvidenceCatalog().Build();

    Require(evidence.Count == 9, "Expected v1.49 evidence items.");
    Require(evidence.Any(static item => item.Id == "ANALYZEFULL-253" && item.Complete), "Expected console evidence.");
    Require(evidence.Any(static item => item.Id == "ANALYZEFULL-261" && !item.Complete), "Expected real/SITL log evidence gap.");
}

static void AuditAnalyzeRuntimeParityGaps()
{
    var evidence = new AnalyzeRuntimeEvidenceCatalog().Build();
    var audit = new AnalyzeRuntimeParityAudit().Audit(evidence);

    Require(audit.CompleteItems == 8, "Expected eight complete analyze/log evidence items.");
    Require(audit.DeferredItems == 1, "Expected one real/SITL evidence gap.");
    Require(audit.Summary.Contains("real/SITL"), "Expected real/SITL gap summary.");
}

static void MapAnalyzeViewQgcQmlParityWithoutRuntimeOverclaim()
{
    var catalog = new QgcQmlParityCatalog().Build();
    var analyze = catalog.Single(static item => item.Module == "AnalyzeView");
    var logManager = catalog.Single(static item => item.Module == "LogManager");
    var audit = new QgcQmlParityAudit().Audit(catalog);

    Require(analyze.Status == QgcQmlParityStatus.Migrated, "Expected AnalyzeView to be migrated, not complete.");
    Require(analyze.RequiredEvidence == QgcQmlEvidenceLevel.Runtime, "Expected AnalyzeView runtime evidence requirement.");
    Require(analyze.Blocker.Contains("real log", StringComparison.OrdinalIgnoreCase), "Expected AnalyzeView real log evidence gap.");
    Require(analyze.Blocker.Contains("SITL", StringComparison.Ordinal), "Expected AnalyzeView SITL evidence gap.");
    Require(logManager.Status == QgcQmlParityStatus.Migrated, "Expected LogManager shared runtime migration.");
    Require(logManager.Blocker.Contains("PX4", StringComparison.Ordinal), "Expected PX4 log evidence gap.");
    Require(logManager.Blocker.Contains("ArduPilot", StringComparison.Ordinal), "Expected ArduPilot log evidence gap.");
    Require(!audit.CanClaimQmlUiParity, "Analyze mapping must not claim full QML UI parity.");
    Require(!audit.CanClaimQgcReplacement, "Analyze mapping must not claim QGC replacement.");
}

static void ParseExternalGpsNmeaFix()
{
    var runtime = new ExternalGpsRuntime();
    var accepted = runtime.IngestNmea("$GPGGA,123519,4807.038,N,01131.000,E,4,12,0.8,545.4,M,46.9,M,,*47", DateTimeOffset.Parse("2026-06-29T00:00:00Z"));

    var position = runtime.GetPositionAsync().GetAwaiter().GetResult();
    var rtk = runtime.GetRtkStateAsync().GetAwaiter().GetResult();

    Require(accepted, "Expected GGA sentence to be accepted.");
    Require(position is not null, "Expected external GPS position.");
    Require(Math.Abs(position!.Latitude - 48.1173) < 0.0001, "Expected parsed latitude.");
    Require(Math.Abs(position.Longitude - 11.5166667) < 0.0001, "Expected parsed longitude.");
    Require(Math.Abs(position.AltitudeMeters - 545.4) < 0.001, "Expected parsed altitude.");
    Require(position.Fix == GpsFixQuality.RtkFixed, "Expected RTK fixed quality from GGA fix code 4.");
    Require(position.Satellites == 12, "Expected satellite count.");
    Require(rtk.IsActive && rtk.Observations == 12, "Expected RTK state projection from external GPS.");
    Require(runtime.Snapshot.StatusText.Contains("External GPS", StringComparison.Ordinal), "Expected external GPS status text.");
}

static void SelectPositioningSourceByFixQuality()
{
    var selector = new PositionSourceSelector();
    var native = new PositionSourceSnapshot(
        PositionSourceKind.NativeLocation,
        new GpsPosition(47.0, 8.0, 500, GpsFixQuality.Fix3D, 9),
        DateTimeOffset.Parse("2026-06-29T00:00:02Z"),
        "Native");
    var external = new PositionSourceSnapshot(
        PositionSourceKind.ExternalGps,
        new GpsPosition(48.0, 11.0, 545, GpsFixQuality.RtkFloat, 14),
        DateTimeOffset.Parse("2026-06-29T00:00:01Z"),
        "External");

    var selected = selector.SelectBest(native, external);

    Require(selected.Kind == PositionSourceKind.ExternalGps, "Expected higher quality RTK float source to win.");
    Require(selected.Position?.Fix == GpsFixQuality.RtkFloat, "Expected RTK float source position.");
}

static void RunRtkCorrectionSession()
{
    var runtime = new RtkCorrectionRuntime();
    var config = new NtripCasterConfiguration("caster.example.test", 2101, "VRS", "user", "pass", UseTls: false);

    var configured = runtime.Configure(config);
    var connecting = runtime.Start();
    var streaming = runtime.MarkStreaming();
    var packet = runtime.AcceptRtcmPacket([0xD3, 0x00, 0x13, 0x3E]);
    var request = runtime.BuildNtripRequest();

    Require(configured.State == RtkCorrectionState.Configured, "Expected RTK configuration state.");
    Require(connecting.State == RtkCorrectionState.Connecting, "Expected RTK connecting state.");
    Require(streaming.State == RtkCorrectionState.Streaming, "Expected RTK streaming state.");
    Require(packet.BytesReceived == 4 && packet.RtcmPacketsForwarded == 1, "Expected RTCM forwarding counters.");
    Require(request.Contains("GET /VRS HTTP/1.1", StringComparison.Ordinal), "Expected NTRIP request mount point.");
    Require(request.Contains("Ntrip-Version", StringComparison.Ordinal), "Expected NTRIP v2 header.");
}

static void ProjectPlatformPositioningPermissions()
{
    var projector = new PlatformPositionPermissionProjector();

    var android = projector.Project(
        "Android",
        PositionPermissionStatus.Granted,
        PositionPermissionStatus.Denied,
        PositionPermissionStatus.Granted,
        PositionPermissionStatus.Unknown);
    var desktop = projector.Project(
        "Desktop",
        PositionPermissionStatus.Denied,
        PositionPermissionStatus.Unknown,
        PositionPermissionStatus.Denied,
        PositionPermissionStatus.Granted);

    Require(android.CanUseNativeLocation, "Expected Android native location when foreground permission is granted.");
    Require(android.CanUseExternalGps, "Expected Android external GPS when Bluetooth is granted.");
    Require(desktop.CanUseExternalGps, "Expected desktop external GPS when USB is granted.");
    Require(!desktop.CanUseNativeLocation, "Expected desktop native location blocked without foreground permission.");
}

static void SendFollowMeTargetCommand()
{
    var link = new MockLinkTransport();
    link.ConnectAsync().GetAwaiter().GetResult();
    var sentFrames = new List<byte[]>();
    link.BytesSent += (_, args) => sentFrames.Add(args.Bytes.ToArray());
    var service = new FollowMeService(systemId: 9, componentId: 1);

    service.StartAsync(link).GetAwaiter().GetResult();
    service.SendTargetAsync(link, 47.397742, 8.545594, 488.5).GetAwaiter().GetResult();
    service.SendTargetAsync(link, 127.0, 8.0, 10).GetAwaiter().GetResult();

    Require(service.Snapshot.IsActive, "Expected FollowMe active state.");
    Require(service.Snapshot.SentTargetCount == 1, "Expected exactly one valid FollowMe target sent.");
    Require(service.Snapshot.LastTarget is not null, "Expected FollowMe last target.");
    Require(sentFrames.Count == 1, "Expected one FollowMe MAVLink command frame.");

    var frame = ParseSingleFrame(sentFrames[0]);
    Require(frame.MessageId == MavlinkMessageIds.CommandLong, "Expected COMMAND_LONG FollowMe frame.");
    Require(BitConverter.ToUInt16(frame.Payload, 28) == 115, "Expected MAV_CMD_DO_FOLLOW command id.");
    Require(Math.Abs(BitConverter.ToSingle(frame.Payload, 16) - 47.397742f) < 0.0001f, "Expected FollowMe latitude in param5.");
    Require(Math.Abs(BitConverter.ToSingle(frame.Payload, 20) - 8.545594f) < 0.0001f, "Expected FollowMe longitude in param6.");
    Require(Math.Abs(BitConverter.ToSingle(frame.Payload, 24) - 488.5f) < 0.001f, "Expected FollowMe altitude in param7.");
}

static void CatalogPositioningRuntimeEvidence()
{
    var evidence = new PositioningRuntimeEvidenceCatalog().Build();

    Require(evidence.Count == 7, "Expected v1.50 evidence items.");
    Require(evidence.Any(static item => item.Id == "POSITION-264" && item.Complete), "Expected NMEA evidence.");
    Require(evidence.Any(static item => item.Id == "POSITION-269" && !item.Complete), "Expected field evidence gap.");
}

static void AuditPositioningRuntimeParityGaps()
{
    var evidence = new PositioningRuntimeEvidenceCatalog().Build();
    var audit = new PositioningRuntimeParityAudit().Audit(evidence);

    Require(audit.CompleteItems == 5, "Expected five complete positioning evidence items.");
    Require(audit.DeferredItems == 2, "Expected two positioning field evidence gaps.");
    Require(audit.Summary.Contains("platform/field", StringComparison.Ordinal), "Expected platform/field gap summary.");
}

static void CatalogGate10QgcQmlRemainingModules()
{
    var catalog = new QgcQmlParityCatalog().Build();
    var audit = new QgcQmlParityAudit().Audit(catalog);

    Require(catalog.Single(static item => item.Module == "Vehicle").Status == QgcQmlParityStatus.Migrated, "Expected Vehicle migrated.");
    Require(catalog.Single(static item => item.Module == "GPS").Status == QgcQmlParityStatus.Migrated, "Expected GPS migrated.");
    Require(catalog.Single(static item => item.Module == "FirstRunPromptDialogs").Status == QgcQmlParityStatus.Migrated, "Expected first-run prompts migrated.");
    Require(catalog.Single(static item => item.Module == "MainWindow").Status == QgcQmlParityStatus.Migrated, "Expected MainWindow migrated.");
    Require(catalog.Single(static item => item.Module == "Viewer3D").Status == QgcQmlParityStatus.Deferred, "Expected Viewer3D deferred.");
    Require(!audit.CanClaimQmlUiParity, "Expected Gate 10 not to claim full QML parity.");
    Require(!audit.CanClaimQgcReplacement, "Expected Gate 10 not to claim QGC replacement.");
}

static void ScanJoystickDevices()
{
    var service = new InMemoryJoystickService();
    var device = new JoystickDevice("Operator Controller", Axes: 6, Buttons: 12);
    service.LoadDevices([device]);
    service.SetRawState(device, new JoystickRawState([0.1, -0.2, 0.3, 0.4, 0, 0], [true, false, true]));

    var devices = service.ScanAsync().GetAwaiter().GetResult();
    var state = service.ReadAsync(device).GetAwaiter().GetResult();

    Require(devices.Count == 1, "Expected one joystick device.");
    Require(devices[0].Axes == 6 && devices[0].Buttons == 12, "Expected joystick capabilities.");
    Require(state is not null, "Expected joystick state.");
    Require(state!.Buttons.Length == 3 && state.Buttons[2], "Expected button state projection.");
}

static void CalibrateJoystickAxes()
{
    var device = new JoystickDevice("Operator Controller", Axes: 4, Buttons: 4);
    var service = new JoystickCalibrationService();
    var profile = service.CreateDefaultProfile(device);
    var projection = service.Project(
        new JoystickRawState([0.5, -0.5, 0.02, 1.0], [true, false, true, false]),
        profile);

    Require(Math.Abs(projection.Roll - 0.5) < 0.001, "Expected roll calibration.");
    Require(Math.Abs(projection.Pitch - 0.5) < 0.001, "Expected reversed pitch calibration.");
    Require(Math.Abs(projection.Yaw) < 0.001, "Expected yaw deadband.");
    Require(Math.Abs(projection.Throttle - 1.0) < 0.001, "Expected throttle scaling.");
    Require(projection.ButtonMask == 0b0101, "Expected button bitmask.");
}

static void ProjectJoystickManualControl()
{
    var projector = new JoystickManualControlProjector();
    var command = projector.Project(new JoystickInputProjection(
        Roll: 0.25,
        Pitch: -0.5,
        Yaw: 2.0,
        Throttle: 0.75,
        ButtonMask: 3,
        StatusText: "calibrated"));

    Require(command.X == 250, "Expected roll manual control value.");
    Require(command.Y == -500, "Expected pitch manual control value.");
    Require(command.Z == 750, "Expected throttle manual control value.");
    Require(command.R == 1000, "Expected yaw clamp.");
    Require(command.Buttons == 3, "Expected button mask in manual control.");
}

static void ProjectAdsbTrafficAlertsAndOverlays()
{
    var runtime = new AdsbTrafficRuntime();
    runtime.Upsert(new AdsbVehicle(0xABC123, "NEAR", 47.0008, 8.0000, 560, Heading: 90, SpeedMs: 35));
    runtime.Upsert(new AdsbVehicle(0xDEF456, "FAR", 47.1, 8.1, 1200, Heading: 180, SpeedMs: 40));

    var snapshot = runtime.Project(ownLatitude: 47.0, ownLongitude: 8.0, ownAltitudeMeters: 500);
    var overlay = runtime.BuildTrafficOverlay();

    Require(snapshot.Vehicles.Count == 2, "Expected two ADSB targets.");
    Require(snapshot.Alerts.Count == 1, "Expected one traffic alert.");
    Require(snapshot.Alerts[0].Severity == TrafficAlertSeverity.Warning, "Expected near traffic warning.");
    Require(overlay.Markers.Count == 2, "Expected traffic map markers.");
    Require(overlay.Markers.All(static marker => marker.Layer == MapProviderOverlayLayer.Traffic), "Expected traffic overlay layer.");
}

static void ProjectRemoteIdStateAndOverlay()
{
    var runtime = new RemoteIdRuntime();

    var broadcasting = runtime.Update(
        id: "RID-001",
        isBroadcasting: true,
        operatorId: "operator",
        latitude: 47.397742,
        longitude: 8.545594,
        altitudeMeters: 488.5);
    var overlay = runtime.BuildOverlay();
    var error = runtime.Update("RID-001", isBroadcasting: false, operatorId: "operator", latitude: null, longitude: null, altitudeMeters: null, error: "module offline");

    Require(broadcasting.IsBroadcasting, "Expected RemoteID broadcast state.");
    Require(broadcasting.StatusText.Contains("broadcasting", StringComparison.Ordinal), "Expected broadcasting status text.");
    Require(overlay.Markers.Count == 1, "Expected RemoteID overlay marker.");
    Require(overlay.Markers[0].Layer == MapProviderOverlayLayer.RemoteId, "Expected RemoteID overlay layer.");
    Require(error.Error == "module offline", "Expected RemoteID error state.");
}

static void CatalogInputTrafficRuntimeEvidence()
{
    var joystick = new JoystickRuntimeEvidenceCatalog().Build();
    var traffic = new TrafficRuntimeEvidenceCatalog().Build();

    Require(joystick.Count == 4, "Expected joystick evidence items.");
    Require(traffic.Count == 5, "Expected traffic evidence items.");
    Require(joystick.Any(static item => item.Id == "INPUTTRAFFIC-273" && item.Complete), "Expected MANUAL_CONTROL evidence.");
    Require(traffic.Any(static item => item.Id == "INPUTTRAFFIC-275" && item.Complete), "Expected RemoteID evidence.");
}

static void AuditInputTrafficRuntimeParityGaps()
{
    var joystick = new JoystickRuntimeEvidenceCatalog().Build();
    var traffic = new TrafficRuntimeEvidenceCatalog().Build();
    var audit = new InputTrafficRuntimeParityAudit().Audit(joystick, traffic);

    Require(audit.CompleteItems == 7, "Expected seven complete input/traffic evidence items.");
    Require(audit.DeferredItems == 2, "Expected two hardware/field evidence gaps.");
    Require(audit.Summary.Contains("hardware/field", StringComparison.Ordinal), "Expected hardware/field gap summary.");
}

static void NavigateShellSettingsWorkspace()
{
    var store = new FakeSettingsStore();
    var logger = new AppLogger();
    var linkManager = new LinkManager(logger);
    var protocol = new MavlinkProtocol();
    protocol.Attach(linkManager);
    var heartbeat = new GcsHeartbeatService(linkManager, new MavlinkFrameWriter(), logger);
    var vehicles = new MultiVehicleManager(protocol, logger);
    var settings = new SettingsViewModel(SettingsManager.CreateDefault(), store);
    var shell = new ShellViewModel(
        new AppLifecycleService(store, logger),
        new AppCloseCoordinator(),
        logger,
        linkManager,
        protocol,
        heartbeat,
        vehicles,
        settings);

    Require(shell.ActiveViewName == "Fly", "Expected Fly to be initial shell workspace.");
    shell.ShowSettingsCommand.Execute().Subscribe();

    Require(shell.IsSettingsActive, "Expected Settings workspace to be active.");
    Require(shell.ActiveViewName == "Settings", "Expected Settings active view name.");
    Require(settings.Groups.Count >= 5, "Expected settings groups to load when navigating.");
}

static void OpenShellIndicatorDrawers()
{
    var store = new FakeSettingsStore();
    var logger = new AppLogger();
    var linkManager = new LinkManager(logger);
    var protocol = new MavlinkProtocol();
    protocol.Attach(linkManager);
    var heartbeat = new GcsHeartbeatService(linkManager, new MavlinkFrameWriter(), logger);
    var vehicles = new MultiVehicleManager(protocol, logger);
    var shell = new ShellViewModel(
        new AppLifecycleService(store, logger),
        new AppCloseCoordinator(),
        logger,
        linkManager,
        protocol,
        heartbeat,
        vehicles);

    shell.ShowVehicleIndicatorCommand.Execute().Subscribe();

    Require(shell.IsShellDrawerOpen, "Expected vehicle indicator drawer to open.");
    Require(shell.IsVehicleDrawerOpen, "Expected vehicle drawer kind.");
    Require(shell.ShellDrawerTitle == "Vehicle Status", "Expected vehicle drawer title.");
    Require(shell.ShellDrawerSummary == "No Vehicle", "Expected vehicle summary.");
    Require(shell.ShellDrawerDetailText.Contains("No active vehicle", StringComparison.Ordinal), "Expected no vehicle detail.");

    shell.ShowTelemetryIndicatorCommand.Execute().Subscribe();

    Require(shell.IsTelemetryDrawerOpen, "Expected telemetry drawer kind.");
    Require(shell.ShellDrawerTitle == "Telemetry", "Expected telemetry drawer title.");
    Require(shell.ShellDrawerDetailText.Contains("Packets received", StringComparison.Ordinal), "Expected telemetry details.");

    shell.CloseShellDrawerCommand.Execute().Subscribe();

    Require(!shell.IsShellDrawerOpen, "Expected shell drawer to close.");
}

static void OpenShellToolDrawerNavigationActions()
{
    var store = new FakeSettingsStore();
    var logger = new AppLogger();
    var linkManager = new LinkManager(logger);
    var protocol = new MavlinkProtocol();
    protocol.Attach(linkManager);
    var heartbeat = new GcsHeartbeatService(linkManager, new MavlinkFrameWriter(), logger);
    var vehicles = new MultiVehicleManager(protocol, logger);
    var settings = new SettingsViewModel(SettingsManager.CreateDefault(), store);
    var shell = new ShellViewModel(
        new AppLifecycleService(store, logger),
        new AppCloseCoordinator(),
        logger,
        linkManager,
        protocol,
        heartbeat,
        vehicles,
        settings);

    shell.ShowToolDrawerCommand.Execute().Subscribe();

    Require(shell.IsToolDrawerOpen, "Expected tool drawer to open.");
    Require(shell.ShellDrawerTitle == "Tools", "Expected tool drawer title.");
    Require(shell.ShellDrawerDetailText.Contains("operator tools", StringComparison.Ordinal), "Expected tool drawer details.");

    shell.ShowSetupCommand.Execute().Subscribe();
    Require(shell.IsSetupActive, "Expected setup tool navigation.");

    shell.ShowSettingsCommand.Execute().Subscribe();
    Require(shell.IsSettingsActive, "Expected settings tool navigation.");
    Require(settings.Groups.Count >= 5, "Expected settings groups to load from tool drawer navigation.");
}

static void ShellToolDrawerSwitchesSetupParametersAnalyze()
{
    var store = new FakeSettingsStore();
    var logger = new AppLogger();
    var linkManager = new LinkManager(logger, new AppSettingsLinkConfigurationStore(store));
    var protocol = new MavlinkProtocol();
    protocol.Attach(linkManager);
    var heartbeat = new GcsHeartbeatService(linkManager, new MavlinkFrameWriter(), logger);
    var shell = new ShellViewModel(
        new AppLifecycleService(store, logger),
        new AppCloseCoordinator(),
        logger,
        linkManager,
        protocol,
        heartbeat,
        new MultiVehicleManager(protocol, logger),
        linkConfigurationStore: new AppSettingsLinkConfigurationStore(store));

    shell.ShowSetupCommand.Execute().Subscribe();
    Require(shell.IsToolDrawerActive, "Expected setup tool drawer active.");
    Require(shell.ToolDrawerTitle == "Vehicle Configuration", "Expected setup drawer title.");

    shell.ShowParametersCommand.Execute().Subscribe();
    Require(shell.IsToolDrawerActive, "Expected parameter tool drawer active.");
    Require(shell.ToolDrawerTitle == "Vehicle Configuration", "Expected parameters to stay under setup drawer title.");

    shell.ShowAnalyzeCommand.Execute().Subscribe();
    Require(shell.ToolDrawerTitle == "Analyze Tools", "Expected analyze drawer title.");
}

static void AutoConnectShellTcpEndpointFromEnvironment()
{
    var previousPrimary = Environment.GetEnvironmentVariable(ShellViewModel.AutoConnectTcpEnvironmentVariable);
    var previousAndroid = Environment.GetEnvironmentVariable(ShellViewModel.AndroidAutoConnectTcpEnvironmentVariable);
    var previousStartupEndpoint = ShellViewModel.StartupAutoConnectTcpEndpoint;
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var endpoint = (IPEndPoint)listener.LocalEndpoint;
    using var releaseServer = new CancellationTokenSource();
    var serverAccepted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var serverTask = Task.Run(async () =>
    {
        using var client = await listener.AcceptTcpClientAsync();
        serverAccepted.SetResult();
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, releaseServer.Token);
        }
        catch (OperationCanceledException)
        {
        }
    });

    var store = new FakeSettingsStore();
    var logger = new AppLogger();
    var linkManager = new LinkManager(logger);
    var protocol = new MavlinkProtocol();
    protocol.Attach(linkManager);
    var heartbeat = new GcsHeartbeatService(linkManager, new MavlinkFrameWriter(), logger);
    var vehicles = new MultiVehicleManager(protocol, logger);
    var shell = new ShellViewModel(
        new AppLifecycleService(store, logger),
        new AppCloseCoordinator(),
        logger,
        linkManager,
        protocol,
        heartbeat,
        vehicles);

    try
    {
        Environment.SetEnvironmentVariable(ShellViewModel.AutoConnectTcpEnvironmentVariable, $"{IPAddress.Loopback}:{endpoint.Port}");
        Environment.SetEnvironmentVariable(ShellViewModel.AndroidAutoConnectTcpEnvironmentVariable, null);
        ShellViewModel.StartupAutoConnectTcpEndpoint = null;

        shell.InitializeAsync().GetAwaiter().GetResult();

        Require(linkManager.Links.Count == 1, "Expected one auto-connected TCP link.");
        Require(serverAccepted.Task.Wait(TimeSpan.FromSeconds(5)), "Expected auto-connect TCP server to accept client.");
        Require(linkManager.ActiveLink?.Configuration is TcpLinkConfiguration tcp && tcp.Port == endpoint.Port, "Expected active TCP auto-connect link.");
        Require(heartbeat.IsRunning, "Expected GCS heartbeat to start after TCP auto-connect.");
        Require(shell.StatusText == $"Android TCP auto-connected to {IPAddress.Loopback}:{endpoint.Port}", "Expected auto-connect status text.");
    }
    finally
    {
        heartbeat.StopAsync().GetAwaiter().GetResult();
        linkManager.DisconnectAllAsync().GetAwaiter().GetResult();
        listener.Stop();
        releaseServer.Cancel();
        Environment.SetEnvironmentVariable(ShellViewModel.AutoConnectTcpEnvironmentVariable, previousPrimary);
        Environment.SetEnvironmentVariable(ShellViewModel.AndroidAutoConnectTcpEnvironmentVariable, previousAndroid);
        ShellViewModel.StartupAutoConnectTcpEndpoint = previousStartupEndpoint;
        serverTask.Wait(TimeSpan.FromSeconds(5));
    }
}

static void AutoConnectShellTcpEndpointFromStartupIntent()
{
    var previousPrimary = Environment.GetEnvironmentVariable(ShellViewModel.AutoConnectTcpEnvironmentVariable);
    var previousAndroid = Environment.GetEnvironmentVariable(ShellViewModel.AndroidAutoConnectTcpEnvironmentVariable);
    var previousStartupEndpoint = ShellViewModel.StartupAutoConnectTcpEndpoint;
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var endpoint = (IPEndPoint)listener.LocalEndpoint;
    using var releaseServer = new CancellationTokenSource();
    var serverAccepted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var serverTask = Task.Run(async () =>
    {
        using var client = await listener.AcceptTcpClientAsync();
        serverAccepted.SetResult();
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, releaseServer.Token);
        }
        catch (OperationCanceledException)
        {
        }
    });

    var store = new FakeSettingsStore();
    var logger = new AppLogger();
    var linkManager = new LinkManager(logger);
    var protocol = new MavlinkProtocol();
    protocol.Attach(linkManager);
    var heartbeat = new GcsHeartbeatService(linkManager, new MavlinkFrameWriter(), logger);
    var vehicles = new MultiVehicleManager(protocol, logger);
    var shell = new ShellViewModel(
        new AppLifecycleService(store, logger),
        new AppCloseCoordinator(),
        logger,
        linkManager,
        protocol,
        heartbeat,
        vehicles);

    try
    {
        Environment.SetEnvironmentVariable(ShellViewModel.AutoConnectTcpEnvironmentVariable, null);
        Environment.SetEnvironmentVariable(ShellViewModel.AndroidAutoConnectTcpEnvironmentVariable, null);
        ShellViewModel.StartupAutoConnectTcpEndpoint = $"{IPAddress.Loopback}:{endpoint.Port}";

        shell.InitializeAsync().GetAwaiter().GetResult();

        Require(linkManager.Links.Count == 1, "Expected one startup-intent TCP link.");
        Require(serverAccepted.Task.Wait(TimeSpan.FromSeconds(5)), "Expected startup-intent TCP server to accept client.");
        Require(linkManager.ActiveLink?.Configuration is TcpLinkConfiguration tcp && tcp.Port == endpoint.Port, "Expected active startup-intent TCP auto-connect link.");
        Require(heartbeat.IsRunning, "Expected GCS heartbeat to start after startup-intent TCP auto-connect.");
        Require(shell.StatusText == $"Android TCP auto-connected to {IPAddress.Loopback}:{endpoint.Port}", "Expected startup-intent auto-connect status text.");
    }
    finally
    {
        heartbeat.StopAsync().GetAwaiter().GetResult();
        linkManager.DisconnectAllAsync().GetAwaiter().GetResult();
        listener.Stop();
        releaseServer.Cancel();
        Environment.SetEnvironmentVariable(ShellViewModel.AutoConnectTcpEnvironmentVariable, previousPrimary);
        Environment.SetEnvironmentVariable(ShellViewModel.AndroidAutoConnectTcpEnvironmentVariable, previousAndroid);
        ShellViewModel.StartupAutoConnectTcpEndpoint = previousStartupEndpoint;
        serverTask.Wait(TimeSpan.FromSeconds(5));
    }
}

static void CatalogUiParityWorkflows()
{
    var evidence = new UiParityEvidenceCatalog().Build();

    Require(evidence.Count == 11, "Expected v1.52 UI parity evidence items.");
    Require(evidence.Any(static item => item.Id == "UIPARITY-279" && item.Complete), "Expected shell navigation evidence.");
    Require(evidence.Any(static item => item.Area == UiWorkflowArea.Settings && item.Complete), "Expected Settings workflow evidence.");
    Require(evidence.Any(static item => item.Id == "UIPARITY-286" && !item.Complete), "Expected visual/device residual gap.");
}

static void CatalogQgcQmlModuleInventory()
{
    var inventory = new QgcQmlParityCatalog().Build();

    Require(inventory.Count == 16, "Expected sixteen QGC QML source modules.");
    Require(inventory.Sum(static item => item.FileCount) == 442, "Expected 442 QGC src QML files.");
    Require(inventory.Any(static item => item.Module == "QmlControls" && item.FileCount == 98 && item.Status == QgcQmlParityStatus.Migrated), "Expected QmlControls public component migration.");
    Require(inventory.Any(static item => item.Module == "AutoPilotPlugins" && item.FileCount == 84 && item.Status == QgcQmlParityStatus.Blocked), "Expected AutoPilotPlugins blocker.");
    Require(inventory.Any(static item => item.Module == "FlyView" && item.FileCount == 59), "Expected FlyView QML inventory.");
    Require(inventory.Any(static item => item.Module == "PlanView" && item.FileCount == 43), "Expected PlanView QML inventory.");
}

static void CatalogQgcPublicComponentMigration()
{
    var inventory = new QgcQmlParityCatalog().Build();
    var audit = new QgcQmlParityAudit().Audit(inventory);

    Require(inventory.Count == 16, "Expected QGC QML module inventory.");
    Require(inventory.Sum(static item => item.FileCount) == 442, "Expected full QGC QML file count.");
    Require(inventory.Any(static item => item.Module == "QmlControls" && item.Status == QgcQmlParityStatus.Migrated), "Expected QmlControls migrated.");
    Require(inventory.Any(static item => item.Module == "FactSystem" && item.Status == QgcQmlParityStatus.Migrated), "Expected FactSystem migrated.");
    Require(inventory.Any(static item => item.Module == "Toolbar" && item.Status == QgcQmlParityStatus.Migrated), "Expected Toolbar migrated.");
    Require(inventory.Any(static item => item.Module == "FlyView" && item.Status == QgcQmlParityStatus.Blocked), "Expected runtime UI modules to remain blocked.");
    Require(!audit.CanClaimQmlUiParity, "Expected public component migration not to claim full QML parity.");
    Require(audit.OpenBlockers.Any(static blocker => blocker.Contains("screenshot", StringComparison.OrdinalIgnoreCase) || blocker.Contains("behavior", StringComparison.OrdinalIgnoreCase) || blocker.Contains("runtime", StringComparison.OrdinalIgnoreCase)), "Expected visual or behavior evidence blocker.");
}

static void AuditQgcQmlParityBlockers()
{
    var inventory = new QgcQmlParityCatalog().Build();
    var audit = new QgcQmlParityAudit().Audit(inventory);

    Require(audit.TotalQmlFiles == 442, "Expected audit to cover 442 QGC QML files.");
    Require(audit.MappedModules == 0, "Expected public component modules to move past mapped status.");
    Require(audit.BlockedModules > audit.CompleteModules, "Expected blocked modules to exceed complete modules.");
    Require(!audit.CanClaimQmlUiParity, "Expected QGC QML UI parity claim to remain blocked.");
    Require(audit.OpenBlockers.Any(static blocker => blocker.Contains("FlyView", StringComparison.Ordinal)), "Expected FlyView blocker.");
    Require(audit.OpenBlockers.Any(static blocker => blocker.Contains("PlanView", StringComparison.Ordinal)), "Expected PlanView blocker.");
    Require(audit.OpenBlockers.Any(static blocker => blocker.Contains("AutoPilotPlugins", StringComparison.Ordinal)), "Expected AutoPilotPlugins blocker.");
}

static void PreventQgcQmlMigrationOverclaim()
{
    var audit = new QgcQmlParityAudit().Audit(new QgcQmlParityCatalog().Build());

    Require(!audit.CanClaimQmlUiParity, "Expected no QGC QML 1:1 claim from inventory-only evidence.");
    Require(!audit.CanClaimQgcReplacement, "Expected no QGC replacement claim from inventory-only evidence.");
    Require(audit.Summary.Contains("migration evidence", StringComparison.Ordinal), "Expected migration evidence gap summary.");
}

static void BuildDesktopAndroidUsabilityMatrix()
{
    var matrix = new UiUsabilityMatrix().Build();

    Require(matrix.Count == 6, "Expected desktop/android usability checks.");
    Require(matrix.Any(static item => item.Platform == UiPlatformTarget.Desktop && item.Complete), "Expected desktop checks.");
    Require(matrix.Any(static item => item.Platform == UiPlatformTarget.Android && item.Complete), "Expected Android checks.");
    Require(matrix.Count(static item => !item.Complete) == 2, "Expected deferred screenshot/device checks.");
}

static void AuditUiParityResidualGaps()
{
    var evidence = new UiParityEvidenceCatalog().Build();
    var matrix = new UiUsabilityMatrix().Build();
    var audit = new UiParityAudit().Audit(evidence, matrix);

    Require(audit.CompleteItems == 14, "Expected fourteen complete UI parity/usability items.");
    Require(audit.DeferredItems == 3, "Expected three visual/device evidence gaps.");
    Require(audit.Summary.Contains("visual/device", StringComparison.Ordinal), "Expected visual/device gap summary.");
}

static void CatalogSitlHardwareValidationScenarios()
{
    var scenarios = new SitlHardwareValidationCatalog().BuildRequiredScenarios();

    Require(scenarios.Count == 11, "Expected v1.53 required validation scenarios.");
    Require(scenarios.Any(static scenario => scenario.Id == "SITL-287-PX4-CONNECT" && scenario.RequiredEvidence == ValidationEvidenceLevel.Sitl), "Expected PX4 SITL connection scenario.");
    Require(scenarios.Any(static scenario => scenario.Id == "SITL-288-APM-CONNECT" && scenario.Target == ValidationTarget.ArduPilotSitl), "Expected ArduPilot SITL scenario.");
    Require(scenarios.Any(static scenario => scenario.Area == ValidationScenarioArea.Mission), "Expected mission validation scenario.");
    Require(scenarios.Any(static scenario => scenario.Area == ValidationScenarioArea.FlightLog), "Expected flight log validation scenario.");
    Require(scenarios.Any(static scenario => scenario.RequiredEvidence == ValidationEvidenceLevel.RealHardware), "Expected real hardware validation scenario.");
}

static void BuildGuardedSitlCommandTranscriptPlan()
{
    var entries = new GuardedSitlCommandTranscriptPlan().BuildDryRunPlan();
    var audit = new GuardedSitlCommandTranscriptAudit();

    Require(entries.Count == 5, "Expected guarded SITL command actions.");
    Require(entries.Any(static entry => entry.Action == GuardedSitlCommandAction.ArmDisarm && entry.Disposition == GuardedSitlCommandDisposition.Blocked), "Expected arm/disarm blocked by default.");
    Require(entries.Where(static entry => entry.SendsVehicleCommand).All(static entry => entry.Disposition == GuardedSitlCommandDisposition.Blocked), "Expected vehicle-mutating commands blocked by default.");
    Require(audit.SendsNoVehicleCommands(entries), "Expected dry-run plan to send no vehicle commands.");
}

static void GateSitlCommandAuthorization()
{
    var gate = new SitlCommandAuthorizationGate();
    var blocked = new SitlCommandAuthorizationPolicy(false, "passive-observation", []);
    var authorized = new SitlCommandAuthorizationPolicy(true, "authorized-sitl", [GuardedSitlCommandAction.ParameterList]);

    Require(!gate.CanExecute(blocked, GuardedSitlCommandAction.ParameterWrite), "Expected blocked parameter write without authorization.");
    Require(gate.CanExecute(authorized, GuardedSitlCommandAction.ParameterList), "Expected authorized parameter list action.");
    Require(!gate.CanExecute(authorized, GuardedSitlCommandAction.ModeChange), "Expected unauthorized mode change to remain blocked.");
    Require(gate.Explain(blocked, GuardedSitlCommandAction.ArmDisarm).Contains("Blocked", StringComparison.Ordinal), "Expected blocked explanation text.");

    var outputPath = Path.Combine(Path.GetTempPath(), "vgc-sitl-auth.md");
    var writtenPath = new SitlCommandAuthorizationArtifactWriter().Write(outputPath, blocked, GuardedSitlCommandAction.ArmDisarm);
    Require(File.Exists(writtenPath), "Expected SITL authorization artifact to be written.");
    Require(File.ReadAllText(writtenPath).Contains("CanExecute: False", StringComparison.Ordinal), "Expected blocked authorization artifact content.");
    File.Delete(writtenPath);
}

static void RecordSitlEnvironmentBlockers()
{
    var scenarios = new SitlHardwareValidationCatalog().BuildRequiredScenarios();
    var px4 = scenarios.Single(static scenario => scenario.Id == "SITL-287-PX4-CONNECT");
    var recorder = new ValidationEvidenceRecorder();
    var probe = new ValidationEnvironmentProbe(
        HasPx4: false,
        HasArduPilot: false,
        HasMavProxy: false,
        HasDocker: false,
        HasConfiguredWsl: false,
        HasRealVehicle: false,
        HasAndroidDevice: false,
        HasPayloadHardware: false);

    var result = recorder.RecordBlocked(px4, probe, "PX4, Docker, and configured WSL are unavailable.");

    Require(!probe.CanRunAnySitl, "Expected SITL probe to be blocked.");
    Require(result.Status == ValidationResultStatus.Blocked, "Expected blocked validation result.");
    Require(result.Blocker?.Contains("PX4", StringComparison.Ordinal) == true, "Expected blocker text.");
    Require(recorder.Results.Count == 1, "Expected one recorded validation result.");
}

static void AuditValidationClosureClaims()
{
    var scenarios = new SitlHardwareValidationCatalog().BuildRequiredScenarios();
    var recorder = new ValidationEvidenceRecorder();
    var probe = new ValidationEnvironmentProbe(false, false, false, false, false, false, false, false);

    foreach (var scenario in scenarios)
    {
        if (scenario.Id == "SITL-291-REPLAY")
        {
            recorder.RecordPassed(scenario, ValidationEvidenceLevel.Unit, "Replay workflow unit coverage passed.");
        }
        else
        {
            recorder.RecordBlocked(scenario, probe, "Required simulator or hardware is unavailable in this workspace.");
        }
    }

    var audit = new ValidationClosureAudit().Audit(scenarios, recorder.Results);

    Require(audit.RequiredScenarios == 11, "Expected eleven validation scenarios.");
    Require(audit.Passed == 1, "Expected replay-only validation pass.");
    Require(audit.Blocked == 10, "Expected remaining scenarios blocked without simulator/hardware.");
    Require(!audit.CanClaimSitlValidated, "Expected no SITL validation claim.");
    Require(!audit.CanClaimRealHardwareValidated, "Expected no real hardware validation claim.");
    Require(audit.OpenBlockers.Count == 10, "Expected blocked external evidence list.");
}

static void CatalogSitlHardwareEvidence()
{
    var evidence = new SitlHardwareEvidenceCatalog().Build();

    Require(evidence.Count == 8, "Expected v1.53 evidence catalog items.");
    Require(evidence.Any(static item => item.Id == "SITLHW-287" && item.Complete), "Expected PX4 scenario evidence.");
    Require(evidence.Any(static item => item.Id == "SITLHW-291" && item.EvidenceLevel == "L1"), "Expected replay fixture unit evidence.");
    Require(evidence.Any(static item => item.Id == "SITLHW-294" && !item.Complete), "Expected external SITL/hardware evidence gap.");
}

static void CatalogV154ReleaseReadiness()
{
    var items = new ReleaseReadinessCatalog().BuildV154Items();

    Require(items.Count == 8, "Expected v1.54 release readiness items.");
    Require(items.Any(static item => item.Id == "REL-295-LICENSE-AUDIT" && item.Status == ReleaseItemStatus.Complete), "Expected license audit item.");
    Require(items.Any(static item => item.Id == "REL-297-DESKTOP-PACKAGE" && item.RequiredEvidence == ReleaseEvidenceLevel.PackagedArtifact), "Expected desktop package evidence gate.");
    Require(items.Any(static item => item.Id == "REL-298-ANDROID-SIGNING" && item.Status == ReleaseItemStatus.Blocked), "Expected Android signing blocker.");
    Require(items.Any(static item => item.Id == "REL-300-BROWSER-IOS-SCOPE" && item.Area == ReleaseReadinessArea.PlatformScope), "Expected platform scope item.");
}

static void AuditReleaseLicenseInventory()
{
    var inventory = new ReleaseLicenseCatalog().BuildV154Inventory();
    var audit = new LicenseSourceAudit().Audit(inventory);

    Require(inventory.Count == 5, "Expected v1.54 license inventory entries.");
    Require(inventory.Any(static item => item.Component.Contains("QGroundControl", StringComparison.Ordinal) && !item.BundledInRelease), "Expected QGC to be reference-only.");
    Require(inventory.Any(static item => item.Kind == LicenseSourceKind.ExternalSecret && !item.BundledInRelease), "Expected signing secret to stay external.");
    Require(audit.CanBuildReleaseInventory, "Expected inventory to be buildable without redistributable blockers.");
    Require(audit.AttributionRequired >= 2, "Expected attribution requirements.");
    Require(audit.BlockingIssues == 0, "Expected no bundled license blockers.");
}

static void PlanDesktopAndAndroidReleasePackages()
{
    var planner = new ReleasePackagePlanner();
    var plans = planner.BuildV154Plans();
    var desktop = plans.Single(static plan => plan.Target == ReleasePlatformTarget.Desktop);
    var android = plans.Single(static plan => plan.Target == ReleasePlatformTarget.Android);

    Require(desktop.VersionName == "1.54.0-internal", "Expected desktop v1.54 version name.");
    Require(!desktop.RequiresExternalSecret, "Expected desktop plan not to require signing secret.");
    Require(android.RequiresSigning, "Expected Android signing requirement.");
    Require(android.RequiresExternalSecret, "Expected Android external secret requirement.");

    var blockedAndroid = planner.Evaluate(android, hasPublishOutput: false, hasExternalSigningSecret: false);
    Require(!blockedAndroid.CanProduceSignedArtifact, "Expected Android package to be blocked without artifact and secret.");
    Require(blockedAndroid.MissingInputs.Count == 2, "Expected missing Android artifact and signing secret.");

    var desktopReady = planner.Evaluate(desktop, hasPublishOutput: true, hasExternalSigningSecret: false);
    Require(desktopReady.CanProduceUnsignedArtifact, "Expected desktop unsigned artifact readiness.");
    Require(desktopReady.CanProduceSignedArtifact, "Expected desktop package not to need external signing.");
}

static void BuildAndroidReleaseDeviceMatrix()
{
    var matrix = new AndroidReleaseDeviceMatrix().BuildV154Matrix();

    Require(matrix.Count == 6, "Expected Android release device checks.");
    Require(matrix.Any(static check => check.Id == "ANDROID-299-INSTALL"), "Expected install check.");
    Require(matrix.Any(static check => check.Workflow == "Permissions"), "Expected permission check.");
    Require(matrix.Any(static check => check.Workflow == "Map and payload"), "Expected map/payload check.");
    Require(matrix.All(static check => !check.Complete), "Expected physical-device evidence to remain incomplete.");
    Require(matrix.All(static check => !string.IsNullOrWhiteSpace(check.Blocker)), "Expected explicit blocker text.");
}

static void DecideBrowserAndIosReleaseScope()
{
    var decisions = new PlatformScopeCatalog().BuildV154Decisions(hasBrowserProject: false, hasIosProject: false);

    Require(decisions.Count == 4, "Expected four platform scope decisions.");
    Require(decisions.Single(static decision => decision.Target == ReleasePlatformTarget.Desktop).Status == PlatformScopeDecisionStatus.InScope, "Expected desktop in scope.");
    Require(decisions.Single(static decision => decision.Target == ReleasePlatformTarget.Android).Status == PlatformScopeDecisionStatus.InScope, "Expected Android in scope.");
    Require(decisions.Single(static decision => decision.Target == ReleasePlatformTarget.Browser).Status == PlatformScopeDecisionStatus.OutOfScope, "Expected Browser out of scope without project directory.");
    Require(decisions.Single(static decision => decision.Target == ReleasePlatformTarget.Ios).Status == PlatformScopeDecisionStatus.OutOfScope, "Expected iOS out of scope without project directory.");
}

static void AuditReleaseClosureBlockers()
{
    var items = new ReleaseReadinessCatalog().BuildV154Items();
    var license = new LicenseSourceAudit().Audit(new ReleaseLicenseCatalog().BuildV154Inventory());
    var android = new AndroidReleaseDeviceMatrix().BuildV154Matrix();
    var scope = new PlatformScopeCatalog().BuildV154Decisions(hasBrowserProject: false, hasIosProject: false);
    var audit = new ReleaseClosureAudit().Audit(items, license, android, scope);

    Require(audit.RequiredItems == 8, "Expected eight release readiness items.");
    Require(audit.CompleteItems == 4, "Expected four complete release readiness items.");
    Require(audit.BlockedItems == 4, "Expected four blocked release readiness items.");
    Require(!audit.CanClaimReleaseCandidate, "Expected release candidate claim to be blocked.");
    Require(audit.OpenBlockers.Count >= 10, "Expected package/signing/device blockers.");
}

static void CatalogReleaseEvidence()
{
    var evidence = new ReleaseEvidenceCatalog().BuildV154Evidence();

    Require(evidence.Count == 8, "Expected v1.54 release evidence items.");
    Require(evidence.Any(static item => item.Id == "REL-295" && item.Complete), "Expected license evidence complete.");
    Require(evidence.Any(static item => item.Id == "REL-297" && !item.Complete), "Expected desktop artifact gap.");
    Require(evidence.Any(static item => item.Id == "REL-298" && !item.Complete), "Expected Android signing gap.");
    Require(evidence.Any(static item => item.Id == "REL-302" && item.Description.Contains("release-candidate", StringComparison.Ordinal)), "Expected release overclaim boundary.");
}

static void PreventReleaseCandidateOverclaim()
{
    var items = new ReleaseReadinessCatalog().BuildV154Items();
    var license = new LicenseSourceAudit().Audit(new ReleaseLicenseCatalog().BuildV154Inventory());
    var android = new AndroidReleaseDeviceMatrix().BuildV154Matrix();
    var scope = new PlatformScopeCatalog().BuildV154Decisions(hasBrowserProject: false, hasIosProject: false);
    var audit = new ReleaseClosureAudit().Audit(items, license, android, scope);

    Require(audit.Summary.Contains("blocked", StringComparison.Ordinal), "Expected blocked release summary.");
    Require(audit.OpenBlockers.Any(static blocker => blocker.Contains("signed APK", StringComparison.Ordinal) || blocker.Contains("signed package", StringComparison.Ordinal)), "Expected signed package blocker.");
    Require(audit.OpenBlockers.Any(static blocker => blocker.Contains("SITL", StringComparison.Ordinal)), "Expected SITL evidence blocker from release pack.");
    Require(!audit.CanClaimReleaseCandidate, "Expected no release candidate claim before external evidence.");
}

static void CatalogFinalFullPortModuleMatrix()
{
    var matrix = new FullPortModuleMatrixCatalog().BuildV155Matrix();

    Require(matrix.Count == 14, "Expected v1.55 final module assessments.");
    Require(matrix.Any(static item => item.Id == "FULL-303-VEHICLE" && item.Priority == FullPortPriority.P0), "Expected P0 vehicle assessment.");
    Require(matrix.Any(static item => item.QgcArea.Contains("MAVLink", StringComparison.Ordinal) && item.Disposition == FullPortDisposition.Partial), "Expected partial MAVLink assessment.");
    Require(matrix.Any(static item => item.Id == "FULL-306-QML" && item.Disposition == FullPortDisposition.NotApplicable), "Expected QML not-applicable decision.");
    Require(matrix.All(static item => item.CoveragePercent is >= 0 and <= 100), "Expected bounded coverage values.");
}

static void CatalogFinalFullPortEvidenceGates()
{
    var gates = new FullPortEvidenceGateCatalog().BuildV155Gates();

    Require(gates.Count == 8, "Expected v1.55 evidence gates.");
    Require(gates.Any(static gate => gate.Id == "GATE-304-TESTS" && gate.Complete), "Expected tests gate complete.");
    Require(gates.Any(static gate => gate.Kind == FullPortEvidenceKind.AndroidDevice && !gate.Complete), "Expected Android device blocker.");
    Require(gates.Any(static gate => gate.Kind == FullPortEvidenceKind.Sitl && !gate.Complete), "Expected SITL blockers.");
    Require(gates.Any(static gate => gate.Kind == FullPortEvidenceKind.ReleaseArtifact && !gate.Complete), "Expected release artifact blocker.");
}

static void RecordDeferredAndNotApplicableDecisions()
{
    var decisions = new FullPortDecisionCatalog().BuildV155Decisions();

    Require(decisions.Count == 7, "Expected v1.55 final decisions.");
    Require(decisions.Any(static decision => decision.Id == "DEC-305-QML" && decision.Disposition == FullPortDisposition.NotApplicable), "Expected QML not-applicable decision.");
    Require(decisions.Any(static decision => decision.Id == "DEC-305-MAPSUI" && decision.Disposition == FullPortDisposition.Alternative), "Expected Mapsui alternative decision.");
    Require(decisions.Any(static decision => decision.Id == "DEC-305-SITL" && decision.Disposition == FullPortDisposition.Deferred), "Expected SITL deferred decision.");
    Require(decisions.All(static decision => !string.IsNullOrWhiteSpace(decision.ReopenCondition)), "Expected reopen conditions.");
}

static void CatalogReleaseCandidateBlockers()
{
    var blockers = new FullPortReleaseBlockerCatalog().BuildV155Blockers();

    Require(blockers.Count == 7, "Expected release candidate blockers.");
    Require(blockers.Any(static blocker => blocker.Id == "BLOCK-306-DESKTOP-PUBLISH" && blocker.Priority == FullPortPriority.P0), "Expected desktop publish blocker.");
    Require(blockers.Any(static blocker => blocker.Description.Contains("Android signed", StringComparison.Ordinal)), "Expected Android signed package blocker.");
    Require(blockers.Any(static blocker => blocker.Description.Contains("PX4", StringComparison.Ordinal)), "Expected PX4 SITL blocker.");
    Require(blockers.All(static blocker => !string.IsNullOrWhiteSpace(blocker.RequiredEvidence)), "Expected required evidence text.");
}

static void AuditFinalFullPortCompletionClaims()
{
    var modules = new FullPortModuleMatrixCatalog().BuildV155Matrix();
    var gates = new FullPortEvidenceGateCatalog().BuildV155Gates();
    var blockers = new FullPortReleaseBlockerCatalog().BuildV155Blockers();
    var audit = new FullPortFinalAudit().Audit(modules, gates, blockers);

    Require(audit.AssessedModules == 14, "Expected fourteen assessed modules.");
    Require(audit.CompleteModules == 3, "Expected complete shared module group count.");
    Require(audit.PartialModules >= 7, "Expected partial module groups.");
    Require(audit.DeferredModules == 2, "Expected deferred validation/release modules.");
    Require(audit.NotApplicableModules == 2, "Expected not-applicable module groups.");
    Require(audit.WeightedCoveragePercent >= 50 && audit.WeightedCoveragePercent <= 70, "Expected final coverage to remain approximate and bounded.");
    Require(!audit.CanClaimFullPortComplete, "Expected full-port completion claim to remain blocked.");
    Require(!audit.CanClaimReleaseCandidate, "Expected release-candidate claim to remain blocked.");
}

static void CatalogFinalFullPortEvidence()
{
    var evidence = new FullPortEvidenceCatalog().BuildV155Evidence();

    Require(evidence.Count == 8, "Expected v1.55 evidence catalog items.");
    Require(evidence.All(static item => item.Complete), "Expected v1.55 audit artifacts complete.");
    Require(evidence.Any(static item => item.Id == "FULL-303"), "Expected module matrix evidence.");
    Require(evidence.Any(static item => item.Id == "FULL-310" && item.Description.Contains("without claiming", StringComparison.Ordinal)), "Expected final audit claim boundary.");
}

static void VerifyFullPortRiskRegister()
{
    var modules = new FullPortModuleMatrixCatalog().BuildV155Matrix();
    var gates = new FullPortEvidenceGateCatalog().BuildV155Gates();
    var blockers = new FullPortReleaseBlockerCatalog().BuildV155Blockers();
    var audit = new FullPortFinalAudit().Audit(modules, gates, blockers);

    Require(audit.OpenBlockers.Any(static blocker => blocker.Contains("Android", StringComparison.Ordinal)), "Expected Android blocker in final risk register.");
    Require(audit.OpenBlockers.Any(static blocker => blocker.Contains("SITL", StringComparison.Ordinal)), "Expected SITL blocker in final risk register.");
    Require(audit.OpenBlockers.Any(static blocker => blocker.Contains("release artifact", StringComparison.OrdinalIgnoreCase) || blocker.Contains("Release artifacts", StringComparison.Ordinal)), "Expected release artifact blocker.");
    Require(audit.OpenBlockers.Count >= 20, "Expected consolidated blocker list.");
}

static void PreventQgcFullPortOverclaim()
{
    var modules = new FullPortModuleMatrixCatalog().BuildV155Matrix();
    var gates = new FullPortEvidenceGateCatalog().BuildV155Gates();
    var blockers = new FullPortReleaseBlockerCatalog().BuildV155Blockers();
    var audit = new FullPortFinalAudit().Audit(modules, gates, blockers);

    Require(audit.Summary.Contains("Full-port", StringComparison.Ordinal), "Expected explicit full-port summary.");
    Require(!audit.CanClaimFullPortComplete, "Expected no QGC full-port completion claim.");
    Require(!audit.CanClaimReleaseCandidate, "Expected no release candidate claim.");
    Require(audit.OpenBlockers.Any(static blocker => blocker.Contains("real vehicle", StringComparison.OrdinalIgnoreCase)), "Expected real vehicle blocker.");
}

static void CatalogQgcReplacementPhaseEvidence()
{
    var evidence = new QgcReplacementPhaseEvidenceCatalog().Build();
    var phases = evidence.Select(static item => item.Phase).Distinct().OrderBy(static phase => (int)phase).ToArray();

    Require(phases.Length == 9, "Expected phase evidence for 314-322.");
    Require(phases.First() == QgcReplacementPhase.UiWorkflowHardening, "Expected phase 314 first.");
    Require(phases.Last() == QgcReplacementPhase.FinalReplacementAcceptance, "Expected phase 322 last.");
    Require(evidence.Any(static item => item.Id == "QGCREPL-314-UI-SHARED" && item.Status == QgcReplacementEvidenceStatus.Complete), "Expected shared UI workflow evidence.");
    Require(evidence.Any(static item => item.Id == "QGCREPL-314-QML-INVENTORY" && item.RequiredEvidence == QgcReplacementEvidenceLevel.Static && item.Status == QgcReplacementEvidenceStatus.Complete), "Expected QGC QML inventory evidence.");
    Require(evidence.Any(static item => item.Id == "QGCREPL-315-MAP-RUNTIME" && item.RequiredEvidence == QgcReplacementEvidenceLevel.Unit), "Expected map runtime evidence.");
    Require(evidence.Any(static item => item.Id == "QGCREPL-316-STREAM" && item.Status == QgcReplacementEvidenceStatus.Complete), "Expected payload stream boundary evidence complete.");
    Require(evidence.Any(static item => item.Id == "QGCREPL-320-PX4" && item.RequiredEvidence == QgcReplacementEvidenceLevel.Sitl && item.Status == QgcReplacementEvidenceStatus.Complete), "Expected PX4 SITL evidence complete.");
    Require(evidence.Any(static item => item.Id == "QGCREPL-321-ANDROID-SIGNED" && item.RequiredArtifacts.Any(static artifact => artifact.Contains("signed", StringComparison.OrdinalIgnoreCase))), "Expected Android signed artifact requirement.");
}

static void AuditQgcReplacementAcceptanceBlockers()
{
    var evidence = new QgcReplacementPhaseEvidenceCatalog().Build();
    var audit = new QgcReplacementAcceptanceAudit().Audit(evidence);

    Require(audit.TotalItems == evidence.Count, "Expected audit to cover every evidence item.");
    Require(audit.PhaseStatuses.Count == 9, "Expected phase statuses for 314-322.");
    Require(audit.CompleteItems > 0, "Expected some shared evidence complete.");
    Require(audit.BlockedItems > audit.DeferredItems, "Expected explicit blockers rather than silent deferrals.");
    Require(!audit.CanClaimQgcReplacement, "Expected QGC replacement claim to remain blocked.");
    Require(!audit.CanClaimReleaseCandidate, "Expected release-candidate claim to remain blocked.");
    Require(audit.OpenBlockers.Any(static blocker => blocker.Contains("real vehicle", StringComparison.OrdinalIgnoreCase)), "Expected real vehicle blocker.");
    Require(audit.OpenBlockers.Any(static blocker => blocker.Contains("SITL", StringComparison.Ordinal)), "Expected SITL blocker.");
    Require(audit.OpenBlockers.Any(static blocker => blocker.Contains("Release evidence pack", StringComparison.Ordinal)), "Expected release evidence pack blocker.");
}

static void PlanQgcReplacementEvidencePack()
{
    var evidence = new QgcReplacementPhaseEvidenceCatalog().Build();
    var pack = new QgcReplacementEvidencePackPlanner().Build(evidence);

    Require(pack.Count == 9, "Expected one evidence pack entry per phase.");
    Require(pack.All(static entry => entry.Document.EndsWith("VERIFICATION.md", StringComparison.Ordinal)), "Expected phase verification documents.");
    Require(pack.Any(static entry => entry.Phase == QgcReplacementPhase.AndroidNativePlatformParity
        && entry.VerificationCommands.Contains("dotnet build VGC.Android")), "Expected Android build command for phase 318.");
    Require(pack.Any(static entry => entry.Phase == QgcReplacementPhase.ReleaseCandidatePackaging
        && entry.VerificationCommands.Contains("dotnet build VGC.Desktop")
        && entry.VerificationCommands.Contains("dotnet build VGC.Android")), "Expected desktop and Android build commands for phase 321.");
    Require(pack.Any(static entry => entry.Phase == QgcReplacementPhase.SitlValidationExecution
        && entry.RequiredExternalArtifacts.Any(static artifact => artifact.Contains("PX4", StringComparison.Ordinal))), "Expected PX4 external artifact requirement.");
    Require(pack.Any(static entry => entry.Phase == QgcReplacementPhase.FinalReplacementAcceptance
        && entry.RequiredExternalArtifacts.Any(static artifact => artifact.Contains("final audit", StringComparison.OrdinalIgnoreCase))), "Expected final acceptance audit artifact requirement.");
}

static void AuditQgcReplacementFinalState()
{
    var qmlInventory = new QgcQmlParityCatalog().Build();
    var runtimeEvidence = new Gate11RuntimeEvidenceCatalog().Build();
    var replacementEvidence = new QgcReplacementPhaseEvidenceCatalog().Build();
    var replacement = new QgcReplacementAcceptanceAudit().Audit(replacementEvidence);
    var result = new QgcReplacementFinalAudit().Audit(qmlInventory, runtimeEvidence, replacement);

    Require(result.TotalQgcQmlFiles == 442, "Expected final audit to cover all QGC QML files.");
    Require(result.MappedModules > 0, "Expected mapped QGC QML modules.");
    Require(result.MigratedModules > 0, "Expected migrated QGC QML modules.");
    Require(result.DeferredModules == 1, "Expected Viewer3D to remain deferred.");
    Require(result.CompleteModules == 0, "Expected no complete QML parity modules.");
    Require(result.RuntimeEvidenceBlockers > 0, "Expected runtime evidence blockers.");
    Require(result.ReplacementEvidenceBlockedItems > 0, "Expected replacement evidence blockers.");
    Require(result.ReplacementEvidenceDeferredItems > 0, "Expected deferred replacement evidence.");
    Require(!result.CanClaimQmlUiParity, "Expected no QML UI parity claim.");
    Require(!result.CanClaimQgcReplacement, "Expected no QGC replacement claim.");
    Require(!result.CanClaimReleaseCandidate, "Expected no release candidate claim.");
    Require(!result.AndroidWorkloadBlocked, "Expected Android workload blocker to be cleared by emulator build evidence.");
    Require(result.OpenBlockers.Any(static blocker => blocker.Contains("ArduPilot SITL transcript", StringComparison.OrdinalIgnoreCase)), "Expected ArduPilot SITL blocker text.");
}

static void CatalogScreenshotParityEvidence()
{
    var targets = new ScreenshotParityEvidenceCatalog().BuildFlyPlanTargets();
    var manifest = new ScreenshotParityManifestBuilder().Build(targets);

    Require(targets.Count == 4, "Expected Fly/Plan screenshot parity targets.");
    Require(targets.Any(static target => target.Id == "SHOT-FLY-TOOLBAR" && target.Status == ScreenshotParityStatus.Pending), "Expected Fly toolbar screenshot target.");
    Require(targets.Any(static target => target.Id == "SHOT-PLAN-EDITOR" && target.ArtifactPath.Contains("plan-editor", StringComparison.Ordinal)), "Expected Plan editor artifact path.");
    Require(manifest.CapturedCount == 0, "Expected no captured screenshots in scaffold.");
    Require(manifest.PendingCount == targets.Count, "Expected all screenshot targets pending.");
    Require(manifest.MissingArtifacts.Count == targets.Count, "Expected missing screenshot artifact manifest.");
    Require(manifest.CaptureInstructions.Count == targets.Count, "Expected capture instructions for every screenshot target.");

    var exporter = new ScreenshotParityEvidenceExporter();
    var markdown = exporter.ExportFlyPlanManifestMarkdown();
    Require(markdown.Contains("SHOT-FLY-TOOLBAR", StringComparison.Ordinal), "Expected exported Fly screenshot target.");
    Require(markdown.Contains("Capture instructions", StringComparison.Ordinal), "Expected capture instructions section.");

    var outputPath = Path.Combine(Path.GetTempPath(), "vgc-screenshot-manifest.md");
    var writtenPath = exporter.WriteFlyPlanManifest(outputPath);
    Require(File.Exists(writtenPath), "Expected screenshot manifest file to be written.");
    Require(File.ReadAllText(writtenPath).Contains("SHOT-PLAN-EDITOR", StringComparison.Ordinal), "Expected written screenshot manifest content.");
    File.Delete(writtenPath);
}

static void CatalogQmlParitySubEvidence()
{
    var catalog = new QgcQmlParitySubEvidenceCatalog();
    var items = catalog.BuildFlyPlanFlightMap();
    var audit = new QgcQmlParitySubEvidenceAudit();

    Require(items.Count == 12, "Expected Fly/Plan/FlightMap sub evidence items.");
    Require(items.Any(static item => item.Module == "FlyView" && item.Category == "android" && item.Status == QgcParitySubEvidenceStatus.Complete), "Expected FlyView Android sub evidence complete.");
    Require(items.Any(static item => item.Module == "PlanView" && item.Category == "command" && item.Status == QgcParitySubEvidenceStatus.Blocked), "Expected PlanView command sub evidence blocked.");
    Require(items.Any(static item => item.Module == "FlightMap" && item.Category == "command" && item.Status == QgcParitySubEvidenceStatus.Skipped), "Expected FlightMap command sub evidence skipped.");
    Require(catalog.BuildBlockerText("FlyView").Contains("screenshot", StringComparison.Ordinal), "Expected FlyView blocker text from sub evidence.");
    Require(audit.OpenBlockers(items).Any(static blocker => blocker.Contains("PlanView/command", StringComparison.Ordinal)), "Expected PlanView command blocker export.");

    var outputPath = Path.Combine(Path.GetTempPath(), "vgc-qml-sub-evidence.md");
    var writtenPath = new QgcQmlParitySubEvidenceFileWriter().WriteMarkdown(outputPath);
    Require(File.Exists(writtenPath), "Expected QML sub evidence file to be written.");
    Require(File.ReadAllText(writtenPath).Contains("FlightMap | sitl", StringComparison.Ordinal), "Expected QML sub evidence markdown content.");
    File.Delete(writtenPath);
}

static void CatalogQgcSourcePortInventory()
{
    var items = new QgcSourcePortInventoryCatalog().Build();

    Require(items.Count >= 20, "Expected detailed QGC source inventory areas.");
    Require(items.Sum(static item => item.QmlFiles) == 442, "Expected QGC QML source count to match source tree.");
    Require(items.Sum(static item => item.CppFiles) == 497, "Expected QGC C++ source count to match source tree.");
    Require(items.Sum(static item => item.HeaderFiles) == 571, "Expected QGC header source count to match source tree.");
    Require(items.Any(static item => item.QgcArea == "Vehicle" && item.QgcSource == "src/Vehicle" && item.VgcTarget.Contains("VGC/Vehicles", StringComparison.Ordinal)), "Expected Vehicle area mapped from QGC vehicle runtime.");
    Require(items.Any(static item => item.QgcArea == "MissionManager" && item.QgcSource == "src/MissionManager" && item.VgcTarget.Contains("VGC/Mission", StringComparison.Ordinal)), "Expected MissionManager mapped from QGC mission runtime.");
    Require(items.Any(static item => item.QgcArea == "Comms" && item.QgcSource == "src/Comms"), "Expected QGC Comms source path.");
    Require(items.Any(static item => item.QgcArea == "MAVLink" && item.QgcSource.Contains("MAVLinkProtocol", StringComparison.Ordinal)), "Expected QGC MAVLink protocol source path.");
    Require(items.Any(static item => item.QgcArea == "Viewer3D" && item.QgcSource == "src/Viewer3D" && item.Status == QgcSourceAreaStatus.Deferred), "Expected Viewer3D deferred.");
    Require(items.Any(static item => item.QgcArea == "Android" && item.QgcSource.Contains("android/src", StringComparison.Ordinal) && item.Status == QgcSourceAreaStatus.Blocked), "Expected Android blocker.");
}

static void AuditQgcSourcePortProgress()
{
    var result = new QgcSourcePortAudit().Audit(new QgcSourcePortInventoryCatalog().Build());

    Require(result.TotalQmlFiles == 442, "Expected source audit QML count.");
    Require(result.TotalCppFiles == 497, "Expected source audit C++ count.");
    Require(result.TotalHeaderFiles == 571, "Expected source audit header count.");
    Require(result.PartialAreas > 0, "Expected partial source areas.");
    Require(result.BlockedAreas > 0, "Expected blocked source areas.");
    Require(result.DeferredAreas > 0, "Expected deferred source areas.");
    Require(result.WeightedCoveragePercent is > 0 and < 100, "Expected weighted coverage below full parity.");
    Require(result.OpenBlockers.Any(static blocker => blocker.Contains("SITL", StringComparison.OrdinalIgnoreCase)), "Expected SITL blocker in source audit.");
}

static void PreventQgcSourcePortOverclaim()
{
    var result = new QgcSourcePortAudit().Audit(new QgcSourcePortInventoryCatalog().Build());

    Require(!result.CanClaimQgcSourceParity, "Expected no QGC source parity claim.");
    Require(!result.CanClaimQgcReplacement, "Expected no QGC replacement claim from source inventory.");
    Require(!result.CanClaimReleaseCandidate, "Expected no release candidate claim from source inventory.");
}

static void MatchQgcSourceInventoryWithQmlCatalog()
{
    var source = new QgcSourcePortAudit().Audit(new QgcSourcePortInventoryCatalog().Build());
    var qml = new QgcQmlParityAudit().Audit(new QgcQmlParityCatalog().Build());

    Require(source.TotalQmlFiles == qml.TotalQmlFiles, "Expected source inventory QML total to match QML parity catalog.");
    Require(!qml.CanClaimQmlUiParity, "Expected QML parity still blocked.");
    Require(!source.CanClaimQgcReplacement, "Expected source inventory not to override QML blockers.");
}

static void CatalogVehicleSubManagerParity()
{
    var items = new VehicleSubManagerParityCatalog().BuildPhase323();
    var audit = new VehicleSubManagerParityAudit().Audit(items);

    Require(items.Count == 10, "Expected Phase 323 vehicle sub-manager parity items.");
    Require(items.Any(static item => item.Id == "VEH323-LINK" && item.Status == VehicleSubManagerParityStatus.Complete), "Expected link manager complete.");
    Require(items.Any(static item => item.Id == "VEH323-FACT-ESC" && item.Status == VehicleSubManagerParityStatus.Blocked), "Expected ESC fact group blocker.");
    Require(items.Any(static item => item.Id == "VEH323-OBJECT-AVOIDANCE" && item.Priority == VehicleSubManagerPriority.P1), "Expected object avoidance P1 item.");
    Require(!audit.CanClaimVehicleParity, "Expected vehicle parity claim to remain blocked.");
    Require(audit.OpenBlockers.Any(static blocker => blocker.Contains("Component information", StringComparison.Ordinal)), "Expected component information blocker.");
}

static void CatalogMavlinkDialectGenerationClosure()
{
    var items = new MavlinkDialectGenerationCatalog().BuildPhase324();
    var audit = new MavlinkDialectGenerationAudit().Audit(items);

    Require(items.Count == 5, "Expected Phase 324 dialect generation items.");
    Require(items.Any(static item => item.Id == "MAV324-XML-LOADER" && item.Status == MavlinkDialectGenerationStatus.Partial), "Expected XML loader partial.");
    Require(items.Any(static item => item.Id == "MAV324-ENUMS" && item.Status == MavlinkDialectGenerationStatus.Blocked), "Expected enum metadata blocker.");
    Require(!audit.CanClaimDialectWideGeneration, "Expected dialect-wide generation claim blocked.");
    Require(audit.OpenBlockers.Any(static blocker => blocker.Contains("ardupilotmega", StringComparison.Ordinal)), "Expected ardupilotmega missing input.");
}

static void CatalogMavlinkRuntimeAdoptionClosure()
{
    var items = new MavlinkRuntimeAdoptionCatalog().BuildPhase325();
    var blockers = new MavlinkRuntimeAdoptionAudit().OpenBlockers(items);

    Require(items.Count == 7, "Expected Phase 325 runtime adoption items.");
    Require(items.Any(static item => item.Area == MavlinkRuntimeServiceArea.Ftp && item.Status == MavlinkRuntimeAdoptionStatus.Complete), "Expected FTP adoption complete.");
    Require(items.Any(static item => item.Area == MavlinkRuntimeServiceArea.Signing && item.Status == MavlinkRuntimeAdoptionStatus.Blocked), "Expected signing adoption blocked.");
    Require(blockers.Any(static blocker => blocker.Contains("Camera", StringComparison.Ordinal)), "Expected camera runtime adoption blocker.");
}

static void CatalogFirmwareSetupProductionParity()
{
    var items = new FirmwareSetupProductionCatalog().BuildPhase326();
    var blockers = new FirmwareSetupProductionAudit().OpenBlockers(items);

    Require(items.Count == 6, "Expected Phase 326 firmware setup production items.");
    Require(items.Any(static item => item.Id == "FW326-MOTOR-SAFETY" && item.ArduPilotStatus == FirmwareSetupProductionStatus.Blocked), "Expected ArduPilot motor safety blocker.");
    Require(items.Any(static item => item.Id == "FW326-METADATA-DRIFT" && item.Px4Status == FirmwareSetupProductionStatus.Blocked), "Expected metadata drift blocker.");
    Require(blockers.Any(static blocker => blocker.Contains("real accel", StringComparison.OrdinalIgnoreCase)), "Expected sensor calibration runtime evidence blocker.");
}

static void CatalogDesktopUiRuntimeEvidence()
{
    var requirements = new DesktopUiRuntimeEvidenceCatalog().BuildPhase327();
    var audit = new DesktopUiRuntimeEvidenceAudit().Audit(requirements);

    Require(requirements.Count == 6, "Expected Phase 327 desktop UI workflows.");
    Require(audit.SharedModelCompleteItems == requirements.Count, "Expected all shared UI models complete.");
    Require(audit.RuntimeEvidenceCompleteItems == requirements.Count, "Expected desktop runtime evidence recorded for all desktop workflows.");
    Require(audit.MissingRuntimeEvidence.Any(static item => item.Contains("FlyView", StringComparison.Ordinal)), "Expected FlyView screenshot requirement.");
}

static void CatalogGate11RuntimeEvidenceWithoutParityOverclaim()
{
    var evidence = new Gate11RuntimeEvidenceCatalog().Build();
    var qmlAudit = new QgcQmlParityAudit().Audit(new QgcQmlParityCatalog().Build());

    Require(evidence.Any(static item => item.Id == "GATE11-DESKTOP-BUILD" && item.Complete && item.Status == Gate11RuntimeEvidenceStatus.Complete), "Expected desktop build evidence complete.");
    Require(evidence.Any(static item => item.Id == "GATE11-ANDROID-WORKLOAD" && item.Complete && item.Status == Gate11RuntimeEvidenceStatus.Complete), "Expected Android workload evidence complete.");
    Require(evidence.Any(static item => item.Id == "GATE11-DESKTOP-RUNTIME" && item.Complete && item.Status == Gate11RuntimeEvidenceStatus.Complete), "Expected desktop runtime evidence complete.");
    Require(evidence.Any(static item => item.Id == "GATE11-DESKTOP-SCREENSHOT" && !item.Complete), "Expected screenshot parity to remain incomplete.");
    Require(evidence.Any(static item => item.Id == "GATE11-SITL" && item.Complete), "Expected SITL TCP heartbeat evidence complete.");
    Require(evidence.Any(static item => item.Id == "GATE11-ANDROID-DEVICE" && item.Complete), "Expected Android emulator launch evidence complete.");
    Require(!qmlAudit.CanClaimQmlUiParity, "Expected runtime evidence gate not to claim QML parity.");
    Require(!qmlAudit.CanClaimQgcReplacement, "Expected runtime evidence gate not to claim QGC replacement.");
}

static void CatalogAndroidNativeIntegrationClosure()
{
    var items = new AndroidNativeIntegrationCatalog().BuildPhase328();
    var blockers = new AndroidNativeIntegrationAudit().OpenBlockers(items);

    Require(items.Count == 5, "Expected Phase 328 Android native integration items.");
    Require(items.Any(static item => item.Id == "AND328-USB" && item.Status == AndroidNativeIntegrationStatus.SharedModelOnly), "Expected USB model-only status.");
    Require(items.Any(static item => item.Id == "AND328-PACKAGING" && item.Status == AndroidNativeIntegrationStatus.Blocked), "Expected Android package blocker.");
    Require(blockers.Any(static blocker => blocker.Contains("USB permission", StringComparison.Ordinal)), "Expected USB permission evidence blocker.");
}

static void CatalogMapProductionRuntimeClosure()
{
    var items = new MapProductionRuntimeCatalog().BuildPhase329();
    var missing = new MapProductionRuntimeAudit().MissingEvidence(items);

    Require(items.Count == 5, "Expected Phase 329 map production items.");
    Require(items.Any(static item => item.Id == "MAP329-ATTRIBUTION" && item.CoveredCapabilities.Contains("visible attribution decision")), "Expected attribution coverage.");
    Require(items.Any(static item => item.Id == "MAP329-ANDROID" && item.Status == MapProductionRuntimeStatus.Blocked), "Expected Android map runtime blocker.");
    Require(missing.Any(static evidence => evidence.Contains("persistent disk cache", StringComparison.Ordinal)), "Expected persistent cache evidence gap.");
}

static void CatalogVideoPayloadProductionPipeline()
{
    var items = new VideoPayloadProductionCatalog().BuildPhase330();
    var missing = new VideoPayloadProductionAudit().MissingEvidence(items);

    Require(items.Count == 6, "Expected Phase 330 video payload production items.");
    Require(items.Any(static item => item.Id == "VID330-RTSP-UDP" && item.Status == VideoPayloadProductionStatus.SharedModelOnly), "Expected RTSP/UDP synthetic decode evidence without real stream claim.");
    Require(items.Any(static item => item.Id == "VID330-GIMBAL" && item.CoveredCapabilities.Contains("ROI command")), "Expected gimbal ROI coverage.");
    Require(missing.Any(static evidence => evidence.Contains("real RTSP", StringComparison.Ordinal)), "Expected real RTSP evidence gap.");
}

static void CatalogAnalyzeLogsUtilitiesCompletion()
{
    var items = new AnalyzeLogsUtilitiesCompletionCatalog().BuildPhase331();
    var missing = new AnalyzeLogsUtilitiesCompletionAudit().MissingEvidence(items);

    Require(items.Count == 6, "Expected Phase 331 analyze/log utility items.");
    Require(items.Any(static item => item.Id == "AN331-CONSOLE" && item.Status == AnalyzeUtilityCompletionStatus.Complete), "Expected console complete.");
    Require(items.Any(static item => item.Id == "AN331-UTILITIES" && item.Status == AnalyzeUtilityCompletionStatus.Blocked), "Expected utilities triage blocker.");
    Require(missing.Any(static evidence => evidence.Contains("PX4 onboard log", StringComparison.Ordinal)), "Expected PX4 log evidence gap.");
}

static void MockPayloadServiceBoundaries()
{
    var payload = new PayloadServiceSet(
        new FakeVideoService(),
        new FakeCameraService(),
        new FakeGimbalService());

    var streams = payload.Video.DiscoverStreamsAsync().GetAwaiter().GetResult();
    var videoState = payload.Video.GetStateAsync().GetAwaiter().GetResult();
    var cameraStatus = payload.Camera.GetStatusAsync().GetAwaiter().GetResult();
    var gimbal = payload.Gimbal.GetAttitudeAsync().GetAwaiter().GetResult();

    Require(streams.Count == 1, "Expected one mock video stream.");
    Require(streams[0].Protocol == VideoStreamProtocol.Rtsp, "Expected RTSP stream protocol.");
    Require(videoState.IsStreaming, "Expected mock stream to be active.");
    Require(cameraStatus.IsReady, "Expected mock camera to be ready.");
    Require(!cameraStatus.IsRecordingVideo, "Expected mock camera not recording by default.");
    Require(Math.Abs(gimbal.PitchDegrees + 10) < 0.001, "Expected mock gimbal pitch.");
}

static void SelectVideoStreamRuntimeState()
{
    var front = new VideoStreamDescriptor(
        "front",
        "Front Camera",
        new Uri("rtsp://127.0.0.1/front"),
        VideoStreamProtocol.Rtsp,
        "h264");
    var thermal = new VideoStreamDescriptor(
        "thermal",
        "Thermal Camera",
        new Uri("udp://239.0.0.1:5600"),
        VideoStreamProtocol.Udp,
        "h265");
    var duplicateFront = front with { Name = "Duplicate Front" };
    var runtime = new VideoStreamRuntimeController();

    var empty = runtime.LoadStreams([]);
    Require(empty.Status == VideoStreamRuntimeStatus.Unavailable, "Expected empty stream catalog to be unavailable.");
    Require(empty.SelectedStream is null, "Expected no selected stream for empty catalog.");

    var loaded = runtime.LoadStreams([front, thermal, duplicateFront], preferredStreamId: "thermal");
    Require(loaded.Streams.Count == 2, "Expected duplicate stream ids to be collapsed.");
    Require(loaded.SelectedStream == thermal, "Expected preferred stream selection.");
    Require(loaded.Status == VideoStreamRuntimeStatus.Stopped, "Expected loaded stream to start stopped.");

    var connecting = runtime.StartConnecting();
    Require(connecting.Status == VideoStreamRuntimeStatus.Connecting, "Expected connecting video state.");

    var streaming = runtime.MarkStreaming();
    Require(streaming.Status == VideoStreamRuntimeStatus.Streaming, "Expected streaming video state.");
    Require(streaming.StatusText == "Video streaming", "Expected streaming status text.");

    var selected = runtime.SelectStream("front");
    Require(selected.SelectedStream == front, "Expected front stream selection.");
    Require(selected.Status == VideoStreamRuntimeStatus.Stopped, "Expected stream selection to stop active playback.");

    var missing = runtime.SelectStream("missing");
    Require(missing.Status == VideoStreamRuntimeStatus.Error, "Expected missing stream selection to fail.");
    Require(missing.Error?.Contains("missing", StringComparison.OrdinalIgnoreCase) == true, "Expected missing stream error text.");

    var recovered = runtime.ApplyServiceState(new VideoStreamState(thermal, IsStreaming: true));
    Require(recovered.SelectedStream == thermal, "Expected service state to select active stream.");
    Require(recovered.Status == VideoStreamRuntimeStatus.Streaming, "Expected service streaming state.");

    var failed = runtime.ApplyServiceState(new VideoStreamState(thermal, IsStreaming: false, Error: "decoder unavailable"));
    Require(failed.Status == VideoStreamRuntimeStatus.Error, "Expected service error state.");
    Require(failed.StatusText == "Video error: decoder unavailable", "Expected service error status text.");
}

static void TrackCameraRuntimeCommands()
{
    var runtime = new CameraRuntimeController();

    var noCamera = runtime.BeginImageCapture();
    Require(noCamera.ImageCapture.Status == PayloadCommandStatus.Failed, "Expected capture to fail without ready camera.");
    Require(noCamera.ImageCapture.CanRetry, "Expected capture failure to allow retry.");
    Require(noCamera.ReadyText == "No camera", "Expected no camera ready text.");

    var readyStatus = new CameraStatus(
        SystemId: 1,
        ComponentId: 100,
        IsReady: true,
        IsCapturingImage: false,
        IsRecordingVideo: false,
        Mode: "Photo");
    var ready = runtime.ApplyStatus(
        readyStatus,
        new CameraStorageState(IsAvailable: true, FreeBytes: 5L * 1024L * 1024L * 1024L));
    Require(ready.IsReady, "Expected camera ready state.");
    Require(ready.ModeText == "Photo", "Expected camera mode text.");
    Require(ready.Storage.StatusText == "Storage available | 5.0 GB free", "Expected formatted storage text.");

    var capturePending = runtime.RetryImageCapture();
    Require(capturePending.ImageCapture.Status == PayloadCommandStatus.Pending, "Expected retried capture to be pending.");
    Require(capturePending.ImageCapture.AttemptCount == 1, "Expected first capture attempt.");

    var captureComplete = runtime.CompleteImageCapture();
    Require(captureComplete.ImageCapture.Status == PayloadCommandStatus.Succeeded, "Expected capture success.");

    var recordPending = runtime.BeginVideoRecordingCommand();
    Require(recordPending.VideoRecording.Status == PayloadCommandStatus.Pending, "Expected recording command pending.");

    var recordFailed = runtime.FailVideoRecordingCommand("camera busy");
    Require(recordFailed.VideoRecording.Status == PayloadCommandStatus.Failed, "Expected recording failure.");
    Require(recordFailed.VideoRecording.CanRetry, "Expected recording retry to be available.");
    Require(recordFailed.RecordingText == "Video recording failed: camera busy", "Expected recording failure text.");

    var recordRetry = runtime.RetryVideoRecordingCommand();
    Require(recordRetry.VideoRecording.Status == PayloadCommandStatus.Pending, "Expected recording retry pending.");
    Require(recordRetry.VideoRecording.AttemptCount == 2, "Expected second recording attempt.");

    var capturingStatus = readyStatus with { IsCapturingImage = true, IsRecordingVideo = true };
    var active = runtime.ApplyStatus(capturingStatus);
    Require(active.CaptureText == "Capturing image", "Expected active capture status text.");
    Require(active.RecordingText == "Recording video", "Expected active recording status text.");
}

static void TrackGimbalRuntimeCommands()
{
    var runtime = new GimbalRuntimeController();

    var empty = runtime.State;
    Require(!empty.HasAttitude, "Expected no initial gimbal attitude.");
    Require(empty.AttitudeText == "No gimbal attitude", "Expected empty attitude text.");

    var attitude = runtime.ApplyAttitude(new GimbalAttitude(PitchDegrees: -12.5, RollDegrees: 1.5, YawDegrees: 45, IsLocked: false));
    Require(attitude.HasAttitude, "Expected gimbal attitude.");
    Require(attitude.AttitudeText == "Pitch -12.5 | Yaw 45.0 | Roll 1.5", "Expected attitude text.");
    Require(attitude.LockText == "Gimbal free", "Expected unlocked text.");

    var command = new GimbalCommand(PitchDegrees: -30, YawDegrees: 90, LockYaw: true);
    var pending = runtime.BeginSetAttitude(command);
    Require(pending.Target == command, "Expected target command.");
    Require(pending.SetAttitudeCommand.Status == PayloadCommandStatus.Pending, "Expected pending gimbal command.");
    Require(pending.IsLocked, "Expected target lock to project locked state.");
    Require(pending.TargetText == "Target pitch -30.0 | yaw 90.0", "Expected target text.");

    var failed = runtime.FailSetAttitude("gimbal rejected target");
    Require(failed.SetAttitudeCommand.Status == PayloadCommandStatus.Failed, "Expected gimbal failure.");
    Require(failed.SetAttitudeCommand.CanRetry, "Expected gimbal retry availability.");
    Require(failed.CommandText == "Gimbal attitude failed: gimbal rejected target", "Expected gimbal error text.");

    var retry = runtime.RetrySetAttitude();
    Require(retry.SetAttitudeCommand.Status == PayloadCommandStatus.Pending, "Expected gimbal retry pending.");
    Require(retry.SetAttitudeCommand.AttemptCount == 2, "Expected second gimbal attempt.");

    var complete = runtime.CompleteSetAttitude();
    Require(complete.SetAttitudeCommand.Status == PayloadCommandStatus.Succeeded, "Expected gimbal command success.");

    var cleared = runtime.ClearTarget();
    Require(cleared.Target is null, "Expected cleared gimbal target.");
    Require(cleared.SetAttitudeCommand.Status == PayloadCommandStatus.Idle, "Expected cleared gimbal command state.");
}

static void SelectVideoBackendDecision()
{
    var decision = new VideoBackendDecisionService().Decide(
        requireUdp: true,
        requireAndroid: true,
        preferBundledBinaries: true);

    Require(decision.Selected.Kind == VideoBackendKind.FFmpeg, "Expected FFmpeg boundary when bundled UDP Android support is requested.");
    Require(decision.Candidates.All(static option => option.SupportsAndroid && option.SupportsUdp), "Expected filtered candidates.");
    Require(decision.DeferredRisks.Any(static risk => risk.Contains("license", StringComparison.OrdinalIgnoreCase)), "Expected license risk.");
    Require(decision.DeferredRisks.Any(static risk => risk.Contains("physical-device", StringComparison.OrdinalIgnoreCase)), "Expected Android device evidence risk.");
}

static void ModelVideoDecodePipeline()
{
    var stream = new VideoStreamDescriptor(
        "udp-main",
        "UDP Main",
        new Uri("udp://239.0.0.1:5600"),
        VideoStreamProtocol.Udp,
        "h264");
    var runtime = new VideoDecodePipelineRuntime(new VideoDecodePipelineConfig(
        stream,
        VideoBackendKind.GStreamer,
        "h264",
        HardwareAcceleration: true));

    Require(runtime.Open().State == VideoDecodePipelineState.Opening, "Expected decode pipeline opening.");
    var frame = runtime.ReportFrame(TimeSpan.FromMilliseconds(120));
    Require(frame.State == VideoDecodePipelineState.Decoding, "Expected decoding state.");
    Require(frame.DecodedFrames == 1, "Expected decoded frame count.");
    Require(frame.IsHealthy, "Expected fresh frame to be healthy.");

    var stalled = runtime.MarkStalled(TimeSpan.FromSeconds(3));
    Require(stalled.State == VideoDecodePipelineState.Stalled, "Expected stalled state.");
    Require(!stalled.IsHealthy, "Expected stalled pipeline to be unhealthy.");

    var failed = runtime.Fail("decoder missing");
    Require(failed.Error == "decoder missing", "Expected decode failure error.");
    Require(runtime.Close().State == VideoDecodePipelineState.Closed, "Expected closed pipeline.");
}

static void DecodeSyntheticVideoFrames()
{
    var decoder = new SyntheticVideoDecoder(frameCount: 4, width: 8, height: 6);
    var pipeline = new VideoDecodePipeline(decoder);
    var received = 0;
    pipeline.FrameReceived += (_, frame) =>
    {
        received++;
        Require(frame.Width == 8 && frame.Height == 6, "Expected synthetic frame dimensions.");
        Require(frame.PixelFormat == "RGB24", "Expected synthetic RGB frame format.");
    };

    pipeline.StartAsync("udp://127.0.0.1:5600", VideoProtocol.Udp).GetAwaiter().GetResult();
    var snapshot = pipeline.Snapshot;

    Require(snapshot.State == VideoDecoderState.Decoding, "Expected synthetic decoder pipeline to decode.");
    Require(snapshot.Statistics.FrameCount == 4, "Expected synthetic frame count in pipeline statistics.");
    Require(received == 4, "Expected synthetic frames to be emitted.");
    pipeline.StopAsync().GetAwaiter().GetResult();
}

static void ModelUvcDeviceRuntime()
{
    var format = new UvcVideoFormat(1920, 1080, 30, "MJPEG");
    var device = new UvcDeviceDescriptor(
        "usb-1",
        "USB Camera",
        [format],
        RequiresPermission: true);
    var runtime = new UvcDeviceRuntime(device);

    Require(runtime.State.State == UvcDeviceState.Discovered, "Expected discovered UVC device.");
    Require(runtime.RequestPermission().State == UvcDeviceState.PermissionRequired, "Expected permission-required state.");
    Require(runtime.GrantPermission().State == UvcDeviceState.Ready, "Expected ready UVC state.");
    Require(runtime.Start(format).State == UvcDeviceState.Streaming, "Expected UVC streaming state.");
    Require(runtime.State.SelectedFormat == format, "Expected selected UVC format.");
    Require(runtime.Disconnect().State == UvcDeviceState.Disconnected, "Expected UVC disconnect state.");
}

static void ProjectVideoDisplayLayout()
{
    var stream = new VideoStreamDescriptor(
        "front",
        "Front Camera",
        new Uri("rtsp://127.0.0.1/front"),
        VideoStreamProtocol.Rtsp,
        "h264");
    var runtime = new VideoStreamRuntimeController();
    runtime.LoadStreams([stream], "front");
    var state = runtime.MarkStreaming();
    var projector = new VideoDisplayLayoutProjector();

    var pip = projector.Project(state, preferPip: true);
    Require(pip.IsPictureInPicture, "Expected picture-in-picture layout.");
    Require(pip.Placement == "BottomRight", "Expected deterministic PiP placement.");
    Require(Math.Abs(pip.WidthRatio - 0.32) < 0.001, "Expected PiP width ratio.");

    var full = projector.Project(state, preferPip: false);
    Require(full.Mode == VideoDisplayMode.FullFrame, "Expected full-frame layout.");
    Require(full.ActiveStreamName == "Front Camera", "Expected stream name in display layout.");
}

static void PlanPayloadMediaOutput()
{
    var planner = new PayloadMediaOutputPlanner();
    var createdAt = new DateTimeOffset(2026, 6, 26, 12, 30, 0, TimeSpan.Zero);
    var plan = planner.Plan(
        new PayloadStorageRequest(PayloadMediaKind.Recording, "Flight 01", "MP4", ExpectedBytes: 1024),
        new PayloadStoragePolicy(
            PayloadStoragePlatform.Android,
            PayloadStorageLocationKind.MediaStore,
            "MediaStore/Videos",
            RequiresUserConsent: false,
            AllowsOverwrite: false,
            RequiresScopedStorage: true),
        createdAt);

    Require(plan.StoragePlan.RelativeFileName == "Flight_01.mp4", "Expected safe media filename.");
    Require(plan.MediaId.Contains("20260626123000"), "Expected timestamped media id.");
    Require(plan.IsAndroidScopedStorageSafe, "Expected Android scoped-storage-safe output plan.");
    Require(!plan.StoragePlan.RequiresPrompt, "Expected no prompt for clean scoped storage plan.");
}

static void ProjectThermalStreamMetadata()
{
    var metadata = new ThermalStreamMetadata(
        "thermal",
        ThermalPalette.Ironbow,
        MinTemperatureC: -5.5,
        MaxTemperatureC: 120.3,
        Radiometric: true);
    var ui = new ThermalStreamProjector().Project(metadata);

    Require(metadata.IsValid, "Expected valid thermal range.");
    Require(ui.StreamId == "thermal", "Expected stream id.");
    Require(ui.PaletteText == "Ironbow", "Expected palette text.");
    Require(ui.TemperatureRangeText == "-5.5-120.3 C", "Expected formatted thermal range.");
    Require(ui.HasRadiometricData, "Expected radiometric metadata flag.");
}

static void ValidateCameraDefinitionSettings()
{
    var definition = new CameraDefinition(
        "cam-1",
        "Mapping Camera",
        "Model A",
        SensorWidthMm: 13.2,
        SensorHeightMm: 8.8,
        FocalLengthMm: 8.8,
        ImageWidthPx: 4000,
        ImageHeightPx: 3000);
    var validator = new CameraSettingsValidator();

    var valid = validator.Validate(definition, new CameraSettings(
        "cam-1",
        "Photo",
        IntervalSeconds: 2,
        ExposureSeconds: 0.005,
        Iso: 100,
        WhiteBalance: 5600));
    Require(valid.IsValid, "Expected valid camera settings.");
    Require(valid.Summary.Contains("Mapping Camera"), "Expected camera name in validation summary.");

    var invalid = validator.Validate(definition, new CameraSettings(
        "other",
        "Photo",
        IntervalSeconds: 0,
        ExposureSeconds: -1,
        Iso: 0,
        WhiteBalance: null));
    Require(!invalid.IsValid, "Expected invalid camera settings.");
    Require(invalid.Errors.Count == 4, "Expected all camera setting errors.");
}

static void LinkGimbalRoiTarget()
{
    var controller = new GimbalRoiLinkController();
    var target = new GimbalRoiTarget(47.397742, 8.545594, 488, "Survey target");

    var linked = controller.SetRoi(target, pitchDegrees: -35, yawDegrees: 72, linkToMode: true);
    Require(linked.Target == target, "Expected ROI target.");
    Require(linked.Command?.LockYaw == true, "Expected ROI command to lock yaw.");
    Require(linked.IsLinkedToMode, "Expected ROI to link to mode.");
    Require(linked.Summary.Contains("Survey target"), "Expected ROI summary.");

    var cleared = controller.Clear();
    Require(cleared.Target is null, "Expected cleared ROI target.");
    Require(cleared.Command is null, "Expected cleared ROI command.");
}

static void CatalogPayloadRuntimeEvidence()
{
    var evidence = new PayloadRuntimeEvidenceCatalog().Build();

    Require(evidence.Count == 9, "Expected v1.48 evidence items.");
    Require(evidence.Any(static item => item.Id == "PAYLOADFULL-243" && item.Complete), "Expected backend decision evidence.");
    Require(evidence.Any(static item => item.Id == "PAYLOADFULL-251" && !item.Complete), "Expected real media evidence residual gap.");
}

static void AuditPayloadRuntimeParityGaps()
{
    var evidence = new PayloadRuntimeEvidenceCatalog().Build();
    var audit = new PayloadRuntimeParityAudit().Audit(evidence);

    Require(audit.CompleteItems == 8, "Expected eight complete shared-core evidence items.");
    Require(audit.DeferredItems == 1, "Expected one real media/device evidence gap.");
    Require(audit.Summary.Contains("real media"), "Expected real media gap summary.");
}

static void FlyViewReportsNoActiveVehicle()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var linkManager = new LinkManager(logger);
    var vehicles = new MultiVehicleManager(protocol, logger);
    var flyView = new FlyViewModel(linkManager, protocol, vehicles);

    Require(flyView.VehicleText == "No active vehicle", "Expected no active vehicle text.");
    Require(flyView.ModeText == "No mode", "Expected no mode text.");
    Require(flyView.ArmText == "No vehicle", "Expected no vehicle arm text.");
    Require(flyView.GpsText == "No GPS", "Expected no GPS text.");
    Require(flyView.BatteryText == "No battery", "Expected no battery text.");
    Require(flyView.PositionText == "No position", "Expected no position text.");
    Require(flyView.LinkText == "No links", "Expected no links text.");
}

static void FlyViewProjectsActiveVehicleIndicators()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var linkManager = new LinkManager(logger);
    var vehicles = new MultiVehicleManager(protocol, logger);
    protocol.Attach(linkManager);
    var link = linkManager.CreateConnectedMockLinkAsync().GetAwaiter().GetResult();
    var flyView = new FlyViewModel(linkManager, protocol, vehicles);

    link.EmitIncoming(MavlinkTestFrames.HeartbeatV1(systemId: 9, componentId: 1, autopilot: MavAutopilot.Px4, vehicleType: MavType.Quadrotor, baseMode: 0x80, customMode: 0));
    link.EmitIncoming(MavlinkTestFrames.GlobalPositionInt(systemId: 9, componentId: 1));
    link.EmitIncoming(MavlinkTestFrames.SysStatus(systemId: 9, componentId: 1, voltageMillivolts: 12000, batteryRemaining: 87));
    link.EmitIncoming(MavlinkTestFrames.GpsRawInt(systemId: 9, componentId: 1, fixType: 3, satellitesVisible: 14));

    Require(flyView.VehicleText.Contains("Vehicle 9"), "Expected active vehicle id.");
    Require(flyView.ModeText == "Armed", "Expected armed heartbeat mode name.");
    Require(flyView.ArmText == "Armed", "Expected armed indicator.");
    Require(flyView.GpsText == "GPS fix 3 | 14 sats", "Expected GPS fix and satellites.");
    Require(flyView.BatteryText == "12.0 V | 87%", "Expected battery indicator.");
    Require(flyView.PositionText.Contains("47.397742"), "Expected latitude indicator.");
    Require(flyView.PositionText.Contains("RelAlt 12.3 m"), "Expected relative altitude indicator.");
    Require(flyView.LinkText == "Active link: Mock Link", "Expected active link text.");
    Require(flyView.TelemetryText.Contains("MAVLink"), "Expected telemetry text.");
    Require(flyView.OverlayFrame.ActiveVehicle?.VehicleId == 9, "Expected FlyView overlay frame to include active vehicle.");
}

static void FlyViewExposesOperatorLayoutContract()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var linkManager = new LinkManager(logger);
    var vehicles = new MultiVehicleManager(protocol, logger);
    var video = new VideoStreamRuntimeController();
    var camera = new CameraRuntimeController();
    var gimbal = new GimbalRuntimeController();
    protocol.Attach(linkManager);
    var link = linkManager.CreateConnectedMockLinkAsync().GetAwaiter().GetResult();
    var flyView = new FlyViewModel(linkManager, protocol, vehicles, videoRuntime: video, cameraRuntime: camera, gimbalRuntime: gimbal);

    video.LoadStreams([new VideoStreamDescriptor("front", "Front Camera", new Uri("rtsp://example.test/live"), VideoStreamProtocol.Rtsp)]);
    video.SelectStream("front");
    video.MarkStreaming();
    camera.ApplyStatus(
        new CameraStatus(SystemId: 9, ComponentId: 100, IsReady: true, IsCapturingImage: false, IsRecordingVideo: false, Mode: "Photo"),
        new CameraStorageState(IsAvailable: true, FreeBytes: 1024 * 1024));
    gimbal.ApplyAttitude(new GimbalAttitude(PitchDegrees: -10, RollDegrees: 0, YawDegrees: 35, IsLocked: true));

    link.EmitIncoming(MavlinkTestFrames.HeartbeatV1(systemId: 9, componentId: 1, autopilot: MavAutopilot.Px4, vehicleType: MavType.Quadrotor, baseMode: 0x80));
    link.EmitIncoming(MavlinkTestFrames.GlobalPositionInt(systemId: 9, componentId: 1));
    link.EmitIncoming(MavlinkTestFrames.SysStatus(systemId: 9, componentId: 1, voltageMillivolts: 12000, batteryRemaining: 87));
    link.EmitIncoming(MavlinkTestFrames.GpsRawInt(systemId: 9, componentId: 1, fixType: 3, satellitesVisible: 14));
    link.EmitIncoming(MavlinkTestFrames.StatusText(systemId: 9, componentId: 1, severity: MavlinkSeverity.Warning, text: "Battery low"));

    var layout = flyView.OperatorLayout;
    Require(layout.HasActiveVehicle, "Expected operator layout active vehicle flag.");
    Require(layout.VehicleSummary.Contains("Vehicle 9"), "Expected operator vehicle summary.");
    Require(layout.ModeSummary == "Armed", "Expected operator mode summary.");
    Require(layout.ArmSummary == "Armed", "Expected operator arm summary.");
    Require(layout.LinkSummary == "Active link: Mock Link", "Expected operator link summary.");
    Require(layout.GpsSummary == "GPS fix 3 | 14 sats", "Expected operator GPS summary.");
    Require(layout.BatterySummary == "12.0 V | 87%", "Expected operator battery summary.");
    Require(layout.PositionSummary.Contains("47.397742"), "Expected operator position summary.");
    Require(layout.MapSummary.Contains("Local fallback"), "Expected operator map summary.");
    Require(layout.MapProviderSummary.Contains("Local Vector"), "Expected operator map provider summary.");
    Require(layout.HasWarning, "Expected operator warning flag.");
    Require(layout.WarningSummary == "Warning: Battery low", "Expected operator warning summary.");
    Require(layout.HasPayloadActivity, "Expected operator payload flag.");
    Require(layout.PayloadSummary.Contains("Video streaming"), "Expected operator payload video summary.");
    Require(layout.PayloadSummary.Contains("Camera ready"), "Expected operator payload camera summary.");
    Require(layout.PayloadSummary.Contains("Pitch -10.0"), "Expected operator payload gimbal summary.");
    Require(layout.IsMapFollowingVehicle, "Expected operator follow state.");
}

static void FlyViewExposesGuidedActionCommandSurface()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var linkManager = new LinkManager(logger);
    var vehicles = new MultiVehicleManager(protocol, logger);
    protocol.Attach(linkManager);
    var link = linkManager.CreateConnectedMockLinkAsync().GetAwaiter().GetResult();
    var sent = new List<byte[]>();
    link.BytesSent += (_, args) => sent.Add(args.Bytes);
    var flyView = new FlyViewModel(linkManager, protocol, vehicles);

    link.EmitIncoming(MavlinkTestFrames.HeartbeatV1(systemId: 9, componentId: 1, autopilot: MavAutopilot.Px4, vehicleType: MavType.Quadrotor));
    WaitFor(() => sent.Count >= 6);
    sent.Clear();

    var takeoffReady = flyView.GuidedActions.First(action => action.Kind == GuidedActionKind.Takeoff);
    Require(takeoffReady.State == GuidedActionState.Ready, "Expected FlyView takeoff action to be ready.");
    Require(takeoffReady.IsEnabled, "Expected FlyView takeoff action to be enabled.");

    var confirmation = flyView.RequestGuidedAction(GuidedActionKind.Takeoff);
    Require(confirmation.State == GuidedActionState.ConfirmationRequired, "Expected FlyView takeoff confirmation.");
    Require(flyView.HasPendingGuidedAction, "Expected FlyView pending guided confirmation.");
    Require(flyView.PendingGuidedActionText.Contains("Takeoff"), "Expected FlyView pending takeoff text.");

    var pending = flyView.ConfirmGuidedActionAsync().GetAwaiter().GetResult();
    Require(pending.State == GuidedActionState.Pending, "Expected FlyView takeoff pending state.");
    Require(sent.Count == 1, "Expected FlyView guided action to send one frame.");
    Require(flyView.GuidedActionSummary.Contains("pending"), "Expected FlyView guided pending summary.");

    link.EmitIncoming(MavlinkTestFrames.CommandAck(systemId: 9, componentId: 1, command: MavlinkCommandIds.NavTakeoff, result: MavlinkCommandResult.Accepted));
    var accepted = flyView.GuidedActions.First(action => action.Kind == GuidedActionKind.Takeoff);
    Require(accepted.State == GuidedActionState.Accepted, "Expected FlyView guided action to accept synthetic ACK.");
    Require(flyView.GuidedActionSummary.Contains("Accepted"), "Expected FlyView guided accepted summary.");

    flyView.RequestGuidedAction(GuidedActionKind.Land);
    flyView.ConfirmGuidedActionAsync().GetAwaiter().GetResult();
    flyView.MarkGuidedActionTimeouts(TimeSpan.Zero);
    var timedOut = flyView.GuidedActions.First(action => action.Kind == GuidedActionKind.Land);
    Require(timedOut.State == GuidedActionState.Timeout, "Expected FlyView guided action timeout state.");
}

static void BuildVehicleMapOverlays()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var linkManager = new LinkManager(logger);
    var vehicles = new MultiVehicleManager(protocol, logger);
    protocol.Attach(linkManager);
    var link = linkManager.CreateConnectedMockLinkAsync().GetAwaiter().GetResult();
    link.EmitIncoming(MavlinkTestFrames.HeartbeatV1(systemId: 9, componentId: 1, baseMode: 0x80));
    link.EmitIncoming(MavlinkTestFrames.GlobalPositionInt(systemId: 9, componentId: 1));
    var vehicle = vehicles.ActiveVehicle ?? throw new InvalidOperationException("Expected active vehicle.");

    var projector = new VehicleMapOverlayProjector();
    var frame = projector.Build(
        vehicle,
        home: new VehicleCoordinate(47.397742, 8.545594, 488),
        trajectory:
        [
            new VehicleCoordinate(47.397700, 8.545500, 480),
            new VehicleCoordinate(47.397742, 8.545594, 488)
        ]);

    var activeOverlay = frame.ActiveVehicle ?? throw new InvalidOperationException("Expected active vehicle overlay.");
    Require(activeOverlay.VehicleId == 9, "Expected vehicle id overlay.");
    Require(activeOverlay.Armed, "Expected armed overlay.");
    Require(Math.Abs(activeOverlay.Coordinate.Latitude - 47.397742) < 0.000001, "Expected vehicle latitude overlay.");
    Require(frame.Home is not null, "Expected home overlay.");
    Require(frame.Trajectory?.Points.Count == 2, "Expected trajectory overlay points.");
}

static void ProjectLocalMapDisplayFrame()
{
    var overlays = new MapOverlayFrame(
        new VehicleMapOverlay(
            9,
            new MapCoordinate(47.397742, 8.545594, 488),
            "Position",
            true,
            "Vehicle 9"),
        new HomeMapOverlay(new MapCoordinate(47.397700, 8.545500, 480)),
        new TrajectoryMapOverlay(
            9,
            [
                new MapCoordinate(47.397700, 8.545500, 480),
                new MapCoordinate(47.397742, 8.545594, 488)
            ]));

    var frame = new LocalMapDisplayProjector().Project(overlays);
    var vehicleMarker = frame.ActiveVehicle ?? throw new InvalidOperationException("Expected active vehicle map marker.");

    Require(frame.ProviderName == "Local Vector", "Expected local vector provider label.");
    Require(vehicleMarker.Position.X is > 0.49 and < 0.51, "Expected followed vehicle near map center.");
    Require(vehicleMarker.Position.Y is > 0.49 and < 0.51, "Expected followed vehicle near map center.");
    Require(vehicleMarker.Position.IsVisible, "Expected vehicle marker visible.");
    Require(frame.Home?.Position.IsVisible == true, "Expected home marker visible.");
    Require(frame.Trajectory?.Points.Count == 2, "Expected projected trajectory points.");
}

static void FlyViewExposesLocalMapDisplayState()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var linkManager = new LinkManager(logger);
    var vehicles = new MultiVehicleManager(protocol, logger);
    protocol.Attach(linkManager);
    var link = linkManager.CreateConnectedMockLinkAsync().GetAwaiter().GetResult();
    var flyView = new FlyViewModel(linkManager, protocol, vehicles);

    link.EmitIncoming(MavlinkTestFrames.HeartbeatV1(systemId: 9, componentId: 1, baseMode: 0x80));
    link.EmitIncoming(MavlinkTestFrames.GlobalPositionInt(systemId: 9, componentId: 1));

    Require(flyView.MapDisplayFrame.ProviderName == "Local Vector", "Expected local map display frame.");
    Require(flyView.MapDisplayFrame.HasActiveVehicle, "Expected active vehicle map marker.");
    Require(flyView.MapDisplayFrame.ActiveVehicle?.VehicleId == 9, "Expected active vehicle marker id.");
    Require(flyView.MapRuntimeText.Contains("Local fallback"), "Expected local map runtime text.");
}

static void FlyViewExposesMapHostBindingState()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var linkManager = new LinkManager(logger);
    var vehicles = new MultiVehicleManager(protocol, logger);
    protocol.Attach(linkManager);
    var link = linkManager.CreateConnectedMockLinkAsync().GetAwaiter().GetResult();
    var flyView = new FlyViewModel(linkManager, protocol, vehicles);

    Require(flyView.MapProviderText == "Local Vector", "Expected local map provider text.");
    Require(!flyView.HasMapVehicle, "Expected no map vehicle before coordinate.");
    Require(!flyView.MapPlaceholderText.Contains("deferred", StringComparison.OrdinalIgnoreCase), "Expected FlyView to stop reporting deferred map state.");

    link.EmitIncoming(MavlinkTestFrames.HeartbeatV1(systemId: 12, componentId: 1, baseMode: 0x80));
    link.EmitIncoming(MavlinkTestFrames.GlobalPositionInt(systemId: 12, componentId: 1));

    Require(flyView.HasMapVehicle, "Expected active map vehicle after coordinate.");
    Require(flyView.MapCenterText.Contains("47.397742"), "Expected map center latitude text.");
    Require(flyView.MapZoomText == "Zoom 16", "Expected default local map zoom text.");
    Require(flyView.MapVehicleMarkerText.Contains("Vehicle 12"), "Expected vehicle marker label.");
}

static void FlyViewProjectsHomeAndTrajectoryState()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var linkManager = new LinkManager(logger);
    var vehicles = new MultiVehicleManager(protocol, logger);
    protocol.Attach(linkManager);
    var link = linkManager.CreateConnectedMockLinkAsync().GetAwaiter().GetResult();
    var flyView = new FlyViewModel(linkManager, protocol, vehicles);

    link.EmitIncoming(MavlinkTestFrames.HeartbeatV1(systemId: 15, componentId: 1, baseMode: 0x80));
    link.EmitIncoming(MavlinkTestFrames.GlobalPositionInt(systemId: 15, componentId: 1, latitude: 47.397700, longitude: 8.545500));
    link.EmitIncoming(MavlinkTestFrames.GlobalPositionInt(systemId: 15, componentId: 1, latitude: 47.397742, longitude: 8.545594));

    Require(flyView.OverlayFrame.Home is not null, "Expected local home overlay.");
    Require(flyView.OverlayFrame.Trajectory?.Points.Count == 2, "Expected two trajectory coordinates.");
    Require(flyView.MapDisplayFrame.Home?.Position.IsVisible == true, "Expected projected home marker visible.");
    Require(flyView.MapDisplayFrame.Trajectory?.Points.Count == 2, "Expected projected trajectory points.");
    Require(flyView.HasMapHome, "Expected FlyView home marker state.");
    Require(flyView.HasMapTrajectory, "Expected FlyView trajectory state.");
    Require(flyView.MapTrajectoryText == "Track 2 points", "Expected trajectory count text.");
}

static void SkipVehicleMapOverlayWithoutCoordinate()
{
    var vehicle = new Vehicle(9, 1, MavAutopilot.Px4, MavType.Quadrotor);
    vehicle.MarkHeartbeat(systemStatus: 0, baseMode: 0x80);

    var frame = new VehicleMapOverlayProjector().Build(vehicle);

    Require(frame.ActiveVehicle is null, "Expected no vehicle overlay before coordinate is available.");
    Require(frame.Home is null, "Expected no home overlay by default.");
    Require(frame.Trajectory is null, "Expected no trajectory overlay by default.");
}

static void CatalogFlyViewQmlRuntimeParity()
{
    var fly = new QgcQmlParityCatalog().Build().Single(static item => item.Module == "FlyView");

    Require(fly.Status == QgcQmlParityStatus.Migrated, "Expected FlyView QML workflow migration evidence.");
    Require(fly.RequiredEvidence == QgcQmlEvidenceLevel.Runtime, "Expected FlyView runtime evidence gate.");
    Require(fly.Area == UiWorkflowArea.Fly, "Expected FlyView area.");
    Require(fly.VgcTarget.Contains("VGC/Views/FlyView.axaml", StringComparison.Ordinal), "Expected FlyView AXAML target.");
    Require(fly.Blocker.Contains("SITL", StringComparison.Ordinal) && fly.Blocker.Contains("device", StringComparison.OrdinalIgnoreCase), "Expected FlyView runtime blockers.");
}

static void CatalogFlightMapQmlRuntimeParity()
{
    var map = new QgcQmlParityCatalog().Build().Single(static item => item.Module == "FlightMap");

    Require(map.Status == QgcQmlParityStatus.Migrated, "Expected FlightMap migrated implementation evidence.");
    Require(map.RequiredEvidence == QgcQmlEvidenceLevel.Runtime, "Expected FlightMap runtime evidence gate.");
    Require(map.Area == UiWorkflowArea.Fly, "Expected FlightMap fly area.");
    Require(map.VgcTarget.Contains("VGC.UI/Controls/MapControls.cs", StringComparison.Ordinal), "Expected FlightMap map control target.");
    Require(map.Blocker.Contains("offline tile", StringComparison.OrdinalIgnoreCase) && map.Blocker.Contains("SITL", StringComparison.Ordinal), "Expected remaining FlightMap runtime blockers.");
}

static void KeepQgcUiParityBlockedAfterFlyViewAndFlightMapMapping()
{
    var audit = new QgcQmlParityAudit().Audit(new QgcQmlParityCatalog().Build());

    Require(!audit.CanClaimQmlUiParity, "Expected QGC UI parity to remain blocked after FlyView/FlightMap mapping.");
    Require(!audit.CanClaimQgcReplacement, "Expected QGC replacement to remain blocked after FlyView/FlightMap mapping.");
    Require(audit.OpenBlockers.Any(static blocker => blocker.Contains("FlyView", StringComparison.Ordinal)), "Expected FlyView runtime blocker.");
    Require(audit.OpenBlockers.Any(static blocker => blocker.Contains("FlightMap", StringComparison.Ordinal)), "Expected FlightMap runtime blocker.");
}

static void CatalogPlanViewQmlRuntimeParity()
{
    var plan = new QgcQmlParityCatalog().Build().Single(static item => item.Module == "PlanView");

    Require(plan.FileCount == 43, "Expected PlanView QML file count.");
    Require(plan.Status == QgcQmlParityStatus.Migrated, "Expected PlanView migrated implementation evidence.");
    Require(plan.RequiredEvidence == QgcQmlEvidenceLevel.Runtime, "Expected PlanView runtime evidence gate.");
    Require(plan.Area == UiWorkflowArea.Plan, "Expected PlanView area.");
    Require(plan.VgcTarget.Contains("VGC/Views/PlanView.axaml", StringComparison.Ordinal), "Expected PlanView AXAML target.");
    Require(plan.Blocker.Contains("screenshot", StringComparison.OrdinalIgnoreCase) && plan.Blocker.Contains("SITL", StringComparison.Ordinal), "Expected PlanView remaining runtime blockers.");
}

static void KeepQgcUiParityBlockedAfterPlanViewMapping()
{
    var audit = new QgcQmlParityAudit().Audit(new QgcQmlParityCatalog().Build());

    Require(!audit.CanClaimQmlUiParity, "Expected QGC UI parity to remain blocked after PlanView mapping.");
    Require(!audit.CanClaimQgcReplacement, "Expected QGC replacement to remain blocked after PlanView mapping.");
    Require(audit.OpenBlockers.Any(static blocker => blocker.Contains("PlanView", StringComparison.Ordinal)), "Expected PlanView runtime blocker.");
}

static void OverviewViewReflectsVehicleStatus()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var linkManager = new LinkManager(logger);
    var gcsHeartbeat = new GcsHeartbeatService(linkManager, new MavlinkFrameWriter(), logger);
    var vehicles = new MultiVehicleManager(protocol, logger);
    var overview = new OverviewViewModel(linkManager, protocol, gcsHeartbeat, vehicles, logger.Entries);

    linkManager.CreateConnectedMockLinkAsync().GetAwaiter().GetResult();
    Require(overview.Title == "Overview", "Expected overview title.");
    Require(overview.LinkStatus.Contains("connected"), "Expected link status text.");
    Require(overview.VehicleCount >= 0, "Expected vehicle count property.");
}

static void RoundTripEditedPlanDocument()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var vehicles = new MultiVehicleManager(protocol, logger);
    var planViewModel = new PlanViewModel(vehicles);

    planViewModel.AddWaypoint();
    planViewModel.SelectedCommand = 16;
    planViewModel.UpdateSelectedWaypoint(47.397742, 8.545594, 42);

    var service = new PlanJsonService();
    var json = service.Serialize(planViewModel.Document);
    var roundTripped = service.Deserialize(json);

    Require(roundTripped.FileType == "Plan", "Expected edited Plan file type.");
    Require(roundTripped.Mission.Items.Count == 2, "Expected edited mission items to round-trip.");
    Require(roundTripped.Mission.Items[1].DoJumpId == 2, "Expected edited item order to round-trip.");
    Require(Math.Abs(roundTripped.Mission.Items[1].Params[4] - 47.397742) < 0.000001, "Expected edited latitude to round-trip.");
    Require(Math.Abs(roundTripped.Mission.Items[1].Params[6] - 42) < 0.001, "Expected edited altitude to round-trip.");
}

static void RoundTripGeoFencePlanDocument()
{
    var service = new PlanJsonService();
    var document = PlanDocument.CreateBlank();
    document.GeoFence.Polygons.Add(new GeoFencePolygon
    {
        Inclusion = true,
        Polygon =
        [
            new PlanCoordinate(47.39807773798406, 8.543834631785785),
            new PlanCoordinate(47.39983519888905, 8.550024648373267),
            new PlanCoordinate(47.39641100087146, 8.54499282423751)
        ]
    });
    document.GeoFence.Circles.Add(new GeoFenceCircle
    {
        Inclusion = false,
        Circle = new GeoFenceCircleShape
        {
            Center = new PlanCoordinate(47.39756763610029, 8.544649762407738),
            Radius = 319.85
        }
    });
    document.GeoFence.BreachReturn = new PlanCoordinate(47.397742, 8.545594, 50);

    var json = service.Serialize(document);
    Require(json.Contains("\"geoFence\""), "Expected geoFence object.");
    Require(json.Contains("\"polygons\""), "Expected polygons field.");
    Require(json.Contains("\"circles\""), "Expected circles field.");
    Require(json.Contains("\"breachReturn\""), "Expected breachReturn field.");

    var roundTripped = service.Deserialize(json);
    Require(roundTripped.GeoFence.Version == 2, "Expected GeoFence version.");
    Require(roundTripped.GeoFence.Polygons.Count == 1, "Expected polygon to round-trip.");
    Require(roundTripped.GeoFence.Circles.Count == 1, "Expected circle to round-trip.");
    Require(roundTripped.GeoFence.Polygons[0].Version == 1, "Expected polygon version.");
    Require(roundTripped.GeoFence.Circles[0].Version == 1, "Expected circle version.");
    Require(Math.Abs(roundTripped.GeoFence.Circles[0].Circle.Radius - 319.85) < 0.001, "Expected circle radius.");
    var breachReturn = roundTripped.GeoFence.BreachReturn;
    if (breachReturn is null)
    {
        throw new InvalidOperationException("Expected breach return to round-trip.");
    }

    Require(breachReturn.IsValid3D(), "Expected valid breach return.");
}

static void ValidateGeoFencePlanDocument()
{
    var valid = new GeoFencePlan();
    valid.Polygons.Add(new GeoFencePolygon
    {
        Polygon =
        [
            new PlanCoordinate(47.0, 8.0),
            new PlanCoordinate(47.1, 8.1),
            new PlanCoordinate(47.2, 8.0)
        ]
    });
    valid.Circles.Add(new GeoFenceCircle
    {
        Circle = new GeoFenceCircleShape
        {
            Center = new PlanCoordinate(47.0, 8.0),
            Radius = 100
        }
    });

    Require(GeoFenceValidation.Validate(valid).IsValid, "Expected valid geofence.");

    var invalid = new GeoFencePlan();
    invalid.Polygons.Add(new GeoFencePolygon
    {
        Polygon =
        [
            new PlanCoordinate(91, 8.0),
            new PlanCoordinate(47.1, 8.1)
        ]
    });
    invalid.Circles.Add(new GeoFenceCircle
    {
        Circle = new GeoFenceCircleShape
        {
            Center = new PlanCoordinate(47.0, 181),
            Radius = 0
        }
    });
    invalid.BreachReturn = new PlanCoordinate(47.0, 8.0);

    var result = GeoFenceValidation.Validate(invalid);
    Require(!result.IsValid, "Expected invalid geofence.");
    Require(result.Errors.Count >= 4, "Expected multiple geofence validation errors.");
}

static void ConvertGeoFencePlanMissionItems()
{
    var geoFence = new GeoFencePlan();
    geoFence.Polygons.Add(new GeoFencePolygon
    {
        Inclusion = false,
        Polygon =
        [
            new PlanCoordinate(47.0, 8.0),
            new PlanCoordinate(47.1, 8.1),
            new PlanCoordinate(47.2, 8.0)
        ]
    });
    geoFence.Circles.Add(new GeoFenceCircle
    {
        Inclusion = true,
        Circle = new GeoFenceCircleShape
        {
            Center = new PlanCoordinate(47.397742, 8.545594),
            Radius = 125.5
        }
    });
    geoFence.BreachReturn = new PlanCoordinate(47.4, 8.6, 55);

    var items = GeoFenceMissionItemConverter.ToMissionItems(geoFence, targetSystemId: 9, targetComponentId: 1);
    Require(items.Count == 5, "Expected polygon vertices, circle, and breach return items.");
    Require(items.All(static item => item.MissionType == MavMissionType.Fence), "Expected fence mission type.");
    Require(items[0].Command == MavlinkMissionCommandIds.NavFencePolygonVertexExclusion, "Expected exclusion polygon command.");
    Require(Math.Abs(items[0].Param1 - 3) < 0.001, "Expected polygon vertex count in param1.");
    Require(items[0].Frame == 0, "Expected global frame for polygon.");
    Require(items[3].Command == MavlinkMissionCommandIds.NavFenceCircleInclusion, "Expected inclusion circle command.");
    Require(Math.Abs(items[3].Param1 - 125.5) < 0.001, "Expected circle radius in param1.");
    Require(items[4].Command == MavlinkMissionCommandIds.NavFenceReturnPoint, "Expected breach return command.");
    Require(items[4].Frame == 3, "Expected relative-alt frame for breach return.");
    Require(Math.Abs(items[4].Z - 55) < 0.001, "Expected breach return altitude.");

    var roundTripped = GeoFenceMissionItemConverter.ToGeoFencePlan(items);
    Require(roundTripped.Polygons.Count == 1, "Expected polygon to round-trip.");
    Require(!roundTripped.Polygons[0].Inclusion, "Expected polygon inclusion to round-trip.");
    Require(roundTripped.Polygons[0].Polygon.Count == 3, "Expected polygon vertices to round-trip.");
    Require(roundTripped.Circles.Count == 1, "Expected circle to round-trip.");
    Require(roundTripped.Circles[0].Inclusion, "Expected circle inclusion to round-trip.");
    Require(Math.Abs(roundTripped.Circles[0].Circle.Radius - 125.5) < 0.001, "Expected circle radius to round-trip.");
    Require(roundTripped.BreachReturn is not null, "Expected breach return to round-trip.");
    Require(Math.Abs(roundTripped.BreachReturn!.Altitude.GetValueOrDefault() - 55) < 0.001, "Expected breach return altitude to round-trip.");
}

static void RoundTripRallyPointsPlanDocument()
{
    var service = new PlanJsonService();
    var document = PlanDocument.CreateBlank();
    document.RallyPoints.Points.Add(new PlanCoordinate(47.39760401, 8.5509154, 50));
    document.RallyPoints.Points.Add(new PlanCoordinate(47.39902017, 8.54263274, 55));

    var json = service.Serialize(document);
    Require(json.Contains("\"rallyPoints\""), "Expected rallyPoints object.");
    Require(json.Contains("\"points\""), "Expected rally points field.");

    var roundTripped = service.Deserialize(json);
    Require(roundTripped.RallyPoints.Version == 2, "Expected RallyPoints version.");
    Require(roundTripped.RallyPoints.Points.Count == 2, "Expected rally points to round-trip.");
    Require(Math.Abs(roundTripped.RallyPoints.Points[0].Latitude - 47.39760401) < 0.000001, "Expected first rally latitude.");
    Require(Math.Abs(roundTripped.RallyPoints.Points[0].Longitude - 8.5509154) < 0.000001, "Expected first rally longitude.");
    Require(Math.Abs(roundTripped.RallyPoints.Points[0].Altitude.GetValueOrDefault() - 50) < 0.001, "Expected first rally altitude.");
    Require(Math.Abs(roundTripped.RallyPoints.Points[1].Altitude.GetValueOrDefault() - 55) < 0.001, "Expected second rally altitude.");
}

static void ValidateRallyPointsPlanDocument()
{
    var valid = new RallyPointsPlan();
    valid.Points.Add(new PlanCoordinate(47.39760401, 8.5509154, 50));
    valid.Points.Add(new PlanCoordinate(47.39902017, 8.54263274, 55));

    Require(RallyPointsValidation.Validate(valid).IsValid, "Expected valid rally points.");

    var invalid = new RallyPointsPlan();
    invalid.Points.Add(new PlanCoordinate(91, 8.5509154, 50));
    invalid.Points.Add(new PlanCoordinate(47.39902017, 181, 55));
    invalid.Points.Add(new PlanCoordinate(47.39902017, 8.54263274));

    var result = RallyPointsValidation.Validate(invalid);
    Require(!result.IsValid, "Expected invalid rally points.");
    Require(result.Errors.Count == 3, "Expected three rally validation errors.");
}

static void ConvertRallyPointMissionItems()
{
    var rallyPoints = new RallyPointsPlan();
    rallyPoints.Points.Add(new PlanCoordinate(47.39760401, 8.5509154, 50));
    rallyPoints.Points.Add(new PlanCoordinate(47.39902017, 8.54263274, 55));

    var items = RallyPointMissionItemConverter.ToMissionItems(rallyPoints, targetSystemId: 9, targetComponentId: 1);
    Require(items.Count == 2, "Expected two rally items.");
    Require(items[0].MissionType == MavMissionType.Rally, "Expected rally mission type.");
    Require(items[0].Command == MavlinkMissionCommandIds.NavRallyPoint, "Expected rally command.");
    Require(items[0].Frame == 3, "Expected relative-alt frame.");
    Require(Math.Abs(items[0].Z - 50) < 0.001, "Expected first rally altitude.");
    Require(items[1].Sequence == 1, "Expected second rally sequence.");

    var roundTripped = RallyPointMissionItemConverter.ToRallyPointsPlan(items);
    Require(roundTripped.Points.Count == 2, "Expected rally points to round-trip.");
    Require(Math.Abs(roundTripped.Points[0].Latitude - 47.39760401) < 0.000001, "Expected first rally latitude.");
    Require(Math.Abs(roundTripped.Points[0].Longitude - 8.5509154) < 0.000001, "Expected first rally longitude.");
    Require(Math.Abs(roundTripped.Points[0].Altitude.GetValueOrDefault() - 50) < 0.001, "Expected first rally altitude to round-trip.");
    Require(Math.Abs(roundTripped.Points[1].Altitude.GetValueOrDefault() - 55) < 0.001, "Expected second rally altitude to round-trip.");
}

static void CoordinatePlanDocumentSections()
{
    var coordinator = new PlanSectionCoordinator();
    coordinator.Mission.Items.Add(new MissionPlanItem
    {
        Command = 16,
        Params = [0, 0, 0, 0, 47.397742, 8.545594, 30],
        DoJumpId = 1
    });
    coordinator.GeoFence.Circles.Add(new GeoFenceCircle
    {
        Circle = new GeoFenceCircleShape
        {
            Center = new PlanCoordinate(47.39756763610029, 8.544649762407738),
            Radius = 319.85
        }
    });
    coordinator.RallyPoints.Points.Add(new PlanCoordinate(47.39760401, 8.5509154, 50));

    Require(coordinator.HasMissionItems, "Expected mission section items.");
    Require(coordinator.HasGeoFenceItems, "Expected GeoFence section items.");
    Require(coordinator.HasRallyPoints, "Expected Rally section items.");
    Require(coordinator.ValidateSections().IsValid, "Expected coordinated sections to validate.");
}

static void RoundTripAllPlanDocumentSections()
{
    var coordinator = new PlanSectionCoordinator();
    coordinator.Mission.Items.Add(new MissionPlanItem
    {
        Command = 16,
        Params = [0, 0, 0, 0, 47.397742, 8.545594, 30],
        DoJumpId = 1
    });
    coordinator.GeoFence.Polygons.Add(new GeoFencePolygon
    {
        Polygon =
        [
            new PlanCoordinate(47.0, 8.0),
            new PlanCoordinate(47.1, 8.1),
            new PlanCoordinate(47.2, 8.0)
        ]
    });
    coordinator.RallyPoints.Points.Add(new PlanCoordinate(47.39760401, 8.5509154, 50));

    var json = coordinator.Save();
    var loaded = new PlanSectionCoordinator();
    loaded.Load(json);

    Require(loaded.Mission.Items.Count == 1, "Expected mission section to round-trip.");
    Require(loaded.GeoFence.Polygons.Count == 1, "Expected GeoFence section to round-trip.");
    Require(loaded.RallyPoints.Points.Count == 1, "Expected Rally section to round-trip.");
    Require(loaded.ValidateSections().IsValid, "Expected loaded sections to validate.");
}

static void BuildReadOnlyPlanMapOverlays()
{
    var coordinator = new PlanSectionCoordinator();
    coordinator.Mission.Items.Add(new MissionPlanItem
    {
        Command = 16,
        Params = [0, 0, 0, 0, 47.397742, 8.545594, 30],
        DoJumpId = 1
    });
    coordinator.GeoFence.Polygons.Add(new GeoFencePolygon
    {
        Inclusion = true,
        Polygon =
        [
            new PlanCoordinate(47.0, 8.0),
            new PlanCoordinate(47.1, 8.1),
            new PlanCoordinate(47.2, 8.0)
        ]
    });
    coordinator.GeoFence.Circles.Add(new GeoFenceCircle
    {
        Inclusion = false,
        Circle = new GeoFenceCircleShape
        {
            Center = new PlanCoordinate(47.397742, 8.545594),
            Radius = 100
        }
    });
    coordinator.RallyPoints.Points.Add(new PlanCoordinate(47.39760401, 8.5509154, 50));

    var overlay = new PlanMapOverlayBuilder().Build(coordinator);

    Require(overlay.MissionWaypoints.Count == 1, "Expected waypoint overlay.");
    Require(overlay.GeoFencePolygons.Count == 1, "Expected GeoFence polygon overlay.");
    Require(overlay.GeoFenceCircles.Count == 1, "Expected GeoFence circle overlay.");
    Require(overlay.RallyPoints.Count == 1, "Expected Rally point overlay.");
    Require(overlay.GeoFencePolygons[0].Points.Count == 3, "Expected polygon points.");
    Require(Math.Abs(overlay.GeoFenceCircles[0].Radius - 100) < 0.001, "Expected circle radius.");
    Require(coordinator.GeoFence.Polygons.Count == 1, "Expected overlay build not to mutate GeoFence.");
    Require(coordinator.RallyPoints.Points.Count == 1, "Expected overlay build not to mutate Rally.");
}

static void ProjectPlanMapPreviewOverlays()
{
    var coordinator = new PlanSectionCoordinator();
    coordinator.Mission.Items.Add(new MissionPlanItem
    {
        Command = 16,
        Params = [0, 0, 0, 0, 47.397742, 8.545594, 30],
        DoJumpId = 1
    });
    coordinator.GeoFence.Polygons.Add(new GeoFencePolygon
    {
        Inclusion = true,
        Polygon =
        [
            new PlanCoordinate(47.397700, 8.545500),
            new PlanCoordinate(47.397780, 8.545620),
            new PlanCoordinate(47.397720, 8.545700)
        ]
    });
    coordinator.GeoFence.Circles.Add(new GeoFenceCircle
    {
        Inclusion = false,
        Circle = new GeoFenceCircleShape
        {
            Center = new PlanCoordinate(47.397760, 8.545650),
            Radius = 100
        }
    });
    coordinator.RallyPoints.Points.Add(new PlanCoordinate(47.397730, 8.545610, 50));

    var overlay = new PlanMapOverlayBuilder().Build(coordinator);
    var display = new PlanMapDisplayProjector().Project(overlay);

    Require(display.HasAnyOverlay, "Expected display overlay content.");
    Require(display.MissionWaypoints.Count == 1, "Expected projected mission waypoint.");
    Require(display.MissionWaypoints[0].Position.IsVisible, "Expected mission waypoint visible.");
    Require(display.GeoFencePolygons.Count == 1, "Expected projected geofence polygon.");
    Require(display.GeoFencePolygons[0].Points.Count == 3, "Expected projected polygon points.");
    Require(display.GeoFenceCircles.Count == 1, "Expected projected geofence circle.");
    Require(Math.Abs(display.GeoFenceCircles[0].RadiusMeters - 100) < 0.001, "Expected circle radius preserved.");
    Require(display.RallyPoints.Count == 1, "Expected projected rally point.");
    Require(display.RallyPoints[0].Position.IsVisible, "Expected rally point visible.");
}

static void TrackMapFollowAndRecenterState()
{
    var state = new MapInteractionState();
    var activeCoordinate = new MapCoordinate(47.397742, 8.545594, 488);

    var followed = state.ResolveViewport(activeCoordinate);
    Require(state.IsFollowingVehicle, "Expected map to follow by default.");
    Require(Math.Abs(followed.Center.Latitude - 47.397742) < 0.000001, "Expected follow viewport center.");

    state.MarkManualViewport(new MapViewport(new MapCoordinate(47.5, 8.6), 14));
    var manual = state.ResolveViewport(activeCoordinate);
    Require(!state.IsFollowingVehicle, "Expected manual movement to disable follow.");
    Require(Math.Abs(manual.Center.Latitude - 47.5) < 0.000001, "Expected manual viewport center.");

    state.RecenterOnVehicle();
    var recentered = state.ResolveViewport(activeCoordinate);
    Require(state.IsFollowingVehicle, "Expected recenter to restore follow.");
    Require(Math.Abs(recentered.Center.Latitude - 47.397742) < 0.000001, "Expected recentered viewport.");
}

static void FlyViewExposesMapFollowState()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var linkManager = new LinkManager(logger);
    var vehicles = new MultiVehicleManager(protocol, logger);
    protocol.Attach(linkManager);
    var link = linkManager.CreateConnectedMockLinkAsync().GetAwaiter().GetResult();
    var flyView = new FlyViewModel(linkManager, protocol, vehicles);

    link.EmitIncoming(MavlinkTestFrames.HeartbeatV1(systemId: 16, componentId: 1, baseMode: 0x80));
    link.EmitIncoming(MavlinkTestFrames.GlobalPositionInt(systemId: 16, componentId: 1));
    Require(flyView.IsMapFollowingVehicle, "Expected FlyView map to follow by default.");

    flyView.MarkMapManuallyMoved(new MapViewport(new MapCoordinate(47.5, 8.6), 14));
    Require(!flyView.IsMapFollowingVehicle, "Expected manual map movement to disable follow.");
    Require(flyView.MapFollowText == "Manual map", "Expected manual map text.");
    Require(Math.Abs(flyView.MapDisplayFrame.Viewport.Center.Latitude - 47.5) < 0.000001, "Expected manual display viewport.");

    flyView.RecenterMap();
    Require(flyView.IsMapFollowingVehicle, "Expected recenter to restore follow.");
    Require(flyView.MapFollowText == "Follow vehicle", "Expected follow map text.");
    Require(Math.Abs(flyView.MapDisplayFrame.Viewport.Center.Latitude - 47.397742) < 0.000001, "Expected recentered display viewport.");
}

static void FlyViewExposesPayloadControlState()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var linkManager = new LinkManager(logger);
    var vehicles = new MultiVehicleManager(protocol, logger);
    var video = new VideoStreamRuntimeController();
    var camera = new CameraRuntimeController();
    var gimbal = new GimbalRuntimeController();
    var stream = new VideoStreamDescriptor(
        "front",
        "Front Camera",
        new Uri("rtsp://127.0.0.1/front"),
        VideoStreamProtocol.Rtsp,
        "h264");

    video.LoadStreams([stream], preferredStreamId: "front");
    video.MarkStreaming();
    camera.ApplyStatus(
        new CameraStatus(1, 100, IsReady: true, IsCapturingImage: false, IsRecordingVideo: false, Mode: "Photo"),
        new CameraStorageState(IsAvailable: true, FreeBytes: 1024L * 1024L));
    gimbal.ApplyAttitude(new GimbalAttitude(PitchDegrees: -10, RollDegrees: 0, YawDegrees: 35, IsLocked: false));

    var flyView = new FlyViewModel(
        linkManager,
        protocol,
        vehicles,
        MapProviderHost.CreateLocalOnly(),
        video,
        camera,
        gimbal);

    Require(flyView.PayloadVideoText == "Video streaming", "Expected FlyView video status.");
    Require(flyView.PayloadVideoStreamText == "Front Camera | Rtsp", "Expected FlyView video stream text.");
    Require(flyView.PayloadCameraReadyText == "Camera ready", "Expected FlyView camera ready state.");
    Require(flyView.PayloadCameraModeText == "Photo", "Expected FlyView camera mode.");
    Require(flyView.PayloadCameraStorageText == "Storage available | 1.0 MB free", "Expected FlyView camera storage.");
    Require(flyView.PayloadGimbalAttitudeText == "Pitch -10.0 | Yaw 35.0 | Roll 0.0", "Expected FlyView gimbal attitude.");

    flyView.CaptureImageCommand.Execute().Subscribe();
    Require(flyView.PayloadCameraCaptureText == "Image capture pending", "Expected FlyView capture command projection.");

    flyView.RecordVideoCommand.Execute().Subscribe();
    Require(flyView.PayloadCameraRecordingText == "Video recording pending", "Expected FlyView recording command projection.");

    flyView.TiltGimbalDownCommand.Execute().Subscribe();
    Require(flyView.PayloadGimbalTargetText == "Target pitch -45.0 | yaw 0.0", "Expected FlyView gimbal target projection.");
    Require(flyView.PayloadGimbalCommandText == "Gimbal attitude pending", "Expected FlyView gimbal command projection.");
}

static void TrackPayloadProtocolCommandBoundary()
{
    var boundary = new PayloadProtocolBoundary();
    var tracker = new PayloadProtocolCommandTracker();
    var camera = new PayloadProtocolTarget(1, 100, PayloadProtocolComponentKind.Camera);
    var gimbal = new PayloadProtocolTarget(1, 154, PayloadProtocolComponentKind.Gimbal);

    var capture = boundary.CreateCameraCommand(PayloadProtocolAction.CaptureImage, camera);
    Require(capture.Action == PayloadProtocolAction.CaptureImage, "Expected camera capture action.");
    Require(capture.Target == camera, "Expected camera target.");

    var submitted = tracker.Submit(capture);
    Require(submitted.Status == PayloadProtocolCommandStatus.Pending, "Expected pending payload command.");
    Require(submitted.AttemptCount == 1, "Expected first payload command attempt.");

    var timedOut = tracker.Timeout(capture.Id);
    Require(timedOut.Status == PayloadProtocolCommandStatus.TimedOut, "Expected timeout status.");
    Require(timedOut.CanRetry, "Expected timeout to allow retry.");

    var retry = tracker.Submit(capture);
    Require(retry.AttemptCount == 2, "Expected retry attempt count.");

    var acknowledged = tracker.Acknowledge(capture.Id);
    Require(acknowledged.Status == PayloadProtocolCommandStatus.Acknowledged, "Expected ack status.");
    Require(!acknowledged.CanRetry, "Expected acknowledged command to stop retry.");

    var gimbalCommand = new GimbalCommand(PitchDegrees: -30, YawDegrees: 45, LockYaw: true);
    var setGimbal = boundary.CreateGimbalCommand(gimbal, gimbalCommand);
    Require(setGimbal.Action == PayloadProtocolAction.SetGimbalAttitude, "Expected gimbal action.");
    Require(setGimbal.GimbalTarget == gimbalCommand, "Expected gimbal target command.");

    var rejected = tracker.Submit(setGimbal);
    tracker.Reject(rejected.Command.Id, "unsupported gimbal mode");
    Require(tracker.Commands.Count == 2, "Expected tracked camera and gimbal commands.");

    RequireThrows<InvalidOperationException>(
        () => boundary.CreateCameraCommand(PayloadProtocolAction.CaptureImage, gimbal),
        "Expected camera command to reject gimbal target.");
    RequireThrows<InvalidOperationException>(
        () => boundary.CreateCameraCommand(PayloadProtocolAction.SetGimbalAttitude, camera),
        "Expected camera command to reject gimbal action.");
}

static void PlanPayloadMediaStorage()
{
    var planner = new PayloadStoragePlanner();
    var desktopPolicy = new PayloadStoragePolicy(
        PayloadStoragePlatform.Desktop,
        PayloadStorageLocationKind.UserSelectedFolder,
        "D:/Payload",
        RequiresUserConsent: true,
        AllowsOverwrite: false,
        RequiresScopedStorage: false);
    var snapshot = new PayloadStorageRequest(PayloadMediaKind.Snapshot, "front camera 001", "JPG", ExpectedBytes: 1024);

    var desktopPlan = planner.Plan(snapshot, desktopPolicy);
    Require(desktopPlan.RelativeFileName == "front_camera_001.jpg", "Expected sanitized snapshot file name.");
    Require(desktopPlan.DisplayPath == "D:/Payload/front_camera_001.jpg", "Expected desktop display path.");
    Require(desktopPlan.RequiresPrompt, "Expected user selected folder to require prompt.");
    Require(desktopPlan.Warnings.Count == 0, "Expected no desktop warnings.");

    var androidPolicy = new PayloadStoragePolicy(
        PayloadStoragePlatform.Android,
        PayloadStorageLocationKind.MediaStore,
        "MediaStore/VGC",
        RequiresUserConsent: false,
        AllowsOverwrite: false,
        RequiresScopedStorage: true);
    var recording = new PayloadStorageRequest(PayloadMediaKind.Recording, "mission-recording", "", ExpectedBytes: 10L * 1024L * 1024L);

    var androidPlan = planner.Plan(recording, androidPolicy);
    Require(androidPlan.RelativeFileName == "mission-recording.mp4", "Expected recording default extension.");
    Require(!androidPlan.RequiresPrompt, "Expected scoped MediaStore plan without warnings to skip prompt.");

    var unsafeAndroid = planner.Plan(recording, androidPolicy with { RequiresScopedStorage = false });
    Require(unsafeAndroid.Warnings.Count == 1, "Expected Android scoped storage warning.");
    Require(unsafeAndroid.RequiresPrompt, "Expected warning to require prompt.");

    var tempRecording = planner.Plan(recording, desktopPolicy with { LocationKind = PayloadStorageLocationKind.Temporary, RootPath = "" });
    Require(tempRecording.Warnings.Any(static warning => warning.Contains("temporary", StringComparison.OrdinalIgnoreCase)), "Expected temporary recording warning.");
}

static void MapProviderCatalogExposesProductionCandidates()
{
    var providers = MapProviderCatalog.Defaults;
    var local = MapProviderCatalog.Find(MapProviderKind.LocalFallback);
    var mapsui = MapProviderCatalog.Find(MapProviderKind.MapsuiRaster);
    var tianditu = MapProviderCatalog.Find(MapProviderKind.TiandituRaster);

    Require(providers.Count >= 3, "Expected local fallback and production provider candidates.");
    Require(local.Projection == MapProviderProjection.LocalNormalized, "Expected local provider to stay projection-isolated.");
    Require(mapsui.IsUsableOnCurrentTargets, "Expected Mapsui candidate to support Desktop and Android.");
    Require(mapsui.Capabilities.RequiresVisibleAttribution, "Expected Mapsui tile source attribution boundary.");
    Require(!mapsui.Capabilities.AllowsBulkTileDownload, "Expected OSM public tile bulk download to be blocked.");
    Require(tianditu.Capabilities.RequiresApiKey, "Expected Tianditu tk configuration requirement.");
    Require(tianditu.TileLayers.All(static layer => layer.ApiKeyParameterName == "TIANDITU_TK"), "Expected Tianditu layers to use external key configuration.");
    Require(tianditu.TileLayers.All(static layer => layer.Template is not null && layer.Template.Contains("{tk}")), "Expected Tianditu templates to keep tk as placeholder.");
}

static void LocalMapRuntimeImplementsProviderAdapterBoundary()
{
    IMapProviderAdapter adapter = new LocalMapRuntime();
    var camera = new MapProviderCameraState(new MapCoordinate(47.5, 8.6, 450), 14, BearingDegrees: 12, PitchDegrees: 20);

    adapter.ApplyCameraAsync(camera).GetAwaiter().GetResult();
    var resolved = adapter.GetCameraAsync().GetAwaiter().GetResult();

    Require(adapter.Descriptor.Kind == MapProviderKind.LocalFallback, "Expected local provider descriptor.");
    Require(Math.Abs(resolved.Center.Latitude - 47.5) < 0.000001, "Expected provider camera latitude to round-trip.");
    Require(Math.Abs(resolved.Center.Longitude - 8.6) < 0.000001, "Expected provider camera longitude to round-trip.");
    Require(Math.Abs(resolved.ZoomLevel - 14) < 0.001, "Expected provider camera zoom to round-trip.");
    Require(resolved.ToViewport() == new MapViewport(new MapCoordinate(47.5, 8.6, 450), 14), "Expected camera to convert to viewport.");
}

static void MapProviderHostSelectsLocalFallback()
{
    var host = MapProviderHost.CreateLocalOnly();
    var overlays = new MapOverlayFrame(
        new VehicleMapOverlay(
            7,
            new MapCoordinate(47.397742, 8.545594, 488),
            "Position",
            true,
            "Vehicle 7"),
        null,
        null);

    var frame = host.RenderDisplayFrame(overlays, new MapViewport(new MapCoordinate(47.397742, 8.545594), 16));

    Require(host.State.ActiveProvider.Kind == MapProviderKind.LocalFallback, "Expected local fallback provider.");
    Require(host.State.IsLocalFallback, "Expected host state to mark local fallback.");
    Require(host.State.AvailableProviders.Count == 1, "Expected local-only host provider count.");
    Require(!host.TrySelectProvider(MapProviderKind.TiandituRaster), "Expected unavailable provider selection to fail.");
    Require(frame.HasActiveVehicle, "Expected local host render to keep vehicle overlay.");
    Require(frame.ProviderName == "Local Vector", "Expected local display frame through host.");
}

static void MapProviderHostExposesActiveRasterTiles()
{
    var host = new MapProviderHost(
        [
            new LocalMapRuntime(),
            new RasterTileMapAdapter(MapProviderCatalog.MapsuiOsmRaster)
        ],
        MapProviderKind.MapsuiRaster);

    Require(host.ActiveProvider.Kind == MapProviderKind.MapsuiRaster, "Expected desktop raster provider active.");
    Require(host.ActiveRasterTiles is not null, "Expected active raster tile source.");
    Require(host.ActiveBaseLayer?.Id == "osm-standard", "Expected OSM base layer active.");
}

static void FlyViewExposesProviderHostState()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var linkManager = new LinkManager(logger);
    var vehicles = new MultiVehicleManager(protocol, logger);
    protocol.Attach(linkManager);
    var flyView = new FlyViewModel(linkManager, protocol, vehicles, MapProviderHost.CreateLocalOnly());

    Require(flyView.MapProviderHostState.ActiveProvider.Kind == MapProviderKind.LocalFallback, "Expected FlyView local host provider.");
    Require(flyView.MapProviderHostText == "Host: Local fallback", "Expected FlyView host text.");
    Require(flyView.MapAvailableProvidersText == "Providers 1", "Expected FlyView provider count text.");
    Require(flyView.MapRuntimeText.Contains("Local fallback"), "Expected runtime text to use host state.");
}

static void BridgeVehicleOverlaysToProviderCommands()
{
    var overlays = new MapOverlayFrame(
        new VehicleMapOverlay(
            4,
            new MapCoordinate(47.397742, 8.545594, 488),
            "Position",
            true,
            "Vehicle 4"),
        new HomeMapOverlay(new MapCoordinate(47.397700, 8.545500, 480)),
        new TrajectoryMapOverlay(
            4,
            [
                new MapCoordinate(47.397700, 8.545500, 480),
                new MapCoordinate(47.397742, 8.545594, 488)
            ]));

    var frame = new MapProviderOverlayBridge().Build(overlays);

    Require(frame.Commands.Count == 3, "Expected vehicle, home, and trajectory commands.");
    Require(frame.Markers.Any(static marker => marker.Id == "vehicle:4" && marker.Symbol == "vehicle-armed"), "Expected vehicle marker command.");
    Require(frame.Markers.Any(static marker => marker.Id == "home" && marker.Layer == MapProviderOverlayLayer.Home), "Expected home marker command.");
    Require(frame.Polylines.Count == 1, "Expected trajectory polyline command.");
    Require(frame.Polylines[0].Id == "trajectory:4", "Expected deterministic trajectory id.");
    Require(frame.Polylines[0].Points.Count == 2, "Expected trajectory command points.");
}

static void BridgePlanOverlaysToProviderCommands()
{
    var planOverlay = new PlanMapOverlay
    {
        MissionWaypoints =
        [
            new PlanMapWaypointOverlay(0, new PlanCoordinate(47.397742, 8.545594, 30), 16)
        ],
        GeoFencePolygons =
        [
            new PlanMapPolygonOverlay(
                1,
                [
                    new PlanCoordinate(47.0, 8.0),
                    new PlanCoordinate(47.1, 8.1),
                    new PlanCoordinate(47.2, 8.0)
                ],
                Inclusion: true)
        ],
        GeoFenceCircles =
        [
            new PlanMapCircleOverlay(2, new PlanCoordinate(47.397742, 8.545594), 125, Inclusion: false)
        ],
        RallyPoints =
        [
            new PlanMapPointOverlay(3, new PlanCoordinate(47.39760401, 8.5509154, 50))
        ]
    };

    var frame = new MapProviderOverlayBridge().Build(new MapOverlayFrame(null, null, null), planOverlay);

    Require(frame.Commands.Count == 4, "Expected plan overlay commands.");
    Require(frame.Markers.Any(static marker => marker.Id == "mission:waypoint:0" && marker.Layer == MapProviderOverlayLayer.Mission), "Expected mission marker command.");
    Require(frame.Markers.Any(static marker => marker.Id == "rally:3" && marker.Symbol == "rally-point"), "Expected rally marker command.");
    Require(frame.Polygons.Count == 1, "Expected geofence polygon command.");
    Require(frame.Polygons[0].Id == "geofence:polygon:1", "Expected deterministic polygon id.");
    Require(frame.Polygons[0].Inclusion, "Expected polygon inclusion flag.");
    Require(frame.Circles.Count == 1, "Expected geofence circle command.");
    Require(frame.Circles[0].Id == "geofence:circle:2", "Expected deterministic circle id.");
    Require(!frame.Circles[0].Inclusion, "Expected circle exclusion flag.");
    Require(Math.Abs(frame.Circles[0].RadiusMeters - 125) < 0.001, "Expected circle radius preserved.");
}

static void CreateStableMapTileCacheKeys()
{
    var key = new MapTileCacheKey(MapProviderKind.TiandituRaster, "tianditu-vector", 12, 3372, 1552);
    var entry = new MapTileCacheEntry(
        key,
        [1, 2, 3, 4],
        "image/png",
        new DateTimeOffset(2026, 6, 25, 0, 0, 0, TimeSpan.Zero));

    Require(key.StableKey == "TiandituRaster:tianditu-vector:12/3372/1552", "Expected deterministic tile key.");
    Require(entry.SizeBytes == 4, "Expected tile entry size.");
    Require(entry.Key == key, "Expected tile entry key to round-trip.");
}

static void CreateProviderTileCachePolicies()
{
    var local = MapTileCachePolicyFactory.CreateRuntimePolicy(MapProviderCatalog.LocalFallback);
    var mapsui = MapTileCachePolicyFactory.CreateRuntimePolicy(MapProviderCatalog.MapsuiOsmRaster);
    var tianditu = MapTileCachePolicyFactory.CreateRuntimePolicy(MapProviderCatalog.TiandituRaster);

    Require(!local.AllowInteractiveNetworkCache, "Expected local fallback to disable tile cache.");
    Require(local.MaxCacheBytes == 0, "Expected local fallback to allocate no tile cache.");
    Require(mapsui.AllowInteractiveNetworkCache, "Expected Mapsui raster to allow interactive cache.");
    Require(!mapsui.AllowBulkDownload, "Expected OSM public tile bulk download to stay disabled.");
    Require(!mapsui.AllowOfflinePackageImport, "Expected default OSM runtime policy not to enable offline package import.");
    Require(tianditu.AllowInteractiveNetworkCache, "Expected TianDiTu runtime cache for interactive use.");
    Require(!tianditu.AllowBulkDownload, "Expected TianDiTu bulk download to stay disabled.");
    Require(!tianditu.AllowOfflinePackageImport, "Expected TianDiTu offline import to stay disabled by default.");
    Require(tianditu.AndroidPathHint.Contains("AppDataDirectory"), "Expected Android app-scoped storage hint.");
}

static void TrackMapInteractionSelectionRuntime()
{
    var runtime = new MapInteractionRuntime();
    var vehicle = new MapCoordinate(47.397742, 8.545594, 488);
    runtime.UpdateActiveVehicle(vehicle);

    Require(runtime.Snapshot.IsFollowingVehicle, "Expected initial follow mode.");
    Require(runtime.Snapshot.Viewport.Center == vehicle, "Expected viewport to follow active vehicle.");

    runtime.PanTo(new MapCoordinate(47.4, 8.55));
    runtime.ZoomTo(18.5);
    runtime.SelectFeature(new MapFeatureSelection(
        "mission:waypoint:1",
        MapProviderOverlayLayer.Mission,
        new MapCoordinate(47.401, 8.551),
        "Waypoint 1"));

    Require(!runtime.Snapshot.IsFollowingVehicle, "Expected manual pan to disable follow.");
    Require(Math.Abs(runtime.Snapshot.Viewport.ZoomLevel - 18.5) < 0.001, "Expected zoom level to be preserved.");
    Require(runtime.Snapshot.SelectedFeature?.FeatureId == "mission:waypoint:1", "Expected selected map feature.");

    runtime.RecenterOnVehicle();
    runtime.ClearSelection();

    Require(runtime.Snapshot.IsFollowingVehicle, "Expected recenter to restore follow.");
    Require(runtime.Snapshot.SelectedFeature is null, "Expected selection to clear.");
}

static void ProjectMapProviderAttributionPolicy()
{
    var projector = new MapAttributionUiProjector();
    var local = projector.Project(MapProviderCatalog.LocalFallback);
    var osm = projector.Project(MapProviderCatalog.MapsuiOsmRaster);
    var tianditu = projector.Project(MapProviderCatalog.TiandituRaster);

    Require(!local.MustShowAttribution, "Expected local fallback to require no attribution.");
    Require(osm.MustShowAttribution, "Expected OSM attribution to be visible.");
    Require(osm.DisplayText.Contains("OpenStreetMap"), "Expected OSM attribution text.");
    Require(!osm.BulkDownloadAllowed, "Expected OSM public tile bulk download to stay blocked.");
    Require(tianditu.PolicyText.Contains("tk"), "Expected Tianditu policy to mention external tk.");
}

static void EstimateOfflineMapRegionTiles()
{
    var estimator = new OfflineMapRegionEstimator();
    var estimate = estimator.Estimate(new OfflineMapRegionRequest(
        "Test field",
        MapProviderCatalog.MapsuiOsmRaster,
        new MapBounds(47.39, 8.54, 47.40, 8.56),
        MinZoom: 12,
        MaxZoom: 14));

    Require(estimate.TileCount > 0, "Expected tile count estimate.");
    Require(estimate.EstimatedBytes == estimate.TileCount * 24 * 1024, "Expected deterministic size estimate.");
    Require(!estimate.IsDownloadAllowed, "Expected default OSM bulk offline download to be blocked.");
    Require(estimate.ProviderKind == MapProviderKind.MapsuiRaster, "Expected provider kind to round-trip.");
}

static void PlanOfflineMapProviderPolicy()
{
    var planner = new OfflineMapRegionPlanner();
    var osmPolicy = planner.BuildPolicy(MapProviderCatalog.MapsuiOsmRaster);
    var tiandituPolicy = planner.BuildPolicy(MapProviderCatalog.TiandituRaster);
    var region = planner.PlanRegion(" Test field ", new MapCoordinate(47.397, 8.545), 12, 14);

    Require(osmPolicy.CanPlanOfflineRegion, "Expected OSM offline region planning to be available.");
    Require(!osmPolicy.CanBulkDownload, "Expected OSM bulk tile download to remain disabled by policy.");
    Require(!tiandituPolicy.CanPlanOfflineRegion, "Expected Tianditu offline planning to remain disabled.");
    Require(region.IsValid, "Expected planned offline region to be valid.");
    Require(region.Name == "Test field", "Expected offline region name trimming.");
    Require(region.EstimatedSizeBytes == region.EstimatedTileCount * 32 * 1024, "Expected deterministic offline size estimate.");
}

static void RunOfflineMapDownloadQueue()
{
    var allowedRegion = new OfflineMapRegionEstimate(
        "Allowed",
        MapProviderKind.LocalFallback,
        new MapBounds(1, 1, 2, 2),
        1,
        2,
        TileCount: 10,
        EstimatedBytes: 1000,
        IsDownloadAllowed: true,
        PolicyText: "test-only provider");
    var blockedRegion = allowedRegion with { Name = "Blocked", IsDownloadAllowed = false };
    var queue = new OfflineMapDownloadQueue();

    var blocked = queue.Enqueue("blocked", blockedRegion);
    Require(blocked.State == OfflineMapDownloadState.Blocked, "Expected provider-policy blocked job.");

    queue.Enqueue("job", allowedRegion);
    Require(queue.Start("job").State == OfflineMapDownloadState.Downloading, "Expected job start.");
    Require(queue.ReportProgress("job", 4).ProgressPercent == 40, "Expected progress percent.");
    Require(queue.Pause("job").State == OfflineMapDownloadState.Paused, "Expected pause state.");
    Require(queue.Resume("job").State == OfflineMapDownloadState.Downloading, "Expected resume state.");
    Require(queue.Fail("job", "network").FailureReason == "network", "Expected failure reason.");
    Require(queue.Retry("job").State == OfflineMapDownloadState.Queued, "Expected retry to requeue.");
    Require(queue.Start("job").State == OfflineMapDownloadState.Downloading, "Expected restarted download.");
    Require(queue.ReportProgress("job", 10).State == OfflineMapDownloadState.Completed, "Expected completed download.");
}

static void EvictMapTileCacheEntries()
{
    var store = new InMemoryMapTileCacheStore();
    var now = new DateTimeOffset(2026, 6, 26, 0, 0, 0, TimeSpan.Zero);
    var policy = new MapTileCacheStoragePolicy(
        MapTileCacheStorageKind.RuntimeHttpCache,
        "desktop",
        "android",
        MaxCacheBytes: 6,
        MaxAge: TimeSpan.FromDays(7),
        AllowInteractiveNetworkCache: true,
        AllowBulkDownload: false,
        AllowOfflinePackageImport: false,
        LicensingNotes: "test");

    store.StoreAsync(new MapTileCacheEntry(
        new MapTileCacheKey(MapProviderKind.MapsuiRaster, "osm", 1, 1, 1),
        [1, 2, 3, 4],
        "image/png",
        now.AddDays(-10))).GetAwaiter().GetResult();
    store.StoreAsync(new MapTileCacheEntry(
        new MapTileCacheKey(MapProviderKind.MapsuiRaster, "osm", 1, 1, 2),
        [1, 2, 3, 4],
        "image/png",
        now.AddHours(-1))).GetAwaiter().GetResult();
    store.StoreAsync(new MapTileCacheEntry(
        new MapTileCacheKey(MapProviderKind.MapsuiRaster, "osm", 1, 1, 3),
        [1, 2, 3, 4],
        "image/png",
        now.AddMinutes(-30),
        now.AddMinutes(-1))).GetAwaiter().GetResult();

    var result = store.CleanupAsync(policy, now).GetAwaiter().GetResult();
    var remaining = store.ListAsync().GetAwaiter().GetResult();

    Require(result.RemovedEntries == 2, "Expected expired/stale cache eviction.");
    Require(result.RemainingBytes == 4, "Expected one cache entry to remain.");
    Require(remaining.Count == 1, "Expected one cache metadata row.");
}

static void ModelAndroidMapLifecycleRisks()
{
    var lifecycle = new AndroidMapLifecycleCoordinator();

    Require(lifecycle.Apply(AndroidMapLifecycleEvent.Start).IsVisible, "Expected visible map after start.");
    Require(!lifecycle.State.ShouldSuspendTileRequests, "Expected requests allowed after start.");
    Require(lifecycle.Apply(AndroidMapLifecycleEvent.NetworkLost).ShouldSuspendTileRequests, "Expected network loss to suspend requests.");
    Require(lifecycle.Apply(AndroidMapLifecycleEvent.StorageLow).ShouldTrimMemory, "Expected low storage to request trimming.");
    Require(!lifecycle.Apply(AndroidMapLifecycleEvent.NetworkAvailable).ShouldSuspendTileRequests, "Expected network recovery while visible.");
    Require(lifecycle.Apply(AndroidMapLifecycleEvent.Pause).ShouldSuspendTileRequests, "Expected pause to suspend requests.");
    Require(lifecycle.State.Summary.Contains("suspended"), "Expected lifecycle summary to explain suspension.");
}

static void CatalogMapRuntimeEvidence()
{
    var evidence = new MapRuntimeEvidenceCatalog().Build();

    Require(evidence.Count == 7, "Expected v1.47 evidence items.");
    Require(evidence.Any(static item => item.Id == "MAPOFF-235" && item.Complete), "Expected map interaction evidence.");
    Require(evidence.Any(static item => item.Id == "MAPOFF-241" && !item.Complete), "Expected runtime evidence residual gap.");
}

static void AuditMapOfflineParityGaps()
{
    var evidence = new MapRuntimeEvidenceCatalog().Build();
    var audit = new MapOfflineParityAudit().Audit(evidence);

    Require(audit.CompleteItems == 6, "Expected six complete shared-core evidence items.");
    Require(audit.DeferredItems == 1, "Expected one runtime/device evidence gap.");
    Require(audit.Summary.Contains("deferred"), "Expected audit summary.");
}

static void ApplySectionScopedPlanMapEdits()
{
    var coordinator = CreateEditableOverlayPlan();
    var applier = new PlanMapEditApplier();

    var missionResult = applier.Apply(coordinator, new MoveMissionWaypointCommand(0, new PlanCoordinate(47.5, 8.5, 45)));
    Require(missionResult.Applied, "Expected mission waypoint edit.");
    Require(Math.Abs(coordinator.Mission.Items[0].Params[4] - 47.5) < 0.000001, "Expected mission latitude edit.");
    Require(Math.Abs(coordinator.Mission.Items[0].Params[6] - 45) < 0.001, "Expected mission altitude edit.");

    var polygonResult = applier.Apply(coordinator, new MoveGeoFencePolygonPointCommand(0, 1, new PlanCoordinate(47.15, 8.15)));
    Require(polygonResult.Applied, "Expected GeoFence polygon point edit.");
    Require(Math.Abs(coordinator.GeoFence.Polygons[0].Polygon[1].Latitude - 47.15) < 0.000001, "Expected polygon point latitude edit.");

    var circleMoveResult = applier.Apply(coordinator, new MoveGeoFenceCircleCommand(0, new PlanCoordinate(47.4, 8.6)));
    Require(circleMoveResult.Applied, "Expected GeoFence circle center edit.");
    Require(Math.Abs(coordinator.GeoFence.Circles[0].Circle.Center.Longitude - 8.6) < 0.000001, "Expected circle center longitude edit.");

    var circleResizeResult = applier.Apply(coordinator, new ResizeGeoFenceCircleCommand(0, 250));
    Require(circleResizeResult.Applied, "Expected GeoFence circle radius edit.");
    Require(Math.Abs(coordinator.GeoFence.Circles[0].Circle.Radius - 250) < 0.001, "Expected circle radius edit.");

    var rallyResult = applier.Apply(coordinator, new MoveRallyPointCommand(0, new PlanCoordinate(47.6, 8.7, 60)));
    Require(rallyResult.Applied, "Expected Rally point edit.");
    Require(Math.Abs(coordinator.RallyPoints.Points[0].Altitude.GetValueOrDefault() - 60) < 0.001, "Expected Rally altitude edit.");

    Require(coordinator.ValidateSections().IsValid, "Expected section validation after map edits.");
}

static void RejectInvalidPlanMapEdits()
{
    var coordinator = CreateEditableOverlayPlan();
    var applier = new PlanMapEditApplier();

    var badMissionIndex = applier.Apply(coordinator, new MoveMissionWaypointCommand(99, new PlanCoordinate(47.5, 8.5, 45)));
    Require(!badMissionIndex.Applied && badMissionIndex.Error is not null, "Expected bad mission index rejection.");

    var badGeoFenceRadius = applier.Apply(coordinator, new ResizeGeoFenceCircleCommand(0, 0));
    Require(!badGeoFenceRadius.Applied, "Expected bad GeoFence radius rejection.");
    Require(badGeoFenceRadius.Error is not null && badGeoFenceRadius.Error.Contains("radius"), "Expected GeoFence radius error.");

    var badRallyPoint = applier.Apply(coordinator, new MoveRallyPointCommand(0, new PlanCoordinate(47.6, 181, 60)));
    Require(!badRallyPoint.Applied, "Expected bad Rally point rejection.");
    Require(badRallyPoint.Error is not null && badRallyPoint.Error.Contains("Rally point coordinate"), "Expected Rally point error.");
}

static void RoundTripComplexMissionModels()
{
    var plan = new ComplexMissionPlan();
    plan.SurveyItems.Add(new SurveyMissionItem
    {
        Polygon =
        [
            new PlanCoordinate(47.0, 8.0),
            new PlanCoordinate(47.1, 8.0),
            new PlanCoordinate(47.1, 8.1)
        ],
        GridAngle = 45,
        TransectSpacing = 30,
        Altitude = 60
    });
    plan.CorridorItems.Add(new CorridorMissionItem
    {
        Polyline =
        [
            new PlanCoordinate(47.0, 8.0),
            new PlanCoordinate(47.2, 8.2)
        ],
        CorridorWidth = 80
    });
    plan.StructureScanItems.Add(new StructureScanMissionItem
    {
        Footprint =
        [
            new PlanCoordinate(47.0, 8.0),
            new PlanCoordinate(47.0, 8.1),
            new PlanCoordinate(47.1, 8.1)
        ],
        ScanDistance = 15
    });

    var json = JsonSerializer.Serialize(plan);
    Require(json.Contains("\"complexItemType\":\"survey\""), "Expected survey item type.");
    Require(json.Contains("\"complexItemType\":\"corridorScan\""), "Expected corridor item type.");
    Require(json.Contains("\"complexItemType\":\"structureScan\""), "Expected structure item type.");

    var roundTripped = JsonSerializer.Deserialize<ComplexMissionPlan>(json)
        ?? throw new InvalidOperationException("Expected complex mission plan JSON.");
    Require(roundTripped.SurveyItems.Count == 1, "Expected one survey item.");
    Require(roundTripped.CorridorItems[0].Polyline.Count == 2, "Expected corridor polyline points.");
    Require(Math.Abs(roundTripped.StructureScanItems[0].ScanDistance - 15) < 0.001, "Expected structure scan distance.");
}

static void CalculateComplexMissionPreviews()
{
    var calculator = new BasicComplexMissionCalculator();

    var survey = calculator.Calculate(new ComplexMissionCalculationRequest(
        ComplexMissionItemKind.Survey,
        [
            new PlanCoordinate(47.0, 8.0),
            new PlanCoordinate(47.1, 8.0),
            new PlanCoordinate(47.1, 8.1)
        ],
        SpacingMeters: 30,
        AltitudeMeters: 60));
    Require(survey.IsValid, "Expected survey preview to be valid.");
    Require(survey.PreviewSegments.Count > 3, "Expected survey spacing to generate transect previews.");
    Require(Math.Abs(survey.PreviewSegments[0].Start.Altitude.GetValueOrDefault() - 60) < 0.001, "Expected survey altitude in preview.");

    var corridor = calculator.Calculate(new ComplexMissionCalculationRequest(
        ComplexMissionItemKind.Corridor,
        [
            new PlanCoordinate(47.0, 8.0),
            new PlanCoordinate(47.1, 8.0),
            new PlanCoordinate(47.2, 8.1)
        ]));
    Require(corridor.IsValid, "Expected corridor preview to be valid.");
    Require(corridor.PreviewSegments.Count == 2, "Expected open corridor preview path.");

    var invalid = calculator.Calculate(new ComplexMissionCalculationRequest(
        ComplexMissionItemKind.StructureScan,
        [new PlanCoordinate(47.0, 181.0)]));
    Require(!invalid.IsValid, "Expected invalid structure geometry to be rejected.");
    Require(invalid.Error is not null && invalid.Error.Contains("invalid coordinate"), "Expected invalid coordinate error.");
}

static void CalculateComplexAuthoringPreviewsFromSettings()
{
    var calculator = new BasicComplexMissionCalculator();

    var survey = new SurveyMissionItem
    {
        Polygon =
        [
            new PlanCoordinate(47.000, 8.000),
            new PlanCoordinate(47.001, 8.000),
            new PlanCoordinate(47.001, 8.001),
            new PlanCoordinate(47.000, 8.001)
        ],
        TransectSpacing = 55,
        Altitude = 70
    };
    var surveyPreview = calculator.Calculate(ComplexMissionCalculationRequest.FromSurvey(survey));
    Require(surveyPreview.IsValid, "Expected survey authoring preview.");
    Require(surveyPreview.PreviewSegments.Count == 3, "Expected survey spacing to produce deterministic transect count.");
    Require(Math.Abs(surveyPreview.PreviewSegments[0].Start.Altitude.GetValueOrDefault() - 70) < 0.001, "Expected survey authoring altitude.");

    var corridor = new CorridorMissionItem
    {
        Polyline =
        [
            new PlanCoordinate(47.000, 8.000),
            new PlanCoordinate(47.001, 8.001)
        ],
        CorridorWidth = 80,
        Altitude = 60
    };
    var corridorPreview = calculator.Calculate(ComplexMissionCalculationRequest.FromCorridor(corridor));
    Require(corridorPreview.IsValid, "Expected corridor authoring preview.");
    Require(corridorPreview.PreviewSegments.Count == 3, "Expected center and two width guide segments.");

    var structure = new StructureScanMissionItem
    {
        Footprint =
        [
            new PlanCoordinate(47.000, 8.000),
            new PlanCoordinate(47.001, 8.000),
            new PlanCoordinate(47.001, 8.001),
            new PlanCoordinate(47.000, 8.001)
        ],
        Altitude = 30,
        LayerHeight = 10
    };
    var structurePreview = calculator.Calculate(ComplexMissionCalculationRequest.FromStructureScan(structure));
    Require(structurePreview.IsValid, "Expected structure authoring preview.");
    Require(structurePreview.PreviewSegments.Count == 12, "Expected three footprint layers.");
    Require(Math.Abs(structurePreview.PreviewSegments[^1].End.Altitude.GetValueOrDefault() - 30) < 0.001, "Expected final structure layer altitude.");
}

static void TrackComplexItemDirtySerialization()
{
    var item = new EditableComplexMissionItem(ComplexMissionItemKind.Survey)
    {
        Name = "North Survey"
    };
    item.SetGeometry([
        new PlanCoordinate(47.0, 8.0, 60),
        new PlanCoordinate(47.1, 8.0, 60),
        new PlanCoordinate(47.1, 8.1, 60)
    ]);

    Require(item.IsDirty, "Expected complex item dirty after edit.");
    var json = item.Serialize();
    Require(json.Contains("North Survey", StringComparison.Ordinal), "Expected serialized complex item name.");
    Require(json.Contains("Survey", StringComparison.Ordinal), "Expected serialized complex item kind.");

    var document = PlanDocument.CreateBlank();
    document.ComplexMission.SurveyItems.Add(new SurveyMissionItem { Polygon = item.Geometry.ToList() });
    var roundTrip = new PlanJsonService().Deserialize(new PlanJsonService().Serialize(document));
    Require(roundTrip.ComplexMission.SurveyItems.Count == 1, "Expected complex mission to round-trip through plan document.");

    item.MarkClean();
    Require(!item.IsDirty, "Expected MarkClean to clear dirty state.");
}

static void PlanSurveyGridCameraSpacing()
{
    var planner = new SurveyPlanningService();
    var result = planner.Plan(new SurveyPlanningSettings(
        [
            new PlanCoordinate(47.0, 8.0),
            new PlanCoordinate(47.001, 8.0),
            new PlanCoordinate(47.001, 8.001),
            new PlanCoordinate(47.0, 8.001)
        ],
        GridAngleDegrees: 0,
        CameraFootprintWidthMeters: 40,
        ForwardOverlapPercent: 75,
        SideOverlapPercent: 50,
        TurnaroundDistanceMeters: 20,
        AltitudeMeters: 80));

    Require(result.Transects.Count > 1, "Expected survey transects.");
    Require(Math.Abs(result.CameraSpacingMeters - 10) < 0.001, "Expected camera spacing from overlap.");
    Require(Math.Abs(result.TransectSpacingMeters - 20) < 0.001, "Expected transect spacing from side overlap.");
    Require(result.Transects[0].Start.Longitude < 8.0, "Expected turnaround extension.");
}

static void PlanCorridorScanExpansion()
{
    var planner = new CorridorScanPlanner();
    var result = planner.Plan(
        [new PlanCoordinate(47.0, 8.0), new PlanCoordinate(47.01, 8.02)],
        widthMeters: 80,
        altitudeMeters: 70);

    Require(result.ExpandedPolygon.Count == 4, "Expected corridor polygon from path expansion.");
    Require(result.PreviewSegments.Count == 3, "Expected center and width guide preview segments.");
    Require(result.Summary.Contains("80", StringComparison.Ordinal), "Expected corridor summary width.");
}

static void PlanStructureScanLayers()
{
    var planner = new StructureScanPlanner();
    var result = planner.Plan(
        [
            new PlanCoordinate(47.0, 8.0),
            new PlanCoordinate(47.001, 8.0),
            new PlanCoordinate(47.001, 8.001),
            new PlanCoordinate(47.0, 8.001)
        ],
        maxAltitudeMeters: 30,
        layerHeightMeters: 10,
        scanDistanceMeters: 15);

    Require(result.Layers.Count == 3, "Expected structure scan layers.");
    Require(Math.Abs(result.Layers[^1].AltitudeMeters - 30) < 0.001, "Expected final layer altitude.");
    Require(result.PreviewSegments.Count == 12, "Expected layered footprint preview segments.");
}

static void ValidateFixedWingLandingPattern()
{
    var planner = new FixedWingLandingPlanner();
    var pattern = planner.Create(
        new PlanCoordinate(47.0, 8.0, 120),
        new PlanCoordinate(47.01, 8.01, 100),
        new PlanCoordinate(47.02, 8.02, 0),
        loiterRadiusMeters: 80);

    Require(pattern.Validation.Contains("Valid", StringComparison.Ordinal), "Expected valid landing pattern.");
    Require(Math.Abs(pattern.LoiterRadiusMeters - 80) < 0.001, "Expected loiter radius.");
}

static void CreateVtolLandingBoundary()
{
    var planner = new VtolLandingPlanner();
    var item = planner.Create(
        new PlanCoordinate(47.0, 8.0, 100),
        new PlanCoordinate(47.001, 8.001, 0),
        transitionAltitudeMeters: 60);

    Require(item.RequiresVtolVehicle, "Expected VTOL vehicle gate.");
    Require(item.BoundaryNote.Contains("VTOL", StringComparison.Ordinal), "Expected VTOL boundary note.");
    Require(Math.Abs(item.TransitionAltitudeMeters - 60) < 0.001, "Expected transition altitude.");
}

static void ModelCameraAndSpeedSections()
{
    var sections = new CameraSpeedSectionModel();
    sections.AddCameraTrigger(0, 5, 12);
    sections.AddSpeedChange(2, 8);

    Require(sections.Actions.Count == 2, "Expected camera and speed actions.");
    Require(sections.Actions[0].Type == PlanSectionActionType.CameraTrigger && sections.Actions[0].Units == "m", "Expected camera trigger distance.");
    Require(sections.Actions[1].Type == PlanSectionActionType.SpeedChange && sections.Actions[1].Units == "m/s", "Expected speed change units.");
}

static void RoundTripKmlShapeBoundary()
{
    var service = new KmlShapeService();
    var coordinates = new[]
    {
        new PlanCoordinate(47.0, 8.0, 50),
        new PlanCoordinate(47.1, 8.1, 60)
    };

    var kml = service.ExportKml(coordinates, "Test Shape");
    var imported = service.ImportKml(kml);
    var geoJson = service.ExportGeoJson(imported);

    Require(imported.Count == 2, "Expected KML coordinate round-trip.");
    Require(Math.Abs(imported[0].Latitude - 47.0) < 0.001, "Expected imported latitude.");
    Require(geoJson.Contains("LineString", StringComparison.Ordinal), "Expected GeoJSON line boundary.");
}

static void QueryTerrainThroughCacheBoundary()
{
    var provider = new FakeTerrainProvider([
        new TerrainSample(new TerrainCoordinate(47.0, 8.0), 500, "fake"),
        new TerrainSample(new TerrainCoordinate(47.1, 8.1), 525, "fake")
    ]);
    var cache = new InMemoryTerrainCache();
    var service = new TerrainService(provider, cache);

    var first = service.QueryAsync([
        new TerrainCoordinate(47.0, 8.0),
        new TerrainCoordinate(47.1, 8.1)
    ]).GetAwaiter().GetResult();
    Require(first.IsComplete, "Expected first terrain query to complete.");
    Require(first.Samples.Count == 2, "Expected two terrain samples.");
    Require(provider.QueryCount == 1, "Expected provider query for uncached samples.");

    var second = service.QueryAsync([new TerrainCoordinate(47.0, 8.0)]).GetAwaiter().GetResult();
    Require(second.IsComplete, "Expected cached terrain query to complete.");
    Require(second.Samples[0].Source == "fake", "Expected cached terrain sample.");
    Require(provider.QueryCount == 1, "Expected cached query to avoid provider.");
}

static void PlanTerrainAdjustedMissionAltitudes()
{
    var provider = new FakeTerrainProvider([
        new TerrainSample(new TerrainCoordinate(47.0, 8.0), 500, "fake"),
        new TerrainSample(new TerrainCoordinate(47.1, 8.1), 525, "fake")
    ]);
    var planner = new MissionTerrainAltitudePlanner(new TerrainService(provider, new InMemoryTerrainCache()));

    var adjusted = planner.PlanAsync(new MissionTerrainAltitudeRequest(
        [
            new PlanCoordinate(47.0, 8.0),
            new PlanCoordinate(47.1, 8.1)
        ],
        ClearanceMeters: 60)).GetAwaiter().GetResult();

    Require(adjusted.Count == 2, "Expected two terrain adjusted coordinates.");
    Require(Math.Abs(adjusted[0].PlannedAltitudeMeters - 560) < 0.001, "Expected terrain clearance altitude.");
    Require(Math.Abs(adjusted[1].TerrainElevationMeters - 525) < 0.001, "Expected terrain elevation.");
}

static void PreviewTerrainBackedMissionAltitudes()
{
    var provider = new FakeTerrainProvider([
        new TerrainSample(new TerrainCoordinate(47.0, 8.0), 500, "fake")
    ]);
    var preview = new MissionTerrainPreviewService(new TerrainService(provider, new InMemoryTerrainCache()));

    var result = preview.PreviewAsync(new MissionTerrainPreviewRequest(
        [new PlanCoordinate(47.0, 8.0, 30)],
        ClearanceMeters: 75,
        FallbackTerrainElevationMeters: 100)).GetAwaiter().GetResult();

    Require(result.Count == 1, "Expected one terrain preview point.");
    Require(result[0].HasTerrainData, "Expected terrain data to be used.");
    Require(Math.Abs(result[0].TerrainElevationMeters.GetValueOrDefault() - 500) < 0.001, "Expected terrain elevation.");
    Require(Math.Abs(result[0].PlannedAltitudeMeters - 575) < 0.001, "Expected terrain clearance altitude.");
    Require(result[0].Annotation.Contains("fake"), "Expected terrain source annotation.");
}

static void PreviewMissionAltitudesWithoutTerrain()
{
    var provider = new FakeTerrainProvider([]);
    var preview = new MissionTerrainPreviewService(new TerrainService(provider, new InMemoryTerrainCache()));

    var result = preview.PreviewAsync(new MissionTerrainPreviewRequest(
        [
            new PlanCoordinate(47.0, 8.0, 120),
            new PlanCoordinate(47.1, 8.1)
        ],
        ClearanceMeters: 50,
        FallbackTerrainElevationMeters: 400)).GetAwaiter().GetResult();

    Require(result.Count == 2, "Expected two fallback preview points.");
    Require(!result[0].HasTerrainData, "Expected first point to miss terrain.");
    Require(Math.Abs(result[0].PlannedAltitudeMeters - 120) < 0.001, "Expected source altitude fallback.");
    Require(result[0].Annotation.Contains("source altitude"), "Expected source altitude annotation.");
    Require(!result[1].HasTerrainData, "Expected second point to miss terrain.");
    Require(Math.Abs(result[1].PlannedAltitudeMeters - 450) < 0.001, "Expected configured fallback clearance altitude.");
    Require(result[1].Annotation.Contains("fallback"), "Expected fallback annotation.");
}

static void ProjectTerrainPlanUiRows()
{
    var provider = new FakeTerrainProvider([
        new TerrainSample(new TerrainCoordinate(47.0, 8.0), 500, "fake")
    ]);
    var projection = new TerrainPlanUiProjection(new MissionTerrainPreviewService(new TerrainService(provider, new InMemoryTerrainCache())));

    var rows = projection.ProjectAsync([new PlanCoordinate(47.0, 8.0, 80)], clearanceMeters: 50).GetAwaiter().GetResult();

    Require(rows.Count == 1, "Expected one terrain UI row.");
    Require(rows[0].HasTerrainData, "Expected terrain-backed UI row.");
    Require(Math.Abs(rows[0].PlannedAltitudeMeters - 550) < 0.001, "Expected terrain adjusted altitude.");
    Require(rows[0].Annotation.Contains("fake", StringComparison.Ordinal), "Expected terrain annotation.");
}

static void ProjectAdvancedPlanUiPanels()
{
    var preview = new BasicComplexMissionCalculator().Calculate(new ComplexMissionCalculationRequest(
        ComplexMissionItemKind.Corridor,
        [new PlanCoordinate(47.0, 8.0), new PlanCoordinate(47.1, 8.1)]));
    var state = new AdvancedPlanUiProjector().Project(preview);

    Require(state.Panels.Count == 4, "Expected advanced plan panels.");
    Require(state.Panels.Any(static panel => panel.Id == "survey" && panel.IsAvailable), "Expected survey panel.");
    Require(state.PreviewSegments.Count == preview.PreviewSegments.Count, "Expected preview segments projected.");
    Require(state.StatusText.Contains("Preview", StringComparison.Ordinal), "Expected preview status.");
}

static void CatalogPlanAdvancedEvidence()
{
    var checklist = PlanAdvancedEvidenceCatalog.CreateV146Checklist();

    Require(checklist.Count == 6, "Expected v1.46 evidence checklist.");
    Require(checklist.Any(static item => item.Name.Contains("Shape", StringComparison.Ordinal)), "Expected shape evidence.");
    Require(checklist.All(static item => item.IsComplete), "Expected shared-core plan evidence complete.");
}

static void AuditPlanAdvancedParityGaps()
{
    var audit = PlanAdvancedParityAudit.CreateV146Audit();

    Require(audit.Count == 4, "Expected v1.46 parity audit items.");
    Require(audit.Any(static item => item.Area == "Shape Files" && item.ResidualGap.Contains("SHP", StringComparison.Ordinal)), "Expected SHP residual gap.");
    Require(audit.Any(static item => item.Area == "Runtime Evidence" && item.Completed.Contains("Unit tests", StringComparison.Ordinal)), "Expected runtime evidence status.");
}

static void LookupMissionCommandMetadata()
{
    var catalog = InMemoryMissionCommandMetadataCatalog.CreateDefault();

    var waypoint = catalog.Find(MavlinkMissionCommandIds.NavWaypoint)
        ?? throw new InvalidOperationException("Expected waypoint metadata.");
    var fenceCircle = catalog.Find(MavlinkMissionCommandIds.NavFenceCircleInclusion)
        ?? throw new InvalidOperationException("Expected fence circle metadata.");

    Require(waypoint.Label == "Waypoint", "Expected waypoint label.");
    Require(waypoint.Parameters.Any(static parameter => parameter.Label == "Latitude"), "Expected latitude parameter.");
    Require(fenceCircle.RequiresGeoFenceSupport, "Expected fence command to require geofence support.");
    Require(catalog.Find(65535) is null, "Expected unknown command lookup to return null.");
}

static void LoadQgcMissionCommandMetadata()
{
    var source = new JsonMissionCommandMetadataSource(ReadFixture("qgc-mavcmdinfo-common.json"));
    var catalog = source.LoadAsync().GetAwaiter().GetResult();

    var waypoint = catalog.Find(MavlinkMissionCommandIds.NavWaypoint)
        ?? throw new InvalidOperationException("Expected waypoint metadata.");
    var takeoff = catalog.Find(22)
        ?? throw new InvalidOperationException("Expected takeoff metadata.");

    Require(waypoint.Label == "Waypoint", "Expected QGC friendly name.");
    Require(waypoint.RawName == "MAV_CMD_NAV_WAYPOINT", "Expected raw command name.");
    Require(waypoint.Category == "Basic", "Expected category.");
    Require(waypoint.Description is not null && waypoint.Description.Contains("3D space"), "Expected description.");
    Require(waypoint.FriendlyEdit, "Expected friendly edit flag.");
    Require(waypoint.Parameters.Any(static parameter => parameter.Index == 1 && parameter.Label == "Hold" && parameter.Units == "secs"), "Expected param1 metadata.");
    Require(waypoint.Parameters.Any(static parameter => parameter.Index == 2), "Expected waypoint param2 to remain visible.");
    Require(takeoff.Parameters.All(static parameter => parameter.Index != 2 && parameter.Index != 3), "Expected paramRemove to hide takeoff params 2 and 3.");
    Require(takeoff.Parameters.Any(static parameter => parameter.Index == 7 && parameter.DefaultValue == 50), "Expected altitude default.");
}

static void GateLoadedMissionCommandMetadataByFirmware()
{
    const string json = """
    {
      "fileType": "MavCmdInfo",
      "version": 1,
      "mavCmdInfo": [
        {
          "id": 5001,
          "rawName": "MAV_CMD_NAV_FENCE_CIRCLE_INCLUSION",
          "friendlyName": "Fence Circle Inclusion",
          "category": "GeoFence"
        }
      ]
    }
    """;
    var catalog = new JsonMissionCommandMetadataSource(json).LoadAsync().GetAwaiter().GetResult();
    var availability = new MissionCommandAvailabilityService(catalog);
    var firmwareManager = new FirmwarePluginManager();
    var generic = firmwareManager.GetPlugin(MavAutopilot.Generic);
    var px4 = firmwareManager.GetPlugin(MavAutopilot.Px4);

    Require(!availability.GetAvailability(5001, generic).IsAvailable, "Expected loaded GeoFence command to be blocked by generic firmware.");
    Require(availability.GetAvailability(5001, px4).IsAvailable, "Expected loaded GeoFence command to be available on PX4.");
}

static void GateMissionCommandAvailabilityByFirmware()
{
    var catalog = InMemoryMissionCommandMetadataCatalog.CreateDefault();
    var availability = new MissionCommandAvailabilityService(catalog);
    var firmwareManager = new FirmwarePluginManager();
    var generic = firmwareManager.GetPlugin(MavAutopilot.Generic);
    var px4 = firmwareManager.GetPlugin(MavAutopilot.Px4);
    var ardupilot = firmwareManager.GetPlugin(MavAutopilot.ArduPilotMega);

    Require(availability.GetAvailability(MavlinkMissionCommandIds.NavWaypoint, generic).IsAvailable, "Expected waypoint to be generic.");
    Require(!availability.GetAvailability(MavlinkMissionCommandIds.NavFenceCircleInclusion, generic).IsAvailable, "Expected generic firmware to block geofence command.");
    Require(!availability.GetAvailability(MavlinkMissionCommandIds.NavRallyPoint, generic).IsAvailable, "Expected generic firmware to block rally command.");
    Require(availability.GetAvailability(MavlinkMissionCommandIds.NavFenceCircleInclusion, px4).IsAvailable, "Expected PX4 geofence command availability.");
    Require(availability.GetAvailability(MavlinkMissionCommandIds.NavRallyPoint, ardupilot).IsAvailable, "Expected ArduPilot rally command availability.");
    Require(!availability.GetAvailability(65535, px4).IsAvailable, "Expected unknown command to be unavailable.");
}

static void TrackGeoFenceTransferStateBoundary()
{
    var manager = new GeoFenceTransferManager();
    var empty = new GeoFencePlan();

    Require(!manager.BeginWrite(empty), "Expected empty GeoFence write to fail.");
    Require(manager.LastError == GeoFenceTransferError.EmptyGeoFence, "Expected empty GeoFence error.");

    var geoFence = new GeoFencePlan();
    geoFence.Polygons.Add(new GeoFencePolygon
    {
        Polygon =
        [
            new PlanCoordinate(47.0, 8.0),
            new PlanCoordinate(47.1, 8.1),
            new PlanCoordinate(47.2, 8.0)
        ]
    });
    geoFence.Circles.Add(new GeoFenceCircle
    {
        Circle = new GeoFenceCircleShape
        {
            Center = new PlanCoordinate(47.397742, 8.545594),
            Radius = 100
        }
    });

    Require(manager.BeginWrite(geoFence), "Expected GeoFence write to begin.");
    Require(manager.TransferType == GeoFenceTransferType.Write, "Expected GeoFence write transfer.");
    Require(manager.PolygonCount == 1, "Expected one GeoFence polygon count.");
    Require(manager.CircleCount == 1, "Expected one GeoFence circle count.");

    manager.MarkProgress(0.5);
    Require(Math.Abs(manager.Progress - 0.5) < 0.001, "Expected GeoFence progress.");

    Require(!manager.BeginRead(), "Expected overlapping GeoFence transfer to be rejected.");
    Require(manager.LastError == GeoFenceTransferError.Busy, "Expected busy GeoFence error.");

    manager.Complete();
    Require(!manager.InProgress, "Expected GeoFence transfer complete.");
    Require(Math.Abs(manager.Progress - 1) < 0.001, "Expected complete GeoFence progress.");
}

static void KeepGeoFenceTransferSeparateFromMissionTransfer()
{
    var geoFenceManager = new GeoFenceTransferManager();
    var missionManager = new MissionTransferManager();
    var geoFence = new GeoFencePlan();
    geoFence.Circles.Add(new GeoFenceCircle
    {
        Circle = new GeoFenceCircleShape
        {
            Center = new PlanCoordinate(47.397742, 8.545594),
            Radius = 100
        }
    });

    Require(geoFenceManager.BeginWrite(geoFence), "Expected GeoFence write to begin.");
    Require(!missionManager.InProgress, "Expected mission manager to remain idle.");
    Require(missionManager.LastError == MissionTransferError.None, "Expected no mission transfer error.");

    var missionAction = missionManager.BeginRead();
    Require(missionAction.Type == MissionTransferActionType.SendMissionRequestList, "Expected mission read to begin independently.");
    Require(geoFenceManager.InProgress, "Expected GeoFence transfer to remain active.");
}

static void GeoFenceTransferServiceWritesFenceItems()
{
    var (service, sent) = CreateGeoFenceTransferService();
    var missionFrames = new MavlinkMissionService(systemId: 1, componentId: 1);
    var geoFence = new GeoFencePlan();
    geoFence.Circles.Add(new GeoFenceCircle
    {
        Inclusion = true,
        Circle = new GeoFenceCircleShape
        {
            Center = new PlanCoordinate(47.397742, 8.545594),
            Radius = 75
        }
    });

    var begin = service.BeginWriteAsync(geoFence).GetAwaiter().GetResult();
    Require(begin.Type == MissionTransferActionType.SendMissionCount, "Expected GeoFence write to send count.");
    Require(sent.Count == 1, "Expected initial GeoFence count frame.");
    Require(MavlinkMissionService.TryReadMissionCount(CreatePacket(sent[0]), out var count), "Expected GeoFence mission count.");
    Require(count.MissionType == MavMissionType.Fence, "Expected Fence mission count.");
    Require(count.Count == 1, "Expected one GeoFence mission item.");

    var requestPacket = CreatePacket(missionFrames.CreateMissionRequestIntFrame(new MavlinkMissionRequestInt(255, 190, 0, MavMissionType.Fence)));
    var itemAction = service.HandlePacketAsync(requestPacket).GetAwaiter().GetResult();
    Require(itemAction.Type == MissionTransferActionType.SendMissionItemInt, "Expected GeoFence item send action.");
    Require(sent.Count == 2, "Expected GeoFence count and item frames.");
    Require(MavlinkMissionService.TryReadMissionItemInt(CreatePacket(sent[1]), out var item), "Expected GeoFence mission item.");
    Require(item.MissionType == MavMissionType.Fence, "Expected Fence mission item.");
    Require(item.Command == MavlinkMissionCommandIds.NavFenceCircleInclusion, "Expected fence circle command.");
    Require(item.TargetSystemId == 9 && item.TargetComponentId == 1, "Expected GeoFence item target ids.");
}

static void GeoFenceTransferServiceReadsFencePlan()
{
    var (service, _) = CreateGeoFenceTransferService();
    var missionFrames = new MavlinkMissionService(systemId: 1, componentId: 1);
    var geoFence = new GeoFencePlan();
    geoFence.Circles.Add(new GeoFenceCircle
    {
        Circle = new GeoFenceCircleShape
        {
            Center = new PlanCoordinate(47.397742, 8.545594),
            Radius = 75
        }
    });
    var items = GeoFenceMissionItemConverter.ToMissionItems(geoFence, targetSystemId: 9, targetComponentId: 1);

    service.BeginReadAsync().GetAwaiter().GetResult();
    service.HandlePacketAsync(CreatePacket(missionFrames.CreateMissionCountFrame(new MavlinkMissionCount(255, 190, checked((ushort)items.Count), MavMissionType.Fence)))).GetAwaiter().GetResult();
    foreach (var item in items)
    {
        service.HandlePacketAsync(CreatePacket(missionFrames.CreateMissionItemIntFrame(item))).GetAwaiter().GetResult();
    }

    Require(!service.Manager.InProgress, "Expected GeoFence read to complete.");
    Require(service.Manager.LastError == GeoFenceTransferError.None, "Expected no GeoFence read error.");
    Require(service.LastReadPlan.Circles.Count == 1, "Expected one read GeoFence circle.");
    Require(Math.Abs(service.LastReadPlan.Circles[0].Circle.Radius - 75) < 0.001, "Expected GeoFence radius to round-trip.");
}

static void GeoFenceTransferServiceMapsTimeout()
{
    var (service, sent) = CreateGeoFenceTransferService();
    service.MissionService.MaxRetryCount = 1;
    service.BeginReadAsync().GetAwaiter().GetResult();

    service.HandleTimeoutAsync().GetAwaiter().GetResult();
    service.HandleTimeoutAsync().GetAwaiter().GetResult();

    Require(sent.Count == 2, "Expected one GeoFence retry before timeout.");
    Require(!service.Manager.InProgress, "Expected GeoFence transfer to stop on timeout.");
    Require(service.Manager.LastError == GeoFenceTransferError.Timeout, "Expected GeoFence timeout error.");
}

static void TrackRallyTransferStateBoundary()
{
    var manager = new RallyPointTransferManager();
    var empty = new RallyPointsPlan();

    Require(!manager.BeginWrite(empty), "Expected empty Rally write to fail.");
    Require(manager.LastError == RallyPointTransferError.EmptyRallyPoints, "Expected empty Rally error.");

    var rallyPoints = new RallyPointsPlan();
    rallyPoints.Points.Add(new PlanCoordinate(47.39760401, 8.5509154, 50));
    rallyPoints.Points.Add(new PlanCoordinate(47.39902017, 8.54263274, 55));

    Require(manager.BeginWrite(rallyPoints), "Expected Rally write to begin.");
    Require(manager.TransferType == RallyPointTransferType.Write, "Expected Rally write transfer.");
    Require(manager.PointCount == 2, "Expected Rally point count.");

    manager.MarkProgress(0.25);
    Require(Math.Abs(manager.Progress - 0.25) < 0.001, "Expected Rally progress.");

    Require(!manager.BeginClear(), "Expected overlapping Rally transfer to be rejected.");
    Require(manager.LastError == RallyPointTransferError.Busy, "Expected busy Rally error.");

    manager.Complete();
    Require(!manager.InProgress, "Expected Rally transfer complete.");
    Require(Math.Abs(manager.Progress - 1) < 0.001, "Expected complete Rally progress.");
}

static void KeepRallyTransferSeparateFromMissionAndGeoFenceTransfer()
{
    var rallyManager = new RallyPointTransferManager();
    var geoFenceManager = new GeoFenceTransferManager();
    var missionManager = new MissionTransferManager();
    var rallyPoints = new RallyPointsPlan();
    rallyPoints.Points.Add(new PlanCoordinate(47.39760401, 8.5509154, 50));
    var geoFence = new GeoFencePlan();
    geoFence.Circles.Add(new GeoFenceCircle
    {
        Circle = new GeoFenceCircleShape
        {
            Center = new PlanCoordinate(47.397742, 8.545594),
            Radius = 100
        }
    });

    Require(rallyManager.BeginWrite(rallyPoints), "Expected Rally write to begin.");
    Require(geoFenceManager.BeginWrite(geoFence), "Expected GeoFence write to begin independently.");
    var missionAction = missionManager.BeginRead();

    Require(missionAction.Type == MissionTransferActionType.SendMissionRequestList, "Expected mission read to begin independently.");
    Require(rallyManager.InProgress, "Expected Rally transfer to remain active.");
    Require(geoFenceManager.InProgress, "Expected GeoFence transfer to remain active.");
    Require(missionManager.InProgress, "Expected mission transfer to remain active.");
}

static void RallyTransferServiceWritesRallyItems()
{
    var (service, sent) = CreateRallyTransferService();
    var missionFrames = new MavlinkMissionService(systemId: 1, componentId: 1);
    var rallyPoints = new RallyPointsPlan();
    rallyPoints.Points.Add(new PlanCoordinate(47.39760401, 8.5509154, 50));

    var begin = service.BeginWriteAsync(rallyPoints).GetAwaiter().GetResult();
    Require(begin.Type == MissionTransferActionType.SendMissionCount, "Expected Rally write to send count.");
    Require(sent.Count == 1, "Expected initial Rally count frame.");
    Require(MavlinkMissionService.TryReadMissionCount(CreatePacket(sent[0]), out var count), "Expected Rally mission count.");
    Require(count.MissionType == MavMissionType.Rally, "Expected Rally mission count type.");
    Require(count.Count == 1, "Expected one Rally mission item.");

    var requestPacket = CreatePacket(missionFrames.CreateMissionRequestIntFrame(new MavlinkMissionRequestInt(255, 190, 0, MavMissionType.Rally)));
    var itemAction = service.HandlePacketAsync(requestPacket).GetAwaiter().GetResult();
    Require(itemAction.Type == MissionTransferActionType.SendMissionItemInt, "Expected Rally item send action.");
    Require(sent.Count == 2, "Expected Rally count and item frames.");
    Require(MavlinkMissionService.TryReadMissionItemInt(CreatePacket(sent[1]), out var item), "Expected Rally mission item.");
    Require(item.MissionType == MavMissionType.Rally, "Expected Rally mission item type.");
    Require(item.Command == MavlinkMissionCommandIds.NavRallyPoint, "Expected rally command.");
    Require(item.TargetSystemId == 9 && item.TargetComponentId == 1, "Expected Rally item target ids.");
}

static void RallyTransferServiceReadsRallyPlan()
{
    var (service, _) = CreateRallyTransferService();
    var missionFrames = new MavlinkMissionService(systemId: 1, componentId: 1);
    var rallyPoints = new RallyPointsPlan();
    rallyPoints.Points.Add(new PlanCoordinate(47.39760401, 8.5509154, 50));
    var items = RallyPointMissionItemConverter.ToMissionItems(rallyPoints, targetSystemId: 9, targetComponentId: 1);

    service.BeginReadAsync().GetAwaiter().GetResult();
    service.HandlePacketAsync(CreatePacket(missionFrames.CreateMissionCountFrame(new MavlinkMissionCount(255, 190, checked((ushort)items.Count), MavMissionType.Rally)))).GetAwaiter().GetResult();
    foreach (var item in items)
    {
        service.HandlePacketAsync(CreatePacket(missionFrames.CreateMissionItemIntFrame(item))).GetAwaiter().GetResult();
    }

    Require(!service.Manager.InProgress, "Expected Rally read to complete.");
    Require(service.Manager.LastError == RallyPointTransferError.None, "Expected no Rally read error.");
    Require(service.LastReadPlan.Points.Count == 1, "Expected one read Rally point.");
    Require(Math.Abs(service.LastReadPlan.Points[0].Altitude.GetValueOrDefault() - 50) < 0.001, "Expected Rally altitude to round-trip.");
}

static void RallyTransferServiceMapsTimeout()
{
    var (service, sent) = CreateRallyTransferService();
    service.MissionService.MaxRetryCount = 1;
    service.BeginReadAsync().GetAwaiter().GetResult();

    service.HandleTimeoutAsync().GetAwaiter().GetResult();
    service.HandleTimeoutAsync().GetAwaiter().GetResult();

    Require(sent.Count == 2, "Expected one Rally retry before timeout.");
    Require(!service.Manager.InProgress, "Expected Rally transfer to stop on timeout.");
    Require(service.Manager.LastError == RallyPointTransferError.Timeout, "Expected Rally timeout error.");
}

static void GatePlanTransferSupport()
{
    var policy = new PlanTransferSupportPolicy();

    Require(policy.CanEditOffline(PlanDocumentSection.Mission), "Expected offline mission editing.");
    Require(policy.CanEditOffline(PlanDocumentSection.GeoFence), "Expected offline GeoFence editing.");
    Require(policy.CanEditOffline(PlanDocumentSection.Rally), "Expected offline Rally editing.");

    var offlineGeoFence = policy.CanTransferGeoFence(PlanTransferSupport.Offline);
    Require(!offlineGeoFence.IsAllowed, "Expected offline GeoFence transfer to be blocked.");
    Require(offlineGeoFence.Reason is not null && offlineGeoFence.Reason.Contains("connected vehicle"), "Expected offline GeoFence reason.");

    var connectedNoSupport = new PlanTransferSupport(IsConnected: true, GeoFenceSupported: false, RallySupported: false);
    var blockedGeoFence = policy.CanTransferGeoFence(connectedNoSupport);
    var blockedRally = policy.CanTransferRally(connectedNoSupport);
    Require(!blockedGeoFence.IsAllowed, "Expected unsupported GeoFence transfer to be blocked.");
    Require(!blockedRally.IsAllowed, "Expected unsupported Rally transfer to be blocked.");

    var connectedSupported = new PlanTransferSupport(IsConnected: true, GeoFenceSupported: true, RallySupported: true);
    Require(policy.CanTransferGeoFence(connectedSupported).IsAllowed, "Expected supported GeoFence transfer.");
    Require(policy.CanTransferRally(connectedSupported).IsAllowed, "Expected supported Rally transfer.");
}

static void SelectFirmwarePluginsByAutopilot()
{
    var manager = new FirmwarePluginManager();

    Require(manager.GetPlugin(MavAutopilot.Px4) is Px4FirmwarePlugin, "Expected PX4 plugin.");
    Require(manager.GetPlugin(MavAutopilot.ArduPilotMega) is ArduPilotFirmwarePlugin, "Expected ArduPilot plugin.");
    Require(manager.GetPlugin(MavAutopilot.Generic) is GenericFirmwarePlugin, "Expected Generic plugin.");
    Require(manager.GetPlugin(MavAutopilot.Invalid) is GenericFirmwarePlugin, "Expected Invalid autopilot to use Generic plugin.");
    Require(manager.GetPlugin((MavAutopilot)250) is GenericFirmwarePlugin, "Expected unknown autopilot to use Generic plugin.");
}

static void ExposeFirmwareVehicleSupportProfiles()
{
    var manager = new FirmwarePluginManager();
    var generic = manager.GetPlugin(MavAutopilot.Generic);
    var px4 = manager.GetPlugin(MavAutopilot.Px4);
    var arduPilot = manager.GetPlugin(MavAutopilot.ArduPilotMega);

    Require(!generic.Supports.GeoFenceTransfer, "Expected Generic GeoFence transfer to be unsupported.");
    Require(!generic.Supports.RallyPointTransfer, "Expected Generic Rally transfer to be unsupported.");
    Require(px4.Supports.GeoFenceTransfer, "Expected PX4 GeoFence transfer support.");
    Require(px4.Supports.RallyPointTransfer, "Expected PX4 Rally transfer support.");
    Require(arduPilot.Supports.GeoFenceTransfer, "Expected ArduPilot GeoFence transfer support.");
    Require(arduPilot.Supports.RallyPointTransfer, "Expected ArduPilot Rally transfer support.");
}

static void ResolveFirmwareSpecificFlightModes()
{
    var manager = new FirmwarePluginManager();
    var resolver = new FirmwareFlightModeResolver();
    var px4 = manager.GetPlugin(MavAutopilot.Px4);
    var ardupilot = manager.GetPlugin(MavAutopilot.ArduPilotMega);

    var px4Position = resolver.Resolve(px4, baseMode: 0x01, customMode: 0x00030000);
    var ardupilotAuto = resolver.Resolve(ardupilot, baseMode: 0x01, customMode: 3);
    var px4Unknown = resolver.Resolve(px4, baseMode: 0x01, customMode: 0xdeadbeef);

    Require(px4Position.Name == "Position", "Expected PX4 position mode.");
    Require(ardupilotAuto.Name == "Auto", "Expected ArduPilot auto mode.");
    Require(px4Unknown.Name == "Custom:0xdeadbeef", "Expected unknown mode fallback.");
}

static void ExposeFirmwareCommandCapabilityTables()
{
    var manager = new FirmwarePluginManager();
    var generic = manager.GetPlugin(MavAutopilot.Generic);
    var px4 = manager.GetPlugin(MavAutopilot.Px4);
    var ardupilot = manager.GetPlugin(MavAutopilot.ArduPilotMega);

    Require(generic.Behavior.FindCommand(MavlinkMissionCommandIds.NavWaypoint)?.IsSupported == true, "Expected generic waypoint command support.");
    Require(generic.Behavior.FindCommand(MavlinkMissionCommandIds.NavRallyPoint)?.IsSupported == false, "Expected generic rally command block.");
    Require(px4.Behavior.FindCommand(MavlinkMissionCommandIds.NavFenceCircleInclusion)?.IsSupported == true, "Expected PX4 fence command support.");
    Require(ardupilot.Behavior.FindCommand(MavlinkMissionCommandIds.NavRallyPoint)?.IsSupported == true, "Expected ArduPilot rally command support.");
    Require(px4.Behavior.FlightModes.Any(static mode => mode.Name == "Mission"), "Expected PX4 mission flight mode.");
    Require(ardupilot.Behavior.FlightModes.Any(static mode => mode.Name == "Guided"), "Expected ArduPilot guided flight mode.");
}

static void SelectVehicleSetupComponentsByFirmware()
{
    var manager = new FirmwarePluginManager();
    var catalog = new VehicleSetupComponentCatalog();
    var generic = manager.GetPlugin(MavAutopilot.Generic);
    var px4 = manager.GetPlugin(MavAutopilot.Px4);
    var ardupilot = manager.GetPlugin(MavAutopilot.ArduPilotMega);

    var genericQuad = catalog.GetComponents(generic, MavType.Quadrotor);
    var px4Quad = catalog.GetComponents(px4, MavType.Quadrotor);
    var ardupilotFixedWing = catalog.GetComponents(ardupilot, MavType.FixedWing);

    Require(genericQuad.Any(static component => component.Id == "motors" && component.IsAvailable), "Expected multirotor motor setup component.");
    Require(genericQuad.Any(static component => component.Id == "flight-modes" && !component.IsAvailable), "Expected generic flight modes to be unavailable.");
    Require(px4Quad.Any(static component => component.Id == "geofence" && component.IsAvailable), "Expected PX4 GeoFence setup component.");
    Require(ardupilotFixedWing.Any(static component => component.Id == "airframe" && component.IsAvailable), "Expected fixed-wing airframe setup component.");
}

static void ProjectReadOnlySetupSummary()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var linkManager = new LinkManager(logger);
    var vehicles = new MultiVehicleManager(protocol, logger);
    var setup = new SetupViewModel(vehicles);
    protocol.Attach(linkManager);
    var link = linkManager.CreateConnectedMockLinkAsync().GetAwaiter().GetResult();
    link.EmitIncoming(MavlinkTestFrames.HeartbeatV1(systemId: 9, componentId: 1, autopilot: MavAutopilot.Px4, vehicleType: MavType.Quadrotor));

    Require(setup.Title == "Setup", "Expected setup title.");
    Require(setup.Summary.Contains("Vehicle 9"), "Expected active vehicle summary.");
    Require(setup.FirmwareText.Contains("PX4"), "Expected firmware summary.");
    Require(setup.VehicleTypeText.Contains("Quadrotor"), "Expected vehicle type summary.");
    Require(setup.Components.Count > 0, "Expected setup components.");
    Require(setup.AvailableComponentCount > 0, "Expected available setup components.");
}

static void ProjectSetupComponentStatusFromParameters()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var linkManager = new LinkManager(logger);
    var vehicles = new MultiVehicleManager(protocol, logger);
    var setup = new SetupViewModel(vehicles);
    protocol.Attach(linkManager);
    var link = linkManager.CreateConnectedMockLinkAsync().GetAwaiter().GetResult();
    link.EmitIncoming(MavlinkTestFrames.HeartbeatV1(systemId: 9, componentId: 1, autopilot: MavAutopilot.Px4, vehicleType: MavType.Quadrotor));
    link.EmitIncoming(MavlinkTestFrames.ParamValue(systemId: 9, componentId: 1, name: "BAT_LOW_THR", value: 14.2f, count: 1, index: 0));

    var power = setup.Components.Single(component => component.Id == "power");
    var sensors = setup.Components.Single(component => component.Id == "sensors");
    var radio = setup.Components.Single(component => component.Id == "radio");

    Require(power.Readiness == VehicleSetupReadiness.Ready, "Expected power setup ready when required parameter exists.");
    Require(sensors.Readiness == VehicleSetupReadiness.Blocked, "Expected sensors blocked by missing required parameters.");
    Require(sensors.MissingParameters.Contains("CAL_ACC0_ID"), "Expected sensor blocker parameter.");
    Require(radio.Readiness == VehicleSetupReadiness.Warning, "Expected optional radio parameters to warn.");
    Require(setup.BlockedComponentCount > 0, "Expected blocked component count.");
    Require(setup.Summary.Contains("Blocked"), "Expected setup summary to include blocked count.");
}

static void RunSensorCalibrationWorkflowStates()
{
    var workflow = new SensorCalibrationWorkflow();

    var request = workflow.RequestStart(SensorCalibrationType.Compass);
    Require(workflow.Snapshot.State == SensorCalibrationState.AwaitingConfirmation, "Expected confirmation state.");
    Require(request.RequiresSafetyConfirmation, "Expected safety confirmation.");
    Require(request.CommandName == "MAV_CMD_PREFLIGHT_CALIBRATION", "Expected calibration command boundary.");
    Require(request.Parameters.ContainsKey("compass"), "Expected compass calibration parameter.");

    var confirmed = workflow.ConfirmStart();
    Require(confirmed == request, "Expected confirmed command request.");
    Require(workflow.Snapshot.State == SensorCalibrationState.InProgress, "Expected in-progress state.");

    workflow.ReportProgress(0.5, "Rotate vehicle");
    Require(Math.Abs(workflow.Snapshot.Progress - 0.5) < 0.001, "Expected progress update.");
    Require(workflow.Snapshot.StatusText == "Rotate vehicle", "Expected progress status text.");

    workflow.Complete();
    Require(workflow.Snapshot.State == SensorCalibrationState.Completed, "Expected completed calibration.");
    Require(Math.Abs(workflow.Snapshot.Progress - 1) < 0.001, "Expected complete progress.");
    Require(workflow.Snapshot.PendingCommand is null, "Expected pending command cleared.");
}

static void CancelAndFailSensorCalibrationWorkflow()
{
    var workflow = new SensorCalibrationWorkflow();

    workflow.RequestStart(SensorCalibrationType.Accelerometer);
    workflow.Cancel();
    Require(workflow.Snapshot.State == SensorCalibrationState.Cancelled, "Expected cancelled calibration.");
    Require(workflow.Snapshot.PendingCommand is null, "Expected cancelled command cleared.");

    workflow.Reset();
    workflow.RequestStart(SensorCalibrationType.Gyroscope);
    workflow.ConfirmStart();
    workflow.Fail("Gyro calibration failed");
    Require(workflow.Snapshot.State == SensorCalibrationState.Failed, "Expected failed calibration.");
    Require(workflow.Snapshot.StatusText == "Gyro calibration failed", "Expected failure reason.");
}

static void ProjectRadioAndPowerSetupBoundaries()
{
    var manager = new ParameterManager();
    ApplyParamValue(manager, componentId: 1, name: "RC_MAP_ROLL", count: 6, index: 0);
    ApplyParamValue(manager, componentId: 1, name: "RC_MAP_PITCH", count: 6, index: 1);
    ApplyParamValue(manager, componentId: 1, name: "RC_MAP_THROTTLE", count: 6, index: 2);
    ApplyParamValue(manager, componentId: 1, name: "RC_MAP_YAW", count: 6, index: 3);
    ApplyParamValue(manager, componentId: 1, name: "BAT_LOW_THR", count: 6, index: 4);

    var service = new RadioPowerSetupService();
    var status = service.Project(manager);

    Require(status.Radio.IsComplete, "Expected radio setup complete when channel map parameters exist.");
    Require(!status.Power.IsComplete, "Expected power setup incomplete with missing battery parameters.");
    Require(status.Power.MissingParameters.Contains("BAT_CRIT_THR"), "Expected missing critical battery threshold.");
    Require(status.Power.AndroidLifecycleRisk.Contains("Android"), "Expected Android lifecycle risk note.");
}

static void RunMotorSafetyWorkflowStates()
{
    var workflow = new MotorSafetyWorkflow();

    var request = workflow.RequestAction(MotorSafetyActionType.MotorTest);
    Require(workflow.Snapshot.State == MotorSafetyActionState.AwaitingSafetyConfirmation, "Expected awaiting confirmation state.");
    Require(request.RequiresExplicitConfirmation, "Expected explicit confirmation.");
    Require(request.SafetyNotice.Contains("real vehicle"), "Expected safety notice.");

    workflow.ConfirmAction();
    Require(workflow.Snapshot.State == MotorSafetyActionState.Armed, "Expected armed motor action.");
    workflow.Complete();
    Require(workflow.Snapshot.State == MotorSafetyActionState.Completed, "Expected completed motor action.");
    Require(workflow.Snapshot.PendingAction is null, "Expected pending action cleared.");
}

static void ProjectMotorSafetySetupBoundaries()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var linkManager = new LinkManager(logger);
    var vehicles = new MultiVehicleManager(protocol, logger);
    var setup = new SetupViewModel(vehicles);
    protocol.Attach(linkManager);
    var link = linkManager.CreateConnectedMockLinkAsync().GetAwaiter().GetResult();
    link.EmitIncoming(MavlinkTestFrames.HeartbeatV1(systemId: 9, componentId: 1, autopilot: MavAutopilot.Px4, vehicleType: MavType.Quadrotor));
    link.EmitIncoming(MavlinkTestFrames.ParamValue(systemId: 9, componentId: 1, name: "MOT_SPIN_MIN", value: 0.15f, count: 2, index: 0));
    link.EmitIncoming(MavlinkTestFrames.ParamValue(systemId: 9, componentId: 1, name: "COM_RC_LOSS_T", value: 3, count: 2, index: 1));

    Require(setup.MotorStatus.IsAvailable, "Expected motor setup available.");
    Require(setup.MotorStatus.RequiresExplicitConfirmation, "Expected motor setup to require confirmation.");
    Require(setup.MotorStatus.StatusText.Contains("Confirm"), "Expected motor safety text.");
    Require(setup.MotorStatus.DeviceOnlyRisk.Contains("real hardware"), "Expected motor device risk.");
    Require(setup.SafetyStatus.IsAvailable, "Expected safety setup available.");
    Require(setup.SafetyStatus.StatusText.Contains("Confirm"), "Expected safety confirmation text.");
    Require(setup.SafetyStatus.DeviceOnlyRisk.Contains("PX4"), "Expected firmware-aware safety risk.");
}

static void DerivePlanTransferSupportFromVehicleFirmware()
{
    var policy = new PlanTransferSupportPolicy();

    var offline = policy.GetSupportForVehicle(null);
    Require(!offline.IsConnected, "Expected null vehicle to be offline.");
    Require(!policy.CanTransferGeoFence(offline).IsAllowed, "Expected offline GeoFence transfer to be blocked.");

    var genericVehicle = new Vehicle(id: 1, componentId: 1, autopilot: MavAutopilot.Generic, vehicleType: MavType.Quadrotor);
    var genericSupport = policy.GetSupportForVehicle(genericVehicle);
    Require(genericSupport.IsConnected, "Expected generic vehicle support to be connected.");
    Require(!policy.CanTransferGeoFence(genericSupport).IsAllowed, "Expected generic GeoFence transfer to be blocked.");
    Require(!policy.CanTransferRally(genericSupport).IsAllowed, "Expected generic Rally transfer to be blocked.");

    var px4Vehicle = new Vehicle(id: 2, componentId: 1, autopilot: MavAutopilot.Px4, vehicleType: MavType.Quadrotor);
    var px4Support = policy.GetSupportForVehicle(px4Vehicle);
    Require(policy.CanTransferGeoFence(px4Support).IsAllowed, "Expected PX4 GeoFence transfer to be allowed.");
    Require(policy.CanTransferRally(px4Support).IsAllowed, "Expected PX4 Rally transfer to be allowed.");

    var arduPilotVehicle = new Vehicle(id: 3, componentId: 1, autopilot: MavAutopilot.ArduPilotMega, vehicleType: MavType.FixedWing);
    var arduPilotSupport = policy.GetSupportForVehicle(arduPilotVehicle);
    Require(policy.CanTransferGeoFence(arduPilotSupport).IsAllowed, "Expected ArduPilot GeoFence transfer to be allowed.");
    Require(policy.CanTransferRally(arduPilotSupport).IsAllowed, "Expected ArduPilot Rally transfer to be allowed.");
}

static PlanSectionCoordinator CreateEditableOverlayPlan()
{
    var coordinator = new PlanSectionCoordinator();
    coordinator.Mission.Items.Add(new MissionPlanItem
    {
        Command = 16,
        Params = [0, 0, 0, 0, 47.397742, 8.545594, 30],
        DoJumpId = 1
    });
    coordinator.GeoFence.Polygons.Add(new GeoFencePolygon
    {
        Polygon =
        [
            new PlanCoordinate(47.0, 8.0),
            new PlanCoordinate(47.1, 8.1),
            new PlanCoordinate(47.2, 8.0)
        ]
    });
    coordinator.GeoFence.Circles.Add(new GeoFenceCircle
    {
        Circle = new GeoFenceCircleShape
        {
            Center = new PlanCoordinate(47.397742, 8.545594),
            Radius = 100
        }
    });
    coordinator.RallyPoints.Points.Add(new PlanCoordinate(47.39760401, 8.5509154, 50));
    return coordinator;
}

static (MissionTransferService Service, List<byte[]> Sent) CreateMissionTransferService()
{
    var link = new MockLinkTransport();
    var sent = new List<byte[]>();
    link.BytesSent += (_, args) => sent.Add(args.Bytes);
    link.ConnectAsync().GetAwaiter().GetResult();
    return (new MissionTransferService(link, targetSystemId: 9, targetComponentId: 1), sent);
}

static (GeoFenceTransferService Service, List<byte[]> Sent) CreateGeoFenceTransferService()
{
    var link = new MockLinkTransport();
    var sent = new List<byte[]>();
    link.BytesSent += (_, args) => sent.Add(args.Bytes);
    link.ConnectAsync().GetAwaiter().GetResult();
    return (new GeoFenceTransferService(link, targetSystemId: 9, targetComponentId: 1), sent);
}

static (RallyPointTransferService Service, List<byte[]> Sent) CreateRallyTransferService()
{
    var link = new MockLinkTransport();
    var sent = new List<byte[]>();
    link.BytesSent += (_, args) => sent.Add(args.Bytes);
    link.ConnectAsync().GetAwaiter().GetResult();
    return (new RallyPointTransferService(link, targetSystemId: 9, targetComponentId: 1), sent);
}

static MavlinkPacket CreatePacket(byte[] bytes)
{
    var frame = ParseSingleFrame(bytes);
    return new MavlinkPacket(new MockLinkTransport(), frame.Version, frame.SystemId, frame.ComponentId, frame.MessageId, frame.Payload);
}

static List<byte[]> SendMissionTransferAction(MissionTransferManager manager, MissionTransferAction action)
{
    var link = new MockLinkTransport();
    var sent = new List<byte[]>();
    link.BytesSent += (_, args) => sent.Add(args.Bytes);
    link.ConnectAsync().GetAwaiter().GetResult();

    var sender = new MissionTransferActionSender();
    sender.SendAsync(link, targetSystemId: 9, targetComponentId: 1, manager, action).GetAwaiter().GetResult();
    return sent;
}

static (byte Version, byte Sequence, byte SystemId, byte ComponentId, uint MessageId, byte[] Payload) ParseSingleFrame(byte[] bytes)
{
    var parser = new MavlinkFrameParser();
    var frames = parser.Parse(bytes);
    Require(frames.Count == 1, "Expected one frame.");
    return frames[0];
}

static void ApplyVehicleFrame(Vehicle vehicle, MavlinkFrameParser parser, byte[] bytes)
{
    var frames = parser.Parse(bytes);
    Require(frames.Count == 1, "Expected one frame.");
    var frame = frames[0];
    Require(vehicle.ApplyPacket(new MavlinkPacket(new MockLinkTransport(), frame.Version, frame.SystemId, frame.ComponentId, frame.MessageId, frame.Payload)), "Expected vehicle to apply packet.");
}

static MavlinkMissionItemInt CreateMissionItem(ushort sequence, MavMissionType missionType = MavMissionType.Mission)
{
    return new MavlinkMissionItemInt(
        TargetSystemId: 9,
        TargetComponentId: 1,
        Sequence: sequence,
        Command: 16,
        Frame: 3,
        Current: 0,
        AutoContinue: 1,
        Param1: 0,
        Param2: 0,
        Param3: 0,
        Param4: 0,
        X: 473977420 + sequence,
        Y: 85455940 + sequence,
        Z: 30,
        MissionType: missionType);
}

static void RoundTripMinimalPlanDocument()
{
    var service = new PlanJsonService();
    var document = PlanDocument.CreateBlank();
    document.Mission.Items.Add(new MissionPlanItem
    {
        Command = 16,
        Params = [0, 0, 0, 0, 47.397742, 8.545594, 30],
        DoJumpId = 1
    });

    var json = service.Serialize(document);
    Require(json.Contains("\"fileType\""), "Expected QGC fileType field.");
    Require(json.Contains("\"mission\""), "Expected mission object.");
    Require(json.Contains("\"geoFence\""), "Expected geoFence object.");
    Require(json.Contains("\"rallyPoints\""), "Expected rallyPoints object.");

    var roundTripped = service.Deserialize(json);
    Require(roundTripped.FileType == "Plan", "Expected Plan file type.");
    Require(roundTripped.Mission.Items.Count == 1, "Expected mission item to round-trip.");
    Require(roundTripped.GeoFence.Version == 2, "Expected geoFence version.");
    Require(roundTripped.RallyPoints.Version == 2, "Expected rally points version.");
}

static void ParseQgcPlanFixtureWithSections()
{
    var service = new PlanJsonService();
    var json = ReadFixture("qgc-plan-with-sections.plan");

    var document = service.Deserialize(json);

    Require(document.FileType == "Plan", "Expected QGC Plan file type.");
    Require(document.GroundStation == "QGroundControl", "Expected source ground station.");
    Require(document.Version == 1, "Expected top-level plan version.");
    Require(document.Mission.Version == 2, "Expected mission version.");
    Require(document.Mission.GlobalPlanAltitudeMode == 1, "Expected global plan altitude mode.");
    Require(document.Mission.Items.Count == 1, "Expected one mission item.");
    Require(document.Mission.Items[0].Command == 22, "Expected takeoff command.");
    Require(double.IsNaN(document.Mission.Items[0].Params[3]), "Expected nullable QGC param to import as NaN.");
    Require(Math.Abs(document.Mission.Items[0].Params[4] - 47.3985099) < 0.0000001, "Expected mission latitude.");
    Require(document.GeoFence.Version == 2, "Expected GeoFence section version.");
    Require(document.GeoFence.Circles.Count == 1, "Expected one GeoFence circle.");
    Require(document.GeoFence.Polygons.Count == 1, "Expected one GeoFence polygon.");
    Require(document.GeoFence.Polygons[0].Polygon.Count == 4, "Expected polygon coordinates.");
    Require(document.RallyPoints.Version == 2, "Expected Rally section version.");
    Require(document.RallyPoints.Points.Count == 2, "Expected Rally points.");
}

static void ExportQgcCompatiblePlanShape()
{
    var service = new PlanJsonService();
    var document = service.Deserialize(ReadFixture("qgc-plan-with-sections.plan"));

    var exported = service.Serialize(document);
    using var json = JsonDocument.Parse(exported);
    var root = json.RootElement;

    Require(root.GetProperty("fileType").GetString() == "Plan", "Expected exported fileType.");
    Require(root.GetProperty("version").GetInt32() == 1, "Expected exported top-level version.");
    Require(root.GetProperty("mission").GetProperty("version").GetInt32() == 2, "Expected exported mission version.");
    Require(root.GetProperty("mission").GetProperty("globalPlanAltitudeMode").GetInt32() == 1, "Expected exported global altitude mode.");
    Require(root.GetProperty("geoFence").GetProperty("version").GetInt32() == 2, "Expected exported GeoFence version.");
    Require(root.GetProperty("rallyPoints").GetProperty("version").GetInt32() == 2, "Expected exported Rally version.");
    Require(root.GetProperty("mission").GetProperty("items")[0].GetProperty("params")[3].ValueKind == JsonValueKind.Null, "Expected NaN param to export as null.");
}

static void ReportStructuredPlanImportErrors()
{
    const string invalid = """
    {
      "fileType": "Mission",
      "version": 1,
      "mission": {
        "version": 2,
        "plannedHomePosition": [0, 0, 0],
        "items": [
          {
            "type": "SimpleItem",
            "command": 16,
            "frame": 3,
            "params": [0],
            "autoContinue": true,
            "doJumpId": 1
          }
        ]
      },
      "geoFence": {
        "version": 2,
        "polygons": [],
        "circles": []
      },
      "rallyPoints": {
        "version": 2,
        "points": []
      }
    }
    """;

    var service = new PlanImportExportService();
    var result = service.Import(invalid);

    Require(!result.IsSuccess, "Expected invalid import to fail.");
    Require(result.Issues.Any(static issue => issue.Path == "$.fileType"), "Expected file type issue.");
    Require(result.Issues.Any(static issue => issue.Path == "$.mission.items[0].params"), "Expected params issue.");
}

static void RoundTripPlanImportExportService()
{
    var service = new PlanImportExportService();

    var imported = service.Import(ReadFixture("qgc-plan-with-sections.plan"));
    Require(imported.IsSuccess, "Expected QGC fixture import.");
    Require(imported.Document is not null, "Expected imported document.");

    var exported = service.Export(imported.Document!);
    Require(exported.IsSuccess, "Expected export to pass validation.");
    Require(exported.Json is not null && exported.Json.Contains("\"geoFence\""), "Expected exported GeoFence section.");

    var roundTripped = service.Import(exported.Json!);
    Require(roundTripped.IsSuccess, "Expected exported plan to re-import.");
    Require(roundTripped.Document!.Mission.Items.Count == 1, "Expected mission items to round-trip.");
    Require(roundTripped.Document.GeoFence.Circles.Count == 1, "Expected GeoFence circle to round-trip.");
    Require(roundTripped.Document.RallyPoints.Points.Count == 2, "Expected Rally points to round-trip.");
}

static void PlanViewExposesImportExportStatus()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var vehicles = new MultiVehicleManager(protocol, logger);
    var planViewModel = new PlanViewModel(vehicles);

    var failed = planViewModel.ImportPlanJson("{");
    Require(!failed.IsSuccess, "Expected malformed import to fail.");
    Require(planViewModel.PlanImportExportStatusText.Contains("failed"), "Expected failed import status.");

    var imported = planViewModel.ImportPlanJson(ReadFixture("qgc-plan-with-sections.plan"));
    Require(imported.IsSuccess, "Expected fixture import.");
    Require(planViewModel.PlanImportExportStatusText == "Plan import complete", "Expected import completion status.");
    Require(planViewModel.GeoFenceCircles.Count == 1, "Expected imported GeoFence circle in ViewModel.");
    Require(planViewModel.RallyPoints.Count == 2, "Expected imported Rally points in ViewModel.");

    var exported = planViewModel.ExportPlanJson();
    Require(exported.IsSuccess, "Expected ViewModel export.");
    Require(planViewModel.PlanImportExportStatusText == "Plan export complete", "Expected export completion status.");
    Require(exported.Json is not null && exported.Json.Contains("\"mission\""), "Expected exported JSON.");
}

static void PlanViewExposesAuthoringPreviewState()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var vehicles = new MultiVehicleManager(protocol, logger);
    var planViewModel = new PlanViewModel(vehicles);

    planViewModel.UpdateSelectedWaypoint(47.397742, 8.545594, 35);
    planViewModel.AddGeoFencePolygon();
    planViewModel.AddRallyPoint();

    Require(planViewModel.PlanMapPreviewSummary.Contains("Mission 1"), "Expected mission preview count.");
    Require(planViewModel.PlanMapPreviewSummary.Contains("Fence 1"), "Expected fence preview count.");
    Require(planViewModel.PlanMapPreviewSummary.Contains("Rally 1"), "Expected rally preview count.");
    Require(planViewModel.SelectedItemSummary.Contains("Command 16"), "Expected selected item command summary.");
    Require(planViewModel.SelectedItemCoordinateText.Contains("47.397742"), "Expected selected item coordinate summary.");

    var frame = planViewModel.ProjectPlanMapDisplayFrame();
    Require(frame.HasAnyOverlay, "Expected projected Plan map overlays.");
    Require(frame.MissionWaypoints.Count == 1, "Expected one projected mission waypoint.");
    Require(frame.GeoFencePolygons.Count == 1, "Expected one projected GeoFence polygon.");
    Require(frame.RallyPoints.Count == 1, "Expected one projected Rally point.");
}

static void PlanViewExposesWorkflowPanelState()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var vehicles = new MultiVehicleManager(protocol, logger);
    var planViewModel = new PlanViewModel(vehicles);

    planViewModel.UpdateSelectedWaypoint(47.397742, 8.545594, 35);
    planViewModel.AddGeoFencePolygon();
    planViewModel.AddGeoFenceCircle();
    planViewModel.AddRallyPoint();

    var nodes = planViewModel.PlanWorkflowNodes;
    Require(nodes.Count == 4, "Expected Mission, Complex, GeoFence, and Rally workflow nodes.");

    var mission = nodes.First(node => node.Id == "mission");
    Require(mission.Section == PlanSection.Mission, "Expected mission workflow section.");
    Require(mission.Count == 1, "Expected mission workflow count.");
    Require(mission.IsActive, "Expected mission workflow to be active by default.");
    Require(!mission.HasValidationIssue, "Expected mission workflow to validate.");

    var complex = nodes.First(node => node.Id == "complex");
    Require(complex.IsComplexAuthoring, "Expected complex authoring workflow node.");
    Require(complex.Summary.Contains("pending"), "Expected complex authoring pending summary.");

    var geoFence = nodes.First(node => node.Id == "geofence");
    Require(geoFence.Section == PlanSection.GeoFence, "Expected GeoFence workflow section.");
    Require(geoFence.Count == 2, "Expected GeoFence polygon and circle count.");

    var rally = nodes.First(node => node.Id == "rally");
    Require(rally.Section == PlanSection.Rally, "Expected Rally workflow section.");
    Require(rally.Count == 1, "Expected Rally point count.");
}

static void PlanViewExposesDeepWorkflowStates()
{
    var planViewModel = new PlanViewModel(new MultiVehicleManager(new MavlinkProtocol(), new AppLogger()));
    planViewModel.AddWaypoint();
    planViewModel.AddGeoFencePolygon();
    planViewModel.AddGeoFenceCircle();
    planViewModel.AddRallyPoint();

    var nodes = planViewModel.PlanWorkflowNodes;
    Require(nodes.Any(static node => node.Id == "complex" && node.IsComplexAuthoring), "Expected complex workflow node to exist but remain pending.");
    Require(planViewModel.FileWorkflowState.AndroidStorageRiskText.Length > 0, "Expected Android storage risk text.");
    Require(planViewModel.PlanTransferCommands.Count > 0, "Expected plan transfer commands.");
}

static void AnalyzeViewRemainsPartialChartImplementation()
{
    var analyze = new AnalyzeViewModel(new MavlinkProtocol());
    Require(analyze.ChartSnapshot.Series.Count > 0, "Expected chart runtime scaffold.");
    Require(analyze.ChartSnapshot.StatusText.Contains("series", StringComparison.OrdinalIgnoreCase), "Expected chart scaffold status text.");
}

static void SetupViewExposesPartialDeepComponentCoverage()
{
    var vehicles = new MultiVehicleManager(new MavlinkProtocol(), new AppLogger());
    var setup = new SetupViewModel(vehicles);
    var components = setup.Components;

    Require(components.Any(static component => component.Id == "sensors"), "Expected sensors component.");
    Require(components.Any(static component => component.Id == "flight-modes"), "Expected flight-modes component entry.");
    Require(components.Any(static component => component.Id == "airframe" || component.Id == "motors" || component.Id == "joystick"), "Expected partial deep setup components to be exposed by current skeleton.");
}

static void PlanViewExposesFileWorkflowState()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var vehicles = new MultiVehicleManager(protocol, logger);
    var planViewModel = new PlanViewModel(vehicles);

    var initial = planViewModel.FileWorkflowState;
    Require(initial.CanCreateNew, "Expected new plan workflow to be available.");
    Require(initial.CanImport, "Expected import workflow to be available.");
    Require(initial.CanExport, "Expected export workflow to be available.");
    Require(initial.CanSave, "Expected save workflow to be available.");
    Require(!initial.IsDirty, "Expected initial plan not to be dirty.");
    Require(initial.AndroidStorageRiskText.Contains("Android scoped-storage"), "Expected Android storage risk text.");

    planViewModel.SelectedAltitude = 42;
    Require(planViewModel.FileWorkflowState.IsDirty, "Expected selected item edit to mark plan dirty.");

    var saved = planViewModel.SavePlanJson();
    Require(saved.IsSuccess, "Expected save workflow to export valid JSON.");
    Require(!planViewModel.FileWorkflowState.IsDirty, "Expected save workflow to clear dirty state.");
    Require(planViewModel.FileWorkflowState.StatusText == "Plan save complete", "Expected save status text.");

    planViewModel.NewPlan();
    Require(!planViewModel.FileWorkflowState.IsDirty, "Expected new plan to reset dirty state.");
    Require(planViewModel.MissionItems.Count == 1, "Expected new plan to include a starter waypoint.");

    var imported = planViewModel.ImportPlanJson(ReadFixture("qgc-plan-with-sections.plan"));
    Require(imported.IsSuccess, "Expected file workflow import fixture.");
    Require(!planViewModel.FileWorkflowState.IsDirty, "Expected imported plan to be clean.");

    planViewModel.Document.Mission.Items[0].Params = [1, 2];
    var invalid = planViewModel.FileWorkflowState;
    Require(!invalid.CanExport, "Expected invalid plan to block export.");
    Require(!invalid.CanSave, "Expected invalid plan to block save.");
    Require(invalid.ValidationSummary.Contains("params"), "Expected validation summary to include params error.");
}

static string ReadFixture(string fileName)
{
    return File.ReadAllText(FindFixturePath(fileName));
}

static string FindFixturePath(string fileName)
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        var candidate = Path.Combine(directory.FullName, "VGC.Tests", "Fixtures", fileName);
        if (File.Exists(candidate))
        {
            return candidate;
        }

        directory = directory.Parent;
    }

    throw new FileNotFoundException($"Could not find test fixture '{fileName}'.");
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static T WaitForResult<T>(Task<T> task, string message)
{
    var completed = Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5))).GetAwaiter().GetResult();
    Require(completed == task, message);
    return task.GetAwaiter().GetResult();
}

static void RequireThrows<TException>(Action action, string message)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}

static byte[] BuildQgcTelemetryLog(params (ulong TimestampMicros, byte[] Frame)[] entries)
{
    using var stream = new MemoryStream();
    Span<byte> timestampBytes = stackalloc byte[sizeof(ulong)];
    foreach (var entry in entries)
    {
        BinaryPrimitives.WriteUInt64BigEndian(timestampBytes, entry.TimestampMicros);
        stream.Write(timestampBytes);
        stream.Write(entry.Frame);
    }

    return stream.ToArray();
}

static void LoadTiandituApiKeyFromSettings()
{
    var store = new FakeSettingsStore();
    store.Current.TiandituApiKey = "test-key-123";
    var service = new TiandituApiKeyService(store);

    Require(service.HasKey, "Expected HasKey true when settings store has key.");
    Require(service.GetKey() == "test-key-123", "Expected key from settings store.");
}

static void LoadTiandituApiKeyFromEnvironmentFallback()
{
    var store = new FakeSettingsStore();
    store.Current.TiandituApiKey = null;
    var service = new TiandituApiKeyService(store);

    Require(!service.HasKey, "Expected HasKey false when no key configured.");
    Require(service.GetKey() is null, "Expected null key when no configuration.");
}

static void DisableTiandituProviderWhenKeyMissing()
{
    var store = new FakeSettingsStore();
    store.Current.TiandituApiKey = null;
    var service = new TiandituApiKeyService(store);
    var adapter = new TiandituProviderAdapter(service);

    Require(!adapter.Descriptor.IsUsableOnCurrentTargets, "Expected Tianditu descriptor unusable without key.");
    Require(!adapter.Descriptor.Capabilities.SupportsDesktop, "Expected Desktop unsupported without key.");
    Require(!adapter.Descriptor.Capabilities.SupportsAndroid, "Expected Android unsupported without key.");
}

static void GenerateTiandituTileUrlWithKeyInjection()
{
    var store = new FakeSettingsStore();
    store.Current.TiandituApiKey = "my-tk-key";
    var service = new TiandituApiKeyService(store);
    var adapter = new TiandituProviderAdapter(service);

    var url = adapter.GetTileUrl("tianditu-vector", 10, 827, 374);
    Require(url is not null, "Expected non-null tile URL.");
    Require(url!.Contains("tk=my-tk-key"), "Expected injected API key in URL.");
    Require(url.Contains("TILEROW=374"), "Expected Y coordinate in URL.");
    Require(url.Contains("TILECOL=827"), "Expected X coordinate in URL.");
    Require(url.Contains("TILEMATRIX=10"), "Expected zoom level in URL.");
    Require(url.Contains("vec_w/wmts"), "Expected vec layer in URL.");
}

static void AddAndDiscoverConfiguredVideoStreams()
{
    var service = new DesktopVideoService();
    var stream = new VideoStreamDescriptor("cam1", "Primary Camera", new Uri("rtsp://192.168.1.100:554/stream1"), VideoStreamProtocol.Rtsp);
    service.ConfigureStream(stream);

    var discovered = service.DiscoverStreamsAsync().GetAwaiter().GetResult();
    Require(discovered.Count == 1, "Expected one configured stream.");
    Require(discovered[0].Id == "cam1", "Expected correct stream id.");
    Require(service.RuntimeState.Status == VideoStreamRuntimeStatus.Stopped, "Expected stopped status after configure.");
}

static void StartAndStopVideoStreamStateMachine()
{
    var service = new DesktopVideoService();
    var stream = new VideoStreamDescriptor("cam1", "Primary Camera", new Uri("udp://127.0.0.1:5600"), VideoStreamProtocol.Udp);
    service.ConfigureStream(stream);

    var startTask = service.StartStreamAsync(stream);
    startTask.GetAwaiter().GetResult();
    Require(service.RuntimeState.Status == VideoStreamRuntimeStatus.Streaming, "Expected streaming status after start.");

    var state = service.GetStateAsync().GetAwaiter().GetResult();
    Require(state.IsStreaming, "Expected IsStreaming true.");
    Require(state.ActiveStream?.Id == "cam1", "Expected active stream cam1.");

    service.StopStreamAsync().GetAwaiter().GetResult();
    Require(service.RuntimeState.Status == VideoStreamRuntimeStatus.Stopped, "Expected stopped status after stop.");
}

static void RejectStartVideoStreamWhenAlreadyActive()
{
    var service = new DesktopVideoService();
    var stream = new VideoStreamDescriptor("cam1", "Primary Camera", new Uri("udp://127.0.0.1:5600"), VideoStreamProtocol.Udp);
    service.ConfigureStream(stream);
    service.StartStreamAsync(stream).GetAwaiter().GetResult();

    try
    {
        service.StartStreamAsync(stream).GetAwaiter().GetResult();
        throw new InvalidOperationException("Expected InvalidOperationException for duplicate start.");
    }
    catch (InvalidOperationException)
    {
    }

    service.StopStreamAsync().GetAwaiter().GetResult();
}

static void SendCameraImageCaptureCommand()
{
    var link = new MockLinkTransport();
    var sent = new List<byte[]>();
    link.BytesSent += (_, args) => sent.Add(args.Bytes);
    link.ConnectAsync().GetAwaiter().GetResult();

    var camera = new MavlinkCameraService(systemId: 255, componentId: 100, link);
    camera.StartImageCaptureAsync().GetAwaiter().GetResult();

    Require(sent.Count == 1, "Expected one command frame sent.");
    var frames = new MavlinkFrameParser().Parse(sent[0]);
    Require(frames.Count == 1, "Expected one MAVLink frame.");
    Require(frames[0].MessageId == 76, "Expected COMMAND_LONG message.");
    Require(camera.CurrentStatus.IsCapturingImage, "Expected IsCapturingImage true.");
}

static void SendCameraVideoRecordCommands()
{
    var link = new MockLinkTransport();
    var sent = new List<byte[]>();
    link.BytesSent += (_, args) => sent.Add(args.Bytes);
    link.ConnectAsync().GetAwaiter().GetResult();

    var camera = new MavlinkCameraService(systemId: 255, componentId: 100, link);
    camera.StartVideoRecordingAsync().GetAwaiter().GetResult();
    Require(camera.CurrentStatus.IsRecordingVideo, "Expected recording started.");

    camera.StopVideoRecordingAsync().GetAwaiter().GetResult();
    Require(!camera.CurrentStatus.IsRecordingVideo, "Expected recording stopped.");
    Require(sent.Count == 2, "Expected two command frames sent.");
}

static void SendGimbalPitchYawCommand()
{
    var link = new MockLinkTransport();
    var sent = new List<byte[]>();
    link.BytesSent += (_, args) => sent.Add(args.Bytes);
    link.ConnectAsync().GetAwaiter().GetResult();

    var gimbal = new MavlinkGimbalService(systemId: 255, componentId: 154, link);
    var command = new GimbalCommand(PitchDegrees: -30, YawDegrees: 90, LockYaw: true);
    gimbal.SetAttitudeAsync(command).GetAwaiter().GetResult();

    Require(sent.Count == 1, "Expected one command frame.");
    Require(Math.Abs(gimbal.CurrentAttitude.PitchDegrees + 30) < 0.001, "Expected pitch -30.");
    Require(Math.Abs(gimbal.CurrentAttitude.YawDegrees - 90) < 0.001, "Expected yaw 90.");
    Require(gimbal.CurrentAttitude.IsLocked, "Expected locked.");
}

static void CreateRasterTileAdapterWithOsmDescriptor()
{
    var descriptor = MapProviderCatalog.MapsuiOsmRaster;
    using var adapter = new RasterTileMapAdapter(descriptor);
    Require(adapter.Descriptor.Kind == MapProviderKind.MapsuiRaster, "Expected MapsuiRaster kind.");
    adapter.SetViewportAsync(new MapViewport(new MapCoordinate(47.397, 8.545), 10)).GetAwaiter().GetResult();
}

static void FetchRasterTileThroughAdapter()
{
    using var httpClient = new HttpClient(new FakeHttpMessageHandler([0x89, 0x50, 0x4E, 0x47]));
    using var adapter = new RasterTileMapAdapter(MapProviderCatalog.MapsuiOsmRaster, httpClient);

    var bytes = adapter.FetchTileAsync("osm-standard", 16, 33720, 15520).GetAwaiter().GetResult();

    if (bytes is null)
    {
        throw new InvalidOperationException("Expected raster tile bytes.");
    }

    Require(bytes.Length == 4, "Expected raster tile byte length.");
    Require(bytes[0] == 0x89 && bytes[1] == 0x50, "Expected PNG-like tile bytes from provider fetch path.");
}

static void StoreAndReloadMapTileCacheEntry()
{
    var store = new InMemoryMapTileCacheStore();
    var key = new MapTileCacheKey(MapProviderKind.MapsuiRaster, "osm-standard", 16, 33720, 15520);
    var entry = new MapTileCacheEntry(
        key,
        [0x89, 0x50, 0x4E, 0x47],
        "image/png",
        new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 7, 8, 0, 0, 0, TimeSpan.Zero));

    store.StoreAsync(entry).GetAwaiter().GetResult();
    var loaded = store.LoadAsync(key).GetAwaiter().GetResult();
    var list = store.ListAsync().GetAwaiter().GetResult();

    Require(loaded is not null, "Expected cached tile to reload.");
    Require(loaded!.SizeBytes == 4, "Expected cached tile size.");
    Require(list.Count == 1 && list[0].Key == key, "Expected cache metadata to list stored tile.");
}

static void CreateTiandituRasterAdapter()
{
    var store = new FakeSettingsStore();
    store.Current.TiandituApiKey = "test-tk";
    var keyService = new TiandituApiKeyService(store);
    using var adapter = new TiandituRasterAdapter(keyService);
    Require(adapter.IsAvailable, "Expected Tianditu adapter available with key.");
    adapter.SetViewportAsync(new MapViewport(new MapCoordinate(39.9, 116.4), 10)).GetAwaiter().GetResult();
}

static void FetchTiandituRasterTileThroughAdapter()
{
    var store = new FakeSettingsStore();
    store.Current.TiandituApiKey = "test-tk";
    var keyService = new TiandituApiKeyService(store);
    using var httpClient = new HttpClient(new FakeHttpMessageHandler([0x89, 0x50, 0x4E, 0x47]));
    using var adapter = new TiandituRasterAdapter(keyService, httpClient);

    var bytes = adapter.FetchTileAsync("tianditu-vector", 5, 26, 12).GetAwaiter().GetResult();

    Require(bytes is not null, "Expected Tianditu raster tile bytes.");
    Require(bytes!.Length == 4, "Expected Tianditu raster tile byte length.");
}

static void ValidateCrcExtraRegistryCoverage()
{
    Require(MavlinkCrcExtraRegistry.RegisteredCount >= 30, "Expected at least 30 CRC registered messages.");
    Require(MavlinkCrcExtraRegistry.TryGet(0, out _), "Expected HEARTBEAT CRC.");
    Require(MavlinkCrcExtraRegistry.TryGet(76, out _), "Expected COMMAND_LONG CRC.");
    Require(MavlinkCrcExtraRegistry.TryGet(147, out _), "Expected BATTERY_STATUS CRC.");
    Require(MavlinkCrcExtraRegistry.TryGet(241, out _), "Expected GIMBAL DEVICE ATTITUDE CRC.");
    Require(!MavlinkCrcExtraRegistry.TryGet(9999, out _), "Expected unknown message to return false.");
}

static void CatalogMavlinkFullProtocolCoverageGaps()
{
    var items = new MavlinkFullProtocolCoverageCatalog().Build();

    Require(items.Count >= 8, "Expected MAVLink full protocol coverage areas.");
    Require(items.Any(static item => item.Id == "MAV-312-GENERATOR-DECISION"
        && item.Disposition == MavlinkFullProtocolCoverageDisposition.Complete), "Expected generator decision coverage.");
    Require(items.Any(static item => item.Id == "MAV-312-SEED-DEFINITIONS"
        && item.Disposition == MavlinkFullProtocolCoverageDisposition.Partial), "Expected seed definition partial coverage.");
    Require(items.Any(static item => item.Id == "MAV-312-DIALECT-WIDE-GENERATOR"
        && item.Disposition == MavlinkFullProtocolCoverageDisposition.Blocked), "Expected dialect-wide generator blocker.");
    Require(items.Any(static item => item.Id == "MAV-312-DIALECT-FIXTURES"
        && item.Disposition == MavlinkFullProtocolCoverageDisposition.Partial), "Expected curated dialect fixtures to be partial.");
    Require(items.Any(static item => item.EvidenceTests.Contains("Expose MAVLink protocol evidence catalog")), "Expected protocol evidence test reference.");

    var testProgram = File.ReadAllText(FindRepositoryPath("VGC.Tests", "Program.cs"));
    foreach (var testName in items.SelectMany(static item => item.EvidenceTests).Distinct(StringComparer.Ordinal))
    {
        Require(testProgram.Contains($"(\"{testName}\"", StringComparison.Ordinal), $"Expected MAVLink coverage test registered: {testName}.");
    }
}

static void AuditMavlinkFullProtocolCoverageBlockers()
{
    var items = new MavlinkFullProtocolCoverageCatalog().Build();
    var summary = new MavlinkFullProtocolCoverageAudit().Audit(items);

    Require(summary.TotalAreas == items.Count, "Expected audit total to match catalog.");
    Require(summary.CompleteAreas >= 3, "Expected completed MAVLink coverage areas.");
    Require(summary.PartialAreas >= 2, "Expected partial MAVLink coverage areas.");
    Require(summary.BlockedAreas >= 1, "Expected explicit MAVLink full generator blocker.");
    Require(summary.GeneratedSeedMessageCount == MavlinkSeedMessageDefinitions.All.Count, "Expected seed message count from generated definitions.");
    Require(summary.RegisteredCrcCount == MavlinkCrcExtraRegistry.RegisteredCount, "Expected CRC count from registry.");
    Require(summary.RequiredEvidenceAreasCovered == MavlinkProtocolEvidenceCatalog.RequiredAreas.Count, "Expected all required evidence areas covered.");
    Require(!summary.CanClaimDialectWideGeneratedCoverage, "Expected dialect-wide generated coverage to remain blocked.");
    Require(summary.OpenBlockers.Any(static blocker => blocker.Contains("DIALECT-WIDE-GENERATOR", StringComparison.Ordinal)), "Expected dialect-wide generator blocker.");
}

static void SelectMavlinkGeneratorDecision()
{
    var decision = MavlinkGeneratorDecision.Current;
    Require(decision.StrategyId == "local-xml-build-time-generator", "Expected local XML generator strategy.");
    Require(decision.Strategy == MavlinkSchemaStrategy.LocalXmlBuildTimeGenerator, "Expected build-time XML generation.");
    Require(decision.SchemaInputPath == "VGC/Mavlink/Definitions", "Expected local schema input path.");
    Require(decision.GeneratorProjectPath == "tools/VGC.MavlinkGenerator", "Expected generator project path.");
    Require(decision.GeneratedNamespace == "VGC.Mavlink.Generated", "Expected generated MAVLink namespace.");
    Require(!decision.UsesRuntimeMavlinkPackage, "Expected no runtime MAVLink package dependency.");
    Require(decision.GeneratesStrongMessageTypes, "Expected strong message generation.");
    Require(decision.GeneratesCrcRegistry, "Expected generated CRC registry.");
    Require(decision.PreservesManualWireBoundary, "Expected parser/writer wire boundary to remain owned by VGC.");

    foreach (var messageId in new uint[] { 0, 1, 20, 21, 22, 23, 33, 43, 44, 47, 51, 73, 76, 77 })
    {
        Require(decision.Phase186SeedMessageIds.Contains(messageId), $"Expected Phase 186 seed message {messageId}.");
    }
}

static void ExposeMavlinkSeedMessageDefinitions()
{
    var decision = MavlinkGeneratorDecision.Current;
    Require(MavlinkSeedMessageDefinitions.All.Count == decision.Phase186SeedMessageIds.Count, "Expected seed definition count to match Phase 186 decision.");

    foreach (var messageId in decision.Phase186SeedMessageIds)
    {
        Require(MavlinkSeedMessageDefinitions.TryGet(messageId, out var definition), $"Expected seed definition for message {messageId}.");
        Require(definition.MinPayloadLength > 0, $"Expected minimum payload length for {definition.Name}.");
        Require(definition.MaxPayloadLength >= definition.MinPayloadLength, $"Expected valid payload length range for {definition.Name}.");
        Require(definition.Fields.Count > 0, $"Expected fields for {definition.Name}.");
    }

    Require(MavlinkSeedMessageDefinitions.TryGet("COMMAND_ACK", out var commandAck), "Expected COMMAND_ACK lookup by name.");
    Require(commandAck.MessageId == MavlinkMessageIds.CommandAck, "Expected COMMAND_ACK message id.");
    Require(commandAck.HasExtensionFields, "Expected COMMAND_ACK extension field metadata.");
    Require(MavlinkSeedMessageDefinitions.TryGet(MavlinkMessageIds.GlobalPositionInt, out var globalPosition), "Expected GLOBAL_POSITION_INT definition.");
    Require(globalPosition.ClrTypeName == nameof(MavlinkGlobalPositionInt), "Expected GLOBAL_POSITION_INT strong CLR type.");

    var fixture = XDocument.Load(Path.Combine("VGC", "Mavlink", "Definitions", "common.seed.xml"));
    var messages = fixture.Root?.Element("messages")?.Elements("message").ToArray() ?? [];
    Require(messages.Length == MavlinkSeedMessageDefinitions.All.Count, "Expected XML seed fixture to match registry count.");
}

static void LoadMavlinkArduPilotMegaSeedFixture()
{
    var fixture = XDocument.Load(Path.Combine("VGC", "Mavlink", "Definitions", "ardupilotmega.seed.xml"));
    var includes = fixture.Root?.Elements("include").Select(static element => element.Value).ToArray() ?? [];
    var messages = fixture.Root?.Element("messages")?.Elements("message").ToArray() ?? [];

    Require(includes.Contains("common.seed.xml"), "Expected ArduPilotMega seed fixture to include common seed definitions.");
    Require(messages.Any(static message => (string?)message.Attribute("name") == "GOPRO_HEARTBEAT"), "Expected ArduPilotMega seed-specific message fixture.");

    var result = MavlinkDialectXmlGenerator.Generate(fixture, ["GOPRO_HEARTBEAT"]);
    Require(result.TryGet("GOPRO_HEARTBEAT", out var goproHeartbeat), "Expected generated ArduPilotMega seed message definition.");
    Require(goproHeartbeat.MessageId == 11030, "Expected GOPRO_HEARTBEAT message id from ArduPilotMega seed.");
    Require(goproHeartbeat.CrcExtra > 0, "Expected generated GOPRO_HEARTBEAT CRC extra.");
}

static void LoadMavlinkDialectManifest()
{
    var manifest = File.ReadAllText(Path.Combine("VGC", "Mavlink", "Definitions", "dialect-manifest.json"));

    Require(manifest.Contains("\"common\"", StringComparison.Ordinal), "Expected common dialect manifest entry.");
    Require(manifest.Contains("\"ardupilotmega\"", StringComparison.Ordinal), "Expected ardupilotmega dialect manifest entry.");
    Require(manifest.Contains("common.seed.xml", StringComparison.Ordinal), "Expected common seed fixture reference.");
    Require(manifest.Contains("ardupilotmega.seed.xml", StringComparison.Ordinal), "Expected ardupilotmega seed fixture reference.");
    Require(manifest.Contains("full upstream common.xml normalization", StringComparison.Ordinal), "Expected full upstream common blocker.");
}

static void AuditMavlinkDialectIngestionPlan()
{
    var plan = new MavlinkDialectIngestionPlan();
    var items = plan.Build();
    var projections = plan.Project(items);
    var audit = new MavlinkDialectIngestionAudit();
    var blockers = audit.OpenBlockers(items);
    var consistency = new MavlinkDialectIngestionConsistency();
    var loader = new MavlinkFullUpstreamStubLoader();

    Require(items.Count == 2, "Expected common and ardupilotmega ingestion items.");
    Require(items.All(static item => item.Status == MavlinkDialectIngestionStatus.FullUpstreamMissing), "Expected full upstream dialects missing.");
    Require(items.All(static item => !item.FullUpstreamExists), "Expected missing full upstream files.");
    Require(projections.Count == items.Count, "Expected one projection per dialect.");
    Require(projections.Any(static item => item.Summary.Contains("common.xml", StringComparison.Ordinal)), "Expected projected common.xml missing summary.");
    Require(consistency.AllFullUpstreamFilesMissing(items), "Expected all upstream files missing in stub phase.");
    Require(loader.DescribeMissingFile("common", "VGC/Mavlink/Definitions/common.xml").Contains("common", StringComparison.Ordinal), "Expected missing-file description.");
    Require(!audit.CanClaimFullDialectCoverage(items), "Expected no full dialect coverage claim.");
    Require(blockers.Any(static blocker => blocker.Contains("common", StringComparison.Ordinal)), "Expected common dialect blocker.");
    Require(blockers.Any(static blocker => blocker.Contains("ardupilotmega", StringComparison.Ordinal)), "Expected ardupilotmega dialect blocker.");
}

static void AlignMavlinkSeedDefinitionsWithCrcRegistry()
{
    foreach (var definition in MavlinkSeedMessageDefinitions.All)
    {
        Require(MavlinkCrcExtraRegistry.TryGet(definition.MessageId, out var crcExtra), $"Expected CRC extra for {definition.Name}.");
        Require(crcExtra == definition.CrcExtra, $"Expected CRC extra match for {definition.Name}.");
        Require(MavlinkCrcExtraRegistry.TryGetEntry(definition.MessageId, out var entry), $"Expected CRC entry for {definition.Name}.");
        Require(entry.Source == MavlinkCrcExtraSource.GeneratedSeedDefinition, $"Expected generated CRC source for {definition.Name}.");
        Require(entry.MessageName == definition.Name, $"Expected generated CRC name for {definition.Name}.");
    }
}

static void AuditGeneratedMavlinkCrcRegistry()
{
    var audit = MavlinkCrcExtraRegistry.Audit();
    Require(audit.IsValid, "Expected generated CRC registry audit to pass.");
    Require(audit.GeneratedSeedCount == MavlinkSeedMessageDefinitions.All.Count, "Expected all seed definitions in generated CRC source.");
    Require(audit.LegacyManualCount > 0, "Expected non-seed legacy CRC entries to remain available.");
    Require(MavlinkCrcExtraRegistry.RegisteredCount == audit.GeneratedSeedCount + audit.LegacyManualCount, "Expected registry count to match generated plus legacy entries.");
}

static void KeepLegacyMavlinkCrcEntriesAvailable()
{
    Require(MavlinkCrcExtraRegistry.TryGetEntry(MavlinkMessageIds.Heartbeat, out var heartbeat), "Expected HEARTBEAT CRC entry.");
    Require(heartbeat.Source == MavlinkCrcExtraSource.GeneratedSeedDefinition, "Expected HEARTBEAT to come from generated seed definitions.");
    Require(MavlinkCrcExtraRegistry.TryGetEntry(147, out var battery), "Expected BATTERY_STATUS CRC entry.");
    Require(battery.CrcExtra == 197, "Expected BATTERY_STATUS CRC extra.");
    Require(battery.Source == MavlinkCrcExtraSource.LegacyManualTable, "Expected BATTERY_STATUS to remain legacy until added to seed definitions.");

    var snapshot = MavlinkCrcExtraRegistry.Snapshot();
    Require(snapshot.Count == MavlinkCrcExtraRegistry.RegisteredCount, "Expected snapshot count to match registry count.");
    Require(snapshot.SequenceEqual(snapshot.OrderBy(static e => e.MessageId)), "Expected snapshot ordered by message id.");
}

static void SignAndValidateMavlinkV2Frame()
{
    var writer = new MavlinkFrameWriter();
    var unsigned = writer.CreateV2Frame(255, 190, MavlinkMessageIds.Heartbeat, CreateHeartbeatPayload(), crcExtra: 50);
    var signing = CreateSigningController();

    var signed = signing.SignFrame(unsigned);
    Require(signed.Length == unsigned.Length + SigningController.SignatureBlockLength, "Expected signed frame to append signature block.");
    Require((signed[2] & 0x01) == 0x01, "Expected MAVLink v2 signed incompat flag.");

    var validation = signing.ValidateSignedFrame(signed);
    Require(validation.IsValid, validation.Error ?? "Expected signed frame to validate.");
    var signatureBlock = validation.SignatureBlock;
    Require(signatureBlock is not null, "Expected signature block.");
    Require(signatureBlock!.LinkId == 7, "Expected signing link id.");
    Require(signatureBlock.Signature.Length == 6, "Expected six byte MAVLink signature.");

    var disabled = CreateSigningController();
    disabled.DisableSigning();
    Require(!disabled.ValidateSignature(signed, signed.Length - SigningController.SignatureBlockLength), "Expected disabled signing to reject validation.");
}

static void RejectTamperedMavlinkV2Signature()
{
    var writer = new MavlinkFrameWriter();
    var unsigned = writer.CreateV2Frame(255, 190, MavlinkMessageIds.Heartbeat, CreateHeartbeatPayload(), crcExtra: 50);
    var signing = CreateSigningController();
    var signed = signing.SignFrame(unsigned);

    var tamperedPayload = signed.ToArray();
    tamperedPayload[10] ^= 0x01;
    Require(!signing.ValidateSignedFrame(tamperedPayload).IsValid, "Expected payload tampering to fail.");

    var tamperedSignature = signed.ToArray();
    tamperedSignature[^1] ^= 0x01;
    Require(!signing.ValidateSignedFrame(tamperedSignature).IsValid, "Expected signature tampering to fail.");

    var wrongKey = new SigningController(() => new DateTimeOffset(2026, 6, 26, 0, 0, 1, TimeSpan.Zero));
    wrongKey.EnableSigning(new SigningKey([9, 9, 9, 9], 7, new DateTimeOffset(2026, 6, 26, 0, 0, 0, TimeSpan.Zero)));
    Require(!wrongKey.ValidateSignedFrame(signed).IsValid, "Expected wrong key to fail.");
}

static void ParseSignedMavlinkV2Frame()
{
    var writer = new MavlinkFrameWriter();
    var unsigned = writer.CreateV2Frame(255, 190, MavlinkMessageIds.Heartbeat, CreateHeartbeatPayload(), crcExtra: 50);
    var signing = CreateSigningController();
    var signed = signing.SignFrame(unsigned);

    var frames = new MavlinkFrameParser().Parse(signed);
    Require(frames.Count == 1, "Expected parser to accept signed MAVLink v2 frame shape.");
    Require(frames[0].Version == 2, "Expected MAVLink v2 frame.");
    Require(frames[0].MessageId == MavlinkMessageIds.Heartbeat, "Expected signed HEARTBEAT frame.");
    Require(frames[0].Payload.Length == 9, "Expected signed frame payload length.");
}

static SigningController CreateSigningController()
{
    var timestamp = new DateTimeOffset(2026, 6, 26, 0, 0, 1, TimeSpan.Zero);
    var signing = new SigningController(() => timestamp);
    signing.EnableSigning(new SigningKey([1, 2, 3, 4, 5, 6, 7, 8], 7, timestamp.AddSeconds(-1)));
    return signing;
}

static byte[] CreateHeartbeatPayload()
{
    var payload = new byte[9];
    payload[4] = (byte)MavType.Gcs;
    payload[5] = (byte)MavAutopilot.Invalid;
    payload[7] = 4;
    payload[8] = 3;
    return payload;
}

static void RoundTripMavlinkFtpPayload()
{
    var request = new MavlinkFtpPacket(
        Sequence: 42,
        Session: 3,
        Opcode: MavlinkFtpOpcode.ReadFile,
        Size: 4,
        RequestOpcode: 0,
        BurstComplete: false,
        Offset: 128,
        Data: [1, 2, 3, 4]);

    var payload = request.ToPayload(targetNetwork: 0, targetSystem: 9, targetComponent: 1);
    Require(payload.Length == MavlinkFtpPacket.PayloadLength, "Expected MAVLink FTP payload length.");
    Require(payload[1] == 9 && payload[2] == 1, "Expected FTP target system/component.");
    Require(MavlinkFtpPacket.TryRead(payload, out var parsed), "Expected FTP payload parse.");
    Require(parsed.Sequence == 42, "Expected FTP sequence.");
    Require(parsed.Session == 3, "Expected FTP session.");
    Require(parsed.Opcode == MavlinkFtpOpcode.ReadFile, "Expected FTP opcode.");
    Require(parsed.Offset == 128, "Expected FTP offset.");
    Require(parsed.Data.SequenceEqual(new byte[] { 1, 2, 3, 4 }), "Expected FTP data roundtrip.");
}

static void ListMavlinkFtpDirectory()
{
    var client = new MavlinkFtpClient();
    var request = client.RequestListDirectory("/fs/microsd/log");
    Require(request.Type == MavlinkFtpActionType.SendRequest, "Expected FTP list request action.");
    Require(request.Packet is not null, "Expected FTP list request packet.");
    Require(request.Packet!.Opcode == MavlinkFtpOpcode.ListDirectory, "Expected LIST_DIRECTORY opcode.");
    Require(Encoding.UTF8.GetString(request.Packet.Data) == "/fs/microsd/log", "Expected list path data.");

    var ack = CreateFtpAck(request.Packet, Encoding.UTF8.GetBytes("F log1.ulg\0F log2.bin"));
    var complete = client.HandlePacket(ack);
    var snapshot = client.Snapshot();
    Require(complete.Type == MavlinkFtpActionType.Complete, "Expected list complete action.");
    Require(snapshot.State == MavlinkFtpTransferState.Completed, "Expected FTP list completed state.");
    Require(snapshot.DirectoryEntries.Count == 2, "Expected two directory entries.");
    Require(snapshot.DirectoryEntries[0] == "F log1.ulg", "Expected first directory entry.");
}

static void DownloadMavlinkFtpFileChunks()
{
    var client = new MavlinkFtpClient();
    var open = client.RequestDownload("/fs/microsd/log/1.ulg");
    Require(open.Packet is not null, "Expected open request packet.");
    Require(open.Packet!.Opcode == MavlinkFtpOpcode.OpenFileReadOnly, "Expected open file opcode.");

    var readAction = client.HandlePacket(CreateFtpAck(open.Packet, [], session: 5));
    Require(readAction.Type == MavlinkFtpActionType.SendRequest, "Expected first read action.");
    Require(readAction.Packet is not null, "Expected read packet.");
    Require(readAction.Packet!.Opcode == MavlinkFtpOpcode.ReadFile, "Expected read opcode.");
    Require(readAction.Packet.Session == 5, "Expected read session.");

    var nextRead = client.HandlePacket(CreateFtpAck(readAction.Packet, [10, 11, 12], session: 5));
    Require(nextRead.Type == MavlinkFtpActionType.SendRequest, "Expected next read action.");
    Require(nextRead.Packet is not null && nextRead.Packet.Offset == 3, "Expected next read offset.");

    var complete = client.HandlePacket(CreateFtpAck(nextRead.Packet!, [13, 14], session: 5, burstComplete: true));
    var snapshot = client.Snapshot();
    Require(complete.Type == MavlinkFtpActionType.Complete, "Expected download complete action.");
    Require(snapshot.State == MavlinkFtpTransferState.Completed, "Expected FTP download completed state.");
    Require(snapshot.DownloadedBytes.SequenceEqual(new byte[] { 10, 11, 12, 13, 14 }), "Expected downloaded bytes.");
}

static void HandleMavlinkFtpNakAndRetry()
{
    var client = new MavlinkFtpClient(maxRetryCount: 1);
    var request = client.RequestListDirectory("/");
    Require(request.Packet is not null, "Expected FTP request packet.");

    var retry = client.RetryPending();
    Require(retry.Type == MavlinkFtpActionType.RetryRequest, "Expected retry request.");
    Require(retry.Packet == request.Packet, "Expected retry to resend pending request.");
    Require(client.Snapshot().RetryCount == 1, "Expected retry count.");

    var failedRetry = client.RetryPending();
    Require(failedRetry.Type == MavlinkFtpActionType.Fail, "Expected retry exhaustion failure.");
    Require(client.Snapshot().State == MavlinkFtpTransferState.Failed, "Expected failed state after retry exhaustion.");

    var nakClient = new MavlinkFtpClient();
    var nakRequest = nakClient.RequestListDirectory("/");
    Require(nakRequest.Packet is not null, "Expected NAK request packet.");
    var fail = nakClient.HandlePacket(CreateFtpNak(nakRequest.Packet!, MavlinkFtpNakError.FileNotFound));
    Require(fail.Type == MavlinkFtpActionType.Fail, "Expected NAK failure action.");
    Require(nakClient.Snapshot().Error?.Contains("FileNotFound") == true, "Expected NAK error text.");
}

static MavlinkFtpPacket CreateFtpAck(MavlinkFtpPacket request, byte[] data, byte? session = null, bool burstComplete = false)
{
    return new MavlinkFtpPacket(
        request.Sequence,
        session ?? request.Session,
        MavlinkFtpOpcode.Ack,
        (byte)data.Length,
        request.Opcode,
        burstComplete,
        request.Offset,
        data);
}

static MavlinkFtpPacket CreateFtpNak(MavlinkFtpPacket request, MavlinkFtpNakError error)
{
    return new MavlinkFtpPacket(
        request.Sequence,
        request.Session,
        MavlinkFtpOpcode.Nak,
        1,
        request.Opcode,
        false,
        request.Offset,
        [(byte)error]);
}

static void DefineMavlinkMissingStrongMessageRecords()
{
    var sysStatus = new MavlinkSysStatus(
        OnboardControlSensorsPresent: 1,
        OnboardControlSensorsEnabled: 1,
        OnboardControlSensorsHealth: 1,
        Load: 500,
        VoltageBattery: 12000,
        CurrentBattery: 100,
        BatteryRemaining: 80,
        DropRateComm: 0,
        ErrorsComm: 0,
        ErrorsCount1: 0,
        ErrorsCount2: 0,
        ErrorsCount3: 0,
        ErrorsCount4: 0);
    var position = new MavlinkGlobalPositionInt(
        TimeBootMs: 42,
        LatitudeE7: 473977420,
        LongitudeE7: 85455940,
        AltitudeMillimeters: 500000,
        RelativeAltitudeMillimeters: 12000,
        VelocityNorthCms: 50,
        VelocityEastCms: 0,
        VelocityDownCms: -5,
        HeadingCentidegrees: 18000);
    var parameter = new MavlinkParameterValue("MPC_XY_VEL", 4.5f, MavlinkParamType.Real32, Count: 10, Index: 2);
    var attitude = new MavlinkAttitude(42, Roll: 0.1f, Pitch: 0.2f, Yaw: 1.5f, RollSpeed: 0.01f, PitchSpeed: 0.02f, YawSpeed: 0.03f);
    var statusText = new MavlinkStatusText(SystemId: 1, ComponentId: 1, MavlinkSeverity.Warning, "EKF variance");

    Require(sysStatus.BatteryRemaining == 80, "Expected SYS_STATUS strong record.");
    Require(position.LatitudeE7 == 473977420, "Expected GLOBAL_POSITION_INT strong record.");
    Require(parameter.Name == "MPC_XY_VEL" && parameter.Index == 2, "Expected PARAM_VALUE strong record.");
    Require(Math.Abs(attitude.Yaw - 1.5f) < 0.001f, "Expected ATTITUDE strong record.");
    Require(statusText.Text == "EKF variance" && statusText.Severity == MavlinkSeverity.Warning, "Expected STATUSTEXT strong record.");
}

static void GenerateMavlinkDefinitionsFromDialectXml()
{
    var result = MavlinkDialectXmlGenerator.Generate(MinimalMavlinkDialectXml(), RequiredMavlinkGeneratorMessages());

    Require(result.Definitions.Count == RequiredMavlinkGeneratorMessages().Count, "Expected required generated message definitions.");
    Require(result.TryGet("HEARTBEAT", out var heartbeat) && heartbeat.CrcExtra == 50 && heartbeat.MaxPayloadLength == 9, "Expected generated HEARTBEAT definition.");
    Require(result.TryGet("ATTITUDE", out var attitude) && attitude.CrcExtra == 39 && attitude.MaxPayloadLength == 28, "Expected generated ATTITUDE definition.");
    Require(result.TryGet("STATUSTEXT", out var statusText) && statusText.CrcExtra == 83 && statusText.MinPayloadLength == 51 && statusText.MaxPayloadLength == 54, "Expected generated STATUSTEXT extension definition.");
    Require(result.TryGet("MISSION_ITEM_INT", out var missionItem) && missionItem.HasExtensionFields && missionItem.CrcExtra == 38, "Expected generated mission extension definition.");
    Require(result.TryGet("COMMAND_ACK", out var commandAck) && commandAck.HasExtensionFields && commandAck.CrcExtra == 143, "Expected generated command ack extension definition.");
}

static void BuildMavlinkCrcRegistryFromDialectXml()
{
    var result = MavlinkDialectXmlGenerator.Generate(MinimalMavlinkDialectXml(), RequiredMavlinkGeneratorMessages());
    var generatedRegistry = MavlinkCrcExtraRegistry.BuildRegistryFromGeneratedDefinitions(result.Definitions);

    Require(generatedRegistry.Count == result.Definitions.Count, "Expected generated CRC registry count to match definitions.");
    Require(generatedRegistry[MavlinkMessageIds.Heartbeat].CrcExtra == 50, "Expected generated HEARTBEAT CRC.");
    Require(generatedRegistry[MavlinkMessageIds.Attitude].CrcExtra == 39, "Expected generated ATTITUDE CRC.");
    Require(generatedRegistry[MavlinkMessageIds.Statustext].CrcExtra == 83, "Expected generated STATUSTEXT CRC.");
    Require(MavlinkCrcExtraRegistry.TryGet(MavlinkMessageIds.Attitude, out var runtimeAttitudeCrc) && runtimeAttitudeCrc == 39, "Expected runtime registry to prefer generated ATTITUDE CRC.");
}

static void RoundTripGeneratedMavlinkParserWriterFrames()
{
    var result = MavlinkDialectXmlGenerator.Generate(MinimalMavlinkDialectXml(), RequiredMavlinkGeneratorMessages());
    Require(result.TryGet("ATTITUDE", out var attitude), "Expected ATTITUDE definition.");
    Require(result.TryGet("STATUSTEXT", out var statusText), "Expected STATUSTEXT definition.");

    var writer = new MavlinkFrameWriter();
    var parser = new MavlinkFrameParser();
    var attitudePayload = new byte[attitude.MaxPayloadLength];
    BitConverter.GetBytes((uint)1000).CopyTo(attitudePayload, 0);
    BitConverter.GetBytes(0.1f).CopyTo(attitudePayload, 4);
    BitConverter.GetBytes(0.2f).CopyTo(attitudePayload, 8);
    BitConverter.GetBytes(1.5f).CopyTo(attitudePayload, 12);
    var attitudeFrame = writer.CreateV1Frame(2, 1, (byte)attitude.MessageId, attitudePayload, attitude.CrcExtra);
    var frames = parser.Parse(attitudeFrame);
    Require(frames.Count == 1 && frames[0].MessageId == MavlinkMessageIds.Attitude, "Expected generated ATTITUDE frame to parse.");

    var statusPayload = new byte[statusText.MaxPayloadLength];
    statusPayload[0] = (byte)MavlinkSeverity.Info;
    Encoding.ASCII.GetBytes("generator ok").CopyTo(statusPayload, 1);
    BitConverter.GetBytes((ushort)3).CopyTo(statusPayload, 51);
    statusPayload[53] = 0;
    var statusFrame = writer.CreateV2Frame(2, 1, statusText.MessageId, statusPayload, statusText.CrcExtra);
    frames = parser.Parse(statusFrame);
    Require(frames.Count == 1 && frames[0].MessageId == MavlinkMessageIds.Statustext, "Expected generated STATUSTEXT frame to parse.");
}

static IReadOnlyList<string> RequiredMavlinkGeneratorMessages()
{
    return
    [
        "HEARTBEAT",
        "SYS_STATUS",
        "GLOBAL_POSITION_INT",
        "ATTITUDE",
        "PARAM_REQUEST_READ",
        "PARAM_REQUEST_LIST",
        "PARAM_VALUE",
        "PARAM_SET",
        "MISSION_REQUEST_LIST",
        "MISSION_COUNT",
        "MISSION_REQUEST_INT",
        "MISSION_ITEM_INT",
        "MISSION_ACK",
        "MISSION_CLEAR_ALL",
        "COMMAND_LONG",
        "COMMAND_ACK",
        "STATUSTEXT"
    ];
}

static string MinimalMavlinkDialectXml()
{
    return """
<mavlink>
  <messages>
    <message id="0" name="HEARTBEAT">
      <field type="uint32_t" name="custom_mode" />
      <field type="uint8_t" name="type" />
      <field type="uint8_t" name="autopilot" />
      <field type="uint8_t" name="base_mode" />
      <field type="uint8_t" name="system_status" />
      <field type="uint8_t_mavlink_version" name="mavlink_version" />
    </message>
    <message id="1" name="SYS_STATUS">
      <field type="uint32_t" name="onboard_control_sensors_present" />
      <field type="uint32_t" name="onboard_control_sensors_enabled" />
      <field type="uint32_t" name="onboard_control_sensors_health" />
      <field type="uint16_t" name="load" />
      <field type="uint16_t" name="voltage_battery" />
      <field type="int16_t" name="current_battery" />
      <field type="uint16_t" name="drop_rate_comm" />
      <field type="uint16_t" name="errors_comm" />
      <field type="uint16_t" name="errors_count1" />
      <field type="uint16_t" name="errors_count2" />
      <field type="uint16_t" name="errors_count3" />
      <field type="uint16_t" name="errors_count4" />
      <field type="int8_t" name="battery_remaining" />
    </message>
    <message id="20" name="PARAM_REQUEST_READ">
      <field type="int16_t" name="param_index" />
      <field type="uint8_t" name="target_system" />
      <field type="uint8_t" name="target_component" />
      <field type="char[16]" name="param_id" />
    </message>
    <message id="21" name="PARAM_REQUEST_LIST">
      <field type="uint8_t" name="target_system" />
      <field type="uint8_t" name="target_component" />
    </message>
    <message id="22" name="PARAM_VALUE">
      <field type="float" name="param_value" />
      <field type="uint16_t" name="param_count" />
      <field type="uint16_t" name="param_index" />
      <field type="char[16]" name="param_id" />
      <field type="uint8_t" name="param_type" />
    </message>
    <message id="23" name="PARAM_SET">
      <field type="float" name="param_value" />
      <field type="uint8_t" name="target_system" />
      <field type="uint8_t" name="target_component" />
      <field type="char[16]" name="param_id" />
      <field type="uint8_t" name="param_type" />
    </message>
    <message id="30" name="ATTITUDE">
      <field type="uint32_t" name="time_boot_ms" />
      <field type="float" name="roll" />
      <field type="float" name="pitch" />
      <field type="float" name="yaw" />
      <field type="float" name="rollspeed" />
      <field type="float" name="pitchspeed" />
      <field type="float" name="yawspeed" />
    </message>
    <message id="33" name="GLOBAL_POSITION_INT">
      <field type="uint32_t" name="time_boot_ms" />
      <field type="int32_t" name="lat" />
      <field type="int32_t" name="lon" />
      <field type="int32_t" name="alt" />
      <field type="int32_t" name="relative_alt" />
      <field type="int16_t" name="vx" />
      <field type="int16_t" name="vy" />
      <field type="int16_t" name="vz" />
      <field type="uint16_t" name="hdg" />
    </message>
    <message id="43" name="MISSION_REQUEST_LIST">
      <field type="uint8_t" name="target_system" />
      <field type="uint8_t" name="target_component" />
      <extensions />
      <field type="uint8_t" name="mission_type" />
    </message>
    <message id="44" name="MISSION_COUNT">
      <field type="uint16_t" name="count" />
      <field type="uint8_t" name="target_system" />
      <field type="uint8_t" name="target_component" />
      <extensions />
      <field type="uint8_t" name="mission_type" />
    </message>
    <message id="45" name="MISSION_CLEAR_ALL">
      <field type="uint8_t" name="target_system" />
      <field type="uint8_t" name="target_component" />
      <extensions />
      <field type="uint8_t" name="mission_type" />
    </message>
    <message id="47" name="MISSION_ACK">
      <field type="uint8_t" name="target_system" />
      <field type="uint8_t" name="target_component" />
      <field type="uint8_t" name="type" />
      <extensions />
      <field type="uint8_t" name="mission_type" />
    </message>
    <message id="51" name="MISSION_REQUEST_INT">
      <field type="uint16_t" name="seq" />
      <field type="uint8_t" name="target_system" />
      <field type="uint8_t" name="target_component" />
      <extensions />
      <field type="uint8_t" name="mission_type" />
    </message>
    <message id="73" name="MISSION_ITEM_INT">
      <field type="float" name="param1" />
      <field type="float" name="param2" />
      <field type="float" name="param3" />
      <field type="float" name="param4" />
      <field type="int32_t" name="x" />
      <field type="int32_t" name="y" />
      <field type="float" name="z" />
      <field type="uint16_t" name="seq" />
      <field type="uint16_t" name="command" />
      <field type="uint8_t" name="target_system" />
      <field type="uint8_t" name="target_component" />
      <field type="uint8_t" name="frame" />
      <field type="uint8_t" name="current" />
      <field type="uint8_t" name="autocontinue" />
      <extensions />
      <field type="uint8_t" name="mission_type" />
    </message>
    <message id="76" name="COMMAND_LONG">
      <field type="float" name="param1" />
      <field type="float" name="param2" />
      <field type="float" name="param3" />
      <field type="float" name="param4" />
      <field type="float" name="param5" />
      <field type="float" name="param6" />
      <field type="float" name="param7" />
      <field type="uint16_t" name="command" />
      <field type="uint8_t" name="target_system" />
      <field type="uint8_t" name="target_component" />
      <field type="uint8_t" name="confirmation" />
    </message>
    <message id="77" name="COMMAND_ACK">
      <field type="uint16_t" name="command" />
      <field type="uint8_t" name="result" />
      <extensions />
      <field type="uint8_t" name="progress" />
      <field type="int32_t" name="result_param2" />
      <field type="uint8_t" name="target_system" />
      <field type="uint8_t" name="target_component" />
    </message>
    <message id="253" name="STATUSTEXT">
      <field type="uint8_t" name="severity" />
      <field type="char[50]" name="text" />
      <extensions />
      <field type="uint16_t" name="id" />
      <field type="uint8_t" name="chunk_seq" />
    </message>
  </messages>
</mavlink>
""";
}

static void ApplyVehicleAttitudeTelemetry()
{
    var parser = new MavlinkFrameParser();
    var frames = parser.Parse(MavlinkTestFrames.Attitude());
    Require(frames.Count == 1, "Expected attitude frame.");

    var vehicle = new Vehicle(1, 1, MavAutopilot.Px4, MavType.Quadrotor);
    var frame = frames[0];
    vehicle.ApplyPacket(new MavlinkPacket(new MockLinkTransport(), frame.Version, frame.SystemId, frame.ComponentId, frame.MessageId, frame.Payload));

    Require(vehicle.PitchDegrees.HasValue, "Expected pitch value.");
    Require(vehicle.RollDegrees.HasValue, "Expected roll value.");
}

static void SerializeQgcCompatiblePlanWithCoordinate()
{
    var item = new MissionPlanItem
    {
        Command = 16,
        DoJumpId = 1,
        Params = [0, 0, 0, 0, 47.397742, 8.545594, 30]
    };
    item.SyncCoordinateFromParams();

    var service = new PlanJsonService();
    var doc = PlanDocument.CreateBlank();
    doc.Mission.Items.Add(item);
    var json = service.Serialize(doc);

    Require(json.Contains("\"coordinate\""), "Expected coordinate field in JSON.");
    Require(json.Contains("47.397742"), "Expected latitude in coordinate.");
    Require(json.Contains("8.545594"), "Expected longitude in coordinate.");
    Require(json.Contains("\"fileType\""), "Expected fileType field.");

    var roundTripped = service.Deserialize(json);
    Require(roundTripped.Mission.Items.Count == 1, "Expected one item after round trip.");
    var rtItem = roundTripped.Mission.Items[0];
    Require(rtItem.HasCoordinate, "Expected coordinate after deserialization.");
    Require(Math.Abs(rtItem.CoordinateLat - 47.397742) < 0.000001, "Expected latitude round trip.");
}

static void EvaluatePreflightChecklistWithTelemetry()
{
    var vehicle = new Vehicle(1, 1, MavAutopilot.Px4, MavType.Quadrotor);
    var parser = new MavlinkFrameParser();

    var gpsFrames = parser.Parse(MavlinkTestFrames.GpsRawInt(fixType: 3, satellitesVisible: 14));
    vehicle.ApplyPacket(new MavlinkPacket(new MockLinkTransport(), gpsFrames[0].Version, gpsFrames[0].SystemId, gpsFrames[0].ComponentId, gpsFrames[0].MessageId, gpsFrames[0].Payload));

    var posFrames = parser.Parse(MavlinkTestFrames.GlobalPositionInt());
    vehicle.ApplyPacket(new MavlinkPacket(new MockLinkTransport(), posFrames[0].Version, posFrames[0].SystemId, posFrames[0].ComponentId, posFrames[0].MessageId, posFrames[0].Payload));

    var sysFrames = parser.Parse(MavlinkTestFrames.SysStatus());
    vehicle.ApplyPacket(new MavlinkPacket(new MockLinkTransport(), sysFrames[0].Version, sysFrames[0].SystemId, sysFrames[0].ComponentId, sysFrames[0].MessageId, sysFrames[0].Payload));

    var service = new PreflightChecklistService();
    var checklist = service.Evaluate(vehicle);

    Require(checklist.Items.Count == 7, "Expected 7 checklist items.");
    Require(checklist.Items.Any(i => i.Id == "gps-fix" && i.Status == ChecklistItemStatus.Passed), "Expected GPS fix passed.");
    Require(checklist.Items.Any(i => i.Id == "battery"), "Expected battery check.");
    Require(checklist.Items.Any(i => i.Id == "home" && i.Status == ChecklistItemStatus.Passed), "Expected home position passed.");
}

static void DetectCommunicationLostOnHeartbeatTimeout()
{
    var protocol = new MavlinkProtocol();
    var logger = new VGC.Core.Logging.AppLogger();
    var linkManager = new LinkManager(logger);
    protocol.Attach(linkManager);
    var vehicles = new MultiVehicleManager(protocol, logger);

    var lostDetected = false;
    vehicles.CommunicationLost += (_, _) => lostDetected = true;

    var link = linkManager.CreateConnectedMockLinkAsync().GetAwaiter().GetResult();
    link.EmitIncoming(MavlinkTestFrames.HeartbeatV1(systemId: 7));

    Require(vehicles.Vehicles.Count == 1, "Expected one vehicle.");
    Require(!vehicles.Vehicles[0].IsCommunicationLost, "Expected not lost initially.");

    vehicles.CheckHeartbeatTimeouts();
    Require(!lostDetected, "Expected no loss for recent heartbeat.");
}

static void IgnoreNonVehicleHeartbeats()
{
    var protocol = new MavlinkProtocol();
    var logger = new VGC.Core.Logging.AppLogger();
    var linkManager = new LinkManager(logger);
    protocol.Attach(linkManager);
    var vehicles = new MultiVehicleManager(protocol, logger);
    var link = linkManager.CreateConnectedMockLinkAsync().GetAwaiter().GetResult();

    link.EmitIncoming(MavlinkTestFrames.HeartbeatV1(systemId: 8, componentId: 1, vehicleType: MavType.Gcs));
    link.EmitIncoming(MavlinkTestFrames.HeartbeatV1(systemId: 9, componentId: 154, vehicleType: MavType.Quadrotor));
    link.EmitIncoming(MavlinkTestFrames.HeartbeatV1(systemId: 10, componentId: 1, vehicleType: MavType.Quadrotor));

    Require(vehicles.Vehicles.Count == 1, "Expected only autopilot vehicle heartbeat to create a vehicle.");
    Require(vehicles.Vehicles[0].Id == 10, "Expected GCS/component heartbeats ignored like QGC MultiVehicleManager.");
}

static void TrackVehicleLinkManagerBytesAndErrors()
{
    var link = new MockLinkTransport();
    link.ConnectAsync().GetAwaiter().GetResult();

    var vehicle = new Vehicle(1, 1, MavAutopilot.Px4, MavType.Quadrotor);
    vehicle.LinkManager.ActiveLink = link;
    Require(vehicle.LinkManager.HasActiveLink, "Expected active link.");
    Require(vehicle.LinkManager.CanSend, "Expected send-capable link.");

    vehicle.LinkManager.Detach();
    Require(!vehicle.LinkManager.HasActiveLink, "Expected no active link after detach.");
}

static void EnqueueAndAcknowledgeVehicleCommand()
{
    var queue = new VehicleCommandQueue();
    var result = queue.TryEnqueue(targetComponentId: 1, command: MavlinkCommandIds.ComponentArmDisarm);
    Require(result.Sent, "Expected command to be sent.");
    Require(queue.PendingCount == 1, "Expected one pending command.");

    var duplicate = queue.TryEnqueue(1, MavlinkCommandIds.ComponentArmDisarm);
    Require(!duplicate.Sent, "Expected duplicate rejection.");

    var ack = queue.TryAcknowledge(1, MavlinkCommandIds.ComponentArmDisarm);
    Require(ack, "Expected ACK to clear pending command.");
    Require(queue.PendingCount == 0, "Expected zero pending after ACK.");
}

static void RetryExpiredVehicleCommand()
{
    var queue = new VehicleCommandQueue { CommandTimeout = TimeSpan.Zero };
    queue.TryEnqueue(targetComponentId: 1, command: MavlinkCommandIds.ComponentArmDisarm, maxRetries: 2);

    var retry = queue.TryRetry(1, MavlinkCommandIds.ComponentArmDisarm);
    Require(retry is not null && retry.Sent, "Expected retry to send.");
    var retriedCommand = retry?.PendingCommand;
    Require(retriedCommand is not null && retriedCommand.RetryCount == 1, "Expected retry count 1.");
}

static void FailVehicleCommandAfterMaxRetries()
{
    var queue = new VehicleCommandQueue { CommandTimeout = TimeSpan.Zero };
    queue.TryEnqueue(targetComponentId: 1, command: MavlinkCommandIds.ComponentArmDisarm, maxRetries: 0);

    var failed = queue.TryRetry(1, MavlinkCommandIds.ComponentArmDisarm);
    Require(failed is not null && !failed.Sent, "Expected retry failure.");
    var failureReason = failed?.FailureReason;
    Require(failureReason is not null && failureReason.Contains("retry limit"), "Expected retry limit failure reason.");
    Require(queue.PendingCount == 0, "Expected failed command to be removed.");
}

static void GetVehicleCapabilitiesFromFirmwarePlugin()
{
    var vehicle = new Vehicle(1, 1, MavAutopilot.Px4, MavType.Quadrotor);
    var caps = vehicle.Capabilities.GetCapabilities(vehicle);
    Require(caps.GeoFenceTransfer, "Expected PX4 GeoFence support.");
    Require(caps.RallyPointTransfer, "Expected PX4 Rally support.");
    Require(caps.FlightModeCount > 0, "Expected flight modes for PX4.");
    Require(caps.SupportedCommandCount > 0, "Expected supported commands for PX4.");
    Require(vehicle.Capabilities.CanExecuteCommand(vehicle, MavlinkMissionCommandIds.NavWaypoint), "Expected waypoint support.");
    Require(vehicle.Capabilities.HasFlightMode(vehicle, 0x04040000), "Expected PX4 Mission mode support.");
}

static void SetMessageIntervalViaCommandLong()
{
    var link = new MockLinkTransport();
    var sent = new List<byte[]>();
    link.BytesSent += (_, args) => sent.Add(args.Bytes.ToArray());
    link.ConnectAsync().GetAwaiter().GetResult();
    var manager = new MessageIntervalManager(systemId: 255, componentId: 190);
    manager.SetMessageIntervalAsync(link, messageId: 33, intervalMicroseconds: 500000).GetAwaiter().GetResult();
    Require(manager.RequestCount == 1, "Expected one interval request.");
    Require(manager.ActiveRequests.ContainsKey(33), "Expected message 33 registered.");

    var frames = new MavlinkFrameParser().Parse(sent[0]);
    Require(frames.Count == 1 && frames[0].MessageId == 76, "Expected COMMAND_LONG frame.");
    Require(BitConverter.ToUInt16(frames[0].Payload, 28) == 511, "Expected MAV_CMD_SET_MESSAGE_INTERVAL.");
    Require(Math.Abs(BitConverter.ToSingle(frames[0].Payload, 0) - 33) < 0.001f, "Expected message id in param1.");
    Require(Math.Abs(BitConverter.ToSingle(frames[0].Payload, 4) - 500000) < 0.001f, "Expected interval in param2.");
}

static void ApplyMessageIntervalRuntimePolicies()
{
    var link = new MockLinkTransport();
    link.ConnectAsync().GetAwaiter().GetResult();
    var manager = new MessageIntervalManager(systemId: 255, componentId: 190);

    manager.SetDefaultRatesAsync(link).GetAwaiter().GetResult();
    Require(manager.ActiveRequests.Count == 5, "Expected five default rate requests.");
    Require(manager.ActiveRequests[0].IntervalMicroseconds == 1000000, "Expected 1 Hz heartbeat default.");
    Require(manager.ActiveRequests[33].IntervalMicroseconds == 200000, "Expected 5 Hz global position default.");

    manager.SetHighRateTelemetryAsync(link).GetAwaiter().GetResult();
    Require(manager.ActiveRequests[33].IntervalMicroseconds == 100000, "Expected high-rate global position.");
    Require(manager.ActiveRequests[30].IntervalMicroseconds == 50000, "Expected high-rate attitude.");

    manager.SetLowRateTelemetryAsync(link).GetAwaiter().GetResult();
    Require(manager.ActiveRequests[33].IntervalMicroseconds == 1000000, "Expected low-rate global position.");
    Require(manager.ActiveRequests[1].IntervalMicroseconds == 5000000, "Expected low-rate SYS_STATUS.");
}

static void RetryAndAcknowledgeMessageIntervalRequests()
{
    var link = new MockLinkTransport();
    var sent = new List<byte[]>();
    link.BytesSent += (_, args) => sent.Add(args.Bytes.ToArray());
    link.ConnectAsync().GetAwaiter().GetResult();
    var manager = new MessageIntervalManager(systemId: 255, componentId: 190) { RequestTimeout = TimeSpan.Zero };

    manager.SetMessageIntervalAsync(link, messageId: 33, intervalMicroseconds: 500000).GetAwaiter().GetResult();
    Require(manager.GetExpiredRequests().Any(id => id == 33), "Expected interval request to be expired.");

    var retry = manager.RetryExpiredAsync(link, 33).GetAwaiter().GetResult();
    Require(retry, "Expected retry to send.");
    Require(manager.ActiveRequests[33].RetryCount == 1, "Expected retry count to increment.");
    Require(sent.Count == 2, "Expected original and retry frames.");

    var ack = manager.AcknowledgeInterval(33);
    Require(ack, "Expected ACK to clear interval request.");
    Require(manager.RequestCount == 0, "Expected no active interval requests after ACK.");
}

static void ExposeMavlinkStreamConfigProfiles()
{
    var defaults = MavlinkStreamConfig.GetProfile(MavlinkStreamProfile.Default);
    Require(defaults.Count == 5, "Expected five default stream entries.");
    Require(defaults.Any(e => e.MessageId == MavlinkMessageIds.Heartbeat && e.IntervalMicroseconds == 1000000), "Expected default HEARTBEAT rate.");
    Require(defaults.Any(e => e.MessageId == MavlinkMessageIds.GlobalPositionInt && e.IntervalMicroseconds == 200000), "Expected default GLOBAL_POSITION_INT rate.");

    var high = MavlinkStreamConfig.GetProfile(MavlinkStreamProfile.HighRateTelemetry);
    Require(high.Count == 2, "Expected two high-rate stream entries.");
    Require(high.Any(e => e.MessageId == 30 && e.IntervalMicroseconds == 50000), "Expected high-rate ATTITUDE.");

    var low = MavlinkStreamConfig.GetProfile(MavlinkStreamProfile.LowRateTelemetry);
    Require(low.Any(e => e.MessageId == MavlinkMessageIds.SysStatus && e.IntervalMicroseconds == 5000000), "Expected low-rate SYS_STATUS.");
}

static void ApplyStreamConfigProfileThroughVehicleManager()
{
    var link = new MockLinkTransport();
    var sent = new List<byte[]>();
    link.BytesSent += (_, args) => sent.Add(args.Bytes.ToArray());
    link.ConnectAsync().GetAwaiter().GetResult();
    var vehicle = new Vehicle(9, 1, MavAutopilot.Px4, MavType.Quadrotor);

    vehicle.MessageIntervalManager.ApplyStreamProfileAsync(link, MavlinkStreamProfile.HighRateTelemetry).GetAwaiter().GetResult();

    var snapshot = vehicle.MessageIntervalManager.Snapshot();
    Require(snapshot.ActiveProfile == MavlinkStreamProfile.HighRateTelemetry, "Expected active high-rate stream profile.");
    Require(snapshot.DesiredRates.Count == 2, "Expected high-rate desired rates.");
    Require(snapshot.PendingRequests.Count == 2, "Expected pending requests for high-rate profile.");
    Require(snapshot.PendingRequests.Any(r => r.MessageId == MavlinkMessageIds.GlobalPositionInt && r.IntervalMicroseconds == 100000), "Expected high-rate position request.");
    Require(sent.Count == 2, "Expected two SET_MESSAGE_INTERVAL frames.");
}

static void TrackInitialConnectStateMachine()
{
    var service = new InitialConnectService();
    Require(service.State == InitialConnectState.Disconnected, "Expected disconnected initially.");

    service.MarkHeartbeatReceived();
    Require(service.State == InitialConnectState.WaitingForHeartbeat, "Expected waiting for heartbeat.");

    service.BeginParameterRequest(expectedCount: 100);
    Require(service.State == InitialConnectState.RequestingParameters, "Expected requesting parameters.");

    for (var i = 0; i < 100; i++)
    {
        service.MarkParameterReceived();
    }
    Require(service.State == InitialConnectState.RequestingMission, "Expected requesting mission after params complete.");

    service.MarkMissionReceived();
    service.MarkHomeReceived();
    Require(service.State == InitialConnectState.RequestingComponentInformation, "Expected requesting component information after home.");
    Require(Math.Abs(service.Snapshot.Progress - 0.9) < 0.001, "Expected 90% progress while loading component information.");

    service.BeginComponentInformationRequest(expectedCount: 1);
    service.MarkComponentInformationReceived();
    Require(service.State == InitialConnectState.Ready, "Expected ready.");
    Require(Math.Abs(service.Snapshot.Progress - 1.0) < 0.001, "Expected 100% progress.");
}

static void RequestStreamsAndParametersAfterInitialHeartbeat()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var linkManager = new LinkManager(logger);
    protocol.Attach(linkManager);
    var manager = new MultiVehicleManager(protocol, logger);
    var link = linkManager.CreateConnectedMockLinkAsync("initial-connect").GetAwaiter().GetResult();
    var sent = new List<byte[]>();
    link.BytesSent += (_, args) => sent.Add(args.Bytes.ToArray());

    link.EmitIncoming(MavlinkTestFrames.HeartbeatV1(systemId: 7, componentId: 1, autopilot: MavAutopilot.ArduPilotMega));
    WaitFor(() => manager.ActiveVehicle is not null && sent.Count >= 6);

    var vehicle = manager.ActiveVehicle;
    Require(vehicle is not null, "Expected active vehicle after heartbeat.");
    Require(ReferenceEquals(vehicle!.LinkManager.ActiveLink, link), "Expected initial heartbeat link to become active vehicle link.");
    Require(vehicle.InitialConnect.State == InitialConnectState.RequestingParameters, "Expected initial connect to request parameters.");
    Require(vehicle.MessageIntervalManager.ActiveProfile == MavlinkStreamProfile.Default, "Expected default stream profile after heartbeat.");

    var frames = new MavlinkFrameParser().Parse(sent.SelectMany(static bytes => bytes).ToArray());
    Require(frames.Count(static frame => frame.MessageId == MavlinkMessageIds.CommandLong) == 5, "Expected five SET_MESSAGE_INTERVAL commands.");
    Require(frames.Any(static frame => frame.MessageId == MavlinkMessageIds.ParamRequestList), "Expected PARAM_REQUEST_LIST after heartbeat.");
}

static void WaitFor(Func<bool> condition, int attempts = 50)
{
    for (var i = 0; i < attempts; i++)
    {
        if (condition())
        {
            return;
        }

        Task.Delay(10).GetAwaiter().GetResult();
    }
}

static void UpdateBatteryFactGroupFromVehicleTelemetry()
{
    var vehicle = new Vehicle(1, 1, MavAutopilot.Px4, MavType.Quadrotor);
    var parser = new MavlinkFrameParser();
    var frames = parser.Parse(MavlinkTestFrames.SysStatus());
    vehicle.ApplyPacket(new MavlinkPacket(new MockLinkTransport(), frames[0].Version, frames[0].SystemId, frames[0].ComponentId, frames[0].MessageId, frames[0].Payload));

    Require(vehicle.Battery.Voltage is not null, "Expected battery voltage fact.");
    Require(vehicle.Battery.RemainingPercent is not null, "Expected battery remaining fact.");
    Require(vehicle.Battery.Summary.Contains("V"), "Expected voltage in summary.");
}

static void UpdateVehicleBaselineFactGroups()
{
    var vehicle = new Vehicle(1, 1, MavAutopilot.Px4, MavType.Quadrotor);
    var parser = new MavlinkFrameParser();

    var gps = parser.Parse(MavlinkTestFrames.GpsRawInt(fixType: 3, satellitesVisible: 14))[0];
    vehicle.ApplyPacket(new MavlinkPacket(new MockLinkTransport(), gps.Version, gps.SystemId, gps.ComponentId, gps.MessageId, gps.Payload));
    Require(vehicle.Gps.FixType?.RawValue is not null, "Expected GPS fix fact value.");
    Require(Convert.ToInt32(vehicle.Gps.Satellites?.RawValue) == 14, "Expected satellite count fact.");

    var attitude = parser.Parse(MavlinkTestFrames.Attitude(roll: 0.1f, pitch: 0.2f, yaw: 1.5f))[0];
    vehicle.ApplyPacket(new MavlinkPacket(new MockLinkTransport(), attitude.Version, attitude.SystemId, attitude.ComponentId, attitude.MessageId, attitude.Payload));
    Require(vehicle.Attitude.Pitch?.RawValue is not null, "Expected attitude pitch fact.");
    Require(vehicle.Attitude.Roll?.RawValue is not null, "Expected attitude roll fact.");
    Require(vehicle.Attitude.Heading?.RawValue is not null, "Expected attitude heading fact.");

    var estimator = parser.Parse(MavlinkTestFrames.EstimatorStatus(flags: 1))[0];
    vehicle.ApplyPacket(new MavlinkPacket(new MockLinkTransport(), estimator.Version, estimator.SystemId, estimator.ComponentId, estimator.MessageId, estimator.Payload));
    Require(vehicle.Ekf.Healthy?.RawValue is true, "Expected EKF healthy fact.");
    Require(Convert.ToUInt32(vehicle.Ekf.Flags?.RawValue) == 1u, "Expected EKF flags fact.");
}

static void CatalogVehicleCoreParityGaps()
{
    var catalog = new VehicleCoreParityCatalog().Build();

    Require(catalog.Count == 16, "Expected Phase 311 vehicle parity areas.");
    Require(catalog.Any(static item => item.QgcArea == "StandardModes" && item.Disposition == VehicleCoreParityDisposition.Complete), "Expected StandardModes complete.");
    Require(catalog.Any(static item => item.QgcArea == "ComponentInformation" && item.Disposition == VehicleCoreParityDisposition.Partial), "Expected ComponentInformation partial.");
    Require(catalog.Any(static item => item.QgcArea == "VehicleObjectAvoidance" && item.Disposition == VehicleCoreParityDisposition.Missing), "Expected ObjectAvoidance missing.");
    Require(catalog.Any(static item => item.QgcArea.Contains("MAVLinkLogManager", StringComparison.Ordinal) && item.VgcOwner == "Phase 317"), "Expected log/FTP ownership to be assigned to Phase 317.");
}

static void ResolveVehicleStandardModes()
{
    var catalog = new VehicleStandardModeCatalog();

    var px4Modes = catalog.GetModes(MavAutopilot.Px4);
    Require(px4Modes.Any(static mode => mode.Id == "PX4-POSITION" && mode.IsCommandable), "Expected PX4 Position mode.");
    Require(catalog.TryFind(MavAutopilot.Px4, 3, out var px4Position), "Expected PX4 mode lookup.");
    Require(px4Position.DisplayName == "Position", "Expected PX4 Position display name.");

    var foundApm = catalog.TryFind(MavAutopilot.ArduPilotMega, 6, out var rtl);
    Require(foundApm && rtl.DisplayName == "RTL", "Expected ArduPilot RTL mode.");

    var foundUnknown = catalog.TryFind(MavAutopilot.Px4, 12345, out var unknown);
    Require(!foundUnknown, "Expected unknown mode lookup to return false.");
    Require(unknown.IsDisplayOnly, "Expected unknown mode to be display-only.");
}

static void TrackComponentInformationRequestState()
{
    var runtime = new ComponentInformationRuntime();

    Require(runtime.Entries.Count == 4, "Expected four component information kinds.");
    Require(runtime.Get(ComponentInformationKind.General).State == ComponentInformationState.Unavailable, "Expected initial unavailable state.");

    var requested = runtime.Request(ComponentInformationKind.General);
    Require(requested.State == ComponentInformationState.Requested, "Expected requested state.");

    var cached = runtime.MarkCached(ComponentInformationKind.General, "mavlinkftp://component/general.json", "1.0");
    Require(cached.State == ComponentInformationState.Cached, "Expected cached state.");
    Require(cached.Uri?.Contains("general", StringComparison.Ordinal) == true, "Expected cached URI.");

    var failed = runtime.MarkFailed(ComponentInformationKind.Parameters, "metadata timeout");
    Require(failed.State == ComponentInformationState.Failed, "Expected failed state.");
    Require(failed.Message?.Contains("timeout", StringComparison.Ordinal) == true, "Expected failure message.");

    var unsupported = runtime.MarkUnsupported(ComponentInformationKind.Events, "not advertised");
    Require(unsupported.State == ComponentInformationState.Unsupported, "Expected unsupported state.");
}

static void RecordVehicleTrajectoryPoints()
{
    var store = new VehicleTrajectoryStore(maxPoints: 2);
    var t0 = new DateTimeOffset(2026, 6, 29, 12, 0, 0, TimeSpan.Zero);

    Require(store.Add(new VehicleCoordinate(47.397742, 8.545594, 488), t0, headingDegrees: 90, groundSpeedMs: 5), "Expected first trajectory point.");
    Require(store.Add(new VehicleCoordinate(47.397800, 8.545650, 490), t0.AddSeconds(1)), "Expected second trajectory point.");
    Require(store.Add(new VehicleCoordinate(47.397900, 8.545700, 491), t0.AddSeconds(2)), "Expected third trajectory point.");

    Require(store.Points.Count == 2, "Expected bounded trajectory history.");
    Require(store.Points[0].Timestamp == t0.AddSeconds(1), "Expected oldest point to be evicted.");
    Require(Math.Abs((store.LatestPoint?.Coordinate.Latitude ?? 0) - 47.397900) < 0.000001, "Expected latest point.");

    Require(!store.Add(new VehicleCoordinate(120, 8.545700, 491), t0.AddSeconds(3)), "Expected invalid latitude rejection.");
    Require(store.Points.Count == 2, "Expected invalid point not to change history.");

    store.Clear();
    Require(store.Points.Count == 0, "Expected trajectory clear.");
}

static void AuditVehicleCoreParityBlockers()
{
    var items = new VehicleCoreParityCatalog().Build();
    var audit = new VehicleCoreParityAudit().Audit(items);

    Require(audit.TotalAreas == 16, "Expected all vehicle parity areas.");
    Require(audit.CompleteAreas >= 7, "Expected Phase 311 complete areas.");
    Require(audit.PartialAreas >= 2, "Expected partial areas.");
    Require(audit.MissingAreas == 1, "Expected ObjectAvoidance missing.");
    Require(!audit.CanClaimQgcVehicleParity, "Expected QGC Vehicle parity claim to remain blocked.");
    Require(audit.OpenBlockers.Any(static blocker => blocker.Contains("VehicleObjectAvoidance", StringComparison.Ordinal)), "Expected object avoidance blocker.");
    Require(audit.OpenBlockers.Any(static blocker => blocker.Contains("VehicleSigningController", StringComparison.Ordinal)), "Expected signing blocker.");
}

static void TrackMavlinkMessageStatistics()
{
    var tracker = new MavlinkStatisticsTracker();
    tracker.RecordPacket(0);
    tracker.RecordPacket(0);
    tracker.RecordPacket(33);

    var snapshot = tracker.Snapshot();
    Require(snapshot.TotalPacketsReceived == 3, "Expected 3 total.");
    Require(snapshot.MessageStats.Count >= 2, "Expected at least 2 message types.");
    Require(snapshot.MessageStats.Any(s => s.MessageId == 0 && s.Count == 2), "Expected HEARTBEAT count 2.");
}

static void ExposeMavlinkParserFrameSequence()
{
    var writer = new MavlinkFrameWriter();
    var parser = new MavlinkFrameParser();
    var first = writer.CreateV2Frame(9, 1, MavlinkMessageIds.Heartbeat, CreateHeartbeatPayload(), crcExtra: 50);
    var second = writer.CreateV2Frame(9, 1, MavlinkMessageIds.Heartbeat, CreateHeartbeatPayload(), crcExtra: 50);

    var frames = parser.Parse(first.Concat(second).ToArray());
    Require(frames.Count == 2, "Expected two parsed frames.");
    Require(frames[0].Sequence == 0, "Expected first sequence.");
    Require(frames[1].Sequence == 1, "Expected second sequence.");
}

static void TrackMavlinkSequenceLossPerLink()
{
    var now = new DateTimeOffset(2026, 6, 26, 12, 0, 0, TimeSpan.Zero);
    var tracker = new MavlinkStatisticsTracker(() => now);
    tracker.RecordPacket(MavlinkMessageIds.Heartbeat, sequence: 10, linkId: "primary");
    now = now.AddSeconds(1);
    tracker.RecordPacket(MavlinkMessageIds.Heartbeat, sequence: 13, linkId: "primary");
    tracker.RecordPacket(MavlinkMessageIds.SysStatus, sequence: 1, linkId: "secondary");

    var snapshot = tracker.Snapshot();
    Require(snapshot.TotalPacketsReceived == 3, "Expected total received count.");
    Require(snapshot.TotalPacketsLost == 2, "Expected lost packets from sequence gap.");
    Require(Math.Abs(snapshot.PacketLossPercent - 40.0) < 0.001, "Expected total loss percent.");
    var primary = snapshot.LinkStats.First(s => s.LinkId == "primary");
    Require(primary.TotalPacketsReceived == 2, "Expected primary received count.");
    Require(primary.PacketsLost == 2, "Expected primary lost count.");
    Require(primary.LastSequence == 13, "Expected primary last sequence.");
    var secondary = snapshot.LinkStats.First(s => s.LinkId == "secondary");
    Require(secondary.PacketsLost == 0, "Expected independent secondary link stats.");
}

static void RecordMavlinkProtocolPerLinkStatistics()
{
    var logger = new VGC.Core.Logging.AppLogger();
    var protocol = new MavlinkProtocol();
    var linkManager = new LinkManager(logger);
    protocol.Attach(linkManager);
    var writer = new MavlinkFrameWriter();
    var link = linkManager.CreateConnectedMockLinkAsync("stats-link").GetAwaiter().GetResult();
    link.EmitIncoming(writer.CreateV2Frame(9, 1, MavlinkMessageIds.Heartbeat, CreateHeartbeatPayload(), crcExtra: 50));
    _ = writer.CreateV2Frame(9, 1, MavlinkMessageIds.SysStatus, new byte[31], crcExtra: 124);
    link.EmitIncoming(writer.CreateV2Frame(9, 1, MavlinkMessageIds.Heartbeat, CreateHeartbeatPayload(), crcExtra: 50));

    var snapshot = protocol.Statistics.Snapshot();
    Require(snapshot.TotalPacketsReceived == 2, "Expected protocol received count.");
    Require(snapshot.TotalPacketsLost == 1, "Expected protocol sequence loss count.");
    var linkStats = snapshot.LinkStats.Single(s => s.LinkId == "stats-link");
    Require(linkStats.PacketsLost == 1, "Expected per-link sequence loss.");
    Require(snapshot.MessageStats.Any(s => s.MessageId == MavlinkMessageIds.Heartbeat && s.Count == 2), "Expected heartbeat message count.");
}

static void DefineAndReadSettingsGroupFacts()
{
    var group = new SettingsGroup("General");
    group.DefineSetting("distanceUnit", "Distance Unit", FactValueType.String, "Meters");
    group.DefineSetting("defaultCruiseSpeed", "Cruise Speed", FactValueType.Float, 15, "m/s");

    Require(group.Facts.Count == 2, "Expected 2 settings.");
    var foundDistanceUnit = group.TryGet("distanceUnit", out var fact);
    Require(foundDistanceUnit && fact is not null && fact.DisplayValue.Contains("Meters"), "Expected distance unit.");
}

static void RegisterAndPersistSettingsManagerDefaults()
{
    var store = new FakeSettingsStore();
    store.Current.SpeedUnit = "km/h";
    store.Current.AutoConnectOnStartup = false;
    var manager = SettingsManager.CreateDefault();

    manager.LoadAsync(store).GetAwaiter().GetResult();

    Require(manager.Groups.ContainsKey("Units"), "Expected Units settings group.");
    Require(manager.Groups.ContainsKey("Links"), "Expected Links settings group.");
    Require(manager.Groups["Units"].TryGet("speedUnit", out var speedUnit), "Expected speed unit fact.");
    Require(speedUnit?.RawValue?.ToString() == "km/h", "Expected loaded speed unit.");
    Require(manager.Groups["Links"].TryGet("autoConnectOnStartup", out var autoConnect), "Expected auto-connect fact.");
    Require(autoConnect?.RawValue is false, "Expected loaded auto-connect value.");

    speedUnit!.SetRawValue("m/s");
    autoConnect!.SetRawValue(true);
    manager.SaveAsync(store).GetAwaiter().GetResult();

    Require(store.Current.SpeedUnit == "m/s", "Expected persisted speed unit.");
    Require(store.Current.AutoConnectOnStartup, "Expected persisted auto-connect flag.");
    Require(store.SaveCount == 1, "Expected settings save call.");
}

static void SettingsViewModelEditsGroupedFacts()
{
    var store = new FakeSettingsStore();
    var manager = SettingsManager.CreateDefault();
    var viewModel = new SettingsViewModel(manager, store);

    viewModel.LoadAsync().GetAwaiter().GetResult();
    var units = viewModel.Groups.Single(group => group.Name == "Units");
    var cruise = units.Facts.Single(fact => fact.Key == "defaultCruiseSpeed");
    cruise.Value = 22d;
    viewModel.SaveAsync().GetAwaiter().GetResult();

    Require(Math.Abs(store.Current.DefaultCruiseSpeed - 22d) < 0.001, "Expected edited cruise speed to persist.");
    Require(cruise.ValidationError is null, "Expected valid settings edit.");
}

static void ReactiveFactGroupTracksLifecycle()
{
    var group = new ReactiveFactGroup("Runtime");
    var added = 0;
    var removed = 0;
    group.FactAdded += (_, _) => added++;
    group.FactRemoved += (_, _) => removed++;

    var fact = group.DefineFact("battery.percent", FactValueType.Int32, 80, "%", "Battery Percent");

    Require(group.Facts.Count == 1, "Expected fact to be added.");
    Require(added == 1, "Expected added event.");
    Require(group.TryGet("battery.percent", out var found) && ReferenceEquals(found, fact), "Expected lookup by key.");
    Require(found!.MetaData.Units == "%", "Expected metadata binding.");

    var replacement = new Fact(0, "battery.percent", new FactMetaData("battery.percent", FactValueType.Int32), 90);
    group.AddOrReplace(replacement);
    Require(group.Facts.Count == 1, "Expected replacement without duplicate.");
    Require(group.TryGet("battery.percent", out found) && ReferenceEquals(found, replacement), "Expected replacement lookup.");

    Require(group.Remove("battery.percent"), "Expected fact removal.");
    Require(removed == 1, "Expected removed event.");
}

static void CoordinateAppLifecycleAndCloseGuards()
{
    var store = new FakeSettingsStore();
    var logger = new AppLogger();
    var lifecycle = new AppLifecycleService(store, logger);
    lifecycle.InitializeAsync().GetAwaiter().GetResult();

    Require(lifecycle.IsInitialized, "Expected lifecycle initialized.");
    Require(store.LoadCount == 1, "Expected settings load during startup.");

    var close = new AppCloseCoordinator();
    close.Register(new DelegateAppCloseGuard(
        "Plan",
        _ => Task.FromResult<AppCloseIssue?>(new AppCloseIssue("Plan", "Unsaved plan"))));
    close.Register(new DelegateAppCloseGuard(
        "Parameters",
        _ => Task.FromResult<AppCloseIssue?>(new AppCloseIssue("Parameters", "Pending parameter write"))));

    var result = close.CanCloseAsync().GetAwaiter().GetResult();
    Require(!result.CanClose, "Expected close blocked.");
    Require(result.Issues?.Count == 2, "Expected two close issues.");
    Require(result.Reason?.Contains("Unsaved plan", StringComparison.Ordinal) == true, "Expected unsaved plan reason.");

    lifecycle.ShutdownAsync().GetAwaiter().GetResult();
    Require(!lifecycle.IsInitialized, "Expected lifecycle shutdown.");
}

static void RotateFileLogsAndProjectViewerRows()
{
    var directory = Path.Combine(Directory.GetCurrentDirectory(), ".test-output", "logs-" + Guid.NewGuid().ToString("N"));
    var service = new FileLogService(new FileLogOptions(directory, MaxBytes: 80, MaxFiles: 2));

    service.Write("INFO", "Link", new string('a', 100), DateTimeOffset.Parse("2026-06-26T00:00:00Z"));
    service.Write("WARN", "Settings", "rotated", DateTimeOffset.Parse("2026-06-26T00:00:01Z"));

    Require(File.Exists(service.CurrentFilePath), "Expected current log file.");
    Require(File.Exists(service.CurrentFilePath + ".1"), "Expected rotated log file.");
    var rows = service.ReadRows();
    Require(rows.Count == 1, "Expected one row in current log after rotation.");
    Require(rows[0].Category == "Settings", "Expected category projection.");
    Require(rows[0].Message == "rotated", "Expected log viewer message.");
}

static void ResolveLocalizationBoundaryKeys()
{
    var catalog = LocalizationCatalog.CreateDefaultBoundary();

    Require(catalog.Resolve("settings.general.language") == "Language", "Expected registered localization key.");
    Require(catalog.Resolve("missing.key") == "missing.key", "Expected missing key fallback.");
    Require(catalog.Keys.Values.All(static key => !key.DefaultText.Contains("QGroundControl", StringComparison.OrdinalIgnoreCase)), "Expected VGC-owned defaults.");
}

static void CatalogSettingsLifecycleEvidence()
{
    var checklist = SettingsLifecycleEvidenceCatalog.CreateV144Checklist();

    Require(checklist.Count == 5, "Expected v1.44 evidence items.");
    Require(checklist.Any(static item => item.Name.Contains("Settings", StringComparison.Ordinal)), "Expected settings evidence.");
    Require(checklist.Any(static item => item.Name.Contains("Close", StringComparison.Ordinal)), "Expected close coordinator evidence.");
    Require(checklist.All(static item => item.IsComplete), "Expected completed shared-core evidence.");
}

static void LoadFirmwareMetadataPackages()
{
    var registry = FirmwareMetadataPackageRegistry.CreateDefault();
    var px4 = registry.Resolve(MavAutopilot.Px4, MavType.Quadrotor);
    var ardupilot = registry.Resolve(MavAutopilot.ArduPilotMega, MavType.FixedWing);

    Require(px4 is not null, "Expected PX4 metadata package.");
    Require(ardupilot is not null, "Expected ArduPilot metadata package.");
    Require(px4!.CreateCatalog().Find(1, "COM_RC_LOSS_T") is not null, "Expected PX4 safety metadata.");
    Require(ardupilot!.CreateCatalog().Find(1, "FS_THR_ENABLE")?.RebootRequired == true, "Expected ArduPilot reboot metadata.");
    Require(!px4.Source.Contains("qgroundcontrol", StringComparison.OrdinalIgnoreCase), "Expected VGC-owned metadata source.");
}

static void ProjectFirmwareCommandUiMetadata()
{
    var service = new FirmwareCommandUiMetadataService();

    var px4 = service.Project(MavAutopilot.Px4, MavType.Quadrotor);
    var generic = service.Project(MavAutopilot.Generic, MavType.Generic);

    Require(px4.Any(static command => command.Category == "GeoFence" && command.IsAvailable && command.RequiresConfirmation), "Expected supported PX4 geofence UI command.");
    Require(generic.Any(static command => command.Category == "Rally" && !command.IsAvailable && command.Reason is not null), "Expected generic rally command blocked with reason.");
}

static void ProjectAutoPilotSetupComponents()
{
    var manager = new ParameterManager();
    AddParameter(manager, "CAL_ACC0_ID", 1);
    AddParameter(manager, "CAL_GYRO0_ID", 2);
    var firmware = new FirmwarePluginManager().GetPlugin(MavAutopilot.Px4);
    var service = new AutoPilotSetupComponentService();

    var components = service.Project(firmware, MavType.Quadrotor, manager);

    Require(components.Any(static component => component.Id == "sensors" && component.IsBlocked), "Expected sensors blocked until all required params exist.");
    Require(components.Any(static component => component.Id == "radio" && component.Requirement == SetupComponentRequirement.Optional), "Expected radio optional component.");
    Require(components.Any(static component => component.Id == "motors"), "Expected multirotor motors component.");
}

static void SetupViewSwitchesComponentNavigation()
{
    var vehicles = new MultiVehicleManager(new MavlinkProtocol(), new VGC.Core.Logging.AppLogger());
    var setup = new SetupViewModel(vehicles);

    Require(setup.PageKind == SetupPageKind.Summary, "Expected setup to start on summary page.");
    Require(setup.ShowSummaryPage && !setup.ShowParametersPage, "Expected summary compatibility flags.");

    setup.ShowParameters();
    Require(setup.PageKind == SetupPageKind.Parameters, "Expected parameters page after ShowParameters.");
    Require(setup.ShowParametersPage && !setup.ShowSummaryPage, "Expected parameters compatibility flags.");
    Require(setup.SelectedComponent is null, "Expected no selected component on parameters page.");

    var component = new VehicleSetupComponentStatus("sensors", "Sensors", "Sensors summary", true, true, VehicleSetupReadiness.Ready, "ok", []);
    setup.SelectedComponent = component;
    Require(setup.PageKind == SetupPageKind.Component, "Expected component page after selecting a component.");
    Require(setup.DetailTabKind == SetupDetailTabKind.Sensors, "Expected sensors detail tab.");
    Require(setup.SelectedDetailTab == "sensors", "Expected detail tab compatibility string.");
    Require(!setup.ShowSummaryPage && !setup.ShowParametersPage, "Expected special-page flags cleared on component page.");
    Require(setup.IsComponentSelected("sensors"), "Expected selected component helper to reflect component page.");
}

static void CreateCalibrationCommandBoundaries()
{
    var compass = CalibrationCommandFactory.Create(SensorCalibrationType.Compass);
    var accel = CalibrationCommandFactory.Create(SensorCalibrationType.Accelerometer);
    var gyro = CalibrationCommandFactory.Create(SensorCalibrationType.Gyroscope);
    var levelCancel = CalibrationCommandFactory.Create(SensorCalibrationType.Level, CalibrationCommandKind.Cancel);

    Require(compass.Command.Command == 241 && Math.Abs(compass.Command.Param3 - 1f) < 0.001f, "Expected compass calibration param.");
    Require(Math.Abs(accel.Command.Param5 - 1f) < 0.001f, "Expected accelerometer calibration param.");
    Require(Math.Abs(gyro.Command.Param1 - 1f) < 0.001f, "Expected gyro calibration param.");
    Require(levelCancel.Kind == CalibrationCommandKind.Cancel && Math.Abs(levelCancel.Command.Param1 + 1f) < 0.001f, "Expected cancel calibration command.");
    Require(compass.SafetyWarning.Contains("safe", StringComparison.OrdinalIgnoreCase), "Expected safety warning.");
}

static void ProjectRadioCalibrationAndManualControl()
{
    var manager = new ParameterManager();
    AddParameter(manager, "RC1_MIN", 900);
    AddParameter(manager, "RC1_MAX", 2100);
    AddParameter(manager, "RC1_TRIM", 1510);
    var service = new RadioCalibrationService();

    var channels = service.BuildChannelMap(manager);
    var manual = service.ProjectManualControl(1500, -1500, -50, 20, buttons: 3);

    Require(channels[0].Function == "Roll", "Expected roll channel.");
    Require(channels[0].Min == 900 && channels[0].Max == 2100 && channels[0].Trim == 1510, "Expected RC channel params.");
    Require(manual.X == 1000 && manual.Y == -1000 && manual.Z == 0 && manual.Buttons == 3, "Expected clamped manual control values.");
}

static void ProjectPowerBatterySetupMetadata()
{
    var manager = new ParameterManager();
    AddParameter(manager, "BAT_LOW_THR", 20);
    AddParameter(manager, "BAT_CRIT_THR", 10);
    var catalog = new InMemoryParameterMetadataCatalog([
        new ParameterMetadata("BAT_LOW_THR", ComponentId: 1, Group: "Power", Label: "Low Battery", Units: "%"),
        new ParameterMetadata("BAT_V_EMPTY", ComponentId: 1, Group: "Battery", Label: "Empty Voltage", Units: "V")
    ]);
    var service = new PowerBatterySetupService();

    var projection = service.Project(manager, catalog);

    Require(!projection.IsComplete, "Expected incomplete battery setup.");
    Require(projection.Parameters.Any(static parameter => parameter.Name == "BAT_LOW_THR" && parameter.Label == "Low Battery" && parameter.IsPresent), "Expected metadata label and present flag.");
    Require(projection.Parameters.Any(static parameter => parameter.Name == "BAT_V_EMPTY" && !parameter.IsPresent), "Expected missing voltage parameter.");
}

static void CreateSafetyMotorCommandBoundary()
{
    var motor = SafetyMotorCommandFactory.Create(MotorSafetyActionType.MotorTest);
    var safety = SafetyMotorCommandFactory.Create(MotorSafetyActionType.SafetyConfirm);

    Require(motor.RequiresExplicitConfirmation, "Expected motor test confirmation.");
    Require(motor.Command.Command == 209, "Expected motor test command boundary.");
    Require(safety.Command.Command == MavlinkCommandIds.ComponentArmDisarm, "Expected safety confirm arm/disarm boundary.");
    Require(safety.SafetyNotice.Contains("real-hardware", StringComparison.OrdinalIgnoreCase), "Expected hardware safety notice.");
}

static void ProjectParameterSetupRows()
{
    var manager = new ParameterManager();
    AddParameter(manager, "COM_RC_LOSS_T", 3);
    manager.BeginParameterWrite(1, "COM_RC_LOSS_T");
    var catalog = new InMemoryParameterMetadataCatalog([
        new ParameterMetadata("COM_RC_LOSS_T", ComponentId: 1, Group: "Safety", Label: "RC Loss Timeout", Units: "s", RebootRequired: true)
    ]);
    var projection = new ParameterSetupProjection();

    var rows = projection.BuildRows(manager, catalog, groupFilter: "Safety");

    Require(rows.Count == 1, "Expected one safety parameter setup row.");
    Require(rows[0].RestartRequired, "Expected restart required flag.");
    Require(rows[0].WriteStatus == ParameterWriteStatus.Pending, "Expected pending write projection.");
    Require(rows[0].Label == "RC Loss Timeout", "Expected metadata label.");
}

static void CatalogFirmwareSetupEvidence()
{
    var checklist = FirmwareSetupEvidenceCatalog.CreateV145Checklist();

    Require(checklist.Count == 6, "Expected v1.45 evidence checklist.");
    Require(checklist.Any(static item => item.Name.Contains("Calibration", StringComparison.Ordinal)), "Expected calibration evidence.");
    Require(checklist.All(static item => item.IsComplete), "Expected shared-core evidence complete.");
}

static void ReviewFirmwareSetupRuntimeGaps()
{
    var review = FirmwareSetupRuntimeReview.CreateV145Review();

    Require(review.Count == 4, "Expected v1.45 runtime review items.");
    Require(review.Any(static item => item.Area == "Power/Safety" && item.ResidualGap.Contains("Real hardware", StringComparison.Ordinal)), "Expected hardware safety residual gap.");
    Require(review.All(static item => !string.IsNullOrWhiteSpace(item.Px4Status) && !string.IsNullOrWhiteSpace(item.ArduPilotStatus)), "Expected PX4/APM statuses.");
}

static void CatalogFirmwareSetupParityFlows()
{
    var items = new FirmwareSetupParityCatalog().Build();

    Require(items.Count == 10, "Expected Phase 313 firmware setup parity areas.");
    Require(items.Any(static item => item.Area == FirmwareSetupFlowArea.SetupComponents
        && item.Px4Disposition == FirmwareSetupParityDisposition.Complete
        && item.ArduPilotDisposition == FirmwareSetupParityDisposition.Complete), "Expected setup components complete for PX4/APM.");
    Require(items.Any(static item => item.Area == FirmwareSetupFlowArea.SensorCalibration
        && item.EvidenceTests.Contains("Create calibration command boundaries")), "Expected calibration command evidence.");
    Require(items.Any(static item => item.Area == FirmwareSetupFlowArea.MotorsActuators
        && item.ArduPilotDisposition == FirmwareSetupParityDisposition.Blocked), "Expected ArduPilot actuator blocker.");
    Require(items.Any(static item => item.Area == FirmwareSetupFlowArea.RuntimeEvidence
        && item.VgcOwner == "Phases 319-320"), "Expected runtime evidence ownership.");

    var testProgram = File.ReadAllText(FindRepositoryPath("VGC.Tests", "Program.cs"));
    foreach (var testName in items.SelectMany(static item => item.EvidenceTests).Distinct(StringComparer.Ordinal))
    {
        Require(testProgram.Contains($"(\"{testName}\"", StringComparison.Ordinal), $"Expected firmware setup evidence test registered: {testName}.");
    }
}

static void ProjectFirmwareSetupRuntimeFlow()
{
    var manager = new ParameterManager();
    AddParameter(manager, "CAL_ACC0_ID", 1);
    AddParameter(manager, "CAL_GYRO0_ID", 2);
    AddParameter(manager, "CAL_MAG0_ID", 3);
    AddParameter(manager, "BAT_LOW_THR", 20);
    AddParameter(manager, "COM_RC_LOSS_T", 3);
    AddParameter(manager, "MOT_SPIN_MIN", 0.12f);
    var projector = new FirmwareSetupFlowRuntimeProjector();

    var px4 = projector.Project(MavAutopilot.Px4, MavType.Quadrotor, manager);
    var apm = projector.Project(MavAutopilot.ArduPilotMega, MavType.FixedWing, manager);
    var blocked = projector.Project(MavAutopilot.Px4, MavType.Quadrotor, new ParameterManager());

    Require(px4.FirmwareName.Contains("PX4", StringComparison.OrdinalIgnoreCase), "Expected PX4 firmware projection.");
    Require(px4.HasMetadataPackage, "Expected PX4 metadata package.");
    Require(px4.Components.Any(static component => component.ComponentId == "motors" && component.Readiness == VehicleSetupReadiness.Ready), "Expected PX4 quad motors ready.");
    Require(px4.ParityItems.Count == 10, "Expected parity catalog attached to runtime projection.");
    Require(!px4.HasBlockedComponents, "Expected PX4 setup components not blocked with required seed parameters.");

    Require(apm.HasMetadataPackage, "Expected ArduPilot metadata package.");
    Require(apm.Components.Any(static component => component.ComponentId == "airframe"), "Expected fixed-wing airframe setup component.");
    Require(apm.Components.All(static component => component.ComponentId != "motors"), "Expected fixed-wing projection not to expose motors setup component.");
    Require(blocked.BlockingReasons.Any(static reason => reason.Contains("Sensors", StringComparison.Ordinal)), "Expected missing sensor parameters to block setup.");
    Require(blocked.BlockingReasons.Any(static reason => reason.Contains("Power", StringComparison.Ordinal)), "Expected missing power parameters to block setup.");
}

static void AuditFirmwareSetupParityBlockers()
{
    var items = new FirmwareSetupParityCatalog().Build();
    var audit = new FirmwareSetupParityAudit().Audit(items);

    Require(audit.TotalAreas == 10, "Expected all firmware setup parity areas.");
    Require(audit.CompleteForPx4 >= 1, "Expected at least one complete PX4 setup area.");
    Require(audit.CompleteForArduPilot >= 1, "Expected at least one complete ArduPilot setup area.");
    Require(audit.PartialForPx4 >= 5, "Expected multiple partial PX4 setup areas.");
    Require(audit.PartialForArduPilot >= 5, "Expected multiple partial ArduPilot setup areas.");
    Require(audit.BlockedAreas >= 2, "Expected explicit firmware setup blockers.");
    Require(!audit.CanClaimQgcFirmwareSetupParity, "Expected QGC firmware setup parity claim to remain blocked.");
    Require(audit.OpenBlockers.Any(static blocker => blocker.Contains("AIRFRAME", StringComparison.Ordinal)), "Expected airframe blocker.");
    Require(audit.OpenBlockers.Any(static blocker => blocker.Contains("RUNTIME-EVIDENCE", StringComparison.Ordinal)), "Expected runtime evidence blocker.");
}

static void CatalogGate9SetupParameterSettingsQmlParity()
{
    var catalog = new QgcQmlParityCatalog().Build();
    var setup = catalog.Single(static item => item.Module == "AutoPilotPlugins");
    var firmware = catalog.Single(static item => item.Module == "FirmwarePlugin");
    var settings = catalog.Single(static item => item.Module == "AppSettings");
    var facts = catalog.Single(static item => item.Module == "FactSystem");
    var gps = catalog.Single(static item => item.Module == "GPS");
    var audit = new QgcQmlParityAudit().Audit(catalog);

    Require(setup.Status == QgcQmlParityStatus.Mapped && setup.Area == UiWorkflowArea.Setup, "Expected AutoPilotPlugins setup mapping until SITL/device evidence exists.");
    Require(setup.VgcTarget == "VGC/Views/SetupView.axaml", "Expected SetupView target.");
    Require(setup.Blocker.Contains("SITL", StringComparison.Ordinal) && setup.Blocker.Contains("device", StringComparison.OrdinalIgnoreCase), "Expected setup runtime blockers.");
    Require(firmware.Status == QgcQmlParityStatus.Migrated && firmware.VgcTarget == "VGC/Firmware", "Expected firmware plugin migration.");
    Require(firmware.Blocker.Contains("airframe", StringComparison.OrdinalIgnoreCase) && firmware.Blocker.Contains("device", StringComparison.OrdinalIgnoreCase), "Expected firmware blockers.");
    Require(settings.Status == QgcQmlParityStatus.Mapped && settings.Area == UiWorkflowArea.Settings, "Expected AppSettings mapping until full runtime/device evidence exists.");
    Require(settings.VgcTarget == "VGC/Views/SettingsView.axaml", "Expected SettingsView target.");
    Require(facts.Status == QgcQmlParityStatus.Migrated && facts.Area == UiWorkflowArea.Parameters, "Expected FactSystem to remain migrated.");
    Require(gps.Status == QgcQmlParityStatus.Migrated, "Expected GPS migration for positioning runtime boundaries.");
    Require(!audit.CanClaimQmlUiParity, "Expected Gate 9 not to claim full QML parity.");
    Require(!audit.CanClaimQgcReplacement, "Expected Gate 9 not to claim QGC replacement.");
}

static void AddParameter(ParameterManager manager, string name, float value, int componentId = 1)
{
    manager.GetOrCreateParameter(new MavlinkParamValue(componentId, name, value, 1, 0, 9));
}

static void ValidateMissionItemsWithRules()
{
    var mission = new MissionPlan();
    var issues = MissionValidationRules.Validate(mission);
    Require(issues.Any(i => i.Message.Contains("no items")), "Expected empty mission warning.");

    mission.Items.Add(new MissionPlanItem { Command = 22, DoJumpId = 1 });
    mission.Items.Add(new MissionPlanItem { Command = 16, DoJumpId = 2 });
    var issues2 = MissionValidationRules.Validate(mission);
    Require(!issues2.Any(i => i.IsError), "Expected no hard errors for valid mission.");
}

static void CalculateCameraGsdFromSensorSpecs()
{
    var service = new CameraDefinitionService();
    var camera = new CameraDefinition("cam1", "Test Cam", "ModelX", 13.2, 8.8, 8.0, 5472, 3648);
    service.Register(camera);

    var gsd = service.CalculateGSD(camera, altitudeMeters: 100);
    Require(gsd > 0 && gsd < 1, $"Expected GSD between 0-1m, got {gsd:F4}m.");
}

static void ApplyHomePositionTelemetry()
{
    var vehicle = new Vehicle(1, 1, MavAutopilot.Px4, MavType.Quadrotor);
    var parser = new MavlinkFrameParser();
    var frames = parser.Parse(MavlinkTestFrames.HomePosition());
    vehicle.ApplyPacket(new MavlinkPacket(new MockLinkTransport(), frames[0].Version, frames[0].SystemId, frames[0].ComponentId, frames[0].MessageId, frames[0].Payload));

    Require(vehicle.HomePosition is not null, "Expected home position.");
    var homePosition = vehicle.HomePosition;
    Require(homePosition is not null && Math.Abs(homePosition.Latitude - 47.397742) < 0.0001, "Expected home latitude.");
}

static void RequestMessageAndAcknowledgeResponse()
{
    var link = new MockLinkTransport();
    link.ConnectAsync().GetAwaiter().GetResult();
    var sentFrames = new List<byte[]>();
    link.BytesSent += (_, args) => sentFrames.Add(args.Bytes.ToArray());

    var coordinator = new RequestMessageCoordinator(systemId: 255, componentId: 190);
    coordinator.RequestMessageAsync(link, messageId: 33).GetAwaiter().GetResult();
    Require(coordinator.PendingCount == 1, "Expected one pending request.");
    Require(sentFrames.Count == 1, "Expected one request message frame.");

    var parser = new MavlinkFrameParser();
    var frames = parser.Parse(sentFrames[0]);
    Require(frames.Count == 1, "Expected one parsed request frame.");
    Require(frames[0].MessageId == 76, "Expected request to use COMMAND_LONG.");
    Require(BitConverter.ToUInt16(frames[0].Payload, 28) == 512, "Expected MAV_CMD_REQUEST_MESSAGE.");
    Require(Math.Abs(BitConverter.ToSingle(frames[0].Payload, 0) - 33) < 0.001f, "Expected requested message id in param1.");

    coordinator.RequestMessageAsync(link, messageId: 33).GetAwaiter().GetResult();
    Require(coordinator.PendingCount == 1, "Expected duplicate request to keep one pending request.");
    Require(sentFrames.Count == 1, "Expected duplicate request not to send another frame.");

    var ack = coordinator.AcknowledgeResponse(33);
    Require(ack, "Expected acknowledge to succeed.");
    Require(coordinator.PendingCount == 0, "Expected zero pending after ack.");
}

static void RouteOutboundCommandThroughMavlinkRouter()
{
    var link = new MockLinkTransport();
    link.ConnectAsync().GetAwaiter().GetResult();
    var sentFrames = new List<byte[]>();
    link.BytesSent += (_, args) => sentFrames.Add(args.Bytes.ToArray());
    var routes = new List<string>();
    var router = new MavlinkOutboundRouter();
    router.FrameSent += (_, args) => routes.Add(args.Route);
    var service = new MavlinkCommandService(systemId: 255, componentId: 190, outboundRouter: router);

    service.SendCommandLongAsync(
        link,
        new MavlinkCommandLong(
            TargetSystemId: 9,
            TargetComponentId: 1,
            Command: MavlinkCommandIds.ComponentArmDisarm,
            Confirmation: 0,
            Param1: 1),
        CancellationToken.None).GetAwaiter().GetResult();

    Require(router.FramesSent == 1, "Expected outbound router to count sent command frame.");
    Require(routes.SequenceEqual(["COMMAND_LONG"]), "Expected COMMAND_LONG route event.");
    Require(sentFrames.Count == 1, "Expected one frame sent to link.");
    var frame = ParseSingleFrame(sentFrames[0]);
    Require(frame.MessageId == MavlinkMessageIds.CommandLong, "Expected COMMAND_LONG message id.");
    Require(BitConverter.ToUInt16(frame.Payload, 28) == MavlinkCommandIds.ComponentArmDisarm, "Expected arm command.");
}

static void RouteOutboundServiceFamiliesThroughMavlinkRouter()
{
    var link = new MockLinkTransport();
    link.ConnectAsync().GetAwaiter().GetResult();
    var router = new MavlinkOutboundRouter();
    var routes = new List<string>();
    router.FrameSent += (_, args) => routes.Add(args.Route);
    var parameters = new MavlinkParameterService(outboundRouter: router);
    var missions = new MavlinkMissionService(outboundRouter: router);
    var modes = new MavlinkModeService(outboundRouter: router);

    parameters.SendParamRequestListAsync(link, new MavlinkParameterRequestList(9, 1)).GetAwaiter().GetResult();
    missions.SendMissionClearAllAsync(link, new MavlinkMissionClearAll(9, 1)).GetAwaiter().GetResult();
    modes.SendSetModeAsync(link, new MavlinkSetMode(9, 0x01, 0x1234)).GetAwaiter().GetResult();

    Require(router.FramesSent == 3, "Expected shared router to send all service family frames.");
    Require(routes.SequenceEqual(["PARAM_REQUEST_LIST", "MISSION_CLEAR_ALL", "SET_MODE"]), "Expected typed outbound routes.");
}

static void KeepViewModelsOutOfMavlinkPayloadWriting()
{
    var viewModelDirectory = FindRepositoryPath("VGC", "ViewModels");
    var forbiddenPatterns = new[]
    {
        ".WriteAsync(",
        "MavlinkFrameWriter",
        "CreateCommandLongFrame(",
        "CreateParamRequest",
        "CreateParamSetFrame(",
        "CreateMission",
        "CreateSetModeFrame("
    };
    var violations = Directory
        .EnumerateFiles(viewModelDirectory, "*.cs", SearchOption.AllDirectories)
        .SelectMany(path =>
        {
            var text = File.ReadAllText(path);
            return forbiddenPatterns
                .Where(text.Contains)
                .Select(pattern => $"{Path.GetFileName(path)}:{pattern}");
        })
        .ToArray();

    Require(violations.Length == 0, "Expected ViewModels not to write or create MAVLink frames: " + string.Join(", ", violations));
}

static void ExposeMavlinkProtocolEvidenceCatalog()
{
    var required = MavlinkProtocolEvidenceCatalog.RequiredAreas;
    var items = MavlinkProtocolEvidenceCatalog.Items;

    Require(required.Count == 5, "Expected five required protocol evidence areas.");
    Require(MavlinkProtocolEvidenceCatalog.MissingRequiredAreas().Count == 0, "Expected all required protocol evidence areas covered.");
    foreach (var area in required)
    {
        var item = items.SingleOrDefault(i => i.Area == area);
        Require(item is not null, $"Expected evidence item for {area}.");
        Require(item!.EvidenceLevel == MavlinkProtocolEvidenceLevel.L2BuildVerified, $"Expected L2 evidence for {area}.");
        Require(item.SourceFiles.Count > 0, $"Expected source files for {area}.");
        Require(item.TestNames.Count > 0, $"Expected test names for {area}.");
        Require(!string.IsNullOrWhiteSpace(item.Notes), $"Expected notes for {area}.");
    }
}

static void VerifyMavlinkProtocolEvidenceSourcesAndTests()
{
    var testProgram = File.ReadAllText(FindRepositoryPath("VGC.Tests", "Program.cs"));

    foreach (var item in MavlinkProtocolEvidenceCatalog.Items)
    {
        foreach (var sourceFile in item.SourceFiles)
        {
            var path = FindRepositoryPath(sourceFile.Split('/'));
            Require(File.Exists(path), $"Expected evidence source file to exist: {sourceFile}.");
        }

        foreach (var testName in item.TestNames)
        {
            Require(testProgram.Contains($"(\"{testName}\"", StringComparison.Ordinal), $"Expected evidence test registered: {testName}.");
        }
    }
}

static string FindRepositoryPath(params string[] segments)
{
    foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
        var directory = new DirectoryInfo(start);
        while (directory is not null)
        {
            var path = Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
            if (Directory.Exists(path) || File.Exists(path))
            {
                return path;
            }

            directory = directory.Parent;
        }
    }

    throw new DirectoryNotFoundException("Could not locate repository path: " + Path.Combine(segments));
}

sealed class FakeTerrainProvider : ITerrainQueryProvider
{
    private readonly Dictionary<string, TerrainSample> _samples;

    public FakeTerrainProvider(IEnumerable<TerrainSample> samples)
    {
        _samples = samples.ToDictionary(static sample => CreateKey(sample.Coordinate), static sample => sample);
    }

    public int QueryCount { get; private set; }

    public Task<IReadOnlyList<TerrainSample>> QueryAsync(
        IReadOnlyList<TerrainCoordinate> coordinates,
        CancellationToken cancellationToken = default)
    {
        QueryCount++;
        var result = coordinates
            .Where(coordinate => _samples.ContainsKey(CreateKey(coordinate)))
            .Select(coordinate => _samples[CreateKey(coordinate)])
            .ToList();
        return Task.FromResult<IReadOnlyList<TerrainSample>>(result);
    }

    private static string CreateKey(TerrainCoordinate coordinate)
    {
        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{coordinate.Latitude:F7}:{coordinate.Longitude:F7}");
    }
}

sealed class ImmediateLogReplayDelayScheduler : ILogReplayDelayScheduler
{
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

sealed class FakeSerialPortEnumerator : ISerialPortEnumerator
{
    private readonly IReadOnlyList<string> _ports;

    public FakeSerialPortEnumerator(IReadOnlyList<string> ports)
    {
        _ports = ports;
    }

    public Task<IReadOnlyList<SerialPortInfo>> EnumerateAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SerialPortInfo> ports = _ports.Select(static port => new SerialPortInfo(port, port, 0, 0)).ToArray();
        return Task.FromResult(ports);
    }
}

sealed class FakeSerialPortAdapter : ISerialPortAdapter
{
    public event EventHandler<byte[]>? BytesReceived;

    public bool IsOpen { get; private set; }

    public SerialLinkConfiguration? OpenedWith { get; private set; }

    public List<byte[]> Written { get; } = [];

    public Task OpenAsync(SerialLinkConfiguration configuration, CancellationToken cancellationToken = default)
    {
        IsOpen = true;
        OpenedWith = configuration;
        return Task.CompletedTask;
    }

    public Task CloseAsync()
    {
        IsOpen = false;
        return Task.CompletedTask;
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
    {
        Written.Add(bytes.ToArray());
        return ValueTask.CompletedTask;
    }

    public void EmitIncoming(byte[] bytes)
    {
        BytesReceived?.Invoke(this, bytes);
    }

    public ValueTask DisposeAsync()
    {
        IsOpen = false;
        return ValueTask.CompletedTask;
    }
}

sealed class FakeAndroidUsbSerialPlatform : IAndroidUsbSerialPlatform
{
    private readonly IReadOnlyList<AndroidUsbSerialDevice> _devices;

    public FakeAndroidUsbSerialPlatform(IReadOnlyList<AndroidUsbSerialDevice> devices)
    {
        _devices = devices;
    }

    public bool FailConnect { get; set; }

    public Task<IReadOnlyList<AndroidUsbSerialDevice>> DiscoverDevicesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_devices);
    }

    public Task<bool> RequestPermissionAsync(AndroidUsbSerialDevice device, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task ConnectAsync(AndroidUsbSerialDevice device, CancellationToken cancellationToken = default)
    {
        if (FailConnect)
        {
            throw new InvalidOperationException("USB connect failed.");
        }

        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

sealed class FakeVideoService : IVideoService
{
    private static readonly VideoStreamDescriptor Stream = new(
        "front",
        "Front Camera",
        new Uri("rtsp://127.0.0.1/front"),
        VideoStreamProtocol.Rtsp,
        "h264");

    public Task<IReadOnlyList<VideoStreamDescriptor>> DiscoverStreamsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<VideoStreamDescriptor>>([Stream]);
    }

    public Task<VideoStreamState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new VideoStreamState(Stream, IsStreaming: true));
    }
}

sealed class FakeCameraService : ICameraService
{
    public Task<CameraStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CameraStatus(1, 100, IsReady: true, IsCapturingImage: false, IsRecordingVideo: false, Mode: "Photo"));
    }

    public Task StartImageCaptureAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task StartVideoRecordingAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task StopVideoRecordingAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

sealed class FakeGimbalService : IGimbalService
{
    public Task<GimbalAttitude> GetAttitudeAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new GimbalAttitude(PitchDegrees: -10, RollDegrees: 0, YawDegrees: 45, IsLocked: false));
    }

    public Task SetAttitudeAsync(GimbalCommand command, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly byte[] _bytes;

    public FakeHttpMessageHandler(byte[] bytes)
    {
        _bytes = bytes;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(_bytes)
        };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        return Task.FromResult(response);
    }
}

sealed class FakeSettingsStore : IAppSettingsStore
{
    public AppSettingsSnapshot Current { get; } = new();

    public int LoadCount { get; private set; }

    public int SaveCount { get; private set; }

    public Task LoadAsync(CancellationToken cancellationToken = default)
    {
        LoadCount++;
        return Task.CompletedTask;
    }

    public Task SaveAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
