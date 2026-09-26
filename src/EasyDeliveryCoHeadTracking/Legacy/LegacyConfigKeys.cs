using CameraUnlock.Core.Config;

namespace EasyDeliveryCoHeadTracking.Legacy
{
    /// <summary>Every section and key <see cref="LegacyConfigReader"/> reads. Frozen with it.</summary>
    internal static class LegacyConfigKeys
    {
        public static LegacyKey[] All()
        {
            return new[]
            {
                new LegacyKey("General", "EnabledOnStartup"),
                new LegacyKey("General", "ShowStartupNotification"),
                new LegacyKey("General", "WorldSpaceYaw"),
                new LegacyKey("UI", "ShowConnectionNotifications"),
                new LegacyKey("UI", "ShowReticle"),
                new LegacyKey("Keybindings", "ToggleKey"),
                new LegacyKey("Keybindings", "ToggleReticleKey"),
                new LegacyKey("Keybindings", "CycleTrackingModeKey"),
                new LegacyKey("Keybindings", "YawModeKey"),
                new LegacyKey("Network", "UDPPort"),
                new LegacyKey("Sensitivity", "YawSensitivity"),
                new LegacyKey("Sensitivity", "PitchSensitivity"),
                new LegacyKey("Sensitivity", "RollSensitivity"),
                new LegacyKey("Smoothing", "LocalSmoothing"),
                new LegacyKey("Smoothing", "RemoteSmoothing"),
                new LegacyKey("Position", "PositionEnabled"),
                new LegacyKey("Position", "PositionSensitivityX"),
                new LegacyKey("Position", "PositionSensitivityY"),
                new LegacyKey("Position", "PositionSensitivityZ"),
                new LegacyKey("Position", "PositionLimitX"),
                new LegacyKey("Position", "PositionLimitY"),
                new LegacyKey("Position", "PositionLimitZ"),
                new LegacyKey("Position", "PositionLimitZBack"),
                new LegacyKey("Position", "TrackerPivotForward"),
            };
        }
    }
}
