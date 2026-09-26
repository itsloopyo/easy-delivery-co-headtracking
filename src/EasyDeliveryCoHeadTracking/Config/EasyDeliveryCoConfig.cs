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
                    ConfigConcepts.TrackerPivotForward,
                    ConfigConcepts.ToggleKey,
                    ConfigConcepts.CycleTrackingModeKey,
                    ConfigConcepts.YawModeKey)
                .Select(ConfigConcepts.WorldSpaceYaw).Writable()
                .Select(ConfigConcepts.RotationEnabled).Writable()
                .Select(ConfigConcepts.PositionEnabled).Writable()
                .Select(ConfigConcepts.TrackerPivotForward)
                .Comment("Metres from the pivot of your neck forward to the point the tracker follows.\n" +
                         "Used to remove the lean that turning your head adds. 0 turns it off.")
                .Local("Notifications", "ShowStartupNotification", c => c.ShowStartupNotification,
                    (c, v) => c.ShowStartupNotification = v, new BoolCodec(),
                    "true: show whether head tracking is on, and its hotkeys, when the game starts.")
                .Local("Notifications", "ShowConnectionNotifications", c => c.ShowConnectionNotifications,
                    (c, v) => c.ShowConnectionNotifications = v, new BoolCodec(),
                    "true: show a message when tracker data starts or stops arriving.");
        }
    }
}
