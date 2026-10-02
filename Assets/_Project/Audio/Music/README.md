# Music (not in git)

Soundtrack files are kept out of the repo because they are placeholders we don't have rights to redistribute.

To play with music locally:

1. Put the track here, e.g. `Shin_Megami_Tensei_4_Demon_Fusion_OC_ReMix.mp3` (OC ReMix OCR04994, free download from ocremix.org).
2. Select `Assets/_Project/Data/Songs/BattleTheme1.asset` and drag the clip into **Clip** if it isn't linked.
3. Battle Theme 1 tempo map: 130 BPM, 4/4, first downbeat at 0.244 s, 112 bars, no loop.

Without a clip the Conductor still runs on the metronome alone.

## Level 2

1. Put `Donkey_Kong_Country_A_Different_Kind_of_Peace_OC_ReMix.mp3` here (OC ReMix OCR02936, free download from ocremix.org).
2. Run **Project Hymn → Build Level 2 Scene**; it creates `Data/Songs/Level2Theme.asset` and links the clip.
3. Tempo map: 150 BPM, 4/4, first downbeat 0.03 s, key C minor. 96 bars: 8-bar quiet intro, 80-bar body, 8-bar outro. The arena loops the body (bars 9–88) adaptively.
