# Changelog

## [Unreleased]

### Changed

- Settings move to `BepInEx\config\CameraUnlock.ini`. Earlier versions of the mod kept these settings in `com.cameraunlock.easydeliveryco.headtracking.cfg`, in the same folder. The first time this version starts and finds no `CameraUnlock.ini`, it reads your settings from `com.cameraunlock.easydeliveryco.headtracking.cfg` and writes them into `CameraUnlock.ini`. It never changes `com.cameraunlock.easydeliveryco.headtracking.cfg`, and does not read it again while `CameraUnlock.ini` exists.
- A setting that the defaults the README shows set to `default` is written as `default` when the value imported for it equals its default at that start, which is the value `Defaults.ini` gives it, or the built-in value where `Defaults.ini` gives none. It then follows `Defaults.ini`. Every other setting is written with the value imported for it.
- `RotationEnabled` and `PositionEnabled` are one setting here, the tracking mode, so both are written as `default` or neither is.
- Comments, and keys the mod never read, are not carried over. Nor are these, where your old file had them:
  - A sensitivity, scale, deadzone, response curve or axis inversion you changed from its default. Set these in your tracker instead.
  - Reticle settings, and a key that toggled the reticle.
- An older version of the mod reads `com.cameraunlock.easydeliveryco.headtracking.cfg` and never reads `CameraUnlock.ini`, so a setting you change after updating is not in `com.cameraunlock.easydeliveryco.headtracking.cfg`.
- Deleting only `CameraUnlock.ini` makes the next start read `com.cameraunlock.easydeliveryco.headtracking.cfg` again. To go back to the defaults, replace everything in `CameraUnlock.ini` with the defaults the README shows. Every setting they set to `default` then follows `Defaults.ini`.
- BepInEx's ConfigurationManager no longer lists these settings. Edit `BepInEx\config\CameraUnlock.ini` with any text editor.
- Hotkeys are written as key names, and each hotkey lists every key that triggers it, the Ctrl+Shift chord included: `ToggleKey=End, Ctrl+Shift+Y`.
- A hotkey bound to a plain key no longer fires while Ctrl and Shift are both held, so Ctrl+Shift with that key reaches only a binding that names the chord.
- On Linux and macOS without Wine or Proton, this version reads its settings and saves none: it creates no `CameraUnlock.ini`, reads your settings from `com.cameraunlock.easydeliveryco.headtracking.cfg` again at every start while there is no `CameraUnlock.ini`, and a change made in game lasts until the game closes.
- The tracking mode (`Page Up`) and the yaw mode (`Page Down`) you pick are saved to `CameraUnlock.ini` and are what the next start begins with. Earlier versions started every session from the file's settings. `End` still changes the current session only.
- `TrackerPivotForward` in a new `CameraUnlock.ini` is `default`, whose built-in value is 0.0, where earlier versions started at 0.08. A setting imported from `com.cameraunlock.easydeliveryco.headtracking.cfg` keeps the value it held there.

### Added

- A setting set to `default` in `CameraUnlock.ini` takes its value from `Defaults.ini`, which every head tracking mod that keeps its settings in `CameraUnlock.ini` reads. Head tracking mods that keep their settings in another file do not read it, and neither do earlier versions of this mod. Writing a value in place of `default` changes that setting for this game only. When the mod saves a setting that a hotkey changed in game, it writes the new value in place of `default`, so that setting no longer follows `Defaults.ini` in this game until you set it to `default` again.
- `Defaults.ini` is `%AppData%\CameraUnlock\Defaults.ini` on Windows; `$XDG_CONFIG_HOME/CameraUnlock/Defaults.ini` on Linux, or `~/.config/CameraUnlock/Defaults.ini` where `XDG_CONFIG_HOME` is not set, under Wine and Proton too; and `~/Library/Application Support/CameraUnlock/Defaults.ini` on macOS. The mod's log, where it writes one, names the file it read.
- When the mod starts and finds no `Defaults.ini`, it creates one holding the built-in values, unless Windows runs the game as a packaged app, or the game runs on Linux or macOS without Wine or Proton. The mod never changes `Defaults.ini` after that.

### Removed

- The aim dot, which earlier versions drew only with `ShowReticle` on or after `Insert` turned it on, with the key that toggled it (`Insert`, `Ctrl+Shift+U`) and its settings (`ShowReticle`, `ToggleReticleKey`).
- The sensitivity, scale, deadzone, response curve and axis inversion settings (`YawSensitivity`, `PitchSensitivity`, `RollSensitivity`, `PositionSensitivityX`, `PositionSensitivityY`, `PositionSensitivityZ`). Set these in your tracker app instead.
- With these settings at their shipped defaults the camera moves as it did before.

## [0.2.0] - 2026-08-20

### Fixed

- give the forward lean its own travel budget again
- drop mod-side recentring and always log tracker connections
- The `OpenTrack connection established` log line no longer depends on
  `ShowConnectionNotifications`. Turning the on-screen notification off used to
  also remove the only evidence in the log that tracker packets had arrived.

### Changed

- Removed mod-side recentring. The `Home` key, the `Ctrl+Shift+T` chord and the
  `Keybindings/RecenterKey` config entry are gone, along with the handling of a
  CENTER press sent by the tracker app. The tracker owns the centre now: centre
  yourself in OpenTrack or your phone app and the mod applies that pose as-is.
  Keeping a second centre in the mod meant the two drifted apart and there was
  no way to tell which side was wrong.

## [0.1.3] - 2026-08-18

### Fixed

- follow core's per-connection smoothing split
- match stub member kinds to the shipped Unity assemblies
- compile the uGUI stubs into UnityEngine.UI, not UnityEngine

### Changed

- Smoothing is now two settings instead of one: `Smoothing/LocalSmoothing`
  (default 0.0) applies when the tracker runs on this machine,
  `Smoothing/RemoteSmoothing` (default 0.15) applies when the tracker is a
  remote device on the network. The value is selected per connection from the
  packet source address and re-evaluated whenever the connection changes.
- Removed `Smoothing/Smoothing` and `Position/PositionSmoothing`. Both new
  settings cover rotation and position, so there is no separate position
  smoothing key.
- Removed the hidden 0.15 baseline smoothing floor. Local users now get
  zero-latency tracking by default instead of a silently enforced minimum.

## [0.1.2] - 2026-08-03

### Added

- recenter on remote request from tracker app

### Fixed

- show full control set in pixi install via shared -Controls

## [0.1.1] - 2026-06-07

### Added

- add HeadTrackingSession and expand C++ core with RE Engine, Unreal, and tracking-session modules
- aim projection, reframework/unreal hooks, input/logging hardening, games
- add Mass Effect Legendary Edition to games catalog
- expand games catalog, fix unicode games.json read, stage launcher manifest
- add Pacific Drive to games catalog
- add Homeworld: Remastered Collection to games catalog
- add manifest-mode installer validator and ASI loader subdir support
- authenticate GitHub API requests via env token when present
- add R.E.P.O. detection data

### Fixed

- fail fast in ASI dev-deploy when the game is running
- restore il2cpp camera position by undoing applied local delta
- set SO_REUSEADDR so the receiver reclaims its port on relaunch
- disable pixi run-install in CI, align UnityStubs with real Unity shapes

### Other

- powershell: skip cameraunlock-core remote refresh in CI
- scripts: add UE4SS install template, fix delayed expansion in ASI body, expand games registry
- protocol: reject finite-but-out-of-float-range packet values
- data: add Subnautica 2 to games registry
- detection: add installer-registry game path lookup (Black & White GameDir)
- protocol: reorder tracking data member in udp_receiver
- data: fix Subnautica 2 Steam app id (3367150 -> 1962700)
- data: add Ni no Kuni Remastered and Yakuza 0; switch find-game output to UTF-8
- detection: add Xbox/GDK build support for Subnautica 2 (and any future GDK title)
- find-game: escape `&` in GAME_DISPLAY_NAME so echo doesn't split
- templates: add uninstall.ps1; data: add Deus Ex Mankind Divided
- powershell: add NightlyRelease module for Patreon-gated nightly builds
- protocol: disable SIO_UDP_CONNRESET and add one-shot receiver diagnostics; powershell: write nightly manifest.json without UTF-8 BOM; data: add Mixtape
- powershell: stop redirecting git stderr in Update-CameraUnlockCoreToRemoteTip
- powershell: publish dev builds as GitHub pre-releases
- protocol: disable SIO_UDP_CONNRESET and add one-shot receiver diagnostics
- data: add Mixtape
- powershell: stop redirecting git stderr in Update-CameraUnlockCoreToRemoteTip
- powershell: run gh under Continue so its stderr doesn't abort the dev-release publish
- reframework: strip VR runtime DLLs on install for flatscreen mode
- reframework: cache GetValue method and avoid per-call heap in ArrayGetValue; data: add BioShock Infinite
- uninstall: remove reframework_revision.txt marker dropped at game root
- install: render MOD_CONTROLS multi-line via percent expansion
- Add YAPYAP to games.json
- powershell: write state file BOM-less so Lopari JSON parser accepts it
- powershell: stop redirecting git stderr in Invoke-VersionCommit

## [0.1.0] - 2026-05-16

First release.
