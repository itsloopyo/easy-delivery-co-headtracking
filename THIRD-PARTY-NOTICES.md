# Third-Party Notices

This mod uses or bundles the following third-party software. Each component remains under its own license.

## BepInEx

- **Version:** 5.4.23.5
- **License:** LGPL-2.1
- **Upstream:** https://github.com/BepInEx/BepInEx
- **Usage:** Unity plugin loader. `install.cmd` extracts the bundled archive into the game folder if BepInEx is not already present.
- **Bundled:** yes. Bundled in release ZIP as fallback; fetched latest within range at install time.

---

## HarmonyX

- **Version:** shipped with BepInEx 5.4.23.5 (`0Harmony.dll`)
- **License:** MIT
- **Upstream:** https://github.com/BepInEx/HarmonyX
- **Usage:** Runtime method patching used by the shared CameraUnlock core for camera state save/restore around game updates.
- **Bundled:** no. Loaded at runtime from the user's BepInEx install.

---

## OpenTrack

- **Version:** protocol only (no code bundled)
- **License:** ISC
- **Upstream:** https://github.com/opentrack/opentrack
- **Usage:** Head pose source. The mod listens for OpenTrack's UDP packet format on port 4242. No OpenTrack code is bundled.
- **Bundled:** no.

---

## CameraUnlock.Core / CameraUnlock.Core.Unity

- **Version:** git submodule (see `cameraunlock-core/`)
- **License:** MIT
- **Upstream:** https://github.com/itsloopyo/cameraunlock-core
- **Usage:** Shared head tracking processing pipeline (UDP receiver, interpolator, processor, view matrix modifier). Compiled into the mod assembly.
- **Bundled:** yes.

---
