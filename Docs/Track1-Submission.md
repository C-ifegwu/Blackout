# Track 1: Experience and Accessibility (submission section)

**Owner:** Chibueze (ALU Kigali)
**Project:** BLACKOUT, a Meta Quest 2 VR experience.

## What this track delivers
Everything the player touches outside the world and the sonar mechanic:
- A health and comfort notice, main menu, settings, pause and end screens, all as world-space VR UI.
- A caption system driven by `captions.csv`, with scalable text and a gently following caption bar.
- Accessibility and comfort options: high contrast, reduce flashing (on by default), seated mode, one-handed play, snap or smooth turning, gaze-dwell selection, brightness, caption size and master volume. Settings persist between sessions.
- Interface sounds for hover, click, back and delivery confirmation.
- The UI standard (`Docs/UI-Standard.md`) and the integration contract the other tracks build against.
- Quest 2 build tooling (Blackout > Quest 2 menu) and automated tests.

## How it was verified
- 10 automated EditMode tests pass (caption parsing, settings, game flow, UI contrast and size rules).
- The menu flow was played through in the Unity editor with no console errors.
- A Quest 2 APK builds as a valid ARM64, IL2CPP, OpenXR package.
- On-headset results are recorded using `Docs/Headset-Test-Sheet.md`.

## Integration contract with other tracks
- Track 2 calls `GameFlow.OnDelivered()` when the message is delivered.
- Track 3 listens to `GameFlow.Delivered` for the power-on finale and supplies lines in `Assets/_Shared/Resources/captions.csv`.
- Captions are shown with `CaptionSystem.Instance.Show(id)`.

## Third-party assets (all CC0)
Kenney UI Pack and Kenney Interface Sounds (https://kenney.nl). Full table in the README.

## Safety choices
Reduce flashing is on by default, no hard flashes, a gradual 3 to 5 second finale is expected from Track 3, and the health notice appears before play.
