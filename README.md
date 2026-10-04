# Project Hymn

Rhythm-combat action RPG prototype for Unity 6. Attack on the beat of SNES-style JRPG battle themes; chain inputs into combo Phrases; survive time-signature changes.

## Open it

1. Install **Unity 6 LTS (6000.0.x)** through Unity Hub. Any 6000.0 patch works; Hub will offer to upgrade the version file.
2. Hub → **Add → Add project from disk** → pick this `ProjectHymn` folder. First import resolves packages (Input System, uGUI, Test Framework, 2D).
3. If Unity asks to enable the new Input System backends, click **Yes** (editor restarts). The code also runs on the legacy Input Manager.
4. Menu **Project Hymn → Build Prototype Scene**. This creates the starter Phrase and Song assets plus `Assets/_Project/Scenes/Prototype.unity`.
5. Press **Play**. A metronome clicks; hit the pink training dummy on the beat.

## Sword rhythm prototype (M2)

Menu **Project Hymn → Build Prototype Scene**, then press **Play**. You fight a straw training dummy with a generated, skinned swordsman whose attacks are keyframes timed in beats.

**Buttons:** 1 = light (J / X), 2 = heavy (K / Y). Press them on the beat.

| Rhythm | Saber / Broadsword moves |
| --- | --- |
| **1-2-1** Crossing Cuts | diagonal cut, horizontal slash, rising diagonal |
| **2-2** Powerslice | rising cut into an overhead vertical power slice (with a lunge) |
| **1-1-1-2-1-1-1** Tempest | cut, slash, cut, quick left-hand punch, cut, slash, finishing rising cut |

- **Damage needs rhythm.** A chain is notes on consecutive beats. The first note of a chain swings but deals nothing ("–" on the dummy); every following on-beat note hits. Off-beat notes swing slowly, deal nothing and break the chain. Finishing a sequence multiplies its last hit (×1.6 to ×2.5) with a burst and a chord.
- **Animation follows the music.** Each move's keys are in beats around the hit (0 = contact), and the hit is placed on the judged beat, so swings land on the beat at any tempo. On fast songs the wind-up and follow-through swing less far, on slow songs further. While a rhythm is in progress the next move winds up half a beat early and holds just before the hit; pressing on the beat continues it with no seam. A late press catches up within a quarter beat.
- **Calibrate:** C = audio latency (tap J on the clicks). **[ and ]** shift all animation against the music in 10 ms steps (saved). Each move's keyframes are editable in `Data/Sword/Moves` (times in beats, bone rotations in degrees); **Project Hymn → Reset Sword Moves to Defaults** restores them.
- **Tempo test:** F1 = Battle Theme 1, F2/F3/F4 = metronome at 90/130/170 BPM.
- **Stances:** Tab switches between the **saber** (one-handed, side-on fencing stance, quicker) and the **broadsword** (two-handed square guard, left hand kept on the hilt by IK, bigger turns and lunges, more damage).
- **Effects:** sword tracers on the blade tip (move colour on the beat, grey off it), a ring at your feet on every on-beat note (gold Perfect, white Great, blue Good), sparks on contact, a flash on the blade, a burst and rings on completed sequences, floating damage numbers, a wobbling dummy.
- **HUD:** a beat track (last four beats and the next three, your notes on them), the three sequences with the matched steps lit and the next note marked, the chain counter, judgement, score and Groove.

## Cliffside slice (vertical slice 1: movement)

Menu **Project Hymn → Build Cliffside Slice**, then press **Play** (scene `Scenes/Cliffside.unity`). A greybox side-scrolling climb in the style of Dragon Valor's side views: a beach, three cliff tiers, a 5 m gap, two stone pillars and a summit with a goal arch, about 115 m of lane. The goal is to climb it all and swing the sword anywhere along the way.

- **Movement is free** (never judged against the beat): run, jump, jump again in the air for a double jump with a flip, hold jump for full height. Coyote time and a jump buffer make the timing forgiving.
- **Ledges:** jump at a cliff whose lip is between 1.1 and 2.05 m above your feet and you grab it. Up, Space or pushing toward the wall climbs; Down lets go; Space while pushing away kicks off the wall. Grabbing refills the double jump.
- **Combat anywhere:** the M2 sword rhythms work on the ground and in the air. While moving, the sword move owns the upper body and the legs keep running or tucking. An on-beat note in the air stops your fall briefly (up to three per jump), so you can play a rhythm on the dummy hovering over the gap.
- **Running is on the beat:** footfalls land on beats (one step per beat at 130 BPM, two at 90).
- **Checkpoints:** crystal posts light up as you pass; falling into the sea returns you to the last one. **R** restarts. The run time and falls show at the top of the screen.
- **Enemies (slice 2):** a **shield knight** guards the first tier and a **wisp** hovers over the gap. Both attack on the beat: each attack is telegraphed for a full bar (the enemy glows up, a 3-2-1 count hangs over its head, and `!` / `!!` marks the hit beats on the HUD beat track) and lands on a downbeat.
  - **Guard** with **L** (LB / L1) on the hit beat. Perfect or Great timing **parries**: the enemy is staggered for two beats, its remaining hits are cancelled and your on-beat hits deal ×1.5. Good timing **blocks**. Otherwise the hit lands. One press answers one hit, so the knight's two-beat flurry needs two.
  - The knight's shield soaks hits from the front (they only chip it) unless it is staggered or recovering after a swing; jump over it and hit its back for extra damage. It turns around only between attacks, a beat late. It alternates a heavy cut (2 damage) and the flurry (1 per hit).
  - The wisp drifts on the beat, pulls back over a bar, then dives at where you were on the downbeat. Blades pass through it except right after a dive or when it is staggered.
- **Health:** 5 hearts (top left). Hits knock you back (and off ledges). At zero you respawn at the last checkpoint with full health, and enemies still standing reset; defeated ones stay down.
- **Tuning:** movement numbers are on the Player's *Platformer Controller*; enemy health, ranges and attack patterns (hits in beats, telegraph length, recovery, damage) are on each enemy; the level layout is `Movement/CliffsideLayout.cs` (EditMode tests check every jump on the route is reachable with the tuned arcs); movement poses are in `Data/Sword/Locomotion` (reset with **Reset Sword Moves to Defaults**); camera framing per stretch is on the *Camera Zone* objects.

## Hub & Loop slice (title, village, stage and back)

Menu **Project Hymn → Build Hub & Loop**. It builds the game database and three scenes, puts them first in the build list (Title, Village, Cliffside), and opens Title. Press **Play** in Title for the whole loop. Pressing Play in any other scene starts **free play** (Knight, every sequence known, nothing saved).

- **Title:** New Game (choose Knight or Barbarian), Continue (the save), Settings, Quit. The battle theme plays under it.
- **Classes (the weapon is the class):** the **Knight** has sword and shield, 5 hearts, and Good-timed guards parry too. The **Barbarian** has the greatsword, 6 hearts and ×1.3 damage, but only Perfect and Great guards parry. Every scene holds one player per class; *Players* (PlayerSpawner) keeps the save's class and places it at the spawn point it arrived at. The class trainer in the village switches class.
- **Village (Saltmere):** one street between two walls. Inn (rest to full hearts and **save**), shop (potions, battle tonics, the Powerslice scroll), class trainer, three villagers (the elder hands over a lore piece), a ledge over the east end with a lore note (double jump), and the gate to the cliffs. No music clock runs here, so swords are inert.
- **Cliffside** now has Notes caches on the beach, tier 1 and the far ledge, a lore cairn on the beach, and the **Tempest scroll** on a high ledge over tier 3 (double jump). Enemies drop 20–40 Notes. Reaching the summit clears the stage and returns you to the village (saving); the path at the west end of the beach and the pause menu go back too.
- **Sequences are learned:** a new game knows Crossing Cuts only. Scrolls (bought or found) teach Powerslice and Tempest; the Songbook in the pause menu lists what you know and the lore you have found.
- **Items:** the quick slot (bottom left) holds a potion (heals 2) or a tonic (×1.25 damage for 16 beats). **E** uses it, **Q** changes it, or pick one under Items in the pause menu. Stacks hold 9.
- **HUD:** hearts and Notes (top left), the quick item, sequence banners and toasts. Judgement and chain stay on screen; the rest of the prototype rhythm HUD is the debug HUD (Settings, or **F9**).
- **Pause (Esc / Start):** stops the music and the world. Songbook, Items, Settings (volume, easy timing for wider windows, animation offset, debug HUD, latency calibration), Return to the village, Quit to title.
- **Save:** `hymn_save.json` in Unity's persistent data folder (class, Notes, items, known sequences, lore, collected pickups, cleared stages, where you rested). Delete it to start over.
- **Data:** classes and items are assets in `Data/Game` (`Resources/HymnDatabase` lists them and the shop's stock); the village layout is `Movement/VillageLayout.cs`; pickups in Cliffside are markers in `CliffsideLayout.cs` (the name says what they are).

## Art pass (CC0 packs)

The Cliffside builder uses imported art when it finds it, and falls back to the generated placeholders otherwise.

- **Characters (KayKit by Kay Lousberg, CC0):** the Knight (sword and shield) and the Barbarian (greatsword) are the two classes; villagers are the Mage, Rogue, hooded Rogue and Ranger, looping idle clips and turning to face you; the shield-knight enemy is the Skeleton Warrior with blade and large shield. Files live in `Art/Imported/KayKit` (Characters, Weapons, Animations).
- **Animation on the beat:** every attack, the guard, idle, run, jump, fall and land are KayKit clips, baked by the builder into `Data/Clips` (per-bone samples). Each attack's impact frame (measured from the weapon hand's peak speed) is placed exactly on the judged beat: the wind-up is stretched over the beats before it and the follow-through over the beats after, so tempo still sets the speed. Footfalls of the run land on beats. Hanging and climbing use a base pose with the hands placed on the ledge by IK; the double jump still flips. Moves are in `Data/KayKit/Moves` (clip, impact frame, span in beats, damage, reach, trail colour).
- **Environment (Quaternius Medieval Village MegaKit and Poly Haven boulders, CC0):** textured cliffs (generated meshes with world-space UVs), stone borders, vines, fences, crates, a house and a town gate at the summit, scanned boulders on the beach and as sea stacks. Files in `Art/Imported/Village` and `Art/Imported/Boulders`.
- Weapons sit on the KayKit hand slots with no offset; if one is held wrongly, rotate it under `handslot.r` / `handslot.l` on the Player.

## Controls (prototype)

| Action | Keyboard | Gamepad |
| --- | --- | --- |
| Move / run | WASD (A / D in the Cliffside slice) | Left stick |
| Jump, double jump (Cliffside) | Space | A / Cross |
| Climb / let go of a ledge (Cliffside) | W or Space / S | Stick up / down |
| Note 1 (light) | J | X / Square |
| Note 2 (heavy) | K | Y / Triangle |
| Guard / parry (Cliffside) | L | LB / L1 |
| Dash | Left Shift | RB / R1 |
| Talk, shop, rest, use a door | W / Up | Stick up |
| Use quick item / change it | E / Q | B / Circle, D-pad down |
| Pause menu | Esc | Start / Options |
| Restart the slice (Cliffside) | R | – |
| Saber / broadsword (M2 prototype scene only) | Tab | Select / View |
| Rhythm debug HUD | F9 | – |
| Animation offset | [ and ] | D-pad left / right |
| Test tempos | F1 – F4 | – |
| Calibrate latency | C, then tap J on 16 clicks | – |

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
    Combat/     RhythmChain, ComboEngine, PhraseData, GrooveMeter   (pure C#, unit-tested)
    Movement/   JumpRules, JumpArc, StrideClock, CliffsideLayout     (pure C#, unit-tested)
                (Combat/EnemyAttacks: enemy attack scheduling, ParryJudge, Vitality — pure C#, unit-tested)
    Gameplay/   PlayerMotor, PlayerInputReader, TrainingDummy, cameras
      Sword/       RhythmAnimator, SwordCombatController, moves, LocomotionSet, effects
      Platformer/  PlatformerController, SideScrollCamera, CameraZone, Checkpoint, SliceGoal
      Enemies/     BeatEnemy, ShieldKnight, Wisp, PlayerGuard, PlayerHealth, DefenseFeedback
    UI/         SwordHud, CalibrationPanel
    Editor/     ProjectHymnSetup (prototype scene), CliffsideBuilder (slice scene), Sword/ (rig, moves, poses)
  Tests/EditMode/   tempo maps, judging, combos, rhythm chains, jump rules, stride clock, level reachability, enemy scheduling, parry judging
  Data/ Scenes/ Audio/ Art/ Prefabs/   (created by the setup menu)
```

Run tests: **Window → General → Test Runner → EditMode → Run All**.

## How timing works

- The **Conductor** schedules the music with `AudioSource.PlayScheduled` on the DSP clock and derives *song time* from `AudioSettings.dspTime` (smoothed between audio-buffer updates). Frame time is never used for judging.
- The **TempoMap** turns song time into beats across tempo and time-signature changes, and keeps beat indices counting across loops.
- **BeatJudge** picks the nearest beat and grades the offset: Perfect ±45 ms, Great ±90 ms, Good ±135 ms. Input time is corrected by the player's saved calibration.
- **ComboEngine** records one action per beat (empty beats are rests) and matches the longest equipped Phrase ending on the current beat. A miss, or two inputs on one beat, breaks the chain.
