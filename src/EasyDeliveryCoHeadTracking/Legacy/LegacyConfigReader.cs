using System.IO;
using BepInEx.Configuration;

namespace EasyDeliveryCoHeadTracking.Legacy
{
    /// <summary>
    /// The plugin's BepInEx Bind calls as v0.2.0 ran them, each definition's section, key, type,
    /// description and acceptable values unchanged and its default taken from
    /// <see cref="LegacyConfig"/>. Frozen for the life of the repo: it is how a player's .cfg is
    /// read, whichever earlier build wrote it.
    /// <para>
    /// It writes nothing. BepInEx's ConfigFile read the .cfg in its constructor, before whoever
    /// calls this held the file, so saving on set is turned off first and the file is read again
    /// before anything is bound. A missing file reads as the defaults.
    /// </para>
    /// </summary>
    internal static class LegacyConfigReader
    {
        /// <summary>Reads <paramref name="config"/>'s file into a new <see cref="LegacyConfig"/>.</summary>
        /// <param name="found">Whether the file existed.</param>
        public static LegacyConfig Read(ConfigFile config, out bool found)
        {
            config.SaveOnConfigSet = false;
            found = File.Exists(config.ConfigFilePath);
            if (found)
            {
                config.Reload();
            }

            var read = new LegacyConfig();

            read.EnabledOnStartup = config.Bind(
                "General", "EnabledOnStartup", read.EnabledOnStartup,
                "Whether head tracking is enabled when the game starts").Value;

            read.ShowStartupNotification = config.Bind(
                "General", "ShowStartupNotification", read.ShowStartupNotification,
                "Whether to show a notification when the plugin initializes").Value;

            read.WorldSpaceYaw = config.Bind(
                "General", "WorldSpaceYaw", read.WorldSpaceYaw,
                "Yaw mode: true = horizon-locked yaw (default), false = camera-local").Value;

            read.ShowConnectionNotifications = config.Bind(
                "UI", "ShowConnectionNotifications", read.ShowConnectionNotifications,
                "Whether to show notifications when OpenTrack connection is lost or restored").Value;

            read.ShowReticle = config.Bind(
                "UI", "ShowReticle", read.ShowReticle,
                "Whether to show the aim reticle during gameplay (not needed for driving)").Value;

            read.ToggleKey = config.Bind(
                "Keybindings", "ToggleKey", read.ToggleKey,
                "Key to toggle head tracking on/off").Value;

            read.ToggleReticleKey = config.Bind(
                "Keybindings", "ToggleReticleKey", read.ToggleReticleKey,
                "Key to toggle the aim reticle on/off").Value;

            read.CycleTrackingModeKey = config.Bind(
                "Keybindings", "CycleTrackingModeKey", read.CycleTrackingModeKey,
                "Key to cycle tracking mode (normal -> rotation only -> position only -> normal)").Value;

            read.YawModeKey = config.Bind(
                "Keybindings", "YawModeKey", read.YawModeKey,
                "Key to toggle world-locked vs camera-local yaw").Value;

            read.UDPPort = config.Bind(
                "Network", "UDPPort", read.UDPPort,
                new ConfigDescription(
                    "UDP port to listen for OpenTrack data",
                    new AcceptableValueRange<int>(1024, 65535))).Value;

            read.YawSensitivity = config.Bind(
                "Sensitivity", "YawSensitivity", read.YawSensitivity,
                new ConfigDescription(
                    "Multiplier for horizontal head rotation (left/right)",
                    new AcceptableValueRange<float>(0.1f, 3.0f))).Value;

            read.PitchSensitivity = config.Bind(
                "Sensitivity", "PitchSensitivity", read.PitchSensitivity,
                new ConfigDescription(
                    "Multiplier for vertical head rotation (up/down)",
                    new AcceptableValueRange<float>(0.1f, 3.0f))).Value;

            read.RollSensitivity = config.Bind(
                "Sensitivity", "RollSensitivity", read.RollSensitivity,
                new ConfigDescription(
                    "Multiplier for head tilt (ear to shoulder)",
                    new AcceptableValueRange<float>(0.0f, 3.0f))).Value;

            read.LocalSmoothing = config.Bind(
                "Smoothing", "LocalSmoothing", read.LocalSmoothing,
                new ConfigDescription(
                    "Smoothing applied when the tracker runs on this machine (loopback). 0 = no smoothing, 1 = heavy.",
                    new AcceptableValueRange<float>(0f, 1f))).Value;

            read.RemoteSmoothing = config.Bind(
                "Smoothing", "RemoteSmoothing", read.RemoteSmoothing,
                new ConfigDescription(
                    "Smoothing applied when the tracker is a remote device on the network. 0 = no smoothing, 1 = heavy.",
                    new AcceptableValueRange<float>(0f, 1f))).Value;

            read.PositionEnabled = config.Bind(
                "Position", "PositionEnabled", read.PositionEnabled,
                "Enable positional tracking (lean in/out/side-to-side)").Value;

            read.PositionSensitivityX = config.Bind(
                "Position", "PositionSensitivityX", read.PositionSensitivityX,
                new ConfigDescription(
                    "Multiplier for lateral (left/right) position",
                    new AcceptableValueRange<float>(0f, 5.0f))).Value;

            read.PositionSensitivityY = config.Bind(
                "Position", "PositionSensitivityY", read.PositionSensitivityY,
                new ConfigDescription(
                    "Multiplier for vertical (up/down) position",
                    new AcceptableValueRange<float>(0f, 5.0f))).Value;

            read.PositionSensitivityZ = config.Bind(
                "Position", "PositionSensitivityZ", read.PositionSensitivityZ,
                new ConfigDescription(
                    "Multiplier for depth (forward/back) position",
                    new AcceptableValueRange<float>(0f, 5.0f))).Value;

            read.PositionLimitX = config.Bind(
                "Position", "PositionLimitX", read.PositionLimitX,
                new ConfigDescription(
                    "Maximum lateral displacement in meters",
                    new AcceptableValueRange<float>(0.01f, 0.5f))).Value;

            read.PositionLimitY = config.Bind(
                "Position", "PositionLimitY", read.PositionLimitY,
                new ConfigDescription(
                    "Maximum vertical displacement in meters",
                    new AcceptableValueRange<float>(0.01f, 0.5f))).Value;

            read.PositionLimitZ = config.Bind(
                "Position", "PositionLimitZ", read.PositionLimitZ,
                new ConfigDescription(
                    "Maximum forward displacement in meters",
                    new AcceptableValueRange<float>(0.01f, 0.5f))).Value;

            read.PositionLimitZBack = config.Bind(
                "Position", "PositionLimitZBack", read.PositionLimitZBack,
                new ConfigDescription(
                    "Maximum backward displacement in meters",
                    new AcceptableValueRange<float>(0.01f, 0.5f))).Value;

            read.TrackerPivotForward = config.Bind(
                "Position", "TrackerPivotForward", read.TrackerPivotForward,
                new ConfigDescription(
                    "Distance from pivot point to tracker face point. " +
                    "Compensates lateral arc from head yaw in position data.",
                    new AcceptableValueRange<float>(0f, 0.20f))).Value;

            return read;
        }
    }
}
