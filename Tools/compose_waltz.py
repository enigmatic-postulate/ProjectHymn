#!/usr/bin/env python3
"""
Renders "Waltz of the Belfry", an original placeholder battle waltz for the Belfry Ridge stage (VS-2), in the style
of a 16-bit JRPG battle theme: pulse-wave lead with vibrato and echo, harp arpeggios, oom-pah-pah chord stabs, a
triangle bass and noise drums. 3/4 at 168 BPM in D minor, 32 bars, and exactly one tempo map long so it loops on the
Conductor's beat grid (beat 0 = sample 0).

    python3 Tools/compose_waltz.py [out.wav]

Placeholder only (the soundtrack is commissioned at the end of the cycle). Written for Project Hymn; no samples used.
"""
import sys
import wave

import numpy as np

SR = 44100
BPM = 168.0
BEATS_PER_BAR = 3
BARS = 32
SPB = 60.0 / BPM
LOOP_BEATS = BARS * BEATS_PER_BAR
LOOP = int(round(LOOP_BEATS * SPB * SR))          # 1,512,000 samples = 34.2857 s

NOTE = {"C": 0, "C#": 1, "Db": 1, "D": 2, "D#": 3, "Eb": 3, "E": 4, "F": 5, "F#": 6, "Gb": 6, "G": 7, "G#": 8,
        "Ab": 8, "A": 9, "A#": 10, "Bb": 10, "B": 11}


def midi(name):
    pitch, octave = name[:-1], int(name[-1])
    return 12 * (octave + 1) + NOTE[pitch]


def hz(m):
    return 440.0 * 2.0 ** ((m - 69) / 12.0)


# ---------------- The tune ----------------

CHORDS = (["Dm", "Dm", "Gm", "Gm", "C", "C", "F", "A7"] +
          ["Dm", "Dm", "Gm", "Gm", "Bb", "Gm", "A", "A7"] +
          ["F", "F", "C", "C", "Bb", "Bb", "A", "A7"] +
          ["Dm", "Dm", "Gm", "Gm", "Bb", "A7", "Dm", "A7"])

CHORD_TONES = {
    "Dm": ["D", "F", "A"], "Gm": ["G", "Bb", "D"], "C": ["C", "E", "G"], "F": ["F", "A", "C"],
    "A7": ["A", "C#", "E", "G"], "A": ["A", "C#", "E"], "Bb": ["Bb", "D", "F"],
}

MELODY = [
    # A
    [(1, "A4"), (1, "D5"), (1, "F5")], [(2, "E5"), (0.5, "D5"), (0.5, "E5")],
    [(1, "F5"), (1, "G5"), (1, "A5")], [(2, "Bb5"), (1, "A5")],
    [(1, "G5"), (0.5, "F5"), (0.5, "E5"), (1, "G5")], [(2, "E5"), (1, "C5")],
    [(1, "F5"), (1, "A5"), (1, "C6")], [(1.5, "C#6"), (0.5, "B5"), (0.5, "A5"), (0.5, "G5")],
    # A'
    [(1, "A5"), (1, "D6"), (1, "A5")], [(1, "F5"), (1, "D5"), (1, "A4")],
    [(1, "Bb4"), (1, "D5"), (1, "G5")], [(2, "Bb5"), (0.5, "A5"), (0.5, "G5")],
    [(1, "F5"), (1, "D5"), (1, "F5")], [(1, "G5"), (1, "Bb5"), (1, "D6")],
    [(2, "C#6"), (1, "E6")], [(0.5, "E6"), (0.5, "C#6"), (0.5, "A5"), (0.5, "G5"), (0.5, "E5"), (0.5, "C#5")],
    # B (brighter)
    [(1, "C5"), (1, "F5"), (1, "A5")], [(2, "C6"), (1, "A5")],
    [(1, "G5"), (1, "C6"), (1, "E6")], [(2, "D6"), (1, "C6")],
    [(1, "Bb5"), (1, "D6"), (1, "F6")], [(1.5, "E6"), (0.5, "D6"), (1, "C6")],
    [(1, "C#6"), (1, "E6"), (1, "A6")], [(2, "G6"), (0.5, "F6"), (0.5, "E6")],
    # A'' (climax)
    [(1, "D6"), (1, "A5"), (1, "F5")], [(0.5, "E5"), (0.5, "F5"), (0.5, "G5"), (0.5, "A5"), (1, "D6")],
    [(1, "G6"), (1, "D6"), (1, "Bb5")], [(0.5, "A5"), (0.5, "Bb5"), (0.5, "C6"), (0.5, "D6"), (1, "G6")],
    [(1, "F6"), (1, "D6"), (1, "Bb5")], [(1, "C#6"), (1, "E6"), (1, "G6")],
    [(2, "F6"), (1, "D6")], [(1, "A5"), (1, "C#6"), (1, "E6")],
]
assert len(MELODY) == BARS and len(CHORDS) == BARS
for i, bar in enumerate(MELODY):
    assert abs(sum(d for d, _ in bar) - BEATS_PER_BAR) < 1e-9, f"bar {i + 1}"


# ---------------- Synthesis ----------------

def poly_blep(t, dt):
    dt = np.broadcast_to(dt, t.shape)
    out = np.zeros_like(t)
    a = t < dt
    x = t[a] / dt[a]
    out[a] = x + x - x * x - 1.0
    b = t > 1.0 - dt
    x = (t[b] - 1.0) / dt[b]
    out[b] = x * x + x + x + 1.0
    return out


def pulse(freq, n, duty=0.5, vibrato=0.0, vib_delay=0.15):
    t = np.arange(n) / SR
    f = np.full(n, freq)
    if vibrato > 0:
        depth = np.clip((t - vib_delay) / 0.2, 0, 1) * vibrato
        f = f * 2.0 ** (depth * np.sin(2 * np.pi * 5.6 * t) / 12.0)
    dt = f / SR
    phase = np.cumsum(dt) % 1.0
    sq = np.where(phase < duty, 1.0, -1.0)
    sq += poly_blep(phase, dt)
    sq -= poly_blep((phase - duty) % 1.0, dt)
    return sq


def triangle(freq, n):
    phase = (np.arange(n) * freq / SR) % 1.0
    return 4.0 * np.abs(phase - 0.5) - 1.0


def env(n, a=0.005, d=0.08, s=0.6, r=0.06, hold=None):
    """ADSR over n samples; the release starts at `hold` samples (default: n - release)."""
    t = np.arange(n) / SR
    rel = int(r * SR)
    hold = n - rel if hold is None else hold
    e = np.where(t < a, t / max(a, 1e-6), s + (1 - s) * np.exp(-(t - a) / max(d, 1e-6)))
    if hold < n:
        k = np.arange(n - hold) / max(rel, 1)
        e[hold:] *= np.clip(1.0 - k, 0, 1)
    return e


rng = np.random.default_rng(3)


def noise(n):
    return rng.uniform(-1, 1, n)


TRACKS = {k: np.zeros(LOOP * 2) for k in ("lead", "arp", "stab", "bass", "drums", "pad")}


def place(track, beat, sig):
    start = int(round(beat * SPB * SR))
    for rep in (0, 1):                   # two loops back to back, so tails wrap into the start
        s = start + rep * LOOP
        e = min(s + len(sig), LOOP * 2)
        TRACKS[track][s:e] += sig[: e - s]


def render():
    for bar in range(BARS):
        b0 = bar * BEATS_PER_BAR
        chord = CHORDS[bar]
        tones = CHORD_TONES[chord]
        root = tones[0]
        climax = bar >= 24

        # Lead: pulse 50% (25% in the B section), vibrato, legato-ish.
        pos = 0.0
        for dur, name in MELODY[bar]:
            n = int(dur * SPB * SR * 0.96)
            sig = pulse(hz(midi(name)), n, duty=0.25 if 16 <= bar < 24 else 0.5, vibrato=0.35 if dur >= 1 else 0.0)
            sig *= env(n, a=0.006, d=0.12, s=0.75, r=0.05)
            place("lead", b0 + pos, sig * (0.25 if climax else 0.22))
            if climax:                                     # an octave-down double for weight
                place("lead", b0 + pos, pulse(hz(midi(name) - 12), n, duty=0.125) * env(n, s=0.7) * 0.1)
            pos += dur

        # Harp arpeggio: eighth notes up and down the chord, octave 4-5.
        arp = [f"{tones[0]}4", f"{tones[1]}4", f"{tones[2]}4", f"{tones[0]}5", f"{tones[2]}4", f"{tones[1]}4"]
        for i, name in enumerate(arp):
            m = midi(name)
            if m < midi("G3"):
                m += 12
            n = int(0.5 * SPB * SR * 1.6)
            sig = pulse(hz(m), n, duty=0.125) * env(n, a=0.002, d=0.09, s=0.0, r=0.02)
            place("arp", b0 + 0.5 * i, sig * 0.09)

        # Oom-pah-pah: bass on 1, chord stabs on 2 and 3.
        bm = midi(f"{root}2")
        if bm < midi("C2"):
            bm += 12
        n = int(1.2 * SPB * SR)
        place("bass", b0, triangle(hz(bm), n) * env(n, a=0.004, d=0.25, s=0.55, r=0.08) * 0.42)
        fifth = midi(f"{tones[2]}2")
        if fifth < bm:
            fifth += 12
        n2 = int(0.45 * SPB * SR)
        place("bass", b0 + 2.5, triangle(hz(fifth), n2) * env(n2, s=0.4) * 0.3)
        for beat in (1, 2):
            n = int(0.32 * SPB * SR)
            stab = sum(pulse(hz(midi(f"{t}4") if midi(f"{t}4") >= midi("F4") else midi(f"{t}5")), n, duty=0.25) for t in tones[:3])
            place("stab", b0 + beat, stab * env(n, a=0.002, d=0.05, s=0.3, r=0.03) * 0.075)

        # Pad: soft sustained chord (two detuned pulses, low duty) for body.
        n = int(3 * SPB * SR)
        pad = sum(pulse(hz(midi(f"{t}4")) * dt, n, duty=0.5) for t in tones[:3] for dt in (0.997, 1.003))
        place("pad", b0, pad * env(n, a=0.25, d=0.4, s=0.7, r=0.3) * 0.018)

        # Drums: kick on 1, snare on 2 and 3 (lighter on 3), closed hat on the off-beats, crash on section starts.
        def kick():
            n = int(0.25 * SR)
            t = np.arange(n) / SR
            f = 120 * np.exp(-t * 28) + 45
            return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 14)

        def snare(level):
            n = int(0.18 * SR)
            t = np.arange(n) / SR
            body = np.sin(2 * np.pi * 190 * t) * np.exp(-t * 30) * 0.5
            hiss = noise(n) * np.exp(-t * 22)
            return (body + hiss) * level

        def hat():
            n = int(0.05 * SR)
            x = noise(n)
            x = np.diff(np.concatenate([[0], x]))       # crude high-pass
            return x * np.exp(-np.arange(n) / SR * 90) * 0.18

        place("drums", b0, kick() * 0.75)
        place("drums", b0 + 1, snare(0.32))
        place("drums", b0 + 2, snare(0.22 if not climax else 0.3))
        for off in (0.5, 1.5, 2.5):
            place("drums", b0 + off, hat())
        if bar % 8 == 0:
            n = int(1.6 * SR)
            x = np.diff(np.concatenate([[0], noise(n)]))
            place("drums", b0, x * np.exp(-np.arange(n) / SR * 2.6) * 0.16)


def echo(x, delay_beats=0.75, feedback=0.33, mix=0.28):
    d = int(round(delay_beats * SPB * SR))
    y = x.copy()
    for i in range(d, len(y), d):
        y[i:i + d] += y[i - d:i][: len(y[i:i + d])] * feedback
    return x * (1 - mix) + y * mix


def main(out):
    render()
    lead = echo(TRACKS["lead"])
    arp = echo(TRACKS["arp"], 0.5, 0.25, 0.2)
    mix = lead + arp + TRACKS["stab"] + TRACKS["bass"] + TRACKS["drums"] + TRACKS["pad"]
    loop = mix[LOOP:2 * LOOP]                      # the second pass: the first loop's tails wrapped into its start
    loop = np.tanh(loop * 1.1)                    # gentle glue
    loop *= 0.89 / np.max(np.abs(loop))           # about -1 dBFS
    pcm = (loop * 32767).astype(np.int16)
    with wave.open(out, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    print(f"{out}: {len(pcm)} samples, {len(pcm) / SR:.4f} s, {BARS} bars of 3/4 at {BPM:g} BPM")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "Waltz_of_the_Belfry.wav")
