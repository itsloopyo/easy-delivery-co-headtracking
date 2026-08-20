# Easy Delivery Co Head Tracking

An unofficial BepInEx mod that adds OpenTrack head tracking to Easy Delivery Co, so you can look around naturally in game just by moving your heaad.

![Mod GIF](https://raw.githubusercontent.com/itsloopyo/easy-delivery-co-headtracking/main/assets/readme-clip.gif)

## Features

- **Decoupled look and aim** - head tracking moves the camera; steering stays on your mouse/controller
- **6DOF positional tracking** - lean and peek with head position

## Requirements

- [Easy Delivery Co](https://store.steampowered.com/app/3293010/Easy_Delivery_Co/) (Steam or Xbox/Game Pass)
- [OpenTrack](https://github.com/opentrack/opentrack) or a compatible head tracking app (smartphone, webcam, or dedicated hardware)
- Windows 10/11

## Installation

1. Download the latest installer ZIP from the [Releases page](https://github.com/itsloopyo/easy-delivery-co-headtracking/releases)
2. Extract the ZIP anywhere
3. Double-click `install.cmd`
4. Configure OpenTrack to output UDP to `127.0.0.1:4242`
5. Launch the game

The installer auto-detects your game via Steam registry lookup. If it can't find your install:
- Set the `EASY_DELIVERY_CO_PATH` environment variable to your game folder, or
- Run from a command prompt: `install.cmd "D:\Games\Easy Delivery Co"`

### Manual Installation

For users who prefer to place files by hand, or who download the Nexus ZIP (which contains only the mod DLLs):

1. Install [BepInEx 5.4.23.5 x64](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5) to your game folder
2. Run the game once to let BepInEx initialize
3. Copy these DLLs to `BepInEx/plugins/`:
   - `EasyDeliveryCoHeadTracking.dll`
   - `CameraUnlock.Core.dll`
   - `CameraUnlock.Core.Unity.dll`

## Setting Up OpenTrack

1. Download and install [OpenTrack](https://github.com/opentrack/opentrack/releases)
2. Configure your tracker as input
3. Set output to **UDP over network**
4. Host: `127.0.0.1`, Port: `4242`
5. Start tracking before launching the game

### Webcam Setup

No special hardware needed - OpenTrack's built-in **neuralnet tracker** uses any webcam for 6DOF face tracking.

1. In OpenTrack, set the input to **neuralnet tracker**
2. Select your webcam in the tracker settings
3. Set output to **UDP over network** (`127.0.0.1:4242`)
4. Start tracking before launching the game
5. Centre your view with OpenTrack's own Center hotkey whenever you need a fresh neutral pose - the mod applies whatever the tracker sends and keeps no centre of its own

### Phone App Setup

This mod smooths network jitter with `RemoteSmoothing` (default 0.15), so you can send directly from your phone on port 4242 without needing OpenTrack on PC.

1. Install an OpenTrack-compatible head tracking app (SmoothTrack, Head Tracker, etc.)
2. Configure it to send to your PC's IP on port 4242 (run `ipconfig` to find it)
3. Set the protocol to OpenTrack/UDP

**With OpenTrack (optional):** If you want curve mapping or visual preview, route through OpenTrack. Set OpenTrack's input to "UDP over network" on a different port (e.g. 5252), point your phone app at that port, and set OpenTrack's output to `127.0.0.1:4242`. Make sure your firewall allows incoming UDP on the input port.

## Controls

Two equivalent binding sets - use whichever your keyboard has:

| Action                       | Nav-cluster | Chord           |
|------------------------------|-------------|-----------------|
| Toggle tracking              | `End`       | `Ctrl+Shift+Y`  |
| Cycle tracking mode          | `Page Up`   | `Ctrl+Shift+G`  |
| Toggle yaw mode              | `Page Down` | `Ctrl+Shift+H`  |
| Toggle aim reticle           | `Insert`    | `Ctrl+Shift+U`  |

`Page Up` / `Ctrl+Shift+G` cycles tracking mode:

1. Normal head-tracked gameplay (rotation + position)
2. Rotation only (positional tracking disabled)
3. Position only (rotational tracking disabled)
4. Back to normal

The `Ctrl+Shift+<letter>` chords are provided for keyboards without a navigation cluster (laptops, 60% / TKL layouts). Both bindings fire the same action.

## Configuration

The mod creates a config file at `BepInEx/config/com.cameraunlock.easydeliveryco.headtracking.cfg` on first run. Edit it to customize:

A comment has to sit on its own line. BepInEx splits each line at the first `=`
and takes everything after it as the value, so a trailing `# note` becomes part
of the value, the conversion fails, and the entry silently keeps its default -
the only trace is a line in `BepInEx/LogOutput.log`. Put explanations above the
key, never after it.

```ini
[General]
# Start with tracking enabled
EnabledOnStartup = true
# Show controls on startup
ShowStartupNotification = true
# true = horizon-locked yaw, false = camera-local
WorldSpaceYaw = true

[UI]
ShowConnectionNotifications = true
# Aim reticle (not needed for driving)
ShowReticle = false

[Keybindings]
ToggleKey = End
ToggleReticleKey = Insert
# Cycle: full -> rotation only -> position only
CycleTrackingModeKey = PageUp
# Toggle world-locked vs camera-local yaw
YawModeKey = PageDown

[Network]
# Must match OpenTrack output port
UDPPort = 4242

[Sensitivity]
# Horizontal rotation (0.1-3.0)
YawSensitivity = 1.0
# Vertical rotation (0.1-3.0)
PitchSensitivity = 1.0
# Head tilt (0.0-3.0)
RollSensitivity = 1.0

[Smoothing]
# Tracker on this machine (loopback). 0 = none, 1 = heavy
LocalSmoothing = 0.0
# Tracker on a remote network device. 0 = none, 1 = heavy
RemoteSmoothing = 0.15

[Position]
# Enable lean/positional tracking
PositionEnabled = true
# Lateral sensitivity (0.0-5.0)
PositionSensitivityX = 1.0
# Vertical sensitivity (0.0-5.0)
PositionSensitivityY = 1.0
# Depth sensitivity (0.0-5.0)
PositionSensitivityZ = 1.0
# Max lateral offset in meters
PositionLimitX = 0.30
# Max vertical offset in meters
PositionLimitY = 0.20
# Max forward offset in meters
PositionLimitZ = 0.40
# Max backward offset in meters
PositionLimitZBack = 0.10
# Neck-to-face distance, compensates yaw orbit
TrackerPivotForward = 0.08
```

Smoothing is picked per connection from the tracker's source address: a tracker
running on this machine (loopback) uses `LocalSmoothing`, a tracker on another
device over the network uses `RemoteSmoothing`. Both cover rotation and
position, so there is no separate position smoothing setting.

## Troubleshooting

**Mod not loading:**
- Ensure `winhttp.dll` exists in the game folder (installed by BepInEx)
- Make sure all 3 DLLs are in `BepInEx/plugins/`
- Check `BepInEx/LogOutput.log` for errors
- On Xbox Game Pass: the installer checks `C:\XboxGames\Easy Delivery Co\Content\` automatically; otherwise set `EASY_DELIVERY_CO_PATH` to your game folder

**No tracking response:**
- Look for `OpenTrack connection established` in `BepInEx/LogOutput.log`. If it is
  absent, no tracker packet ever reached the mod and the problem is upstream of
  the game. BepInEx rewrites that file on every launch, so it only ever holds the
  most recent session - send it when reporting a problem.
- Verify OpenTrack is running and outputting data
- Check the UDP port matches (default 4242)
- Press `End` to enable tracking
- Check that your firewall isn't blocking UDP port 4242

**A config edit had no effect:**
- Make sure nothing follows the value on the line. A trailing `# comment` is read as part of the value, the entry falls back to its default, and the game gives no sign of it. `BepInEx/LogOutput.log` records the failed conversion.

**Jittery / unstable tracking:**
- Increase `RemoteSmoothing` (phone/network tracker) or `LocalSmoothing` (tracker on this PC) in the config (try 0.2-0.4), with nothing after the value on the line
- For wireless phone trackers, prefer 5GHz Wi-Fi or USB tethering
- Lower the tracker's send rate if it's saturating the network

**Wrong rotation axis:**
- Pitch inverted: invert pitch in OpenTrack's output mapping. This mod has no invert settings; the axis corrections it needs are applied internally and are not configurable. `PitchSensitivity` is a magnitude only, clamped to `0.1`-`3.0`, so a negative value does not flip the axis, it is clamped to `0.1` and leaves pitch nearly dead
- Yaw feels wrong at extreme up/down angles: toggle between world-locked and camera-local yaw with `Page Down`. World-locked (default) is horizon-stable; camera-local follows the camera's current up-axis

## Updating

Download the new release and run `install.cmd` again. Your config is preserved.

## Uninstalling

Run `uninstall.cmd` from the release folder. This removes the mod DLLs. BepInEx is only removed if the installer put it there. Use `uninstall.cmd /force` to remove BepInEx anyway.

## Building from Source

### Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) (any recent version)
- [pixi](https://pixi.sh) task runner
- Easy Delivery Co installed (for Unity/BepInEx DLL references)

### Build

```bash
git clone --recurse-submodules https://github.com/itsloopyo/easy-delivery-co-headtracking.git
cd easy-delivery-co-headtracking

# Build and install to game
pixi run install

# Build only
pixi run build

# Package for release
pixi run package
```

### Available Tasks

| Task | Description |
|------|-------------|
| `pixi run build` | Build the mod (Release configuration) |
| `pixi run install` | Build and install to game directory |
| `pixi run uninstall` | Remove the mod from the game |
| `pixi run uninstall -- --force` | Remove the mod and BepInEx |
| `pixi run package` | Create release ZIP |
| `pixi run clean` | Clean build artifacts |
| `pixi run release` | Version bump, build, tag, and push |

## Community & Support

- Discord: [Loop's Head Tracking Hangout](https://discord.com/invite/dxyZdyFNT9) - setup help, bug reports, and new-release announcements
- [Lopari](https://lopari.app) - free Windows launcher with one-click install and launch for the released head-tracking mods
- [Headcam](https://headcam.app) - free app that turns your iPhone or Android phone into the head tracker

## License

MIT License - see [LICENSE](LICENSE) for details.

## Credits

- [Sam C](https://samcameron.notion.site/) / [Oro Interactive](https://www.orointeractive.com/) - [Easy Delivery Co](https://store.steampowered.com/app/3293010/Easy_Delivery_Co/)
- [BepInEx](https://github.com/BepInEx/BepInEx) - Unity modding framework
- [OpenTrack](https://github.com/opentrack/opentrack) - Head tracking software
- [CameraUnlock](https://github.com/itsloopyo/cameraunlock-core) - Shared head tracking library
