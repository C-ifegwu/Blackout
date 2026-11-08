# Blackout

A VR experience for Meta Quest 2. A city-wide power cut plunges a night market into darkness. Ping sonar ripples, follow 3D sounds, and deliver a voice message before dawn. When you succeed, the city lights up.

**Status:** work in progress for the ALU / Holberton AR/VR Portfolio Project (due 13 Nov 2026).

## Team
| Track | Owner | Scope |
|---|---|---|
| 1. Experience and Accessibility | Chibueze | UI, menus, captions, settings, game flow, comfort options, repo and build |
| 2. Sonar and Gameplay | _to add_ | Sonar reveal effect, safety limits, delivery objective, demo video |
| 3. World and Audio | _to add_ | City, lighting and finale, recorded audio, story lines, licence table, DevLog |

## Requirements
- Unity 6000.4.6f1 (Unity 6.4), Universal Render Pipeline
- Android Build Support with OpenJDK and Android SDK and NDK Tools
- Meta Quest 2 (Android, ARM64, OpenXR) or the Editor for desktop testing

## Setup
1. Clone the repository and open it with Unity Hub using Unity 6000.4.6f1.
2. Open `Assets/_Experience/Scenes/Experience_Test.unity` (or run **Blackout > Experience > Create Test Scene**).
3. Press Play. In the Editor use the mouse; Escape pauses; F9 shows a test caption; F10 simulates delivery.
4. Quest 2 build: **Blackout > Quest 2 > Build APK**. The APK is written to `Builds/Quest2/Blackout.apk` (git-ignored). Install with `adb install -r Builds/Quest2/Blackout.apk` while the headset is in developer mode.

## Accessibility
Captions for all voices, adjustable caption size, high-contrast mode, reduced flashing on by default, brightness and volume controls, seated mode, one-handed option, snap or smooth turning, gaze-select as an alternative to controller buttons, and a health notice at start. See `Docs/UI-Standard.md`.

## Project layout
- `Assets/_Experience` Track 1 (runtime code, editor tools, tests)
- `Assets/_Sonar` Track 2
- `Assets/_World` Track 3
- `Assets/_Shared` shared data such as `Resources/captions.csv`

## Tests
Window > General > Test Runner > EditMode > Run All (10 tests covering settings, captions parsing, game flow and UI contrast).

## Asset licences
All assets must be CC0 / Creative Commons, purchased and licensed, or created by the team. Track 3 maintains this table.

| Asset | Source | Licence | Used by |
|---|---|---|---|
| XR Interaction Toolkit samples and Unity VR template assets | Unity packages | Unity package licence | Track 1 test rig |
| Kenney UI Pack | kenney.nl | CC0 1.0 | Track 1 (Assets/_Experience/ThirdParty) |
| Kenney Interface Sounds | kenney.nl | CC0 1.0 | Track 1 (Assets/_Experience/ThirdParty) |
| Kenney City Kit Commercial 2.1 | kenney.nl | CC0 1.0 | Track 3 (Assets/_World/ThirdParty) |
| Kenney City Kit Industrial 1.0 | kenney.nl | CC0 1.0 | Track 3 |
| Kenney City Kit Roads | kenney.nl | CC0 1.0 | Track 3 |
| Kenney Furniture Kit | kenney.nl | CC0 1.0 | Track 3 |
| Kenney Impact Sounds | kenney.nl | CC0 1.0 | Track 3 |
| _add each asset here_ | | | |
