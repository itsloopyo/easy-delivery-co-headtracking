using System;
using CameraUnlock.Core.Unity.Extensions;
using EasyDeliveryCoHeadTracking.Config;
using UnityEngine;

namespace EasyDeliveryCoHeadTracking.Core
{
    public class InputHandler
    {
        private readonly ConfigManager _config;

        public event Action OnTogglePressed;
        public event Action OnRecenterPressed;
        public event Action OnToggleReticlePressed;
        public event Action OnCycleTrackingModePressed;
        public event Action OnToggleYawModePressed;

        public KeyCode ToggleKey => _config.ToggleKey.Value;
        public KeyCode RecenterKey => _config.RecenterKey.Value;
        public KeyCode ToggleReticleKey => _config.ToggleReticleKey.Value;
        public KeyCode CycleTrackingModeKey => _config.CycleTrackingModeKey.Value;
        public KeyCode YawModeKey => _config.YawModeKey.Value;

        public InputHandler(ConfigManager config)
        {
            _config = config;
        }

        public void CheckInput()
        {
            // Common case: nothing pressed this frame. Skip the 5 GetKeyDown probes and
            // 5 ConfigEntry.Value reads. Holding keys without a fresh down-edge also skips,
            // matching Dispatch's GetKeyDown semantics.
            if (!Input.anyKeyDown)
                return;

            Dispatch(_config.ToggleKey.Value, ChordHotkeys.ToggleLetter, OnTogglePressed);
            Dispatch(_config.RecenterKey.Value, ChordHotkeys.RecenterLetter, OnRecenterPressed);
            Dispatch(_config.ToggleReticleKey.Value, ChordHotkeys.FifthToggleLetter, OnToggleReticlePressed);
            Dispatch(_config.CycleTrackingModeKey.Value, ChordHotkeys.PositionLetter, OnCycleTrackingModePressed);
            Dispatch(_config.YawModeKey.Value, ChordHotkeys.FourthToggleLetter, OnToggleYawModePressed);
        }

        private static void Dispatch(KeyCode primary, KeyCode chordLetter, Action handler)
        {
            if (ChordHotkeys.IsActionPressed(primary, chordLetter))
                handler?.Invoke();
        }
    }
}
