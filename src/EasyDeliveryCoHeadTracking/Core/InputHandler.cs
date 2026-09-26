using System;
using CameraUnlock.Core.Unity.Extensions;
using EasyDeliveryCoHeadTracking.Config;
using UnityEngine;

namespace EasyDeliveryCoHeadTracking.Core
{
    public class InputHandler
    {
        private readonly ModConfig _config;

        public event Action OnTogglePressed;
        public event Action OnToggleReticlePressed;
        public event Action OnCycleTrackingModePressed;
        public event Action OnToggleYawModePressed;

        public KeyCode ToggleKey => _config.ToggleKey;
        public KeyCode ToggleReticleKey => _config.ToggleReticleKey;
        public KeyCode CycleTrackingModeKey => _config.CycleTrackingModeKey;
        public KeyCode YawModeKey => _config.YawModeKey;

        public InputHandler(ModConfig config)
        {
            _config = config;
        }

        public void CheckInput()
        {
            // Common case: nothing pressed this frame. Skip the 4 GetKeyDown probes and
            // 4 config reads. Holding keys without a fresh down-edge also skips,
            // matching Dispatch's GetKeyDown semantics.
            if (!Input.anyKeyDown)
                return;

            Dispatch(_config.ToggleKey, ChordHotkeys.ToggleLetter, OnTogglePressed);
            Dispatch(_config.ToggleReticleKey, ChordHotkeys.FifthToggleLetter, OnToggleReticlePressed);
            Dispatch(_config.CycleTrackingModeKey, ChordHotkeys.PositionLetter, OnCycleTrackingModePressed);
            Dispatch(_config.YawModeKey, ChordHotkeys.FourthToggleLetter, OnToggleYawModePressed);
        }

        private static void Dispatch(KeyCode primary, KeyCode chordLetter, Action handler)
        {
            if (ChordHotkeys.IsActionPressed(primary, chordLetter))
                handler?.Invoke();
        }
    }
}
