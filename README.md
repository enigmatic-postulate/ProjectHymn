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

## Story (vertical slice milestone VS-5, Part A)

Built by **Project Hymn → Build Hub & Loop** (the village, the title and the new `Scenes/Ending.unity`). All story text is a placeholder in one file, `Progression/Story.cs`, ready to be rewritten.

- **The intro:** New Game plays four painted story cards (the Hymn, Saltmere's verse, the Last Cantor cracking it, the cantor at the gate) before the village. Text appears a word at a time on the music's eighth notes; **Enter** shows the rest, then the next card; **Esc** skips. New Game over an existing save now asks first.
- **Villagers who follow the story:** the Elder, the Fisher, the Lookout and a new **Lamplighter** say different things as each verse comes home (Cliffside, Belfry Ridge, the Summit). Dialogue now crawls a word per sixteenth note with a short voice blip pitched for each speaker, and shows a **live portrait** of whoever is talking (the Mentor, Innkeeper and Merchant too).
- **The village changes:** iron lantern posts line the street. The first verse **lights the lanterns**; the second strings **bunting** with little lights between them; the third raises a **golden star** over the gate with motes drifting up the street. The waltz gains a layer with each verse (hand drum and shaker; bells; a choir and harp), rendered by `Tools/compose_village.py` in lock-step with the main loop. The first time back after a verse, the village **celebrates it**: a banner, a bell arpeggio, the new layer fading in and the pieces appearing one by one down the street (an older save celebrates only its newest verse). Lanterns and glow now pulse with the village waltz too.
- **The ending:** the Summit's first clear plays three painted ending cards, then the **credits** roll over the battle theme (hold Enter to speed up, Esc to skip), then you're back in Saltmere for the last verse's celebration. Keep playing afterwards to rank up.
- **Art and sound:** the cards are painted by `Tools/paint_story.py` into `Resources/UI/Story/`; the lanterns, bunting and star are built by `Editor/Art/VillageArt.cs`; story sounds (voice blip, page turn, verse chime) are synthesized in `Gameplay/Sfx.cs`.

### Part B: the road and save slots

- **The road from Saltmere:** the village gate opens a map instead of a list. The story path runs left to right (Practice Yard, Cliffside, Belfry Ridge, the Summit), each opening when the one before is cleared; the side roads branch below what opens them (**the Arena after Cliffside, Starlit Spring after Belfry Ridge**). Each stop shows its time signature, the verse it holds and your best rank as a big coloured letter; the next story stop is marked NEXT and selected. The rules live in `Progression/StageRoute.cs`; free play opens everything.
- **Three save slots:** the title has **Continue** (the slot saved most recently), **New Game** (choose a slot; a used one asks first) and **Load Game**. Each slot shows level, class, verses home, play time and when it was saved. Files are `hymn_save_1.json` to `_3.json` in the persistent data folder; the old single save moves into slot 1 the first time the game starts.

## Look & Sound (vertical slice milestone VS-4)

- **Every stage has a look.** `Editor/Art/StageLook.cs` gives each scene a painted sky (the `Project Hymn/Sky` shader, now with clouds and a sun), matching fog, animated water in place of the flat sea, and its own post-processing volume in `Art/Generated/Looks/` (bloom, ACES tonemapping, grading, vignette). Presets: **Day** (Cliffside, Arena), **Morning** (Yard), **Dusk** (Belfry), **Night** (Summit), **Evening** (title and village). Point lights and glowing props **pulse on the beat**. Re-run **Build Hub & Loop** after pulling to regenerate the scenes.
- **Village and title:** the village plays its own waltz, *Evening in the Village* (3/4, 88 BPM, with birdsong; `Tools/compose_village.py`). The title screen shows the painted **Project Hymn logo** (`Resources/UI/HymnLogo.png`, painted by `Tools/paint_logo.py`), which breathes with the beat, over a slowly drifting camera.
- **Sound effects** (synthesized, `Gameplay/Sfx.cs`): footsteps, jumps, air jumps, landings, menu move / confirm / back, and an enemy **wind-up** sting when an attack starts its count (a violet one for unblockable attacks). Landing kicks up dust.
- **Settings → Music** and **Effects** volumes (the metronome click follows Effects).
- **Results screen:** fades in, and the rank letter stamps in large at the top right in its colour.

### Build check (Windows)

Close the Unity editor, then double-click **`Tools\BuildCheck.cmd`**. It runs, in batch mode:

1. the EditMode tests;
2. a script check (every MonoBehaviour in a file named after it; otherwise builds load scenes as "corrupted"), a rebuild of every scene, and a Windows build to `Builds/Windows/ProjectHymn.exe`;
3. a **smoke run** of that build, which opens each scene for 6 seconds and records any error.

Results go to `Logs/BuildCheck/`: `summary.txt` (the three exit codes; 0 is good), `build-report.txt`, `smoke-report.txt`, and the full logs. The same build can be started from **Project Hymn → Build Check (rebuild + Windows build)** (without the tests and smoke run).

## Slice 11: Starlit Spring (brawler arena)

Built by **Project Hymn → Build Hub & Loop** (or **Build Starlit Spring** alone). Scene `Scenes/Spring.unity`; layout `Movement/SpringLayout.cs`; art `Editor/Art/SpringArt.cs` with `Editor/Art/MeshKit.cs`, shaders in `Shaders/`, textures painted by `Tools/paint_spring.py` into `Art/Generated/Spring/`. On the village gate as "Starlit Spring (brawl)", always open.

- **The stage:** a floating island in a night sky, seen from a **fixed camera** (it drifts only slightly with you). The ground spans the island with **open edges**: falling off costs a heart and puts you back in the middle (the last heart restarts the stage, as dying does elsewhere). Above it, **three one-way platforms**: two low side daises and a high crown (reach the crown from a dais). Jump up through them; **Down + Jump** (S + Space) drops through. Ledges on platforms aren't grabbed.
- **Waves:** three, like the Arena, each with enemies on the ground and on a platform (a knight on a dais; a dancer, a knight and a wisp; the lead dancer on the crown with a knight either side). Knights and dancers **follow you between floors**: stay on another floor for about a beat and a half and they jump up (one platform at a time) or drop down, landing at arm's length. Wisps fly. The star portal opens after the last wave. Par 1:30 (placeholder), ranked like any stage. Music: Battle Theme 1.
- **The look** (all original, all generated): a starfield with an aurora and a violet nebula (`Project Hymn/Sky`), a ringed moon and distant islets bobbing in the dark; the island's drum of lilac masonry with 32 arched, gold-latticed windows (normal and emission maps); a pearl-tiled terrace; a pool with rippling reflections, fountain rings and star glints (`Project Hymn/Water`); a three-tier marble fountain under a turning star; water spilling over the whole rim and from the daises (`Project Hymn/Waterfall`); two domed spires with orbiting gold rings and lit windows; luminous trees of petal rosettes, crystal clusters and gold stars (`Project Hymn/Glow`, which **pulses on the beat**); a violet rock underside hung with crystals; drifting motes, rim mist and fountain glints; moonlight with soft shadows and coloured point lights.
- **Post-processing** (first stage to use it): bloom, ACES tonemapping, colour grading and a vignette from `Art/Generated/Spring/SpringVolume.asset`, and SMAA on the camera.

## Slice 10: the Practice Yard (tutorial)

Built by **Project Hymn → Build Hub & Loop** (or **Build Practice Yard** alone). Scene `Scenes/Yard.unity`; layout `Movement/YardLayout.cs`; lessons `Progression/Tutorial.cs`; runtime `Game/TutorialDirector.cs` and `UI/MetronomeStrip.cs`.

- **Optional, first on the village gate** ("Practice Yard (optional: the basics)", highlighted for a new player). On arrival you choose: start the lessons, skip to the final bout, or go back. Unranked: no level-downs, and dying just respawns you with full hearts. The first finish pays 150 experience (never past level 5) and the save remembers it.
- **The metronome strip** (the Yard only; everywhere else the HUD stays hearts-only): beat marks slide into a gold hit line, downbeats bigger, the current beat pulsing; each note you play is marked on its beat in its timing colour, with the judgement over the line. Lessons prompt notes on the coming beats (1 blue, 2 orange). The task and progress sit above the strip, and the metronome click is on.
- **The mentor** introduces each lesson; the game holds while he talks. The first red glow and the first violet glow each freeze the game for a callout.

| # | Lesson | Pass |
| --- | --- | --- |
| 1 | Tune your ears: the tap calibration | 16 taps (C skips) |
| 2 | The beat | J on the beat 8 times in a row (Good or better) |
| 3 | Two notes, on the dummy | 4 light and 4 heavy on the beat |
| 4 | Chains and sequences | Crossing Cuts twice, Powerslice once |
| 5 | The red glow: the training knight (blunted: "BLUNT" instead of losing hearts; can't be killed) | 3 guards |
| 6 | Parry | 2 parries |
| 7 | Dodge: his blows turn violet (can't be guarded) | 2 dodges |
| 8 | Rising Verse | launch the dummy |
| 9 | Final bout: a real bone knight | defeat him; the east gate opens |

- **Music:** "Practice in the Yard", an original placeholder loop (4/4, 100 BPM, G major, 16 bars) with a kick on every beat so the pulse is obvious. Rendered by `Tools/compose_practice.py`; song asset `Data/Songs/PracticeYard`.
- **The dummy** is the M2 prototype's straw dummy, now also launched by Rising Verse.

## Slice 9: the sequence library

Rebuild with **Project Hymn → Build Hub & Loop** (every weapon gets the whole library as sequence assets). The data is one table, `Combat/SequenceLibrary.cs`; names, patterns and multipliers are Update 18's drafts.

- **Home meters:** every sequence has one and only plays in it. A 4/4 sequence does nothing in a 3/4 bar; a meter change starts the match afresh (the chain itself goes on). So the Minstrel has **no sequences in 3/4 or 7/8**: Belfry Ridge and the boss's 3/4 and 7/8 verses are lone notes until you take the Warden (Lantern Step, 3/4) or the Crusader (Crusade, 7/8). The stage intro says when none of yours play in its meter.
- **Class trees:** taking a class (a level-up's choice, or the Fighter fallback) learns its tree, and a learned sequence works for every class. The Minstrel's tree is known from the start. The Fighter still plays sequences only in 4/4, borrowed ones included.

| Tree | 4/4 | Odd meter |
| --- | --- | --- |
| Minstrel | Crossing Cuts 1-2-1 ×1.8, Powerslice 2-2 ×1.6, **Rising Verse** 1-1-2 ×1.7 (launches) | — |
| Fighter | Drill 1-1-1-1 ×1.8, Hammerfall 2-2-2 ×2.0, Iron Count 2-1-2-1 ×2.0 | — (by design) |
| Warden | **Bulwark** 2-1-1 ×1.7 (pushes), Counterpoint 1-2-2-1 ×2.0 | 3/4: Lantern Step 1-2-2 ×1.8 |
| Crusader | Brandish 2-1 ×1.5, Judgement 2-2-1-2 ×2.2 | 7/8: Crusade 2-1-2-1-2-1-2 ×2.6 |
| Balladeer (later) | Tempest 1-1-1-2-1-1-1 ×2.5 | — |

- **Longest match wins:** when a longer known sequence carries on from one you just finished (Brandish 2-1 into Iron Count 2-1-2-1, Powerslice 2-2 into Hammerfall 2-2-2 or Judgement 2-2-1-2), the finisher pays and the chain keeps following the longer one.
- **Rising Verse launches:** its finisher throws the enemy up for 1.5 beats; it can't attack and takes ×1.5 damage until it lands ("LAUNCH"). **Bulwark pushes:** the enemy is shoved back 1.2 m and its next hit comes a beat later ("PUSHED"). A knight's shield stops both; the Bone Cantor can't be launched and isn't shoved, but the push still costs him a beat.
- **Songbook** (pause menu): one page per class tree, with each sequence's notes, home meter, finisher and, for the equipped weapon, its moves; unlearned ones are greyed. Lore has its own page. The level-up class choice lists the tree each class teaches, and learning shows as a toast (and on the results screen for the Fighter fallback).
- **Free play** knows every sequence, Tempest included.

## Slice 8: GDD update 18 alignment

Rebuild with **Project Hymn → Build Hub & Loop** (it now also builds the Arena and deletes the potion, tonic and scroll assets). **Saves reset:** a save from before version 3 loads as a level 1 Minstrel with the sword (Notes, lore and stage records are kept). This replaces the rules in the Rank & Class section below. All numbers are placeholders in `Progression/ClassProgression.cs` (`ProgressionRules`) and `Progression/StageScoring.cs`.

- **Ranks:** **C is a fail**: the results say FAILED and Replay restarts the stage, with no experience. A **clean clear** (every enemy defeated, never below half your hearts) is at least a B. S and A need every enemy; too many hits (more than 3) caps at B; on Cliffside one hit rules out S (other stages allow 2). Off-beat play no longer lowers the letter (it only costs damage). Pars: Cliffside 1:15, Belfry Ridge 0:45, the Summit 0:15, the Arena 1:00.
- **Levels:** a clear earns 300 / 250 / 200 for S / A / B plus 20 per enemy; level *n* needs 100 × *n*; the cap is 10. **Level-downs** happen when they happen: dying (or falling) or losing half your hearts drops one level, once a run. Below level 5 you are a Minstrel.
- **Classes unlock from how you play,** counted over clean clears: the **Warden** when parries and blocks are half or more of the enemy attacks that reached you, the **Crusader** when dodges are; both need 70% of your hits on the beat (and 6+ enemy attacks counted). From **level 6, every level-up** offers a change to any unlocked class (the results button reads *Level up: choose your class*). A Minstrel who passes level 5 with neither unlocked becomes a **Fighter**.
- **Weapons by weight:** the Warden holds light weapons and shields (the sword), the Crusader heavy weapons and no shield (the greatsword); Minstrel and Fighter hold either. Your first B or better puts the **greatsword in the shop** (300 Notes). Choosing the Crusader without one gives you the greatsword.
- **Village:** the shop sells weapons only. The armsmaster is now the **Mentor**: ask how you're doing (your guard, dodge and on-beat shares, and how close each class is), how ranks work and how classes work; the weapon rack is there too (changing reloads the village). **Pause → Weapon** equips an owned weapon your class can hold (from the next stage). **Pause → Character** shows level, experience, unlocked classes, class progress, stage bests and recent ranks.
- **Removed:** potions, tonics, sequence scrolls, the quick slot (E / Q), and Notes, scroll and item pickups in the stages. Sequences come with the class (Crossing Cuts and Powerslice for now).
- **HUD:** hearts only (plus toasts, prompts and the "next bar" meter countdown). Enemy counts, "JUMP", the Notes counter, the item slot and the boss bar are gone. Instead every enemy **glows a beat before each hit** (red; violet for the boss's unblockable shockwave). "STAGGER" still shows.
- **The boss** has 200 health and two verses (Limp Run joins in the second); the phase change no longer holds him.
- **The Arena** (`Scenes/Arena.unity`, layout `Movement/ArenaLayout.cs`; always open at the village gate): a flat stone ring and three waves (a knight; a knight and a wisp; a bone dancer and a knight). Each wave comes in 1.6 s after the last falls, and the goal opens when all three are down. 4/4, Battle Theme 1.

**Playtest keys** (Pause → Character lists them too):

| Key | Does |
| --- | --- |
| F8 / Shift+F8 | Switch to the next / previous class now: unlocks it (kept in a real save), teaches its tree, gives it a weapon it can hold, and restarts the scene. Also Pause → Character → Debug: switch class |
| F10 | Experience to just below the next level (the next clear levels up) |
| F11 / Shift+F11 | Level up / level down now |
| F12 | Force the next clear's rank: S, A, B, C, then real play again |
| Shift+F12 | Finish the stage now (reach the goal) |
| F7 | Test checkpoints on / off |

To try a class change fast: F11 to level 5, F10, then clear any stage (Shift+F12). To try an unlock, play clean clears that mostly parry and block (Warden) or mostly dodge (Crusader), on the beat.

## Core Combat slice (GDD update 3)

Rebuild with **Project Hymn → Build Hub & Loop**, then play Cliffside (from the village gate, or press Play in `Scenes/Cliffside.unity`).

- **Every attack hits.** Timing sets the damage: **True Perfect** (±15 ms) 200%, Perfect (±45) 115%, Great (±90) 110%, Good (±135) 100%, off the beat 25%. Only on-beat notes on consecutive beats build combos and sequences; a finisher's multiplier stacks with True Perfect. True Perfect has its own chord, ring and flash.
- **Presses are timed by the input event**, not the frame they were read in (Input System event times), so a 60 fps frame no longer blurs ±8 ms. Calibration (C) now takes the median of 16 taps and reports their spread; under ±15 ms is good enough for True Perfect. Settings has a fine audio offset in 1 ms steps.
- **No Easy mode.** Windows are the same for everyone.
- **Guard:** only Perfect timing parries (stagger, ×1.5 damage); Great and Good block. **Dodge** (Left Shift / RT) replaces the dash: a roll with 0.25 s of invulnerability; a hit those frames cover is dodged ("DODGE"). The roll animation is KayKit's `Dodge_Forward` from `Rig_Medium_MovementAdvanced.fbx` (Character Animations 1.1, in `Art/Imported/KayKit/Animations`).
- **No checkpoints:** dying or falling into the sea restarts the whole stage, timer included. **R** restarts too. For tuning, **F7** (or Settings → Checkpoints) brings the old checkpoints back.
- **Stage timer and tally:** Cliffside's par is 1:15. Reaching the gate shows a results screen: time and time rank, the rank after its gates, hits by judgement, parries (count as Perfect), blocks and dodges (count as Great), hits taken, enemies, efficiency (14 attacks for a perfect run, a placeholder) and the mastery score. Since Rank & Class the clear also counts toward progress (below).

## The Summit (vertical slice milestone VS-3: the boss)

Built by **Project Hymn → Build Hub & Loop** (or **Build Summit Boss Slice** alone). Scene `Scenes/Summit.unity`; layout `Movement/SummitLayout.cs`. The third road at the village gate opens once Belfry Ridge is cleared.

- **The stage:** a short stair to the crown of the headland at night, then a wide arena ringed by standing stones, with braziers that pulse on the beat. The way down (the goal) stays sealed until the boss falls. Par 1:40, 40 attacks for a perfect run (placeholders); the clear is ranked like any stage (the boss counts as its one enemy).
- **The music:** "Hymn of the Summit", an original placeholder boss theme that changes meter: 8 bars of 4/4 and 8 of 3/4 at 150 BPM, then 8 of 7/8 (eighth = 300, grouped 2+2+3), looping (33.6 s, exact on the beat grid). Rendered by `Tools/compose_summit.py`; song asset `Data/Songs/SummitTheme`.
- **Meter changes are telegraphed a bar ahead:** the current time signature sits in the top-right corner on every stage, and through the bar before a change a big "next bar: 7/8" counts down the beats. The boss raises his staff and conducts through that bar instead of attacking.
- **The Bone Cantor** (`BoneCantor`, a 3.4 m KayKit skeleton mage with a staff; 700 health): every attack is counted in for one bar of the meter it lands in and never straddles a meter change (`Combat/BossPlanner.cs`). He fights in the meter the music is in:
  - 4/4: **Gavel** (one heavy slam), **Down-Up** (one and three), later **Four on the Floor** (every beat).
  - 3/4: **Waltz** (one-two-three), later **Hesitation** (one and three).
  - 7/8: **Limp** (the group starts: 1, 3, 5 of the eighths), last verse **Limp Run** (1, 3, 5, 7).
  - **Shockwave** (from the second verse, any meter): a jump slam whose ring runs 7 m along the ground. No guard stops it; jump it (be off the ground on the beat) or dodge through it. Its count shows "JUMP".
- **Three verses (phases) by health,** marked on the boss bar at the bottom of the screen; each change holds him for a bar and names what's coming. He takes 70% damage until he recovers after an attack (full damage in recovery, ×1.5 staggered). Parries stagger him as usual.
- **Art:** copy `Skeleton_Mage.fbx` (Characters) and the Unity-variant `Skeleton_Staff.fbx` (Weapons) from the KayKit Skeletons pack (a companion asset commit does this). Without them he's the skeleton warrior with a blade; without KayKit, placeholder primitives.

## Belfry Ridge (vertical slice milestone VS-2: the second stage, in 3/4)

Built by **Project Hymn → Build Hub & Loop** (or **Build Belfry Ridge Slice** alone). Scene `Scenes/Belfry.unity`; layout data in `Movement/BelfryLayout.cs` (stages share `StageLayout`, and the reach tests run on every stage).

- **The road:** the village gate is now a stage select ("Where to?"): Cliffside (4/4) and Belfry Ridge (3/4), each with your best rank. Belfry Ridge opens once Cliffside is cleared (free play opens both).
- **The stage:** a dusk climb up the ridge to the old cantors' belfry: two steps, the bell wall (double jump and grab), a broken span with a wisp over it, two rope posts and the belfry yard. Par 1:25, 20 attacks for a perfect run (placeholders). The bell swings once a bar, so it rings on every downbeat. A lore cairn at the foot ("The Cantors' Count") explains the dancers.
- **The music:** "Waltz of the Belfry", an original placeholder battle waltz (3/4, 168 BPM, D minor, 32 bars, loops exactly on the beat grid). It's rendered by `Tools/compose_waltz.py` into `Audio/Generated/` (no samples; regenerate with `python3 Tools/compose_waltz.py Assets/_Project/Audio/Generated/Waltz_of_the_Belfry.wav`). Song asset `Data/Songs/BelfryWaltz`.
- **Bone dancers** (`BoneDancer`, KayKit Skeleton Rogue with two blades): between attacks they glide one step on each downbeat and sway on two and three, so they count the bar. Every attack is telegraphed for one bar (3-2-1). Three attacks: **Waltz** (hits on one, two, three), **Hesitation** (one and three, holding on two) and **Turn** (a heavy spin on the downbeat that reaches behind it too). No shield, but they take only 75% damage until they recover after an attack. The lead dancer in the belfry yard has more health.
- **Count-ins follow the meter:** every enemy's telegraph is now one bar of the current meter (4 beats in 4/4 as before, 3 in 3/4).
- **The Fighter** plays no sequences in 3/4 (lone notes still hit); the stage says so as it starts. Every stage now opens with its name and time signature.
- **Art:** copy `Skeleton_Rogue.fbx` from the KayKit Skeletons pack into `Art/Imported/KayKit/Characters` (a companion asset commit does this); without it the dancers use the skeleton warrior, and without KayKit, placeholder primitives.

## Rank & Class slice (vertical slice milestone VS-1)

*Superseded by Slice 8 (above); kept as a record.*

Ranks now count. Rebuild with **Project Hymn → Build Hub & Loop** (the database gains four classes and the scenes one player per weapon). All numbers are placeholders in `Progression/ClassProgression.cs` (`ProgressionRules`) and the class assets.

- **Start:** New Game starts everyone as the **Minstrel** with the sword, at level 1. No class menu.
- **Experience:** a clear earns 500 / 400 / 300 / 200 for S / A / B / C, plus 20 per enemy. Level *n* needs 100 × *n*.
- **Level-downs:** losing 3+ hearts in the run, or 40%+ of attacks off the beat (with 6+ attacks), each drop one level. Experience is never lost, and that run earns none. Dropping below a class's minimum level (6 for Warden and Crusader) costs the class.
- **Class checks** at levels 6, 8 and 10 (then every 5), judged on the clear that reached the level:
  - **S or A** lets a class rise. Mastery 70+ offers the high class that fits how you defended (mostly parries and blocks: Warden; mostly dodges: Crusader); 85+ offers both. You choose (or keep your class). A Fighter can also move back up to Minstrel.
  - **B** holds the class. A Minstrel who reaches level 10 without rising becomes a Fighter.
  - **C** costs one: a Minstrel becomes a Fighter, a Warden or Crusader falls back to the Minstrel. A Fighter stays a Fighter.
- **Classes:** Minstrel (base: 5 hearts, sword or greatsword). Fighter (D rank: 6 hearts, a +40 ms block window, blocks deal 6 and parries 12 damage, common time only). Warden (6 hearts, sword and shield only; parries deal 20 damage, a block makes the enemy skip its next note). Crusader (4 hearts, ×1.15 damage, 350 ms dodge invulnerability instead of 250; the brandish and new attack animations are still to come). Counters go round the knight's shield.
- **Weapons** are separate from classes, each with its own moveset: the sword and shield, and the greatsword (×1.3 damage), which unlocks with your first **B or better**. Classes list the weapons they can equip. The village **armsmaster** (the old class trainer) changes weapons.
- **Results screen:** after the tally it shows experience and levels, level-downs and why, the class check's verdict, unlocks and a new best. When a check offers a choice, **Choose your class** opens the offer (the class that fits how you fought is marked).
- **Pause → Character:** class, perks, level and experience, the next class check, weapons, and each stage's best rank and time. *Playtest: next clear reaches level N* sets your experience so the next clear hits the next class check.
- **Save:** version 2 adds level, experience, weapon, unlocked weapons and stage records. Older saves load as a Minstrel holding their old weapon (a Barbarian keeps the greatsword).

## Hub & Loop slice (title, village, stage and back)

Menu **Project Hymn → Build Hub & Loop**. It builds the game database and three scenes, puts them first in the build list (Title, Village, Cliffside), and opens Title. Press **Play** in Title for the whole loop. Pressing Play in any other scene starts **free play** (Minstrel, every weapon and sequence, nothing saved).

- **Title:** New Game (starts as the Minstrel), Continue (the save), Settings, Quit. The battle theme plays under it.
- **Players:** every scene holds one player per weapon; *Players* (PlayerSpawner) keeps the save's weapon, applies the class (hearts, damage, perks) and places it at the spawn point it arrived at. (Until Rank & Class the weapon was the class: Knight and Barbarian.)
- **Village (Saltmere):** one street between two walls. Inn (rest to full hearts and **save**), shop (potions, battle tonics, the Powerslice scroll), armsmaster (weapons), three villagers (the elder hands over a lore piece), a ledge over the east end with a lore note (double jump), and the gate (stage select: Cliffside, Belfry Ridge, the Summit). No music clock runs here, so swords are inert.
- **Cliffside** now has Notes caches on the beach, tier 1 and the far ledge, a lore cairn on the beach, and the **Tempest scroll** on a high ledge over tier 3 (double jump). Enemies drop 20–40 Notes. Reaching the summit clears the stage and returns you to the village (saving); the path at the west end of the beach and the pause menu go back too.
- **Sequences are learned:** a new game knows Crossing Cuts only. Scrolls (bought or found) teach Powerslice and Tempest; the Songbook in the pause menu lists what you know and the lore you have found.
- **Items:** the quick slot (bottom left) holds a potion (heals 2) or a tonic (×1.25 damage for 16 beats). **E** uses it, **Q** changes it, or pick one under Items in the pause menu. Stacks hold 9.
- **HUD:** hearts and Notes (top left), the quick item, sequence banners and toasts. Judgement and chain stay on screen; the rest of the prototype rhythm HUD is the debug HUD (Settings, or **F9**).
- **Pause (Esc / Start):** stops the music and the world. Songbook, Items, Settings (volume, easy timing for wider windows, animation offset, debug HUD, latency calibration), Return to the village, Quit to title.
- **Save:** `hymn_save.json` in Unity's persistent data folder (class, level, experience, weapon and unlocked weapons, Notes, items, known sequences, lore, collected pickups, cleared stages and best ranks, where you rested). Delete it to start over.
- **Data:** classes and items are assets in `Data/Game` (`Resources/HymnDatabase` lists them and the shop's stock); the village layout is `Movement/VillageLayout.cs`; pickups in Cliffside are markers in `CliffsideLayout.cs` (the name says what they are).

## Art pass (CC0 packs)

The Cliffside builder uses imported art when it finds it, and falls back to the generated placeholders otherwise.

- **Characters (KayKit by Kay Lousberg, CC0):** the Knight model holds the sword and shield and the Barbarian model the greatsword (one per weapon; art TBD); villagers are the Mage, Rogue, hooded Rogue and Ranger, looping idle clips and turning to face you; the shield-knight enemy is the Skeleton Warrior with blade and large shield. Files live in `Art/Imported/KayKit` (Characters, Weapons, Animations).
- **Animation on the beat:** every attack, the guard, idle, run, jump, fall and land are KayKit clips, baked by the builder into `Data/Clips` (per-bone samples). Each attack's impact frame (measured from the weapon hand's peak speed) is placed exactly on the judged beat: the wind-up is stretched over the beats before it and the follow-through over the beats after, so tempo still sets the speed. Footfalls of the run land on beats. Hanging and climbing use a base pose with the hands placed on the ledge by IK; the double jump still flips. Moves are in `Data/KayKit/Moves` (clip, impact frame, span in beats, damage, reach, trail colour).
- **Environment (Quaternius Medieval Village MegaKit and Poly Haven boulders, CC0):** textured cliffs (generated meshes with world-space UVs), stone borders, vines, fences, crates, a house and a town gate at the summit, scanned boulders on the beach and as sea stacks. Files in `Art/Imported/Village` and `Art/Imported/Boulders`.
- Weapons sit on the KayKit hand slots with no offset; if one is held wrongly, rotate it under `handslot.r` / `handslot.l` on the Player.

## Controls (prototype)

| Action | Keyboard | Gamepad |
| --- | --- | --- |
| Move / run | WASD (A / D in the Cliffside slice) | Left stick |
| Jump, double jump (Cliffside) | Space | A / Cross |
| Drop through a platform (Starlit Spring) | S + Space | Stick down + A |
| Climb / let go of a ledge (Cliffside) | W or Space / S | Stick up / down |
| Note 1 (light) | J | X / Square |
| Note 2 (heavy) | K | Y / Triangle |
| Guard / parry (Cliffside) | L | LB / L1 |
| Dodge (roll, i-frames) | Left Shift | RT / R2 |
| Talk, shop, rest, use a door | W / Up | Stick up |
| Pause menu | Esc | Start / Options |
| Restart the stage (Cliffside) | R | – |
| Test checkpoints on / off | F7 | – |
| Playtest keys (class, experience, level, rank, finish) | F8, F10, F11, Shift+F11, F12, Shift+F12 | – |
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
