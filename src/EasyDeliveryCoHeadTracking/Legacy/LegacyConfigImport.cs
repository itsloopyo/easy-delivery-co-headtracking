using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx.Configuration;
using CameraUnlock.Core.Config;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.Input;
using EasyDeliveryCoHeadTracking.Config;
using UnityEngine;

namespace EasyDeliveryCoHeadTracking.Legacy
{
    /// <summary>
    /// The import the config owner runs on com.cameraunlock.easydeliveryco.headtracking.cfg while
    /// CameraUnlock.ini is absent: <see cref="LegacyConfigReader"/> on the plugin's own ConfigFile,
    /// then the map into <see cref="EasyDeliveryCoConfig"/>.
    /// </summary>
    internal static class LegacyConfigImport
    {
        /// <summary>The rotation multiplier every published build shipped on all three axes.</summary>
        public const float ShippedRotationSensitivity = 1.0f;

        /// <summary>The position multiplier every published build shipped on all three axes.</summary>
        public const float ShippedPositionSensitivity = 1.0f;

        /// <param name="pluginConfig">The plugin's Config, whose file is the legacy file.</param>
        public static LegacyImport<EasyDeliveryCoConfig> For(ConfigFile pluginConfig)
        {
            return new LegacyImport<EasyDeliveryCoConfig>((input, config) => Run(pluginConfig, input, config), LegacyConfigKeys.All());
        }

        public static ImportResult Run(ConfigFile pluginConfig, LegacyImportInput input, EasyDeliveryCoConfig config)
        {
            if (!string.Equals(Path.GetFullPath(input.Path), Path.GetFullPath(pluginConfig.ConfigFilePath), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("the owner hands over " + input.Path + ", and the plugin's ConfigFile reads "
                                                    + pluginConfig.ConfigFilePath);
            }

            bool found;
            LegacyConfig legacy = LegacyConfigReader.Read(pluginConfig, out found);
            var dropped = new List<DroppedValue>();
            var poseShaping = new List<PoseShapingValue>();
            Map(legacy, config, dropped, poseShaping);
            return found ? ImportResult.Imported(dropped, poseShaping) : ImportResult.Absent(dropped, poseShaping);
        }

        /// <summary>
        /// Every float the reader returns is inside its AcceptableValueRange, which BepInEx clamps
        /// NaN and infinity into, so no value reaches here that normalisation N2 would change.
        /// </summary>
        public static void Map(LegacyConfig legacy, EasyDeliveryCoConfig config, List<DroppedValue> dropped,
            List<PoseShapingValue> poseShaping)
        {
            config.EnableOnStartup = legacy.EnabledOnStartup;
            config.ShowStartupNotification = legacy.ShowStartupNotification;
            config.WorldSpaceYaw = legacy.WorldSpaceYaw;
            config.ShowConnectionNotifications = legacy.ShowConnectionNotifications;
            config.UdpPort = legacy.UDPPort;

            config.ToggleKeyName = HotkeyList(legacy.ToggleKey, KeyCode.Y);
            config.CycleTrackingModeKeyName = HotkeyList(legacy.CycleTrackingModeKey, KeyCode.G);
            config.YawModeKeyName = HotkeyList(legacy.YawModeKey, KeyCode.H);

            // The reticle toggle is gone for everyone who had it bound, and the mod draws no aim
            // dot. ShowReticle=false, as it shipped, is what the mod does now, so only a player who
            // turned the dot on loses a choice.
            if (legacy.ToggleReticleKey != KeyCode.None)
            {
                dropped.Add(new DroppedValue(DropRule.Reticle, "Keybindings", "ToggleReticleKey", KeyText((int)legacy.ToggleReticleKey)));
            }
            if (legacy.ShowReticle)
            {
                dropped.Add(new DroppedValue(DropRule.Reticle, "UI", "ShowReticle", "true"));
            }

            LegacyPoseShaping.Record(legacy.YawSensitivity, ShippedRotationSensitivity, "Sensitivity", "YawSensitivity", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.PitchSensitivity, ShippedRotationSensitivity, "Sensitivity", "PitchSensitivity", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.RollSensitivity, ShippedRotationSensitivity, "Sensitivity", "RollSensitivity", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.PositionSensitivityX, ShippedPositionSensitivity, "Position", "PositionSensitivityX", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.PositionSensitivityY, ShippedPositionSensitivity, "Position", "PositionSensitivityY", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.PositionSensitivityZ, ShippedPositionSensitivity, "Position", "PositionSensitivityZ", poseShaping, dropped);

            // The published builds had one position switch and a three-state cycle that always
            // started from rotation and position, so the switch set the startup mode.
            config.RotationEnabled = true;
            config.PositionEnabled = legacy.PositionEnabled;

            config.LocalSmoothing = legacy.LocalSmoothing;
            config.RemoteSmoothing = legacy.RemoteSmoothing;
            PositionSettings p = config.Position;
            // v0.2.0 built its limits with PositionSettings.Symmetric, so PositionLimitY was the
            // downward limit too.
            config.Position = new PositionSettings(
                p.SensitivityX, p.SensitivityY, p.SensitivityZ,
                legacy.PositionLimitX, legacy.PositionLimitY, legacy.PositionLimitY, legacy.PositionLimitZ, legacy.PositionLimitZBack,
                legacy.LocalSmoothing, legacy.RemoteSmoothing,
                p.InvertX, p.InvertY, p.InvertZ);

            config.TrackerPivotForward = legacy.TrackerPivotForward;
        }

        /// <summary>
        /// The keys v0.2.0 fired an action on: the configured key, unless it was None, and the
        /// Ctrl+Shift chord that InputHandler checked beside it. A key code Unity names no key for
        /// (a number in the .cfg, which BepInEx's enum parse accepts) is written as that number,
        /// which no hotkey list reads, so the owner defers the import and says which line.
        /// </summary>
        public static string HotkeyList(KeyCode primary, KeyCode chordLetter)
        {
            string chord = KeyBindings.Format(new[] { new KeyBinding(KeyModifiers.Ctrl | KeyModifiers.Shift, (int)chordLetter) });
            if (primary == KeyCode.None) return chord;
            return KeyText((int)primary) + ", " + chord;
        }

        private static string KeyText(int unityKeyCode)
        {
            try
            {
                return KeyBindings.Format(new[] { new KeyBinding(KeyModifiers.None, unityKeyCode) });
            }
            catch (ArgumentException)
            {
                return unityKeyCode.ToString(CultureInfo.InvariantCulture);
            }
        }
    }
}
