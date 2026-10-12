#!/usr/bin/env python3
"""
Renders "Evening in the Village", an original placeholder loop for the village hub (VS-4): a calm 16-bit style
tune over the sounds of an evening street. A soft flute-like lead, a plucked lute arpeggio, a warm bass, no drums,
and birdsong and a breeze baked in. 3/4 at 88 BPM in D major, 24 bars, looping seamlessly.

    python3 Tools/compose_village.py [out.wav]

Placeholder only. Written for Project Hymn; no samples used.
"""
import sys
import wave

import numpy as np

SR = 44100
BPM = 88.0
BEATS_PER_BAR = 3
BARS = 24
SPB = 60.0 / BPM
LOOP_BEATS = BARS * BEATS_PER_BAR
LOOP = int(round(LOOP_BEATS * SPB * SR))          # 1,693,440 samples = 38.4 s

NOTE = {"C": 0, "C#": 1, "Db": 1, "D": 2, "D#": 3, "Eb": 3, "E": 4, "F": 5, "F#": 6, "Gb": 6, "G": 7, "G#": 8,
        "Ab": 8, "A": 9, "A#": 10, "Bb": 10, "B": 11}


def midi(name):
    pitch, octave = name[:-1], int(name[-1])
    return 12 * (octave + 1) + NOTE[pitch]


def hz(m):
    return 440.0 * 2.0 ** ((m - 69) / 12.0)


# ---------------- The tune ----------------

CHORDS = (["D", "Bm", "G", "A"] * 2 + ["G", "D", "Em", "A", "G", "D", "Em", "A"] + ["D", "Bm", "G", "A", "G", "A", "D", "D"])

CHORD_TONES = {
    "D": ["D", "F#", "A"], "Bm": ["B", "D", "F#"], "G": ["G", "B", "D"], "A": ["A", "C#", "E"], "Em": ["E", "G", "B"],
}

MELODY = [
    [(2, "F#5"), (1, "E5")], [(2, "D5"), (1, "F#5")], [(1, "G5"), (1, "B5"), (1, "A5")], [(3, "E5")],
    [(2, "F#5"), (1, "A5")], [(2, "B5"), (1, "A5")], [(1, "G5"), (1, "F#5"), (1, "E5")], [(3, "E5")],
    [(2, "D5"), (1, "B4")], [(2, "A4"), (1, "D5")], [(1, "E5"), (1, "G5"), (1, "F#5")], [(3, "E5")],
    [(2, "B4"), (1, "D5")], [(2, "F#5"), (1, "A5")], [(1, "G5"), (1, "E5"), (1, "C#5")], [(3, "E5")],
    [(2, "A5"), (1, "F#5")], [(2, "D5"), (1, "B4")], [(1, "D5"), (1, "G5"), (1, "B5")], [(3, "A5")],
    [(1, "B5"), (1, "A5"), (1, "G5")], [(2, "E5"), (1, "C#5")], [(3, "D5")], [(3, "D5")],
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


TRACKS = {k: np.zeros(LOOP * 2) for k in ("lead", "stab", "bass", "drums", "pad")}


def place(track, beat, sig):
    start = int(round(beat * SPB * SR))
    for rep in (0, 1):                   # two loops back to back, so tails wrap into the start
        s = start + rep * LOOP
        e = min(s + len(sig), LOOP * 2)
        TRACKS[track][s:e] += sig[: e - s]


def render():
    rng2 = np.random.default_rng(9)
    for bar in range(BARS):
        b0 = bar * BEATS_PER_BAR
        tones = CHORD_TONES[CHORDS[bar]]
        pos = 0.0
        for dur, name in MELODY[bar]:
            n = int(dur * SPB * SR * 0.95)
            f = hz(midi(name))
            t = np.arange(n) / SR
            vib = 1 + 0.004 * np.sin(2 * np.pi * 5.2 * t) * np.clip((t - 0.2) / 0.3, 0, 1)
            tone = np.sin(2 * np.pi * f * np.cumsum(vib) / SR) + 0.25 * np.sin(4 * np.pi * f * np.cumsum(vib) / SR)
            breath = noise(n) * 0.02
            place("lead", b0 + pos, (tone + breath) * env(n, a=0.06, d=0.3, s=0.75, r=0.12) * 0.15)
            pos += dur
        # Lute: plucked arpeggio on the eighths.
        arp = [tones[0] + "3", tones[1] + "3", tones[2] + "3", tones[0] + "4", tones[2] + "3", tones[1] + "3"]
        for k, name in enumerate(arp):
            m = midi(name)
            if m < midi("D3"):
                m += 12
            n = int(1.2 * SPB * SR)
            sig = pulse(hz(m), n, duty=0.18) * env(n, a=0.002, d=0.12, s=0.0, r=0.05)
            place("stab", b0 + 0.5 * k, sig * 0.06)
        root = midi(tones[0] + "2")
        n = int(2.6 * SPB * SR)
        place("bass", b0, triangle(hz(root), n) * env(n, a=0.02, d=0.6, s=0.4, r=0.2) * 0.3)
        n = int(3 * SPB * SR)
        pad = sum(pulse(hz(midi(t2 + "3")) * d, n, duty=0.5) for t2 in tones for d in (0.996, 1.004))
        place("pad", b0, pad * env(n, a=0.5, d=0.6, s=0.7, r=0.5) * 0.012)
        # Birdsong now and then: two or three quick chirps.
        if rng2.random() < 0.45:
            start = b0 + rng2.random() * 2.5
            base = 2400 + rng2.random() * 1600
            for c in range(int(rng2.integers(2, 4))):
                n = int(0.07 * SR)
                t = np.arange(n) / SR
                f = base * (1 + 0.25 * np.sin(np.pi * t / 0.07))
                chirp = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.sin(np.pi * t / 0.07)
                place("drums", start + c * 0.18, chirp * 0.035)
    # A soft breeze under everything: filtered noise that swells and falls.
    n = LOOP
    b = noise(n)
    lp = np.zeros(n)
    acc = 0.0
    for i in range(0, n):
        acc += 0.01 * (b[i] - acc)
        lp[i] = acc
    swell = 0.5 + 0.5 * np.sin(np.arange(n) / SR * 2 * np.pi / (LOOP / SR / 3))
    TRACKS["pad"][LOOP:2 * LOOP] += lp * swell * 0.25
    TRACKS["pad"][:LOOP] += lp * swell * 0.25


def echo(x, delay_beats=0.75, feedback=0.33, mix=0.28):
    d = int(round(delay_beats * SPB * SR))
    y = x.copy()
    for i in range(d, len(y), d):
        y[i:i + d] += y[i - d:i][: len(y[i:i + d])] * feedback
    return x * (1 - mix) + y * mix


def main(out):
    render()
    lead = echo(TRACKS["lead"], 1.5, 0.3, 0.25)
    mix = lead + TRACKS["stab"] + TRACKS["bass"] + TRACKS["drums"] + TRACKS["pad"]
    loop = mix[LOOP:2 * LOOP]
    loop = np.tanh(loop * 1.05)
    loop *= 0.7 / np.max(np.abs(loop))
    pcm = (loop * 32767).astype(np.int16)
    with wave.open(out, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    print(f"{out}: {len(pcm)} samples, {len(pcm) / SR:.4f} s, {BARS} bars of 3/4 at {BPM:g} BPM")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "Evening_in_the_Village.wav")
