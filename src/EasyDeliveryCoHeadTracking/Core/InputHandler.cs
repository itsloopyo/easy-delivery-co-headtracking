using System;
using EasyDeliveryCoHeadTracking.Config;
using UnityEngine;

namespace EasyDeliveryCoHeadTracking.Core
{
    public class InputHandler
    {
        // Chord letters drawn from the T/Y/U/G/H/J nav cluster. Centralized so the
        // help string and the dispatch table can't drift apart.
        public const KeyCode ToggleChordLetter = KeyCode.Y;
        public const KeyCode RecenterChordLetter = KeyCode.T;
        public const KeyCode ToggleReticleChordLetter = KeyCode.U;
        public const KeyCode CycleTrackingModeChordLetter = KeyCode.G;
        public const KeyCode YawModeChordLetter = KeyCode.H;

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

            Dispatch(_config.ToggleKey.Value, ToggleChordLetter, OnTogglePressed);
            Dispatch(_config.RecenterKey.Value, RecenterChordLetter, OnRecenterPressed);
            Dispatch(_config.ToggleReticleKey.Value, ToggleReticleChordLetter, OnToggleReticlePressed);
            Dispatch(_config.CycleTrackingModeKey.Value, CycleTrackingModeChordLetter, OnCycleTrackingModePressed);
            Dispatch(_config.YawModeKey.Value, YawModeChordLetter, OnToggleYawModePressed);
        }

        private static void Dispatch(KeyCode primary, KeyCode chordLetter, Action handler)
        {
            if (Input.GetKeyDown(primary) || ChordPressed(chordLetter))
                handler?.Invoke();
        }

        private static bool ChordPressed(KeyCode letter)
        {
            if (!Input.GetKeyDown(letter)) return false;
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            return ctrl && shift;
        }
    }
}
