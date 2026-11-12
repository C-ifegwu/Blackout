# BLACKOUT

**A city you hear before you see.** A Meta Quest 2 VR experience for the ALU / Holberton AR/VR Portfolio Project.

NEPA has taken the light. The whole city is dark. Send sonar pings to reveal the streets, follow three relay tones through the night market, and deliver a voice message at the radio tower before dawn. When it arrives, the city lights up.

- Platform: Meta Quest 2 (Android, ARM64, OpenXR, URP)
- Engine: Unity 6000.4.6f1 (Unity 6.4)
- Play time: about 4 to 6 minutes
- Interaction: sonar ping (trigger), walking and turning with the thumbsticks, optional gaze select, full menus and settings

## How to play
1. Look around. It is pitch dark and the city is silent except for distant sound.
2. **Pull a trigger (or press Space in the Editor) to send a sonar ping.** A ring and a light pulse spread out and every surface leaves a glowing echo that fades.
3. **Follow the relay tone.** Only the current relay plays a pulsing tone. Walk toward it. Captions give hints if you are lost.
4. **Reach the relay and ping (or stand still for a moment)** to switch it on. There are three relays.
5. When the third relay is on, the **tower signal** starts. Walk to the tower and ping to send your message.
6. The city relights slowly over several seconds.

### Modes
- **Story** (default): no timer, relaxed pace, hints every 30 seconds.
- **Beat the dawn**: switch it on from the main menu. You have 6 minutes to switch on three relays and reach the tower. Radio warnings count down, and if dawn breaks first the city stays dark. Your fastest time is saved on the device and shown on the results screen.

### Monsters
In the dark, creatures roam the streets. They patrol the roads, see you inside a cone (much less far if you **hold still**) and hear every sonar ping and every relay switching on. Once one has noticed you it charges; break line of sight and it gives up. Contact ends the run on a gentle "They found you" screen (no flashing). When the message is delivered at dawn, every creature cries out and falls.
- **Monsters: Normal / Gentle / Off** is on the main menu. Gentle has fewer, slower monsters you can outwalk. Off removes them.
- Accessible warnings: glowing eyes show how alert a monster is, spatial growls and footsteps, a heartbeat that quickens as danger nears, captions that say which side it is on, and a soft controller pulse.

### Controls
| Action | Quest 2 | Editor |
|---|---|---|
| Sonar ping | Trigger (either hand) or A button | Space |
| Move / turn | Thumbsticks (snap or smooth turn in Settings) | n/a (XR Device Simulator) |
| Pause | Menu button (left controller) | Escape |
| Menu click | Controller ray plus trigger, or gaze for 1.5 s | Mouse |
| Test helpers (dev builds) | n/a | F9 test caption, F10 simulate delivery |

## The lantern and the puzzles

The city is pitch black. Your only light is the lantern in your left hand: hold grip to cover it (monsters see you much less), find oil flasks to keep it burning. Reach the three relays, read the plaque beside each one, take the brass key from its pedestal (watch the floor for pits), light the three braziers in the order the plaques gave, then deliver the message at the tower seal. Tripwires ring a bell; crate alcoves are hiding places.

## Team and roles
| Track | Owner | Delivered |
|---|---|---|
| 1. Experience and Accessibility | Chibueze | World-space VR UI, menus, settings, captions, game flow, comfort and accessibility options, Quest build tooling, tests |
| 2. Sonar and Gameplay | _team member_ | Sonar ping, echoes, relay objective, delivery, hints |
| 3. World and Audio | _team member_ | City, lighting and finale, spatial audio, story captions, licence table, DevLog |

> Fill in the names of the other two members before submitting.

## Accessibility and safety
- Health and comfort notice before play, reduce-flashing **on by default**
- Sonar cooldown above one second, smooth light pulse (dimmed with Reduce Flashing), and a slow 3.5 to 5 second relight. No flashing content
- Captions for every spoken line and important sound, adjustable size, high-contrast option
- Audible cues paired with visual echoes and captions; direction hints in words ("to your left, about 40 metres")
- Seated mode, one-handed option, snap or smooth turning, gaze-select alternative, brightness and volume controls
- Settings persist between sessions

## Setup
1. Clone the repository and open it with Unity Hub using **Unity 6000.4.6f1**. Install Android Build Support (OpenJDK, SDK and NDK).
2. Open `Assets/_World/Scenes/Blackout_City.unity`. If it is missing, run **Blackout > World > Build City Scene**.
3. Press Play. Use the mouse for menus, Space to ping, F10 to simulate delivery.
4. Quest build: **Blackout > Quest 2 > Build APK**, which writes `Builds/Quest2/Blackout.apk`. Install with `adb install -r Builds/Quest2/Blackout.apk` while the headset is in developer mode.

## Project layout
- `Assets/_Experience` UI, menus, captions, settings, game flow, tests (Track 1)
- `Assets/_World` city builder, lighting, gameplay scripts, generated audio, Kenney kits (Tracks 2 and 3)
- `Assets/_Sonar` sonar materials
- `Assets/_Shared/Resources/captions.csv` all story captions
- `Docs` UI standard, headset test sheet, DevLog, video script, presentation outline

## Tests
Window > General > Test Runner > EditMode > Run All.

## Assets and licences
Every asset is CC0, original, a Unity package, or a Unity Asset Store asset licensed to the team account. Kenney packs are CC0 (https://kenney.nl). All sounds in `Assets/_World/Audio` were generated for this project by script (`Docs/audio_generator.py`) and are original.

| Asset | Source | Licence | Used for |
|---|---|---|---|
| Kenney City Kit Commercial 2.1 | kenney.nl | CC0 1.0 | Buildings, skyscrapers, parasols, awnings |
| Kenney City Kit Industrial 1.0 | kenney.nl | CC0 1.0 | Industrial district, delivery tower |
| Kenney City Kit Roads | kenney.nl | CC0 1.0 | Roads, street lamps, props |
| Kenney Furniture Kit | kenney.nl | CC0 1.0 | Market stall tables, crates, plants, stools |
| Kenney UI Pack | kenney.nl | CC0 1.0 | Imported UI sprites (reserved) |
| Kenney Interface Sounds | kenney.nl | CC0 1.0 | Menu hover, click, back, confirmation |
| Kenney Impact Sounds | kenney.nl | CC0 1.0 | Imported, reserved |
| Sonar ping, relay tones, tower signal, generator, market murmur, traffic and horns | Generated by `Docs/audio_generator.py` | Original (team) | Gameplay audio |
| Monster Orc (VK GameDev), Stylized Free Skeleton (ThunderForgeStudio), Low Poly Soldiers Demo (Polygon Blacksmith) | Unity Asset Store (account licence) | Unity Asset Store EULA | Monsters and market townspeople (`Assets/_ThirdParty`) |
| Zombie Sound Pack Free (Catastic), Monster Roars (VoiceBosch), Classic Footstep SFX (Matthew Anett) | Unity Asset Store (account licence) | Unity Asset Store EULA | Monster voices and footsteps |
| XR Interaction Toolkit samples, Unity VR template | Unity packages | Unity package licence | XR rig |

## Known limitations
- Townspeople are a small set of idle figures; sound carries the Lagos atmosphere
- City geometry is a generic kit styled for night; Lagos identity comes from audio, captions and signage colours
- Performance on Quest 2 should be checked with OVR Metrics Tool (target 72 fps)
