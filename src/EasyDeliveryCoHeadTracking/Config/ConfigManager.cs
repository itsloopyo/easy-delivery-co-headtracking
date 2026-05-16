using BepInEx.Configuration;
using UnityEngine;

namespace EasyDeliveryCoHeadTracking.Config
{
    public class ConfigManager
    {
        // General
        public ConfigEntry<bool> EnabledOnStartup { get; private set; }
        public ConfigEntry<bool> ShowStartupNotification { get; private set; }
        public ConfigEntry<bool> WorldSpaceYaw { get; private set; }

        // UI
        public ConfigEntry<bool> ShowConnectionNotifications { get; private set; }
        public ConfigEntry<bool> ShowReticle { get; private set; }

        // Keybindings
        public ConfigEntry<KeyCode> ToggleKey { get; private set; }
        public ConfigEntry<KeyCode> RecenterKey { get; private set; }
        public ConfigEntry<KeyCode> ToggleReticleKey { get; private set; }
        public ConfigEntry<KeyCode> CycleTrackingModeKey { get; private set; }
        public ConfigEntry<KeyCode> YawModeKey { get; private set; }

        // Network
        public ConfigEntry<int> UDPPort { get; private set; }

        // Sensitivity
        public ConfigEntry<float> YawSensitivity { get; private set; }
        public ConfigEntry<float> PitchSensitivity { get; private set; }
        public ConfigEntry<float> RollSensitivity { get; private set; }

        // Smoothing
        public ConfigEntry<float> Smoothing { get; private set; }

        // Position
        public ConfigEntry<bool> PositionEnabled { get; private set; }
        public ConfigEntry<float> PositionSensitivityX { get; private set; }
        public ConfigEntry<float> PositionSensitivityY { get; private set; }
        public ConfigEntry<float> PositionSensitivityZ { get; private set; }
        public ConfigEntry<float> PositionLimitX { get; private set; }
        public ConfigEntry<float> PositionLimitY { get; private set; }
        public ConfigEntry<float> PositionLimitZ { get; private set; }
        public ConfigEntry<float> PositionLimitZBack { get; private set; }
        public ConfigEntry<float> PositionSmoothing { get; private set; }
        public ConfigEntry<float> TrackerPivotForward { get; private set; }

        public void Initialize(ConfigFile config)
        {
            EnabledOnStartup = config.Bind(
                "General", "EnabledOnStartup", true,
                "Whether head tracking is enabled when the game starts");

            ShowStartupNotification = config.Bind(
                "General", "ShowStartupNotification", true,
                "Whether to show a notification when the plugin initializes");

            WorldSpaceYaw = config.Bind(
                "General", "WorldSpaceYaw", true,
                "Yaw mode: true = horizon-locked yaw (default), false = camera-local");

            ShowConnectionNotifications = config.Bind(
                "UI", "ShowConnectionNotifications", true,
                "Whether to show notifications when OpenTrack connection is lost or restored");

            ShowReticle = config.Bind(
                "UI", "ShowReticle", false,
                "Whether to show the aim reticle during gameplay (not needed for driving)");

            ToggleKey = config.Bind(
                "Keybindings", "ToggleKey", KeyCode.End,
                "Key to toggle head tracking on/off");

            RecenterKey = config.Bind(
                "Keybindings", "RecenterKey", KeyCode.Home,
                "Key to recenter head tracking");

            ToggleReticleKey = config.Bind(
                "Keybindings", "ToggleReticleKey", KeyCode.Insert,
                "Key to toggle the aim reticle on/off");

            CycleTrackingModeKey = config.Bind(
                "Keybindings", "CycleTrackingModeKey", KeyCode.PageUp,
                "Key to cycle tracking mode (normal -> rotation only -> position only -> normal)");

            YawModeKey = config.Bind(
                "Keybindings", "YawModeKey", KeyCode.PageDown,
                "Key to toggle world-locked vs camera-local yaw");

            UDPPort = config.Bind(
                "Network", "UDPPort", 4242,
                new ConfigDescription(
                    "UDP port to listen for OpenTrack data",
                    new AcceptableValueRange<int>(1024, 65535)));

            YawSensitivity = config.Bind(
                "Sensitivity", "YawSensitivity", 1.0f,
                new ConfigDescription(
                    "Multiplier for horizontal head rotation (left/right)",
                    new AcceptableValueRange<float>(0.1f, 3.0f)));

            PitchSensitivity = config.Bind(
                "Sensitivity", "PitchSensitivity", 1.0f,
                new ConfigDescription(
                    "Multiplier for vertical head rotation (up/down)",
                    new AcceptableValueRange<float>(0.1f, 3.0f)));

            RollSensitivity = config.Bind(
                "Sensitivity", "RollSensitivity", 1.0f,
                new ConfigDescription(
                    "Multiplier for head tilt (ear to shoulder)",
                    new AcceptableValueRange<float>(0.0f, 3.0f)));

            Smoothing = config.Bind(
                "Smoothing", "Smoothing", 0.0f,
                new ConfigDescription(
                    "Smoothing level (0 = responsive, 1 = heavy smoothing)",
                    new AcceptableValueRange<float>(0f, 1f)));

            PositionEnabled = config.Bind(
                "Position", "PositionEnabled", true,
                "Enable positional tracking (lean in/out/side-to-side)");

            PositionSensitivityX = config.Bind(
                "Position", "PositionSensitivityX", 1.0f,
                new ConfigDescription(
                    "Multiplier for lateral (left/right) position",
                    new AcceptableValueRange<float>(0f, 5.0f)));

            PositionSensitivityY = config.Bind(
                "Position", "PositionSensitivityY", 1.0f,
                new ConfigDescription(
                    "Multiplier for vertical (up/down) position",
                    new AcceptableValueRange<float>(0f, 5.0f)));

            PositionSensitivityZ = config.Bind(
                "Position", "PositionSensitivityZ", 1.0f,
                new ConfigDescription(
                    "Multiplier for depth (forward/back) position",
                    new AcceptableValueRange<float>(0f, 5.0f)));

            PositionLimitX = config.Bind(
                "Position", "PositionLimitX", 0.30f,
                new ConfigDescription(
                    "Maximum lateral displacement in meters",
                    new AcceptableValueRange<float>(0.01f, 0.5f)));

            PositionLimitY = config.Bind(
                "Position", "PositionLimitY", 0.20f,
                new ConfigDescription(
                    "Maximum vertical displacement in meters",
                    new AcceptableValueRange<float>(0.01f, 0.5f)));

            PositionLimitZ = config.Bind(
                "Position", "PositionLimitZ", 0.40f,
                new ConfigDescription(
                    "Maximum forward displacement in meters",
                    new AcceptableValueRange<float>(0.01f, 0.5f)));

            PositionLimitZBack = config.Bind(
                "Position", "PositionLimitZBack", 0.10f,
                new ConfigDescription(
                    "Maximum backward displacement in meters",
                    new AcceptableValueRange<float>(0.01f, 0.5f)));

            PositionSmoothing = config.Bind(
                "Position", "PositionSmoothing", 0.15f,
                new ConfigDescription(
                    "Smoothing for positional tracking (0 = instant, 1 = very slow)",
                    new AcceptableValueRange<float>(0f, 1f)));

            TrackerPivotForward = config.Bind(
                "Position", "TrackerPivotForward", 0.08f,
                new ConfigDescription(
                    "Distance from pivot point to tracker face point. " +
                    "Compensates lateral arc from head yaw in position data.",
                    new AcceptableValueRange<float>(0f, 0.20f)));
        }
    }
}
