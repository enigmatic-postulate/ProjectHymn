#!/usr/bin/env python3
"""
Renders the music of the Drowned Nave (VS-6), original placeholders in compound time, each loop exactly one tempo
map long (beat 0 = sample 0) so the game's beat grid never slips:

  Barcarolle_of_the_Drowned_Nave.wav   the stages' battle theme. 24 bars of 6/8, eighth = 225 (a dotted-quarter
                                       lilt of 75), D minor: harp arpeggios, a glassy lead, a choir, bells, a drum
                                       groove on the two big beats, bubbles.
  Canticle_of_the_Precentor.wav        the boss theme in three phases of 8 bars: 6/8 at eighth = 225; 3/4 at
                                       quarter = 112.5 (a hemiola: the same bar length, accents in threes of
                                       quarters instead of twos of dotted quarters); 9/8 at eighth = 225 (3+3+3).
  Hush_of_the_Air_Bell.wav             the hub: 16 bars of 6/8, eighth = 150, F major, a music-box lullaby, plus
                                       _Verse1 (harp), _Verse2 (glass bells) and _Verse3 (choir) layers in lock-step.

    python3 Tools/compose_drowned.py [out_dir]

Written for Project Hymn; no samples used (Tools/hymnsynth.py).
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from hymnsynth import (SR, Bus, bell, bubbles, choir, echo, env, f, harp, hat, kick, lowpass, midi, hz, noise, pulse,  # noqa: E402
                       reverb, sine, snare, tom, triangle, write)

CHORDS = {
    "Dm": ["D", "F", "A"], "Bb": ["Bb", "D", "F"], "C": ["C", "E", "G"], "Gm": ["G", "Bb", "D"], "A": ["A", "C#", "E"],
    "F": ["F", "A", "C"], "Am": ["A", "C", "E"], "Eb": ["Eb", "G", "Bb"], "A7": ["A", "C#", "E", "G"],
    "Dsus": ["D", "G", "A"], "Bbmaj": ["Bb", "D", "F", "A"],
}


def bar_check(melody, per_bar):
    for i, bar in enumerate(melody):
        total = sum(d for d, _ in bar)
        assert abs(total - per_bar) < 1e-9, f"bar {i + 1}: {total} != {per_bar}"


def tone_of(chord, i, octave):
    t = CHORDS[chord]
    return f"{t[i % len(t)]}{octave + i // len(t)}"


# ======================================================================================================================
# The Barcarolle of the Drowned Nave (stages)
# ======================================================================================================================

NAVE_SPE = 60.0 / 225.0                   # seconds per eighth: 0.2666...; 11,760 samples exactly
NAVE_BARS = 24
NAVE_LOOP = int(round(NAVE_BARS * 6 * NAVE_SPE * SR))

NAVE_CHORDS = (["Dm", "Dm", "Bb", "C", "Dm", "Dm", "Gm", "A"]
               + ["F", "C", "Dm", "Am", "Bb", "F", "Gm", "A"]
               + ["Dm", "Dm", "Bb", "C", "Gm", "A", "Dm", "Dm"])

NAVE_MELODY = [
    [(3, "A4"), (2, "D5"), (1, "E5")], [(3, "F5"), (3, "E5")], [(2, "D5"), (1, "C5"), (2, "D5"), (1, "F5")], [(6, "E5")],
    [(3, "A4"), (2, "D5"), (1, "E5")], [(2, "F5"), (1, "G5"), (3, "A5")], [(2, "Bb5"), (1, "A5"), (2, "G5"), (1, "F5")], [(3, "E5"), (3, "C#5")],
    [(3, "C5"), (2, "F5"), (1, "G5")], [(3, "A5"), (3, "G5")], [(2, "F5"), (1, "E5"), (2, "D5"), (1, "F5")], [(6, "E5")],
    [(3, "D5"), (2, "F5"), (1, "Bb5")], [(3, "A5"), (2, "G5"), (1, "F5")], [(2, "G5"), (1, "A5"), (2, "Bb5"), (1, "G5")], [(3, "A5"), (3, "C#5")],
    [(3, "D6"), (2, "A5"), (1, "F5")], [(3, "E5"), (2, "D5"), (1, "E5")], [(2, "F5"), (1, "E5"), (2, "D5"), (1, "C5")], [(3, "D5"), (3, "E5")],
    [(2, "G5"), (1, "F5"), (2, "E5"), (1, "D5")], [(3, "C#5"), (2, "E5"), (1, "A5")], [(6, "D5")], [(3, "D5"), (3, None)],
]
bar_check(NAVE_MELODY, 6)
assert len(NAVE_CHORDS) == NAVE_BARS == len(NAVE_MELODY)


def lead_note(name, n, glass=True):
    fr = f(name)
    tone = pulse(fr, n, duty=0.25, vibrato=0.35) * 0.55 + sine(fr * 2, n, vibrato=0.2) * 0.25
    if glass:
        tone += bell(fr * 2, n, decay=4.0, bright=0.4) * 0.35
    return tone * env(n, a=0.01, d=0.25, s=0.7, r=0.12)


def nave():
    bus = Bus(NAVE_LOOP, ["lead", "harp", "bass", "pad", "bells", "kick", "snare", "hat", "toms", "fx"])
    rng = np.random.default_rng(5)
    for bar in range(NAVE_BARS):
        t0 = bar * 6 * NAVE_SPE
        ch = NAVE_CHORDS[bar]
        # Lead
        pos = 0
        for dur, name in NAVE_MELODY[bar]:
            if name:
                n = int(dur * NAVE_SPE * SR * 0.96)
                bus.place("lead", t0 + pos * NAVE_SPE, lead_note(name, n) * 0.16)
            pos += dur
        # Harp: an arpeggio on every eighth, up and back (root, 5th, octave, 3rd, 5th, octave).
        order = [0, 2, 3, 1, 2, 3]
        for k in range(6):
            name = tone_of(ch, order[k], 3) if order[k] < 3 else f"{CHORDS[ch][0]}4"
            n = int(1.4 * SR)
            bus.place("harp", t0 + k * NAVE_SPE, harp(f(name), n, bright=0.5) * env(n, a=0.002, d=0.5, s=0.3, r=0.2) * 0.17)
        # Bass on the two big beats (root, then fifth or a passing tone)
        root = midi(CHORDS[ch][0] + "2")
        for k, m in ((0, root), (3, root + 7 if bar % 2 == 0 else root + 12)):
            n = int(2.6 * NAVE_SPE * SR)
            sig = (triangle(hz(m), n) * 0.7 + pulse(hz(m), n, duty=0.5) * 0.15) * env(n, a=0.005, d=0.25, s=0.55, r=0.1)
            bus.place("bass", t0 + k * NAVE_SPE, sig * 0.32)
        # Choir pad: the chord held for the bar
        n = int(6 * NAVE_SPE * SR)
        voices = [f(tone_of(ch, i, 4)) for i in range(3)]
        bus.place("pad", t0, choir(voices, n, vowel="oo") * env(n, a=0.4, d=0.6, s=0.8, r=0.4) * 0.007)
        # Bells: a struck chord tone on the downbeat every other bar
        if bar % 2 == 0:
            n = int(2.5 * SR)
            bus.place("bells", t0, bell(f(tone_of(ch, 2, 5)), n, decay=1.2) * 0.08)
        # Drums: kick on 1, snare on 4, a kick pickup on 6 every other bar, hats on the eighths (accents on 1 and 4).
        bus.place("kick", t0, kick() * 0.55)
        bus.place("snare", t0 + 3 * NAVE_SPE, snare() * 0.32)
        if bar % 2 == 1:
            bus.place("kick", t0 + 5 * NAVE_SPE, kick(punch=0.6) * 0.35)
        for k in range(6):
            bus.place("hat", t0 + k * NAVE_SPE + rng.normal(0, 0.003), hat() * (0.12 if k in (0, 3) else 0.07))
        # Tom fills at the end of each eight-bar phrase
        if bar % 8 == 7:
            for k, fr in ((3, 180.0), (4, 140.0), (5, 105.0)):
                bus.place("toms", t0 + k * NAVE_SPE, tom(fr) * 0.32)
    # Bubbles and a low swell under everything
    bus.tracks["fx"][:NAVE_LOOP] += lowpass(bubbles(NAVE_LOOP, rate=3.0), 3000) * 0.05
    bus.tracks["fx"][NAVE_LOOP:] += bus.tracks["fx"][:NAVE_LOOP]
    t = np.arange(NAVE_LOOP * 2) / SR
    bus.tracks["fx"] += lowpass(noise(NAVE_LOOP * 2), 180) * (0.5 + 0.5 * np.sin(2 * np.pi * t / 9.6)) * 0.35

    lead = reverb(echo(bus.tracks["lead"], 3 * NAVE_SPE, 0.3, 0.25), size=1.3, mix=0.3)
    mix = (lead + reverb(bus.tracks["harp"], 1.2, 0.25) + bus.tracks["bass"] + reverb(bus.tracks["pad"], 1.6, 0.4)
           + reverb(bus.tracks["bells"], 1.6, 0.4) + bus.tracks["kick"] + reverb(bus.tracks["snare"], 0.9, 0.2)
           + bus.tracks["hat"] + reverb(bus.tracks["toms"], 1.0, 0.3) + bus.tracks["fx"])
    return np.tanh(bus.loop_of(mix) * 1.1)


# ======================================================================================================================
# The Canticle of the Precentor (boss): 6/8, then a 3/4 hemiola, then 9/8
# ======================================================================================================================

# (bars, beats per bar, seconds per beat): eighths in A and C, quarters in B (the same bar length as A).
BOSS_SECTIONS = [(8, 6, 60.0 / 225.0), (8, 3, 60.0 / 112.5), (8, 9, 60.0 / 225.0)]
BOSS_START = []
_t = 0.0
for _bars, _bpb, _spb in BOSS_SECTIONS:
    BOSS_START.append(_t)
    _t += _bars * _bpb * _spb
BOSS_LOOP = int(round(_t * SR))

BOSS_CHORDS = [
    ["Dm", "Dm", "Bb", "A", "Dm", "Gm", "Eb", "A7"],
    ["Gm", "Gm", "Dm", "Dm", "Eb", "Bb", "A", "A7"],
    ["Dm", "Dm", "Bb", "C", "Gm", "Eb", "A", "A7"],
]
BOSS_MELODY = [
    [  # A: 6/8, in eighths
        [(3, "D5"), (3, "A5")], [(2, "G5"), (1, "F5"), (3, "E5")], [(3, "F5"), (2, "D5"), (1, "Bb4")], [(6, "C#5")],
        [(3, "D5"), (2, "F5"), (1, "A5")], [(3, "Bb5"), (3, "G5")], [(2, "G5"), (1, "Bb5"), (3, "Eb6")], [(3, "C#6"), (3, "A5")],
    ],
    [  # B: 3/4, in quarters (the hemiola)
        [(1, "G5"), (1, "Bb5"), (1, "D6")], [(2, "C6"), (1, "Bb5")], [(1, "A5"), (1, "F5"), (1, "D5")], [(3, "A5")],
        [(1, "G5"), (1, "Bb5"), (1, "Eb6")], [(2, "D6"), (1, "F5")], [(1, "E5"), (1, "C#5"), (1, "E5")], [(3, "A5")],
    ],
    [  # C: 9/8 (3+3+3), in eighths
        [(3, "D5"), (3, "F5"), (3, "A5")], [(2, "D6"), (1, "C6"), (3, "A5"), (3, "F5")], [(3, "Bb5"), (3, "D6"), (3, "F6")],
        [(3, "E6"), (3, "C6"), (3, "G5")], [(2, "G5"), (1, "Bb5"), (3, "D6"), (3, "G6")], [(3, "Eb6"), (3, "Bb5"), (3, "G5")],
        [(3, "A5"), (3, "C#6"), (3, "E6")], [(3, "G6"), (3, "E6"), (3, "C#6")],
    ],
]
for _s, (_bars, _bpb, _) in enumerate(BOSS_SECTIONS):
    bar_check(BOSS_MELODY[_s], _bpb)


def boss():
    bus = Bus(BOSS_LOOP, ["lead", "organ", "bass", "choir", "bells", "kick", "snare", "hat", "toms", "fx"])
    rng = np.random.default_rng(8)
    for s, (bars, bpb, spb) in enumerate(BOSS_SECTIONS):
        for bar in range(bars):
            t0 = BOSS_START[s] + bar * bpb * spb
            ch = BOSS_CHORDS[s][bar]
            bar_len = bpb * spb
            pos = 0.0
            for dur, name in BOSS_MELODY[s][bar]:
                n = int(dur * spb * SR * 0.95)
                bus.place("lead", t0 + pos * spb, lead_note(name, n, glass=True) * 0.15)
                pos += dur
            # Organ (stacked pulses) holding the chord, with the bass on the section's groups
            n = int(bar_len * SR)
            organ = sum(pulse(f(tone_of(ch, i, 3)), n, duty=0.5) for i in range(3)) / 3.0
            bus.place("organ", t0, lowpass(organ, 1800) * env(n, a=0.05, d=0.4, s=0.75, r=0.1) * 0.08)
            bus.place("choir", t0, choir([f(tone_of(ch, i, 4)) for i in range(3)], n, vowel="ah")
                      * env(n, a=0.25, d=0.5, s=0.8, r=0.3) * 0.008)
            # Groups: A = 3+3 eighths; B = 1+1+1 quarters; C = 3+3+3 eighths.
            groups = [0, 3] if s == 0 else ([0, 1, 2] if s == 1 else [0, 3, 6])
            root = midi(CHORDS[ch][0] + "2")
            for gi, g in enumerate(groups):
                m = root if gi % 2 == 0 else root + 7
                nb = int((3 * spb if s != 1 else spb) * SR * 0.9)
                sig = (triangle(hz(m), nb) * 0.7 + pulse(hz(m), nb, duty=0.33) * 0.2) * env(nb, a=0.004, d=0.2, s=0.5, r=0.08)
                bus.place("bass", t0 + g * spb, sig * 0.34)
                bus.place("kick", t0 + g * spb, kick() * (0.6 if gi == 0 else 0.42))
            # Snare: the last group's start in A and C, beats 2 and 3 in B
            for g in ([3] if s == 0 else [1, 2] if s == 1 else [6]):
                bus.place("snare", t0 + g * spb, snare() * 0.34)
            # Hats on every eighth (B: eighths are half a quarter)
            eighth = spb if s != 1 else spb / 2
            for k in range(int(round(bar_len / eighth))):
                bus.place("hat", t0 + k * eighth + rng.normal(0, 0.003), hat() * (0.12 if k % 3 == 0 else 0.07))
            if bar % 2 == 0:
                bus.place("bells", t0, bell(f(tone_of(ch, 0, 5)), int(2.0 * SR), decay=1.4) * 0.09)
            if bar == bars - 1:
                # A drum fill into the next section (it changes meter there)
                fill = [(bpb - 3) * spb, (bpb - 2) * spb, (bpb - 1) * spb] if s != 1 else [2 * spb, 2.5 * spb]
                for i, at in enumerate(fill):
                    bus.place("toms", t0 + at, tom(200.0 - i * 40.0) * 0.36)
    bus.tracks["fx"][:BOSS_LOOP] += lowpass(bubbles(BOSS_LOOP, rate=2.0), 2500) * 0.04
    bus.tracks["fx"][BOSS_LOOP:] += bus.tracks["fx"][:BOSS_LOOP]
    lead = reverb(echo(bus.tracks["lead"], 0.4, 0.25, 0.2), size=1.4, mix=0.3)
    mix = (lead + reverb(bus.tracks["organ"], 1.6, 0.35) + bus.tracks["bass"] + reverb(bus.tracks["choir"], 1.8, 0.45)
           + reverb(bus.tracks["bells"], 1.6, 0.4) + bus.tracks["kick"] + reverb(bus.tracks["snare"], 1.0, 0.25)
           + bus.tracks["hat"] + reverb(bus.tracks["toms"], 1.1, 0.3) + bus.tracks["fx"])
    return np.tanh(bus.loop_of(mix) * 1.1)


# ======================================================================================================================
# The Hush of the Air Bell (hub), with three verse layers
# ======================================================================================================================

HUB_SPE = 60.0 / 150.0                    # 0.4 s per eighth; 2.4 s a bar
HUB_BARS = 16
HUB_LOOP = int(round(HUB_BARS * 6 * HUB_SPE * SR))
HUB_CHORDS = ["F", "F", "Bbmaj", "C", "Dm", "Am", "Bb", "C", "F", "F", "Bbmaj", "Gm", "Dm", "Bb", "C", "F"]
CHORDS.update({"Gm": ["G", "Bb", "D"]})
HUB_MELODY = [
    [(3, "C5"), (2, "A4"), (1, "C5")], [(3, "F5"), (3, "E5")], [(2, "D5"), (1, "F5"), (3, "A5")], [(6, "G5")],
    [(3, "F5"), (2, "E5"), (1, "D5")], [(3, "C5"), (3, "E5")], [(2, "D5"), (1, "C5"), (3, "Bb4")], [(6, "C5")],
    [(3, "C5"), (2, "A4"), (1, "C5")], [(3, "F5"), (2, "G5"), (1, "A5")], [(3, "Bb5"), (2, "A5"), (1, "G5")], [(6, "G5")],
    [(3, "A5"), (2, "F5"), (1, "D5")], [(3, "D5"), (2, "F5"), (1, "Bb5")], [(3, "A5"), (3, "G5")], [(6, "F5")],
]
bar_check(HUB_MELODY, 6)
assert len(HUB_CHORDS) == HUB_BARS == len(HUB_MELODY)


def hub():
    bus = Bus(HUB_LOOP, ["box", "pad", "bass", "fx", "v1", "v2", "v3"])
    for bar in range(HUB_BARS):
        t0 = bar * 6 * HUB_SPE
        ch = HUB_CHORDS[bar]
        pos = 0
        for dur, name in HUB_MELODY[bar]:
            n = int(max(2.0, dur * HUB_SPE) * SR)
            bus.place("box", t0 + pos * HUB_SPE, bell(f(name), n, decay=2.2, bright=0.55) * 0.18)
            pos += dur
        n = int(6 * HUB_SPE * SR)
        pad = sum(sine(f(tone_of(ch, i, 3)) * d, n) for i in range(3) for d in (0.997, 1.003))
        bus.place("pad", t0, pad * env(n, a=0.8, d=1.0, s=0.8, r=0.6) * 0.03)
        root = midi(CHORDS[ch][0] + "2")
        n = int(5.5 * HUB_SPE * SR)
        bus.place("bass", t0, triangle(hz(root), n) * env(n, a=0.05, d=0.8, s=0.5, r=0.4) * 0.22)
        # Verse 1: a harp rocking on the eighths
        for k, i in enumerate([0, 1, 2, 3, 2, 1]):
            name = tone_of(ch, i, 3)
            n = int(1.6 * SR)
            bus.place("v1", t0 + k * HUB_SPE, harp(f(name), n, bright=0.4) * env(n, a=0.002, d=0.6, s=0.3, r=0.3) * 0.15)
        # Verse 2: glass bells answering on the second big beat
        for k, i in ((3, 2), (4, 1), (5, 0)):
            name = tone_of(ch, i, 5)
            bus.place("v2", t0 + k * HUB_SPE, bell(f(name), int(2.2 * SR), decay=1.8, bright=0.3) * 0.07)
        # Verse 3: a choir on the chord
        n = int(6 * HUB_SPE * SR)
        bus.place("v3", t0, choir([f(tone_of(ch, i, 4)) for i in range(3)], n, vowel="ah") * env(n, a=0.7, d=0.8, s=0.8, r=0.6) * 0.018)
    t = np.arange(HUB_LOOP * 2) / SR
    bus.tracks["fx"] += lowpass(noise(HUB_LOOP * 2), 140) * (0.5 + 0.5 * np.sin(2 * np.pi * t / 19.2)) * 0.4
    b = lowpass(bubbles(HUB_LOOP, rate=1.2), 2500) * 0.05
    bus.tracks["fx"][:HUB_LOOP] += b
    bus.tracks["fx"][HUB_LOOP:] += b
    base = (reverb(bus.tracks["box"], 1.5, 0.35) + reverb(bus.tracks["pad"], 1.8, 0.4) + bus.tracks["bass"] + bus.tracks["fx"])
    layers = [reverb(bus.tracks["v1"], 1.4, 0.3), reverb(echo(bus.tracks["v2"], 3 * HUB_SPE, 0.3, 0.3), 1.6, 0.4),
              reverb(bus.tracks["v3"], 1.8, 0.45)]
    return np.tanh(bus.loop_of(base) * 1.05), [np.tanh(bus.loop_of(l) * 1.05) for l in layers]


def main(out_dir):
    os.makedirs(out_dir, exist_ok=True)
    write(os.path.join(out_dir, "Barcarolle_of_the_Drowned_Nave.wav"), nave(), peak=0.72)
    write(os.path.join(out_dir, "Canticle_of_the_Precentor.wav"), boss(), peak=0.72)
    base, layers = hub()
    gain = write(os.path.join(out_dir, "Hush_of_the_Air_Bell.wav"), base, peak=0.6)
    for i, l in enumerate(layers, start=1):
        write(os.path.join(out_dir, f"Hush_of_the_Air_Bell_Verse{i}.wav"), l, gain=gain)
    print(f"loops: nave {NAVE_LOOP} ({NAVE_LOOP / SR:.3f} s), boss {BOSS_LOOP} ({BOSS_LOOP / SR:.3f} s), hub {HUB_LOOP} ({HUB_LOOP / SR:.3f} s)")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "Assets/_Project/Audio/Generated")
