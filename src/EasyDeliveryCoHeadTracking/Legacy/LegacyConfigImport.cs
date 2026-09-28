using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
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
    /// CameraUnlock.ini is absent: <see cref="LegacyConfigReader"/> on a ConfigFile of its own over
    /// that file, then the map into <see cref="EasyDeliveryCoConfig"/>.
    /// <para>
    /// Not the plugin's Config: ConfigurationManager lists every entry bound there, and one bound
    /// by the import would sit in its window for the rest of the session doing nothing. A ConfigFile
    /// built as BaseUnityPlugin builds the plugin's reads the file the same way.
    /// </para>
    /// </summary>
    internal static class LegacyConfigImport
    {
        /// <summary>The rotation multiplier every published build shipped on all three axes.</summary>
        public const float ShippedRotationSensitivity = 1.0f;

        /// <summary>The position multiplier every published build shipped on all three axes.</summary>
        public const float ShippedPositionSensitivity = 1.0f;

        /// <param name="plugin">The plugin's metadata, which BaseUnityPlugin hands its own ConfigFile.</param>
        public static LegacyImport<EasyDeliveryCoConfig> For(BepInPlugin plugin)
        {
            return new LegacyImport<EasyDeliveryCoConfig>(
                (input, config) => Run(new ConfigFile(input.Path, false, plugin), input, config), LegacyConfigKeys.All());
        }

        /// <param name="legacyFile">A ConfigFile over the legacy file that nothing has bound to.</param>
        public static ImportResult Run(ConfigFile legacyFile, LegacyImportInput input, EasyDeliveryCoConfig config)
        {
            if (!string.Equals(Path.GetFullPath(input.Path), Path.GetFullPath(legacyFile.ConfigFilePath), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("the owner hands over " + input.Path + ", and the ConfigFile reads "
                                                    + legacyFile.ConfigFilePath);
            }

            bool found;
            LegacyConfig legacy = LegacyConfigReader.Read(legacyFile, out found);
            var dropped = new List<DroppedValue>();
            var poseShaping = new List<PoseShapingValue>();
            var followsDefaultsIni = new LegacyFollowsDefaultsIni();
            Map(legacy, config, dropped, poseShaping, followsDefaultsIni);
            return found
                ? ImportResult.Imported(dropped, poseShaping, followsDefaultsIni.Concepts)
                : ImportResult.Absent(dropped, poseShaping, followsDefaultsIni.Concepts);
        }

        /// <summary>
        /// Every float the reader returns is inside its AcceptableValueRange, which BepInEx clamps
        /// NaN and infinity into, so no value reaches here that normalisation N2 would change.
        /// A row whose legacy value equals what every published build shipped, the frozen
        /// <see cref="LegacyConfig"/> defaults, is left to Defaults.ini.
        /// </summary>
        public static void Map(LegacyConfig legacy, EasyDeliveryCoConfig config, List<DroppedValue> dropped,
            List<PoseShapingValue> poseShaping, LegacyFollowsDefaultsIni followsDefaultsIni)
        {
            var shipped = new LegacyConfig();

            config.EnableOnStartup = legacy.EnabledOnStartup;
            followsDefaultsIni.Setting(ConfigConcepts.EnableOnStartup, legacy.EnabledOnStartup, shipped.EnabledOnStartup);
            config.ShowStartupNotification = legacy.ShowStartupNotification;
            config.WorldSpaceYaw = legacy.WorldSpaceYaw;
            followsDefaultsIni.Setting(ConfigConcepts.WorldSpaceYaw, legacy.WorldSpaceYaw, shipped.WorldSpaceYaw);
            config.ShowConnectionNotifications = legacy.ShowConnectionNotifications;
            config.UdpPort = legacy.UDPPort;
            followsDefaultsIni.Setting(ConfigConcepts.UdpPort, legacy.UDPPort, shipped.UDPPort);

            config.ToggleKeyName = HotkeyList(legacy.ToggleKey, KeyCode.Y, "ToggleKey", dropped);
            followsDefaultsIni.Setting(ConfigConcepts.ToggleKey, legacy.ToggleKey, shipped.ToggleKey);
            config.CycleTrackingModeKeyName = HotkeyList(legacy.CycleTrackingModeKey, KeyCode.G, "CycleTrackingModeKey", dropped);
            followsDefaultsIni.Setting(ConfigConcepts.CycleTrackingModeKey, legacy.CycleTrackingModeKey, shipped.CycleTrackingModeKey);
            config.YawModeKeyName = HotkeyList(legacy.YawModeKey, KeyCode.H, "YawModeKey", dropped);
            followsDefaultsIni.Setting(ConfigConcepts.YawModeKey, legacy.YawModeKey, shipped.YawModeKey);

            // The reticle toggle is gone for everyone who had it bound, and the mod draws no aim
            // dot. ShowReticle=false, as it shipped, is what the mod does now, so only a player who
            // turned the dot on loses a choice.
            if (legacy.ToggleReticleKey != KeyCode.None)
            {
                dropped.Add(new DroppedValue(DropRule.Reticle, "Keybindings", "ToggleReticleKey", legacy.ToggleReticleKey.ToString()));
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
            followsDefaultsIni.TrackingMode(legacy.PositionEnabled, shipped.PositionEnabled);

            config.LocalSmoothing = legacy.LocalSmoothing;
            followsDefaultsIni.Setting(ConfigConcepts.LocalSmoothing, legacy.LocalSmoothing, shipped.LocalSmoothing);
            config.RemoteSmoothing = legacy.RemoteSmoothing;
            followsDefaultsIni.Setting(ConfigConcepts.RemoteSmoothing, legacy.RemoteSmoothing, shipped.RemoteSmoothing);
            PositionSettings p = config.Position;
            // v0.2.0 built its limits with PositionSettings.Symmetric, so PositionLimitY was the
            // downward limit too.
            config.Position = new PositionSettings(
                p.SensitivityX, p.SensitivityY, p.SensitivityZ,
                legacy.PositionLimitX, legacy.PositionLimitY, legacy.PositionLimitY, legacy.PositionLimitZ, legacy.PositionLimitZBack,
                legacy.LocalSmoothing, legacy.RemoteSmoothing,
                p.InvertX, p.InvertY, p.InvertZ);
            followsDefaultsIni.Setting(ConfigConcepts.PositionLimitX, legacy.PositionLimitX, shipped.PositionLimitX);
            followsDefaultsIni.Setting(ConfigConcepts.PositionLimitY, legacy.PositionLimitY, shipped.PositionLimitY);
            followsDefaultsIni.Setting(ConfigConcepts.PositionLimitYDown, legacy.PositionLimitY, shipped.PositionLimitY);
            followsDefaultsIni.Setting(ConfigConcepts.PositionLimitZ, legacy.PositionLimitZ, shipped.PositionLimitZ);
            followsDefaultsIni.Setting(ConfigConcepts.PositionLimitZBack, legacy.PositionLimitZBack, shipped.PositionLimitZBack);

            LegacyTrackerPivot.Record(legacy.TrackerPivotForward, shipped.TrackerPivotForward, "Position", "TrackerPivotForward", dropped);
        }

        /// <summary>
        /// The keys v0.2.0 fired an action on: the configured key, unless it was None or a Ctrl,
        /// Shift or Alt key alone (N3, dropped and logged), and the Ctrl+Shift chord that
        /// InputHandler checked beside it. A key code Unity names no key for (a number in the .cfg,
        /// which BepInEx's enum parse accepts) is unbound the same way, recorded as
        /// KeyCodeOutOfRange (N1).
        /// </summary>
        public static string HotkeyList(KeyCode primary, KeyCode chordLetter, string key, List<DroppedValue> dropped)
        {
            string chord = KeyBindings.Format(new[] { new KeyBinding(KeyModifiers.Ctrl | KeyModifiers.Shift, (int)chordLetter) });
            string primaryText = LegacyNormalisations.KeyCodeToBindings((int)primary, "Keybindings", key, dropped);
            return primaryText.Length == 0 ? chord : primaryText + ", " + chord;
        }
    }
}
