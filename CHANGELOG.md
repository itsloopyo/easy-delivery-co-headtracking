# Changelog

## [0.2.0] - 2026-08-20

### Fixed

- give the forward lean its own travel budget again
- drop mod-side recentring and always log tracker connections

## [0.1.3] - 2026-08-18

### Fixed

- follow core's per-connection smoothing split
- match stub member kinds to the shipped Unity assemblies
- compile the uGUI stubs into UnityEngine.UI, not UnityEngine

## [Unreleased]

### Fixed

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
