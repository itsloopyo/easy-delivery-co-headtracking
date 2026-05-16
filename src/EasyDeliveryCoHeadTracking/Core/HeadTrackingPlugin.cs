using BepInEx;
using BepInEx.Logging;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.Processing;
using CameraUnlock.Core.Protocol;
using CameraUnlock.Core.Unity.Rendering;
using CameraUnlock.Core.Unity.UI;
using EasyDeliveryCoHeadTracking.Aim;
using EasyDeliveryCoHeadTracking.Camera;
using EasyDeliveryCoHeadTracking.Config;

namespace EasyDeliveryCoHeadTracking.Core
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class HeadTrackingPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.cameraunlock.easydeliveryco.headtracking";
        public const string PluginName = "Easy Delivery Co Head Tracking";
        public const string PluginVersion = "0.1.0";

        private const float StartupNotificationSeconds = 4f;
        private const float StatusNotificationSeconds = 1.5f;
        private const int ReticleBaseSizeAt1080p = 6;
        private const int ReticleOutlineWidthAt1080p = 2;

        private enum TrackingMode { Full, RotationOnly, PositionOnly }

        public static HeadTrackingPlugin Instance { get; private set; }
        public new ManualLogSource Logger => base.Logger;
        public bool TrackingEnabled { get; private set; }
        public CameraController CameraController => _cameraController;

        private ConfigManager _config;
        private OpenTrackReceiver _receiver;
        private TrackingProcessor _processor;
        private PoseInterpolator _interpolator;
        private PositionProcessor _positionProcessor;
        private PositionInterpolator _positionInterpolator;
        private CameraController _cameraController;
        private GameStateDetector _gameStateDetector;
        private InputHandler _inputHandler;
        private NotificationUI _notificationUI;
        private IMGUIReticle _aimReticle;
        private bool _reticleEnabled;
        private bool _wasReceiving;
        private TrackingMode _trackingMode;
        private bool _initialized;

        // CalculateAimOffset is invoked from IMGUIReticle.OnGUI, which Unity fires multiple
        // times per frame (Layout + Repaint at minimum). The inputs (LastTrackingYaw/Pitch/Roll
        // and FOV/aspect/screen) don't change between OnGUI events within a frame, so we
        // memoize by Time.frameCount.
        private int _aimOffsetCachedFrame = -1;
        private UnityEngine.Vector2 _aimOffsetCached;

        private void Awake()
        {
            Instance = this;
            Logger.LogInfo($"{PluginName} v{PluginVersion} initializing...");

            _config = new ConfigManager();
            _config.Initialize(Config);

            BuildPipeline();
            BuildCameraController();
            BuildGameStateDetector();
            BuildInput();
            BuildUI();

            _receiver.Start(_config.UDPPort.Value);
            TrackingEnabled = _config.EnabledOnStartup.Value;
            _initialized = true;

            Logger.LogInfo($"{PluginName} initialized. Tracking {(TrackingEnabled ? "enabled" : "disabled")}");
            Logger.LogInfo($"Listening on UDP port {_config.UDPPort.Value}");

            if (_config.ShowStartupNotification.Value)
            {
                string status = TrackingEnabled ? "Head Tracking: ON" : "Head Tracking: OFF";
                _notificationUI.ShowNotification($"{status}\n{BuildHotkeyInfo()}", StartupNotificationSeconds);
            }
        }

        private void BuildPipeline()
        {
            _receiver = new OpenTrackReceiver();
            _receiver.Log = msg => Logger.LogInfo(msg);

            _processor = new TrackingProcessor
            {
                SmoothingFactor = _config.Smoothing.Value,
                Sensitivity = new SensitivitySettings(
                    _config.YawSensitivity.Value,
                    _config.PitchSensitivity.Value,
                    _config.RollSensitivity.Value,
                    invertYaw: false,
                    invertPitch: true,
                    invertRoll: false),
                Deadzone = DeadzoneSettings.None
            };
            _interpolator = new PoseInterpolator();

            _positionProcessor = new PositionProcessor
            {
                Settings = new PositionSettings(
                    _config.PositionSensitivityX.Value,
                    _config.PositionSensitivityY.Value,
                    _config.PositionSensitivityZ.Value,
                    _config.PositionLimitX.Value,
                    _config.PositionLimitY.Value,
                    _config.PositionLimitZ.Value,
                    _config.PositionLimitZBack.Value,
                    _config.PositionSmoothing.Value,
                    invertX: true, invertY: false, invertZ: true),
                TrackerPivotForward = _config.TrackerPivotForward.Value
            };
            _positionInterpolator = new PositionInterpolator();
        }

        private void BuildCameraController()
        {
            _cameraController = new CameraController(
                _receiver, _processor, _interpolator,
                _positionProcessor, _positionInterpolator);
            _cameraController.PositionEnabled = _config.PositionEnabled.Value;
            _cameraController.RotationEnabled = true;
            _cameraController.WorldSpaceYaw = _config.WorldSpaceYaw.Value;
            _cameraController.Enable();

            // Seed _trackingMode from the actual initial state so the first cycle
            // press transitions away from the current mode rather than back to it.
            _trackingMode = _config.PositionEnabled.Value ? TrackingMode.Full : TrackingMode.RotationOnly;
        }

        private void BuildGameStateDetector()
        {
            _gameStateDetector = new GameStateDetector();
            _gameStateDetector.StateChanged += OnGameStateChanged;
            _gameStateDetector.Initialize();
        }

        private void BuildInput()
        {
            _inputHandler = new InputHandler(_config);
            _inputHandler.OnTogglePressed += HandleToggle;
            _inputHandler.OnRecenterPressed += HandleRecenter;
            _inputHandler.OnToggleReticlePressed += HandleToggleReticle;
            _inputHandler.OnCycleTrackingModePressed += HandleCycleTrackingMode;
            _inputHandler.OnToggleYawModePressed += HandleToggleYawMode;
        }

        private void BuildUI()
        {
            _notificationUI = new NotificationUI();
            _reticleEnabled = _config.ShowReticle.Value;

            _aimReticle = gameObject.AddComponent<IMGUIReticle>();
            _aimReticle.Style = ReticleStyle.Dot;
            _aimReticle.BaseSizeAt1080p = ReticleBaseSizeAt1080p;
            _aimReticle.OutlineWidthAt1080p = ReticleOutlineWidthAt1080p;
            _aimReticle.ReticleColor = UnityEngine.Color.white;
            _aimReticle.OutlineColor = UnityEngine.Color.black;
            _aimReticle.IsVisible = _reticleEnabled;
            _aimReticle.InitializeWithOffset(
                getOffset: CalculateAimOffset,
                shouldDraw: () => _gameStateDetector.IsGameplayActive
                                  && _reticleEnabled
                                  && _cameraController.IsApplyingTracking);
        }

        private string BuildHotkeyInfo()
        {
            return $"[{_inputHandler.ToggleKey}/Ctrl+Shift+{InputHandler.ToggleChordLetter}] Toggle, " +
                   $"[{_inputHandler.RecenterKey}/Ctrl+Shift+{InputHandler.RecenterChordLetter}] Recenter, " +
                   $"[{_inputHandler.CycleTrackingModeKey}/Ctrl+Shift+{InputHandler.CycleTrackingModeChordLetter}] Cycle Mode, " +
                   $"[{_inputHandler.YawModeKey}/Ctrl+Shift+{InputHandler.YawModeChordLetter}] Yaw, " +
                   $"[{_inputHandler.ToggleReticleKey}/Ctrl+Shift+{InputHandler.ToggleReticleChordLetter}] Reticle";
        }

        private void Update()
        {
            // Awake may have failed partway, leaving a subset of fields null.
            // A single guard avoids per-field NRE risk if init ordering changes.
            if (!_initialized) return;
            _inputHandler.CheckInput();
            _gameStateDetector.Update();
            _notificationUI.Update();
            MonitorConnectionState();
        }

        private void LateUpdate()
        {
            if (!_initialized) return;
            bool shouldTrack = TrackingEnabled && _gameStateDetector.IsGameplayActive;
            _cameraController.ProcessFrame(shouldTrack);
        }

        private void OnGUI()
        {
            _notificationUI?.Draw();
        }

        private void OnDestroy()
        {
            Logger.LogInfo($"{PluginName} shutting down...");

            if (_inputHandler != null)
            {
                _inputHandler.OnTogglePressed -= HandleToggle;
                _inputHandler.OnRecenterPressed -= HandleRecenter;
                _inputHandler.OnToggleReticlePressed -= HandleToggleReticle;
                _inputHandler.OnCycleTrackingModePressed -= HandleCycleTrackingMode;
                _inputHandler.OnToggleYawModePressed -= HandleToggleYawMode;
            }
            if (_gameStateDetector != null)
            {
                _gameStateDetector.StateChanged -= OnGameStateChanged;
                _gameStateDetector.Shutdown();
            }

            _cameraController?.Disable();
            _receiver?.Dispose();

            Instance = null;
        }

        private void MonitorConnectionState()
        {
            bool isReceiving = _receiver.IsReceiving;
            if (isReceiving == _wasReceiving)
                return;

            if (_config.ShowConnectionNotifications.Value)
            {
                if (isReceiving)
                {
                    _notificationUI.ShowConnectionEstablished();
                    Logger.LogInfo("OpenTrack connection established");
                }
                else
                {
                    _notificationUI.ShowConnectionLost();
                    Logger.LogInfo("OpenTrack connection lost");
                }
            }
            _wasReceiving = isReceiving;
        }

        private void HandleToggle()
        {
            TrackingEnabled = !TrackingEnabled;
            if (TrackingEnabled)
            {
                _cameraController.OnTrackingEnabled();
                _notificationUI.ShowTrackingEnabled();
                Logger.LogInfo("Head tracking enabled");
            }
            else
            {
                _cameraController.OnTrackingDisabled();
                _notificationUI.ShowTrackingDisabled();
                Logger.LogInfo("Head tracking disabled");
            }
        }

        private void HandleRecenter()
        {
            _cameraController.Recenter();
            _notificationUI.ShowRecentered();
            Logger.LogInfo("Head tracking recentered");
        }

        private void HandleToggleReticle()
        {
            _reticleEnabled = !_reticleEnabled;
            _aimReticle.IsVisible = _reticleEnabled;
            _notificationUI.ShowNotification(
                _reticleEnabled ? "Reticle: ON" : "Reticle: OFF",
                _reticleEnabled ? NotificationType.Success : NotificationType.Warning,
                StatusNotificationSeconds);
            Logger.LogInfo($"Reticle {(_reticleEnabled ? "enabled" : "disabled")}");
        }

        private void HandleCycleTrackingMode()
        {
            _trackingMode = NextMode(_trackingMode);

            string label;
            switch (_trackingMode)
            {
                case TrackingMode.Full:
                    _cameraController.RotationEnabled = true;
                    _cameraController.PositionEnabled = true;
                    label = "Tracking: Full (rotation + position)";
                    break;
                case TrackingMode.RotationOnly:
                    _cameraController.RotationEnabled = true;
                    _cameraController.PositionEnabled = false;
                    label = "Tracking: Rotation only";
                    break;
                default:
                    _cameraController.RotationEnabled = false;
                    _cameraController.PositionEnabled = true;
                    label = "Tracking: Position only";
                    break;
            }

            _notificationUI.ShowNotification(label, NotificationType.Info, StatusNotificationSeconds);
            Logger.LogInfo(label);
        }

        private static TrackingMode NextMode(TrackingMode mode)
        {
            switch (mode)
            {
                case TrackingMode.Full: return TrackingMode.RotationOnly;
                case TrackingMode.RotationOnly: return TrackingMode.PositionOnly;
                default: return TrackingMode.Full;
            }
        }

        private void HandleToggleYawMode()
        {
            _cameraController.WorldSpaceYaw = !_cameraController.WorldSpaceYaw;
            _notificationUI.ShowNotification(
                _cameraController.WorldSpaceYaw ? "Yaw: World-locked" : "Yaw: Camera-local",
                NotificationType.Info,
                StatusNotificationSeconds);
            Logger.LogInfo($"Yaw mode: {(_cameraController.WorldSpaceYaw ? "world-locked" : "camera-local")}");
        }

        private UnityEngine.Vector2 CalculateAimOffset()
        {
            int frame = UnityEngine.Time.frameCount;
            if (frame == _aimOffsetCachedFrame)
                return _aimOffsetCached;

            var cam = _cameraController.MainCamera;
            if (cam == null)
            {
                _aimOffsetCached = UnityEngine.Vector2.zero;
            }
            else
            {
                _aimOffsetCached = ReticleAimProjection.Compute(
                    yaw: _cameraController.LastTrackingYaw,
                    pitch: _cameraController.LastTrackingPitch,
                    roll: _cameraController.LastTrackingRoll,
                    verticalFovDegrees: cam.fieldOfView,
                    aspect: cam.aspect,
                    screenWidth: UnityEngine.Screen.width,
                    screenHeight: UnityEngine.Screen.height);
            }
            _aimOffsetCachedFrame = frame;
            return _aimOffsetCached;
        }

        private void OnGameStateChanged(GameState newState)
        {
            if (newState == GameState.Gameplay && TrackingEnabled)
                _cameraController.OnTrackingEnabled();
            else if (newState != GameState.Gameplay)
                _cameraController.ResetState();
        }
    }
}
