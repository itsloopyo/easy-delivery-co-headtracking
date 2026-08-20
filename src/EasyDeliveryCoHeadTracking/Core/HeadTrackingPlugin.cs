using BepInEx;
using BepInEx.Logging;
using CameraUnlock.Core.Aim;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.Math;
using CameraUnlock.Core.Processing;
using CameraUnlock.Core.Protocol;
using CameraUnlock.Core.Tracking;
using CameraUnlock.Core.Unity.Extensions;
using CameraUnlock.Core.Unity.Rendering;
using CameraUnlock.Core.Unity.Tracking;
using CameraUnlock.Core.Unity.UI;
using CameraUnlock.Core.Unity.Utilities;
using EasyDeliveryCoHeadTracking.Camera;
using EasyDeliveryCoHeadTracking.Config;

namespace EasyDeliveryCoHeadTracking.Core
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class HeadTrackingPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.cameraunlock.easydeliveryco.headtracking";
        public const string PluginName = "Easy Delivery Co Head Tracking";
        public const string PluginVersion = "0.2.0";

        private const float StartupNotificationSeconds = 4f;
        private const float StatusNotificationSeconds = 1.5f;
        private const int ReticleBaseSizeAt1080p = 6;
        private const int ReticleOutlineWidthAt1080p = 2;

        public static HeadTrackingPlugin Instance { get; private set; }
        public new ManualLogSource Logger => base.Logger;
        public bool TrackingEnabled { get; private set; }
        public ViewMatrixTrackingController CameraController => _cameraController;

        private ConfigManager _config;
        private OpenTrackReceiver _receiver;
        private TrackingProcessor _processor;
        private PoseInterpolator _interpolator;
        private PositionProcessor _positionProcessor;
        private PositionInterpolator _positionInterpolator;
        private ViewMatrixTrackingController _cameraController;
        private GameStateDetector _gameStateDetector;
        private InputHandler _inputHandler;
        private NotificationUI _notificationUI;
        private IMGUIReticle _aimReticle;
        private bool _reticleEnabled;
        private bool _wasReceiving;
        private TrackingMode _trackingMode;
        private bool _initialized;

        // Cached so the connection locality is only pushed into the processors when the
        // tracker actually switches between a same-machine and a remote source.
        private bool _cachedIsRemoteConnection;
        private bool _hasCachedConnectionLocality;

        // The aim offset is read from IMGUIReticle.OnGUI, which Unity fires multiple
        // times per frame (Layout + Repaint at minimum). The inputs (LastTrackingYaw/Pitch/Roll
        // and FOV/aspect/screen) don't change between OnGUI events within a frame.
        private PerFrameCache<UnityEngine.Vector2> _aimOffsetCache;

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
                LocalSmoothing = _config.LocalSmoothing.Value,
                RemoteSmoothing = _config.RemoteSmoothing.Value,
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
                Settings = PositionSettings.Symmetric(
                    _config.PositionSensitivityX.Value,
                    _config.PositionSensitivityY.Value,
                    _config.PositionSensitivityZ.Value,
                    _config.PositionLimitX.Value,
                    _config.PositionLimitY.Value,
                    _config.PositionLimitZ.Value,
                    _config.PositionLimitZBack.Value,
                    _config.LocalSmoothing.Value,
                    _config.RemoteSmoothing.Value,
                    invertX: true, invertY: false, invertZ: false),
                TrackerPivotForward = _config.TrackerPivotForward.Value
            };
            _positionInterpolator = new PositionInterpolator();
        }

        private void BuildCameraController()
        {
            _cameraController = new ViewMatrixTrackingController(
                _receiver, _processor, _interpolator,
                _positionProcessor, _positionInterpolator);
            _cameraController.WorldSpaceYaw = _config.WorldSpaceYaw.Value;

            // Seed the mode from config so the first cycle press transitions away
            // from the current mode rather than back to it.
            SetTrackingMode(_config.PositionEnabled.Value
                ? TrackingMode.RotationAndPosition
                : TrackingMode.RotationOnly);
            _cameraController.Enable();
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
            _inputHandler.OnToggleReticlePressed += HandleToggleReticle;
            _inputHandler.OnCycleTrackingModePressed += HandleCycleTrackingMode;
            _inputHandler.OnToggleYawModePressed += HandleToggleYawMode;
        }

        private void BuildUI()
        {
            _notificationUI = new NotificationUI();
            _reticleEnabled = _config.ShowReticle.Value;
            _aimOffsetCache = new PerFrameCache<UnityEngine.Vector2>(ComputeAimOffset);

            _aimReticle = gameObject.AddComponent<IMGUIReticle>();
            _aimReticle.Style = ReticleStyle.Dot;
            _aimReticle.BaseSizeAt1080p = ReticleBaseSizeAt1080p;
            _aimReticle.OutlineWidthAt1080p = ReticleOutlineWidthAt1080p;
            _aimReticle.ReticleColor = UnityEngine.Color.white;
            _aimReticle.OutlineColor = UnityEngine.Color.black;
            _aimReticle.IsVisible = _reticleEnabled;
            _aimReticle.InitializeWithOffset(
                getOffset: _aimOffsetCache.Get,
                shouldDraw: () => _gameStateDetector.IsGameplayActive
                                  && _reticleEnabled
                                  && _cameraController.IsApplyingTracking);
        }

        private string BuildHotkeyInfo()
        {
            return $"[{_inputHandler.ToggleKey}/Ctrl+Shift+{ChordHotkeys.ToggleLetter}] Toggle, " +
                   $"[{_inputHandler.CycleTrackingModeKey}/Ctrl+Shift+{ChordHotkeys.PositionLetter}] Cycle Mode, " +
                   $"[{_inputHandler.YawModeKey}/Ctrl+Shift+{ChordHotkeys.FourthToggleLetter}] Yaw, " +
                   $"[{_inputHandler.ToggleReticleKey}/Ctrl+Shift+{ChordHotkeys.FifthToggleLetter}] Reticle";
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
            MonitorConnectionLocality();
        }

        /// <summary>
        /// Logs which smoothing parameter the current tracker source selects, so a user
        /// switching between a local OpenTrack instance and a phone on WiFi can see the
        /// change take effect. Read only: the controller owns the write, pushing the same
        /// flag onto both processors from ProcessFrame immediately before either one runs
        /// (it owns them from construction), so a second push here would be redundant
        /// rather than authoritative.
        /// </summary>
        private void MonitorConnectionLocality()
        {
            bool isRemoteConnection = _receiver.IsRemoteConnection;
            if (_hasCachedConnectionLocality && isRemoteConnection == _cachedIsRemoteConnection)
                return;

            _cachedIsRemoteConnection = isRemoteConnection;
            _hasCachedConnectionLocality = true;

            float effective = SmoothingUtils.GetEffectiveSmoothing(
                _config.LocalSmoothing.Value, _config.RemoteSmoothing.Value, isRemoteConnection);
            Logger.LogInfo($"Tracker source is {(isRemoteConnection ? "remote" : "local")}, smoothing={effective:F2}");
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

            // The on-screen notification is a user preference; the log line is the only
            // evidence in the log that tracker packets ever arrived, so it is not gated.
            Logger.LogInfo(isReceiving ? "OpenTrack connection established" : "OpenTrack connection lost");

            if (_config.ShowConnectionNotifications.Value)
            {
                if (isReceiving)
                {
                    _notificationUI.ShowConnectionEstablished();
                }
                else
                {
                    _notificationUI.ShowConnectionLost();
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
            SetTrackingMode((TrackingMode)(((int)_trackingMode + 1) % 3));

            string label = "Tracking: " + _trackingMode.Description();
            _notificationUI.ShowNotification(label, NotificationType.Info, StatusNotificationSeconds);
            Logger.LogInfo(label);
        }

        private void SetTrackingMode(TrackingMode mode)
        {
            _trackingMode = mode;
            _cameraController.RotationEnabled = mode != TrackingMode.PositionOnly;
            _cameraController.PositionEnabled = mode != TrackingMode.RotationOnly;
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

        private UnityEngine.Vector2 ComputeAimOffset()
        {
            var cam = _cameraController.MainCamera;
            if (cam == null)
                return UnityEngine.Vector2.zero;

            float horizontalFov = ScreenOffsetCalculator.CalculateHorizontalFov(cam.fieldOfView, cam.aspect);
            float offsetX, offsetY;
            ScreenOffsetCalculator.Calculate(
                _cameraController.LastTrackingYaw,
                _cameraController.LastTrackingPitch,
                _cameraController.LastTrackingRoll,
                horizontalFov,
                cam.fieldOfView,
                UnityEngine.Screen.width,
                UnityEngine.Screen.height,
                compensationScale: 1f,
                out offsetX,
                out offsetY);

            // ScreenOffsetCalculator Y is up-positive, matching IMGUIReticle's offset convention.
            return new UnityEngine.Vector2(offsetX, offsetY);
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
