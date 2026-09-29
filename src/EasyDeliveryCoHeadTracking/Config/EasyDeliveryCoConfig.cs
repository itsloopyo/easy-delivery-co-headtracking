using CameraUnlock.Core.Config;

namespace EasyDeliveryCoHeadTracking.Config
{
    /// <summary>
    /// Everything the mod reads from BepInEx\config\CameraUnlock.ini. Unity-free, so the test
    /// project compiles it and holds the committed file to it.
    /// </summary>
    public sealed class EasyDeliveryCoConfig : HeadTrackingConfigData
    {
        /// <summary>The game's name as data/games.json spells it.</summary>
        public const string DisplayName = "Easy Delivery Co";

        /// <summary>
        /// The neck pivot distance in metres every published build shipped. Not a setting: the
        /// tracker is authoritative over the pivot, and this keeps the lean compensation players had.
        /// </summary>
        public const float TrackerPivotForwardMetres = 0.08f;

        public bool ShowStartupNotification { get; set; } = true;

        public bool ShowConnectionNotifications { get; set; } = true;

        public static ConfigTable<EasyDeliveryCoConfig> Table()
        {
            return HeadTrackingConfigTable.Create<EasyDeliveryCoConfig>(
                    ConfigConcepts.UdpPort,
                    ConfigConcepts.EnableOnStartup,
                    ConfigConcepts.WorldSpaceYaw,
                    ConfigConcepts.RotationEnabled,
                    ConfigConcepts.LocalSmoothing,
                    ConfigConcepts.RemoteSmoothing,
                    ConfigConcepts.PositionEnabled,
                    ConfigConcepts.PositionLimitX,
                    ConfigConcepts.PositionLimitY,
                    ConfigConcepts.PositionLimitYDown,
                    ConfigConcepts.PositionLimitZ,
                    ConfigConcepts.PositionLimitZBack,
                    ConfigConcepts.CollisionEnabled,
                    ConfigConcepts.CollisionMargin,
                    ConfigConcepts.CollisionReleaseSmoothing,
                    ConfigConcepts.ToggleKey,
                    ConfigConcepts.CycleTrackingModeKey,
                    ConfigConcepts.YawModeKey)
                .Select(ConfigConcepts.WorldSpaceYaw).Writable()
                .Select(ConfigConcepts.RotationEnabled).Writable()
                .Select(ConfigConcepts.PositionEnabled).Writable()
                .Select(ConfigConcepts.CollisionMargin)
                .Comment("How far, in metres, the view is held off a wall when you lean into it.\n" +
                         "The mod raises it where the corners of the camera's near clip plane need more room.")
                .Local("Notifications", "ShowStartupNotification", c => c.ShowStartupNotification,
                    (c, v) => c.ShowStartupNotification = v, new BoolCodec(),
                    "true: show whether head tracking is on, and its hotkeys, when the game starts.")
                .Local("Notifications", "ShowConnectionNotifications", c => c.ShowConnectionNotifications,
                    (c, v) => c.ShowConnectionNotifications = v, new BoolCodec(),
                    "true: show a message when tracker data starts or stops arriving.");
        }
    }
}
