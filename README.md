# Project Hymn

Rhythm-combat action RPG prototype for Unity 6. Attack on the beat of SNES-style JRPG battle themes; chain inputs into combo Phrases; survive time-signature changes.

## Open it

1. Install **Unity 6 LTS (6000.0.x)** through Unity Hub. Any 6000.0 patch works; Hub will offer to upgrade the version file.
2. Hub → **Add → Add project from disk** → pick this `ProjectHymn` folder. First import resolves packages (Input System, uGUI, Test Framework, 2D).
3. If Unity asks to enable the new Input System backends, click **Yes** (editor restarts). The code also runs on the legacy Input Manager.
4. Menu **Project Hymn → Build Prototype Scene**. This creates the starter Phrase and Song assets plus `Assets/_Project/Scenes/Prototype.unity`.
5. Press **Play**. A metronome clicks; hit the pink training dummy on the beat.

## Controls (prototype)

| Action | Keyboard | Gamepad |
| --- | --- | --- |
| Move | WASD | Left stick |
| Strike | J | X / Square |
| Smash | K | Y / Triangle |
| Step (dash) | Space | A / Cross |
| Guard | L | B / Circle |
| Calibrate latency | C, then tap J on 16 clicks | – |

Try the starter Phrases: **Triplet Cut** (Strike, Strike, Smash), **Syncopate** (Strike, rest, Smash), **Backbeat** (rest, Smash, rest, Smash), **Off-step** (Step, Strike). Play every beat of a bar without a miss to queue a **Bar Finisher** on the next downbeat.

Switch the Conductor's Song to **BattleTheme2** to hear the meter change 4/4 → 3/4 → 7/8 (that's where the **Waltz** phrase, Strike ×3, works).

## Adding a real song

1. Drop the audio file into `Assets/_Project/Audio/Music/` (OGG or WAV; set Load Type to *Compressed In Memory*, uncheck *Preload* only if it's huge).
2. Select the Song asset in `Assets/_Project/Data/Songs/` and assign the clip.
3. Set **First Beat Offset** to the seconds where the first downbeat lands. For a looping song, trim the clip so it starts on the downbeat and use 0.
4. Fill **Sections**: measures, BPM and beats-per-measure for each stretch of the song. A single section is fine if the tempo never changes. The asset warns in the Console if a looping clip's length doesn't match the tempo map.
5. Turn the Conductor's metronome on and check the clicks sit on the music's kick/snare; nudge the offset until they do.

Battle Theme 1 and 2 are pre-wired to the two reference tracks (links stored on each asset). Their BPM and meters are placeholders until measured.

**Rights:** existing game soundtracks are fine for a private prototype only. License or replace them before publishing anything.

## Layout

```
Assets/_Project/
  Scripts/
    Core/       Conductor (DSP clock, beat events, metronome), TempoMap, BeatJudge, SongData
    Combat/     ComboEngine, PhraseData, GrooveMeter            (pure C#, unit-tested)
    Gameplay/   BeatCombatController, PlayerMotor, PlayerInputReader, TrainingDummy, BeatPulse
    UI/         HymnHud, CalibrationPanel
    Editor/     ProjectHymnSetup (the one-click scene builder)
  Tests/EditMode/   24 tests: tempo maps, meter changes, judging, combos, bars, groove
  Data/ Scenes/ Audio/ Art/ Prefabs/   (created by the setup menu)
```

Run tests: **Window → General → Test Runner → EditMode → Run All**.

## How timing works

- The **Conductor** schedules the music with `AudioSource.PlayScheduled` on the DSP clock and derives *song time* from `AudioSettings.dspTime` (smoothed between audio-buffer updates). Frame time is never used for judging.
- The **TempoMap** turns song time into beats across tempo and time-signature changes, and keeps beat indices counting across loops.
- **BeatJudge** picks the nearest beat and grades the offset: Perfect ±45 ms, Great ±90 ms, Good ±135 ms. Input time is corrected by the player's saved calibration.
- **ComboEngine** records one action per beat (empty beats are rests) and matches the longest equipped Phrase ending on the current beat. A miss, or two inputs on one beat, breaks the chain.
