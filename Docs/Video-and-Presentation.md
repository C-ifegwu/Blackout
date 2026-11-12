# BLACKOUT: Video walkthrough script and live presentation outline

## Part A. Video walkthrough (about 3 minutes)

Record with the Quest 2 (Meta Quest > Camera > Record) or screen capture of the Editor Game view. Narrate live or add voice over after.

| Time | What is on screen | What you say |
|---|---|---|
| 0:00 to 0:15 | Title card "BLACKOUT" over a dark city | "BLACKOUT is a Meta Quest 2 experience set in a city-wide power cut. You are in the dark, and sound is how you find your way." |
| 0:15 to 0:40 | Health notice, main menu, Settings (show high contrast, caption size, seated mode, gaze select) | "Before play there is a comfort notice. Reduce flashing is on by default. Every comfort option is in Settings: captions, contrast, seated mode, one-handed, snap or smooth turning, gaze select." |
| 0:40 to 1:10 | Start, spawn on the avenue, first caption, first ping | "I pull the trigger to send a sonar ping. A ring and a soft light pulse spread out, and every surface leaves a glowing echo." |
| 1:10 to 1:50 | Walking toward relay 1, ping, relay lights up, caption "First relay is on" | "I follow the relay tone. Only the current relay plays, so my ears guide me. Pinging near it switches it on." |
| 1:50 to 2:20 | Night market with stalls revealed by pings, string lights, second relay | "Echoes reveal the stalls and crowds of the night market. If I get lost, captions give directions in words." |
| 2:20 to 2:45 | Tower signal, approach tower, ping, "Message sent" | "After the third relay the tower signal starts. At the tower I send the voice message." |
| 2:45 to 3:00 | City relights slowly, lamps and neon glow, Play again | "The city relights slowly over a few seconds. No flashing, by design. That is BLACKOUT." |

Tips: record at least two takes, keep the headset steady for the first ping, and capture the settings menu with and without high contrast.

## Part B. Live presentation outline (about 8 minutes)

1. **The idea (1 min).** A game you hear before you see. Why darkness and sound suit VR and Quest 2.
2. **Demo (2 min).** Run the build live or play the video. Ping, follow the tone, relight.
3. **Three independent tracks (1 min).** Experience and Accessibility, Sonar and Gameplay, World and Audio, joined by two small contracts (`GameFlow.OnDelivered`, `CaptionSystem.Show`).
4. **Accessibility and safety (1.5 min).** Reduce flashing on by default, cooldown above 1 second, slow relight, captions, seated and one-handed options, gaze select, health notice. Mention a test that enforces text size and contrast.
5. **Technical choices (1.5 min).** Sonar rays cast at ping time and echoes released as the wave arrives; one particle system for all echoes; pool of eight real lights; shared materials and static batching; generated original audio.
6. **Challenges (0.5 min).** Disk space, quick-tap UI bug, flat lighting, square echoes.
7. **Assets and licences (0.5 min).** Kenney CC0 packs and original generated audio.
8. **What is next and questions (0.5 min).** Recorded voices, people in the market, headset performance pass.

### Likely questions
- *How did you make sure it is accessible?* Captions for every sound, redundant visual and audio cues, comfort options, a health notice, and automated tests.
- *How did you avoid motion sickness?* Teleport-free walking with snap turn option, seated mode, slow transitions and no flashing.
- *Who did what?* See the attribution table in the submission document.
