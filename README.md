[中文 README](README.zh-CN.md)

# ProxyNote

ProxyNote is a Beat Saber mod that adjusts the visual trajectories of notes and bombs. It creates collider-free proxy notes and hides the original note models. Collision detection, cut evaluation, and scoring are still handled by the original notes, so every adjustment made by the mod is visual only.

Unlike [BeatSaber_NoteMovementFix](https://github.com/Loloppe/BeatSaber_NoteMovementFix), which directly modifies the movement logic of the original notes, ProxyNote does not move or replace the original notes used for gameplay evaluation. Proxy notes are visual-only objects and do not participate in collision detection or scoring, preventing visual trajectory adjustments from directly affecting gameplay results.

The plugin settings are available under `Mods` → `ProxyNote` on the song selection screen.

## Features

### Adjust Note Rotation

In the base game, notes gradually rotate toward their final orientation as they approach the player. The `Note rotation coefficient` setting reduces the angular difference between a note's initial and final orientations:

- `0`: Notes use their final orientation as soon as they appear and do not rotate.
- `1`: Preserves the full range of the original rotation.
- `0–1`: Preserves a proportional amount of the original rotation. The default value is `0.2`.

### Disable Note Swaps

The original trajectory may cause adjacent notes to swap positions while approaching the player. When `Preserve note swaps` is disabled, notes appear directly in their final lanes without performing the horizontal swap or its accompanying vertical avoidance movement. Enabling this option preserves the original swap trajectory.

### Advance Note Jumps

In the base game, notes enter their jump trajectories as they approach the player. The `Note jump lead distance (m)` setting makes notes and bombs begin their visual jumps farther away, allowing their final heights and positions to become visible earlier.

This setting can be adjusted from `0–5` meters. A value of `0` uses the original jump distance. It only changes the visual trajectory of the proxy notes and does not affect the actual hit timing or judgment position.

### Show Cut Guide

When `Show cut guide` is enabled, proxy notes with a required cut direction display a directional guide similar to [BeatSaber_NoteCutGuide](https://github.com/Loloppe/BeatSaber_NoteCutGuide). The guide is visual only, has no collider, and automatically disappears as the note approaches the player to avoid obstructing the cut.

### Show Effects in the PC View

By default, proxy notes are shown in the headset while the PC view continues to display the original notes. Enabling `Show plugin effects on PC` makes the PC view display the same proxy notes and visual trajectories as the headset.

### Hide Vanilla Note Debris

When `Hide vanilla note debris` is enabled, the two original note halves are not displayed after a successful cut. Hit effects, haptic feedback, and scoring are unaffected.

### Debug Mode

Enabling `Debug mode` displays both the proxy notes and visual copies of the original notes, making it easier to compare their positions, rotations, and movement. The `Original note opacity` and `New note opacity` settings become available while debug mode is active. Debug copies also have no colliders and do not affect gameplay evaluation.

## Installation

Place `ProxyNote.dll` from the release package in the Beat Saber `Plugins` folder. Make sure the following dependencies are installed before using the mod:

- BSIPA
- BeatSaberMarkupLanguage (BSML)
- CameraUtils

ProxyNote has currently been built and tested only with Beat Saber `1.40.8`. Other game versions have not been verified and are not guaranteed to be compatible.
