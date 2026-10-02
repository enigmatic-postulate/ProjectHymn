# Project Hymn

Rhythm-combat action RPG prototype for Unity 6. Attack on the beat of SNES-style JRPG battle themes; chain inputs into combo Phrases; survive time-signature changes.

## Open it

1. Install **Unity 6 LTS (6000.0.x)** through Unity Hub. Any 6000.0 patch works; Hub will offer to upgrade the version file.
2. Hub → **Add → Add project from disk** → pick this `ProjectHymn` folder. First import resolves packages (Input System, uGUI, Test Framework, 2D).
3. If Unity asks to enable the new Input System backends, click **Yes** (editor restarts). The code also runs on the legacy Input Manager.
4. Menu **Project Hymn → Build Prototype Scene**. This creates the starter Phrase and Song assets plus `Assets/_Project/Scenes/Prototype.unity`.
5. Press **Play**. A metronome clicks; hit the pink training dummy on the beat.

## Combat rules (M1)

- **On-beat** (Perfect ±45 ms, Great ±90 ms, Good ±135 ms): free, builds Groove, counts toward Phrases and Bar Finishers, recovers faster and opens a cancel window lined up with the next beat.
- **Off-beat**: still a normal attack at normal damage, but costs stamina (Strike 15, Smash 25, Step 20 of 100) and breaks the chain. With too little stamina the move still comes out, but you are **Winded** (75% slower recovery).
- Damage barely changes with accuracy (Perfect +15%); **score** is where accuracy shows (Groove multiplies score ×1 to ×3).
- No beat ring: timing comes from the music, the player's bob and the background pulse. (The ring still exists as an optional cue: HUD → Show Beat Cue.)
- Perfect hits get a 40 ms hit-stop and a chime pitched to the song's key; hit sounds snap to the 16th-note grid.

## Arena (M2)

Build Prototype Scene now creates a 2-minute arena instead of the training dummy. Waves spawn on a downbeat after a two-bar count-in; the next wave comes a bar after the last enemy falls, and the list loops with tougher enemies.

| Enemy | Attacks on | Counter |
| --- | --- | --- |
| Metronome Slime | beat 1 every bar | basic on-beat hits |
| Snare Imp | beats 2 and 4 | parry the backbeat with Guard (L) |
| Tuba Knight | beat 1 every other bar, heavy, guarded | Syncopate (Strike, rest, Smash) or a Bar Finisher breaks its guard |
| Piccolo Bat | eighth-note pecks (beats 3, 3-and, 4) every other bar | Off-step (Step, Strike) to reach it |

- Every enemy attack is telegraphed **one full bar ahead**: low sound cue, colour ramps to red, white flash just before it lands.
- **Parry**: press Guard within ±135 ms of the hit (±90 ms for heavy hits). Parries stagger the enemy for a bar and count like a Perfect.
- **Dodge**: an on-beat Step gives i-frames; stepping out of reach also works.
- Going down restarts the current wave two bars later at full HP.
- After each fight a playstyle log (time-signature adaptation, block rate, aggressiveness, weapon preference, dodge rate) is written to `%USERPROFILE%\AppData\LocalLow\<company>\<product>\Playstyle\` (the Console prints the exact path).

## Boss (M3): The First Conductor

Menu **Project Hymn → Build Boss Scene** creates `Scenes/Boss.unity` on Battle Theme 2.

| Phase | HP | Meter | It does | Beat it with |
| --- | --- | --- | --- | --- |
| Overture | 100–66% | 4/4 | strikes on beats 1 and 3, summons 2 Slimes | parry, hit between strikes |
| Waltz | 66–33% | 3/4 | one heavy hit per bar, guarded, summons an Imp | **Waltz** (Strike ×3) breaks the guard |
| Finale | 33% → | 7/8, 10% faster | hits on 1, 3 and 5, summons a Bat | HP stops at 1: only a **Bar Finisher** (all 7 beats on time) ends it |

- **Adaptive music:** each song phase loops until the boss reaches its next HP gate, then the music moves on at the end of the loop, so a phase change always lands on a bar line. The boss's HP is held at the gate until the new meter is heard.
- **One-bar warning:** on the downbeat before any tempo or meter change, the HUD shows "3/4 @ 150 — NEXT BAR" and a cymbal-style swell rises into it.
- Battle Theme 2 has no audio yet: it runs on its placeholder tempo map (4 bars 4/4 @150, 4 bars 3/4 @150, 4 bars 7/8 @165), one section per phase. With real music, set its sections and point each phase at its loop.

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
