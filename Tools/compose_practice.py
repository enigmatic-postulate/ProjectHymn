#!/usr/bin/env python3
"""
Renders "Practice in the Yard", an original placeholder practice loop for the Practice Yard (Slice 10): a calm 16-bit
style tune with the beat made obvious. A soft kick on every beat (four on the floor), a rim click on 2 and 4, a walking
triangle bass on the beats and a gentle pulse-wave melody that mostly moves on the beat. 4/4 at 100 BPM in G major,
16 bars, exactly one tempo map long so it loops on the Conductor's beat grid (beat 0 = sample 0).

    python3 Tools/compose_practice.py [out.wav]

Placeholder only. Written for Project Hymn; no samples used.
"""
import sys
import wave

import numpy as np

SR = 44100
BPM = 100.0
BEATS_PER_BAR = 4
BARS = 16
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

CHORDS = (["G", "Em", "C", "D"] * 2 + ["C", "D", "Bm", "Em", "C", "D", "G", "D"])

CHORD_TONES = {
    "G": ["G", "B", "D"], "Em": ["E", "G", "B"], "C": ["C", "E", "G"], "D": ["D", "F#", "A"], "Bm": ["B", "D", "F#"],
}

MELODY = [
    # A: quarter notes on the beat, easy to follow
    [(1, "D5"), (1, "B4"), (1, "G4"), (1, "B4")], [(1, "E5"), (1, "B4"), (2, "G4")],
    [(1, "E5"), (1, "G5"), (1, "E5"), (1, "C5")], [(2, "D5"), (2, "A4")],
    [(1, "D5"), (1, "B4"), (1, "G4"), (1, "B4")], [(1, "E5"), (1, "G5"), (2, "B5")],
    [(1, "A5"), (1, "G5"), (1, "E5"), (1, "C5")], [(3, "D5"), (1, "F#4")],
    # B: a little more movement
    [(1, "G4"), (1, "C5"), (1, "E5"), (1, "G5")], [(1, "F#5"), (1, "A5"), (2, "D5")],
    [(1, "D5"), (1, "F#5"), (1, "B5"), (1, "A5")], [(2, "G5"), (1, "E5"), (1, "B4")],
    [(1, "C5"), (1, "E5"), (1, "G5"), (1, "E5")], [(1, "F#5"), (1, "A5"), (1, "D6"), (1, "C6")],
    [(2, "B5"), (1, "G5"), (1, "D5")], [(2, "A4"), (1, "F#4"), (1, "A4")],
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
    def kick():
        n = int(0.22 * SR)
        t = np.arange(n) / SR
        f = 110 * np.exp(-t * 30) + 48
        return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 16)

    def rim():
        n = int(0.06 * SR)
        t = np.arange(n) / SR
        return (np.sin(2 * np.pi * 820 * t) * 0.6 + noise(n) * 0.4) * np.exp(-t * 70)

    def hat():
        n = int(0.04 * SR)
        x = np.diff(np.concatenate([[0], noise(n)]))
        return x * np.exp(-np.arange(n) / SR * 110) * 0.1

    for bar in range(BARS):
        b0 = bar * BEATS_PER_BAR
        tones = CHORD_TONES[CHORDS[bar]]

        pos = 0.0
        for dur, name in MELODY[bar]:
            n = int(dur * SPB * SR * 0.9)
            sig = pulse(hz(midi(name)), n, duty=0.25, vibrato=0.25 if dur >= 2 else 0.0, vib_delay=0.3)
            sig *= env(n, a=0.008, d=0.18, s=0.55, r=0.08)
            place("lead", b0 + pos, sig * 0.16)
            pos += dur

        # Walking bass on every beat: root, fifth, octave, fifth.
        root = midi(f"{tones[0]}2")
        if root < midi("E2"):
            root += 12
        for beat, step in enumerate((0, 7, 12, 7)):
            n = int(0.8 * SPB * SR)
            place("bass", b0 + beat, triangle(hz(root + step), n) * env(n, a=0.004, d=0.2, s=0.5, r=0.06) * 0.36)

        # Soft chord on the off-beats (eighths after 1, 2, 3, 4), quiet so the beats stay clear.
        for beat in range(4):
            n = int(0.22 * SPB * SR)
            stab = sum(pulse(hz(midi(f"{t}4")), n, duty=0.125) for t in tones)
            place("stab", b0 + beat + 0.5, stab * env(n, a=0.002, d=0.05, s=0.2, r=0.03) * 0.04)

        n = int(4 * SPB * SR)
        pad = sum(pulse(hz(midi(f"{t}3")) * d, n, duty=0.5) for t in tones for d in (0.997, 1.003))
        place("pad", b0, pad * env(n, a=0.4, d=0.6, s=0.7, r=0.4) * 0.014)

        # The beat, made obvious: a kick on every beat (louder on 1), a rim click on 2 and 4, hats on the off-beats.
        for beat in range(4):
            place("drums", b0 + beat, kick() * (0.85 if beat == 0 else 0.6))
            place("drums", b0 + beat + 0.5, hat())
        for beat in (1, 3):
            place("drums", b0 + beat, rim() * 0.22)


def echo(x, delay_beats=0.75, feedback=0.33, mix=0.28):
    d = int(round(delay_beats * SPB * SR))
    y = x.copy()
    for i in range(d, len(y), d):
        y[i:i + d] += y[i - d:i][: len(y[i:i + d])] * feedback
    return x * (1 - mix) + y * mix


def main(out):
    render()
    lead = echo(TRACKS["lead"], 0.75, 0.25, 0.2)
    mix = lead + TRACKS["stab"] + TRACKS["bass"] + TRACKS["drums"] + TRACKS["pad"]
    loop = mix[LOOP:2 * LOOP]
    loop = np.tanh(loop * 1.05)
    loop *= 0.85 / np.max(np.abs(loop))
    pcm = (loop * 32767).astype(np.int16)
    with wave.open(out, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    print(f"{out}: {len(pcm)} samples, {len(pcm) / SR:.4f} s, {BARS} bars of 4/4 at {BPM:g} BPM")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "Practice_in_the_Yard.wav")
