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

## Valor Look and Valor Stage (M6 Slices B and C)

The default look is now modelled on Dragon Valor (PS1), at modern resolution. **Build All Scenes** regenerates every model with a painted texture the first time after pulling.

- **Painted textures:** each generated model gets its own small texture (up to 256 px, sharp pixels) painted by `Editor/Art/PaintedAtlas.cs`: light painted in from above, cloth folds, metal sheen, wood grain, hair strands, soft mottling, warm lights and cool shadows, 15-bit colour steps. Faces are painted on (eyes, brows, blush) instead of modelled. Building blocks share a tiling painted-stone texture laid on in world space.
- **Valor shading** (`Shaders/HymnToon.shader`, default): lighting per vertex as on PS1, softened toward smooth normals; no outlines; distance fog; the M5 saturation boost is gone (`Art/HymnLookValor.asset`). Bright colour and bloom are kept for beat feedback: telegraphs, hits, spells, lamps.
- **Settings → Look:** Valor (default) or Cel (the M5 look: hard light bands, ink outlines, saturated). One global shader keyword switches every material; nothing is re-imported.
- **Dragon Valor proportions:** slightly larger heads, chunky shoulder pads, oversized weapons (Blade 1.4×, Drum Hammer 1.3×, Lute Bow 1.25×). The First Conductor is rebuilt as the one high-budget model (about 1,800 triangles): tailcoat, cape and high collar, brass epaulettes, wild hair, painted stern face.
- **Moonlit Garden stage** (Slice C): seven rooms on the Level 2 track. Each room loops its own 8-bar stretch of the music until it's cleared. Fountain Court has a **bell plate** that must be rung before its door opens; Moon Bridge hides a Note cache behind a hurdle; the last room switches to Battle Theme 2 and the angled camera for the First Conductor. Rank and Notes cover the whole stage.
- **Finisher spells:** complete a Phrase of four or more steps at the top Groove tier (x3) and a screen-wide spell hits every enemy on screen once, with a measure of glowing shapes and a light flash.

## Lane & Leap (M6 Slice A)

Run **Project Hymn → Build All Scenes** again after pulling. The Practice Hall is now a stage: three side-lane rooms, then the angled arena.

- **Stages:** rooms are fought in order. When a room is clear its door sinks into the floor on the next downbeat; walking into the next room closes the door behind you, moves the camera (starting on the next beat) and brings the first wave in on the following downbeat. No time limit; the run ends when the last room is clear.
- **Lane camera:** low (22°), close, follows you along the lane only. Arena rooms switch back to the angled M5 view.
- **Jump** (Space / A): judged on the beat like every other action, one beat in the air, **always costs 20 stamina** and gives no invulnerability. Off the beat or when winded it's a lower hop. **Step** moved to Shift / RB.
- **Air Phrases:** Rising Slash (Jump, Strike: staggers), Stomp (Jump, Smash: slam down, area hit, extra damage on a downbeat), Lunge (Jump, Step, Strike: air dash and thrust; 350 Notes at the Luthier). Smash in the air always slams down.
- **Hurdles** across the lane can only be crossed in a jump; a **Note cache** waits in an alcove behind one (75 Notes, once per run). Everyone gets a dark **blob shadow**, so you can see where a jump or a flyer will land.
- **Saves:** older saves learn Rising Slash and Stomp automatically (equipped if a slot is free). The playstyle log now records jumps and `jumpShare` (the M6 target is under a third of all inputs).

## 3D look (M5)

The game is now cel-shaded low-poly 3D seen by an angled top-down camera. Run **Project Hymn → Build All Scenes** again after pulling: it generates the models and materials, then rebuilds all five scenes in 3D.

- **Gameplay** runs on the 3D ground plane (X/Z) with 3D physics; all timing, combat, enemy, boss and progression rules are unchanged.
- **Toon shader** (`Shaders/HymnToon.shader`): 3 hard light bands, violet-tinted shadow side, rim light, ink outline. If it ever fails to compile, the builder falls back to URP Simple Lit and logs an error.
- **Look:** warm key light with soft shadows, flat violet ambient, post-processing profile `Art/HymnLook.asset` (saturation +32, contrast, bloom, vignette, neutral tonemapping).
- **Camera:** `CameraRig` — perspective, 30° lens, about 52–55° down. Follows the player in town; frames the whole floor in arenas.
- **Placeholder models** (original, generated by `Editor/Art/HymnModels.cs` into `Art/Models`): the Cantor in three armors, the four enemies, the First Conductor, the three Instruments, the note shot, a lore scroll and station markers. Re-run **Regenerate Placeholder Models** after editing them. To use real art, assign your own prefabs on the Enemy Definition (`Model`) and Item (`Model`) assets: models face +Z with feet at y = 0; weapons have the grip at the origin, pointing +Y.
- **Feedback in 3D:** enemies glow red through the telegraph measure and flash white on hits; the staff lines, lamps and footlights pulse on the beat.

## Vertical slice (M4)

Run **Project Hymn → Build All Scenes** once. It generates the placeholder art, the shop catalogue, and five scenes in build order: **MainMenu → Town → Prototype (Practice Hall) → Level2 (Moonlit Garden) → Boss**. Open `MainMenu` and press Play.

- **Title screen:** Continue / New Game / Settings / Quit. One save slot (`save.json` in the persistent data folder).
- **Town of Cadence** (pre-rendered backdrop): walk with WASD, **E** to use a station.
  - *The Luthier*: buy Phrases (Waltz 300, Backbeat 400, Odd-Time 600), Instruments (Drum Hammer 800, Lute Bow 900) and armor (Metronome Vest 600, Brass Breastplate 700) with Notes.
  - *Wardrobe*: equip up to 6 Phrases, a weapon and armor.
  - *Arena gates*: Practice Hall (first clear teaches Backbeat), Moonlit Garden (first clear teaches Odd-Time), and the First Conductor's gate, which opens once both are cleared.
  - Two hidden lore scrolls in the plaza corners (+100 Notes each).
- **Arena results:** rank S–D from on-beat accuracy and Groove peak (each time you go down costs a rank), score, Notes earned (score ÷ 200 + rank bonus), first-clear Phrase, and an S-rank remix flag. Return to Town or Retry.
- **Instruments:** Blade (balanced) · Drum Hammer (Smash hits again half a beat later) · Lute Bow (Strike fires a note, so you can reach the Piccolo Bat).
- **Music layers:** give a song stems (drums, bass, harmony, lead) and Groove brings them in tier by tier. With one mixed track, the music starts muffled and opens up as Groove rises (MusicMixer → cutoff by tier).
- **Phrase trail:** the last four inputs above the measure bar: `/` Strike, `X` Smash, `>` Step, `O` Guard. Flashes gold on a Phrase.
- **Esc / Start:** pause. Song time freezes and the music re-syncs on the beat when you resume. Settings: volumes, metronome, visual beat cue, easy timing (+50% windows).
- **Placeholder art:** original pixel sprites and backdrops generated by `Editor/Art/HymnArt.cs` into `Art/Generated`. Replace any PNG with real art (or re-run **Regenerate Placeholder Art**).

## Controls (prototype)

| Action | Keyboard | Gamepad |
| --- | --- | --- |
| Move | WASD | Left stick |
| Strike | J | X / Square |
| Smash | K | Y / Triangle |
| Jump (M6) | Space | A / Cross |
| Step (dash) | Shift | RB / R1 |
| Guard | L | B / Circle |
| Calibrate latency | C, then tap J on 16 clicks | – |

Try the starter Phrases: **Triplet Cut** (Strike, Strike, Smash), **Syncopate** (Strike, rest, Smash), **Backbeat** (rest, Smash, rest, Smash), **Off-step** (Step, Strike), and the air Phrases **Rising Slash** (Jump, Strike) and **Stomp** (Jump, Smash). Play every beat of a bar without a miss to queue a **Bar Finisher** on the next downbeat.

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
