# Blackout UI and Accessibility Standard

Owner: Track 1 (Experience and Accessibility). Every track follows this so the project looks and behaves as one product.
All values live in code: `Assets/_Experience/Scripts/Runtime/UIStyle.cs`. Settings live in `GameSettings.cs`.

## Placement and comfort
- World-space panels only. Never lock UI rigidly to the face.
- Menus appear 1.8 m in front of the player, 12 cm below eye level, facing the player (`UIStyle.PanelDistance`, `PanelDrop`).
- Captions follow the head lazily (smoothed) at 1.6 m, below the line of sight.
- One canvas pixel is 1.3 mm in the world (`UIStyle.WorldScale`).

## Text
- Minimum font size 36, body 40, buttons 42, titles 80 (canvas pixels). The factory enforces the minimum.
- Text colour white on near-black. High contrast mode switches to pure black and white.
- Every spoken line or important sound has a caption (`captions.csv`, `CaptionSystem.Show(id)`).

## Targets and input
- Buttons are at least 88 px tall (about 11 cm at menu distance).
- Controller ray, mouse and gamepad all work. Gaze select (dwell 1.5 s) is available in Settings.
- Hover and focus use an outline as well as a colour change, so state never relies on colour alone.
- Pause: Escape, gamepad Start, or the left controller menu button.

## Settings other tracks must respect
| Setting | Default | Who reads it |
|---|---|---|
| `ReduceFlashing` | ON | Sonar: soft fades, cooldown of 1 second or more, no hard flashes |
| `Brightness` | 100% | World / `ComfortApplier` (ambient light) |
| `HighContrast` | OFF | UI, and any in-world cue that needs to stand out |
| `CaptionsOn`, `CaptionScale` | ON, 100% | `CaptionSystem` |
| `Seated` | OFF | `ComfortApplier` (tracking origin and eye height 1.2 m) |
| `SnapTurn` | ON | `ComfortApplier` (swaps snap and smooth turn providers) |
| `OneHanded`, `GazeSelect` | OFF | Gameplay input and `GazeDwellSelector` |
| `MasterVolume` | 100% | `ComfortApplier` |

## Integration contract
- `GameFlow.OnDelivered()`: Sonar calls this when the message is delivered. It shows the end screen.
- `GameFlow.Delivered` event: World listens to this to run the power-on finale.
- `CaptionSystem.Instance.Show("vendor_01")`: shows a caption by id from `Assets/_Shared/Resources/captions.csv`.
- `ComfortApplier.ControlAmbient = false` if World wants full control of ambient light.

## Debug keys (Editor and development builds)
F9 test caption, F10 simulate delivery.
