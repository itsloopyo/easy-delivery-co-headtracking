using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using CameraUnlock.Core.Config;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.Math;
using CameraUnlock.Core.Processing;
using CameraUnlock.Core.Protocol;
using CameraUnlock.Core.Tracking;
using CameraUnlock.Core.Unity.Tracking;
using CameraUnlock.Core.Unity.UI;
using EasyDeliveryCoHeadTracking.Camera;
using EasyDeliveryCoHeadTracking.Config;
using EasyDeliveryCoHeadTracking.Legacy;

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
        private const float ConfigNotificationSeconds = 8f;

        public static HeadTrackingPlugin Instance { get; private set; }
        public new ManualLogSource Logger => base.Logger;
        public bool TrackingEnabled { get; private set; }
        public ViewMatrixTrackingController CameraController => _cameraController;

        private EasyDeliveryCoConfig _config;
        private ConfigOwner<EasyDeliveryCoConfig> _configOwner;
        private OpenTrackReceiver _receiver;
        private TrackingProcessor _processor;
        private PoseInterpolator _interpolator;
        private PositionProcessor _positionProcessor;
        private PositionInterpolator _positionInterpolator;
        private ViewMatrixTrackingController _cameraController;
        private GameStateDetector _gameStateDetector;
        private InputHandler _inputHandler;
        private NotificationUI _notificationUI;
        private bool _wasReceiving;
        private TrackingMode _trackingMode;
        private bool _initialized;

        // Cached so the connection locality is only pushed into the processors when the
        // tracker actually switches between a same-machine and a remote source.
        private bool _cachedIsRemoteConnection;
        private bool _hasCachedConnectionLocality;

        private void Awake()
        {
            Instance = this;
            Logger.LogInfo($"{PluginName} v{PluginVersion} initializing...");

            // Built before the config loads, so the owner's status sink can reach the player
            // when the file cannot be read, imported or created.
            _notificationUI = new NotificationUI();
            LoadConfig();

            BuildPipeline();
            BuildCameraController();
            BuildGameStateDetector();
            BuildInput();

            _receiver.Start(_config.UdpPort);
            TrackingEnabled = _config.EnableOnStartup;
            _initialized = true;

            Logger.LogInfo($"{PluginName} initialized. Tracking {(TrackingEnabled ? "enabled" : "disabled")}");
            Logger.LogInfo($"Listening on UDP port {_config.UdpPort}");

            // A config the owner could not load or create has already put its message up, and the
            // startup toast would replace it.
            if (_config.ShowStartupNotification && !_notificationUI.IsDisplaying)
            {
                string status = TrackingEnabled ? "Head Tracking: ON" : "Head Tracking: OFF";
                _notificationUI.ShowNotification($"{status}\n{BuildHotkeyInfo()}", StartupNotificationSeconds);
            }
        }

        /// <summary>
        /// The settings live in BepInEx\config\CameraUnlock.ini, read and written by core's config
        /// owner, with rows set to default following the player's Defaults.ini. Nothing is bound
        /// on the plugin's Config, so ConfigurationManager does not list them. While
        /// CameraUnlock.ini is absent the owner imports the plugin's .cfg, the file every earlier
        /// build read, through the frozen v0.2.0 reader on a ConfigFile of its own, and never
        /// writes that file.
        /// </summary>
        private void LoadConfig()
        {
            _configOwner = new ConfigOwner<EasyDeliveryCoConfig>(new ConfigOwnerOptions<EasyDeliveryCoConfig>
            {
                Path = ConfigPath,
                Table = EasyDeliveryCoConfig.Table(),
                Import = LegacyConfigImport.For(Info.Metadata),
                LegacySourcePath = Config.ConfigFilePath,
                Header = new RenderHeader(EasyDeliveryCoConfig.DisplayName),
                Defaults = DefaultsFile.PerUser(),
                StatusSink = ShowConfigMessage
            });

            _loadMessages = string.Empty;
            ConfigLoadResult<EasyDeliveryCoConfig> loaded = _configOwner.Load();
            _loadMessages = null;
            _config = loaded.Config;

            // The owner writes each diagnostic as "<path>: <description>" among lines that only
            // report what it did, so the complaints are picked out by their text.
            var complaints = new HashSet<string>();
            foreach (CanonicalDiagnostic diagnostic in loaded.Diagnostics)
                complaints.Add(ConfigPath + ": " + diagnostic.Describe());
            bool usable = loaded.Status == ConfigLoadStatus.Canonical
                          || loaded.Status == ConfigLoadStatus.Migrated
                          || loaded.Status == ConfigLoadStatus.Created;
            foreach (string line in loaded.Log)
            {
                if (usable && !complaints.Contains(line)) Logger.LogInfo(line);
                else Logger.LogWarning(line);
            }
            Logger.LogInfo("Config " + ConfigPath + ": " + loaded.Status);
        }

        private static string ConfigPath
        {
            get { return Path.Combine(Paths.ConfigPath, "CameraUnlock.ini"); }
        }

        // Non-null while Load runs. Load can hand the sink two messages, the config file's and
        // then one about Defaults.ini, and the notification shows one message at a time, so the
        // second is shown beneath the first rather than in its place.
        private string _loadMessages;

        private void ShowConfigMessage(string message)
        {
            if (_loadMessages != null)
            {
                _loadMessages = _loadMessages.Length == 0 ? message : _loadMessages + "\n" + message;
                message = _loadMessages;
            }
            _notificationUI.ShowNotification(message, NotificationType.Warning, ConfigNotificationSeconds);
        }

        /// <summary>
        /// Called after the new value is already applied. A save that fails is logged, the owner
        /// shows the player why, and the session keeps the new value.
        /// </summary>
        private void SaveConfig(Action<EasyDeliveryCoConfig> change)
        {
            ConfigSaveResult saved = _configOwner.Save(change);
            if (saved.Status == ConfigSaveStatus.Saved)
            {
                // A row that held default and now holds a value, so it stops following
                // Defaults.ini in this game.
                foreach (string line in saved.Log) Logger.LogInfo(line);
                return;
            }
            foreach (string line in saved.Log) Logger.LogWarning(line);
            Logger.LogWarning(ConfigPath + ": " + saved.Status + ": " + saved.Reason
                              + " The change applies to this session only.");
        }

        private void BuildPipeline()
        {
            _receiver = new OpenTrackReceiver();
            _receiver.Log = msg => Logger.LogInfo(msg);

            // The pitch inversion here and the lateral one below are the axis conversion every
            // published build applied, with every multiplier at 1. None of it is a setting.
            _processor = new TrackingProcessor
            {
                LocalSmoothing = _config.LocalSmoothing,
                RemoteSmoothing = _config.RemoteSmoothing,
                Sensitivity = new SensitivitySettings(
                    1.0f, 1.0f, 1.0f,
                    invertYaw: false,
                    invertPitch: true,
                    invertRoll: false),
                Deadzone = DeadzoneSettings.None
            };
            _interpolator = new PoseInterpolator();

            _positionProcessor = new PositionProcessor
            {
                Settings = new PositionSettings(
                    1.0f, 1.0f, 1.0f,
                    _config.Position.LimitX,
                    _config.Position.LimitY,
                    _config.Position.LimitYDown,
                    _config.Position.LimitZ,
                    _config.Position.LimitZBack,
                    _config.LocalSmoothing,
                    _config.RemoteSmoothing,
                    invertX: true, invertY: false, invertZ: false),
                TrackerPivotForward = _config.TrackerPivotForward
            };
            _positionInterpolator = new PositionInterpolator();
        }

        private void BuildCameraController()
        {
            _cameraController = new ViewMatrixTrackingController(
                _receiver, _processor, _interpolator,
                _positionProcessor, _positionInterpolator);
            _cameraController.WorldSpaceYaw = _config.WorldSpaceYaw;

            // The pair always names a mode: the table reads a pair that names none as its default.
            // Seeding it from the file makes the first cycle press move on from the saved mode.
            SetTrackingMode(TrackingModeChannels.Decode(_config.RotationEnabled, _config.PositionEnabled).Value);
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
            _inputHandler = new InputHandler(_config, msg => Logger.LogWarning(msg));
            _inputHandler.OnTogglePressed += HandleToggle;
            _inputHandler.OnCycleTrackingModePressed += HandleCycleTrackingMode;
            _inputHandler.OnToggleYawModePressed += HandleToggleYawMode;
        }

        private string BuildHotkeyInfo()
        {
            return $"[{_config.ToggleKeyName}] Toggle, " +
                   $"[{_config.CycleTrackingModeKeyName}] Cycle Mode, " +
                   $"[{_config.YawModeKeyName}] Yaw";
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
                _config.LocalSmoothing, _config.RemoteSmoothing, isRemoteConnection);
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

            if (_config.ShowConnectionNotifications)
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

        /// <summary>The master on/off. It changes this session only and never writes the file.</summary>
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

        private void HandleCycleTrackingMode()
        {
            SetTrackingMode((TrackingMode)(((int)_trackingMode + 1) % 3));

            string label = "Tracking: " + _trackingMode.Description();
            _notificationUI.ShowNotification(label, NotificationType.Info, StatusNotificationSeconds);
            Logger.LogInfo(label);

            bool rotation;
            bool position;
            TrackingModeChannels.Encode(_trackingMode, out rotation, out position);
            SaveConfig(c =>
            {
                c.RotationEnabled = rotation;
                c.PositionEnabled = position;
            });
        }

        private void SetTrackingMode(TrackingMode mode)
        {
            _trackingMode = mode;
            bool rotation;
            bool position;
            TrackingModeChannels.Encode(mode, out rotation, out position);
            _cameraController.RotationEnabled = rotation;
            _cameraController.PositionEnabled = position;
        }

        private void HandleToggleYawMode()
        {
            bool worldSpaceYaw = !_cameraController.WorldSpaceYaw;
            _cameraController.WorldSpaceYaw = worldSpaceYaw;
            _notificationUI.ShowNotification(
                worldSpaceYaw ? "Yaw: World-locked" : "Yaw: Camera-local",
                NotificationType.Info,
                StatusNotificationSeconds);
            Logger.LogInfo($"Yaw mode: {(worldSpaceYaw ? "world-locked" : "camera-local")}");

            SaveConfig(c => c.WorldSpaceYaw = worldSpaceYaw);
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
