# Quest 2 Test Sheet (Track 1: Experience and Accessibility)

Build: `Builds/Quest2/Blackout.apk` (development build, package `com.blackoutteam.blackout`)

## Install
1. Enable Developer Mode on the Quest 2 (Meta Horizon phone app > Devices > Developer Mode).
2. Plug the headset into the PC and accept "Allow USB debugging" inside the headset.
3. Run `adb install -r Builds/Quest2/Blackout.apk` (or drag the APK into SideQuest).
4. In the headset open Library > Unknown Sources > Blackout.

## Checklist (mark Pass / Fail and note anything odd)
| # | Check | Pass criteria | Result |
|---|-------|---------------|--------|
| 1 | Launch | App opens to the health notice with no black screen longer than 5 s | |
| 2 | Panel placement | Menu appears about 1.8 m ahead, slightly below eye level, no neck strain | |
| 3 | Text readability | Title, body and button text readable without leaning in | |
| 4 | Controller ray | Pointing at a button highlights it (outline plus colour) | |
| 5 | Trigger click | A normal trigger pull activates the button first time | |
| 6 | Quick click | A very fast trigger tap still registers | |
| 7 | Hover and click sounds | Soft tick on hover, click on press, no startling volume | |
| 8 | Gaze select | With Gaze Select on, looking at a button for 1.5 s activates it | |
| 9 | Settings toggles | All 7 toggles and 3 steppers change and persist after restart | |
| 10 | High contrast | Colours switch and remain legible | |
| 11 | Captions | Caption bar follows head gently, stays readable, scale steps work | |
| 12 | Seated mode | Eye height correct while seated, floor not too low or high | |
| 13 | Snap turn / smooth turn | Both turn modes work and match the setting | |
| 14 | Brightness | Brightness stepper visibly changes ambient light | |
| 15 | Pause | Menu button opens the pause panel, time and audio pause, Resume works | |
| 16 | Comfort | No nausea or eye strain after 10 minutes | |
| 17 | Frame rate | Stays at 72 fps (use OVR Metrics Tool) | |
| 18 | No crashes | `adb logcat -s Unity` shows no exceptions | |

## Reporting
Send the failed rows (and any `adb logcat -s Unity` output) and Track 1 will fix them and rebuild.
