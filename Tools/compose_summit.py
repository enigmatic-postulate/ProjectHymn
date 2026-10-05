#!/usr/bin/env python3
"""
Renders "Hymn of the Summit", an original placeholder boss theme for the Summit (VS-3), in the style of a 16-bit JRPG
boss battle. It changes meter the way the Bone Cantor fights:

    A  8 bars of 4/4 at 150 BPM   (C minor, driving eighth-note bass, backbeat)
    B  8 bars of 3/4 at 150 BPM   (F minor, a grim waltz)
    C  8 bars of 7/8, eighth = 300 (C minor / Db, grouped 2+2+3)

then loops. The quarter note is 0.4 s in A and B and the eighth 0.2 s in C, so the pulse never changes speed, only the
grouping. Exactly one tempo map long (33.6 s; beat 0 = sample 0), matching the SummitTheme song asset.

    python3 Tools/compose_summit.py [out.wav]

Placeholder only (the soundtrack is commissioned at the end of the cycle). Written for Project Hymn; no samples used.
"""
import sys
import wave

import numpy as np

SR = 44100

# (bars, beats per bar, seconds per beat)
SECTIONS = [(8, 4, 0.4), (8, 3, 0.4), (8, 7, 0.2)]
SECTION_START = []
_t = 0.0
for bars, bpb, spb in SECTIONS:
    SECTION_START.append(_t)
    _t += bars * bpb * spb
LOOP_SECONDS = _t
LOOP = int(round(LOOP_SECONDS * SR))                 # 1,481,760 samples

NOTE = {"C": 0, "C#": 1, "Db": 1, "D": 2, "D#": 3, "Eb": 3, "E": 4, "F": 5, "F#": 6, "Gb": 6, "G": 7, "G#": 8,
        "Ab": 8, "A": 9, "A#": 10, "Bb": 10, "B": 11}


def midi(name):
    pitch, octave = name[:-1], int(name[-1])
    return 12 * (octave + 1) + NOTE[pitch]


def hz(m):
    return 440.0 * 2.0 ** ((m - 69) / 12.0)


CHORD_TONES = {
    "Cm": ["C", "Eb", "G"], "Ab": ["Ab", "C", "Eb"], "Bb": ["Bb", "D", "F"], "G": ["G", "B", "D"],
    "Fm": ["F", "Ab", "C"], "Db": ["Db", "F", "Ab"], "G7": ["G", "B", "D", "F"],
}

CHORDS = [
    ["Cm", "Cm", "Ab", "Ab", "Bb", "Bb", "G", "G"],
    ["Fm", "Fm", "Cm", "Cm", "Ab", "Bb", "G", "G"],
    ["Cm", "Cm", "Db", "Db", "Ab", "Bb", "G", "G7"],
]

# Durations in the section's own beats (quarters in A and B, eighths in C).
MELODY = [
    [
        [(1, "G4"), (1, "C5"), (1, "Eb5"), (1, "G5")], [(3, "F5"), (0.5, "Eb5"), (0.5, "D5")],
        [(1, "Eb5"), (1, "C5"), (1, "Ab4"), (1, "C5")], [(2, "Eb5"), (2, "Ab5")],
        [(1, "F5"), (1, "D5"), (1, "Bb4"), (1, "D5")], [(2, "F5"), (1, "G5"), (1, "Ab5")],
        [(1.5, "G5"), (0.5, "F5"), (1, "Eb5"), (1, "D5")], [(2, "B4"), (1, "D5"), (1, "G5")],
    ],
    [
        [(1, "C5"), (1, "F5"), (1, "Ab5")], [(2, "G5"), (1, "F5")],
        [(1, "Eb5"), (1, "G5"), (1, "C6")], [(2, "Bb5"), (1, "Ab5")],
        [(1, "Ab5"), (1, "C6"), (1, "Eb6")], [(2, "D6"), (1, "F5")],
        [(1, "G5"), (1, "B5"), (1, "D6")], [(2, "F6"), (1, "D6")],
    ],
    [
        [(2, "C5"), (2, "Eb5"), (3, "G5")], [(2, "Ab5"), (2, "G5"), (3, "Eb5")],
        [(2, "Db5"), (2, "F5"), (3, "Ab5")], [(2, "C6"), (2, "Bb5"), (3, "Ab5")],
        [(2, "C6"), (2, "Eb6"), (3, "C6")], [(2, "D6"), (2, "F6"), (3, "D6")],
        [(2, "B5"), (2, "D6"), (3, "G6")], [(2, "F6"), (2, "D6"), (1, "B5"), (1, "G5"), (1, "F5")],
    ],
]
for s, (bars, bpb, _) in enumerate(SECTIONS):
    assert len(MELODY[s]) == bars and len(CHORDS[s]) == bars
    for i, bar in enumerate(MELODY[s]):
        assert abs(sum(d for d, _ in bar) - bpb) < 1e-9, f"section {s} bar {i + 1}"


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
        f = f * 2.0 ** (depth * np.sin(2 * np.pi * 5.8 * t) / 12.0)
    dt = f / SR
    phase = np.cumsum(dt) % 1.0
    sq = np.where(phase < duty, 1.0, -1.0)
    sq += poly_blep(phase, dt)
    sq -= poly_blep((phase - duty) % 1.0, dt)
    return sq


def saw(freq, n):
    dt = freq / SR
    phase = (np.arange(n) * dt) % 1.0
    return 2.0 * phase - 1.0 - poly_blep(phase, np.full(n, dt))


def triangle(freq, n):
    phase = (np.arange(n) * freq / SR) % 1.0
    return 4.0 * np.abs(phase - 0.5) - 1.0


def env(n, a=0.005, d=0.08, s=0.6, r=0.06):
    t = np.arange(n) / SR
    rel = int(r * SR)
    hold = n - rel
    e = np.where(t < a, t / max(a, 1e-6), s + (1 - s) * np.exp(-(t - a) / max(d, 1e-6)))
    if hold < n:
        k = np.arange(n - hold) / max(rel, 1)
        e[hold:] *= np.clip(1.0 - k, 0, 1)
    return e


rng = np.random.default_rng(9)


def noise(n):
    return rng.uniform(-1, 1, n)


TRACKS = {k: np.zeros(LOOP * 2) for k in ("lead", "arp", "stab", "bass", "drums", "pad")}


def place(track, seconds, sig):
    start = int(round(seconds * SR))
    for rep in (0, 1):
        s = start + rep * LOOP
        e = min(s + len(sig), LOOP * 2)
        if e > s:
            TRACKS[track][s:e] += sig[: e - s]


def kick():
    n = int(0.25 * SR)
    t = np.arange(n) / SR
    f = 130 * np.exp(-t * 30) + 45
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 13)


def snare(level):
    n = int(0.2 * SR)
    t = np.arange(n) / SR
    body = np.sin(2 * np.pi * 180 * t) * np.exp(-t * 28) * 0.5
    return (body + noise(n) * np.exp(-t * 20)) * level


def hat(level=0.16):
    n = int(0.05 * SR)
    x = np.diff(np.concatenate([[0], noise(n)]))
    return x * np.exp(-np.arange(n) / SR * 90) * level


def crash():
    n = int(1.8 * SR)
    x = np.diff(np.concatenate([[0], noise(n)]))
    return x * np.exp(-np.arange(n) / SR * 2.4) * 0.17


def render():
    for s, (bars, bpb, spb) in enumerate(SECTIONS):
        for bar in range(bars):
            t0 = SECTION_START[s] + bar * bpb * spb
            chord = CHORDS[s][bar]
            tones = CHORD_TONES[chord]
            root = tones[0]
            late = bar >= 4

            # Lead: brassy saw + pulse layer, vibrato on long notes.
            pos = 0.0
            for dur, name in MELODY[s][bar]:
                n = int(dur * spb * SR * 0.95)
                m = midi(name)
                sig = 0.6 * pulse(hz(m), n, duty=0.5, vibrato=0.4 if dur * spb >= 0.4 else 0.0) + 0.4 * saw(hz(m) * 1.003, n)
                sig *= env(n, a=0.008, d=0.15, s=0.8, r=0.05)
                place("lead", t0 + pos * spb, sig * 0.2)
                if late:
                    place("lead", t0 + pos * spb, pulse(hz(m - 12), n, duty=0.25) * env(n, s=0.7) * 0.08)
                pos += dur

            # Arpeggio: one note per eighth (per beat in C), chord tones up and down.
            ladder = [f"{tones[0]}4", f"{tones[1]}4", f"{tones[2]}4", f"{tones[0]}5"]
            ladder = ladder + ladder[-2:0:-1]
            step = spb / 2 if s < 2 else spb
            count = int(round(bpb * spb / step))
            for i in range(count):
                mm = midi(ladder[i % len(ladder)])
                if mm < midi("G3"):
                    mm += 12
                n = int(step * SR * 1.5)
                place("arp", t0 + i * step, pulse(hz(mm), n, duty=0.125) * env(n, a=0.002, d=0.08, s=0.0, r=0.02) * 0.08)

            # Bass and chords by meter.
            bm = midi(f"{root}2")
            if bm < midi("C2"):
                bm += 12
            if s == 0:
                # Driving eighths on the root, octave on the off-beats; stabs on the "and" of 2 and 4.
                for i in range(8):
                    n = int(0.18 * SR)
                    note = bm + (12 if i % 2 else 0)
                    place("bass", t0 + i * 0.2, triangle(hz(note), n) * env(n, a=0.003, d=0.1, s=0.5, r=0.03) * 0.42)
                for off in (1.5, 3.5):
                    n = int(0.15 * SR)
                    stab = sum(pulse(hz(midi(f"{t}4") if midi(f"{t}4") >= midi("F4") else midi(f"{t}5")), n, duty=0.25) for t in tones[:3])
                    place("stab", t0 + off * spb, stab * env(n, a=0.002, d=0.05, s=0.3, r=0.03) * 0.07)
                place("drums", t0, kick() * 0.8)
                place("drums", t0 + 2 * spb, kick() * 0.7)
                place("drums", t0 + 1 * spb, snare(0.36))
                place("drums", t0 + 3 * spb, snare(0.4))
                for i in range(8):
                    place("drums", t0 + i * spb / 2, hat(0.13 if i % 2 == 0 else 0.18))
            elif s == 1:
                # Oom-pah-pah, heavy.
                n = int(1.1 * spb * SR)
                place("bass", t0, triangle(hz(bm), n) * env(n, a=0.004, d=0.25, s=0.55, r=0.06) * 0.45)
                for beat in (1, 2):
                    n = int(0.3 * SR)
                    stab = sum(pulse(hz(midi(f"{t}4") if midi(f"{t}4") >= midi("F4") else midi(f"{t}5")), n, duty=0.25) for t in tones[:3])
                    place("stab", t0 + beat * spb, stab * env(n, a=0.002, d=0.06, s=0.3, r=0.03) * 0.07)
                place("drums", t0, kick() * 0.85)
                place("drums", t0 + spb, snare(0.3))
                place("drums", t0 + 2 * spb, snare(0.3))
                for off in (0.5, 1.5, 2.5):
                    place("drums", t0 + off * spb, hat())
            else:
                # 7/8 grouped 2+2+3: bass and kick on the group starts (eighths 0, 2, 4), snare on 2 and 4, a pickup on 6.
                for g, eighth in enumerate((0, 2, 4)):
                    length = 2 if g < 2 else 3
                    n = int(length * spb * SR * 0.9)
                    place("bass", t0 + eighth * spb, triangle(hz(bm + (7 if g == 1 else 0)), n) * env(n, a=0.003, d=0.15, s=0.5, r=0.04) * 0.44)
                    n2 = int(0.14 * SR)
                    stab = sum(pulse(hz(midi(f"{t}4") if midi(f"{t}4") >= midi("F4") else midi(f"{t}5")), n2, duty=0.25) for t in tones[:3])
                    place("stab", t0 + (eighth + 1) * spb, stab * env(n2, a=0.002, d=0.05, s=0.3, r=0.03) * 0.06)
                place("drums", t0, kick() * 0.85)
                place("drums", t0 + 2 * spb, snare(0.34))
                place("drums", t0 + 4 * spb, snare(0.38))
                place("drums", t0 + 6 * spb, kick() * 0.55)
                for i in range(7):
                    place("drums", t0 + i * spb, hat(0.15 if i in (0, 2, 4) else 0.11))

            # Pad underneath.
            n = int(bpb * spb * SR)
            pad = sum(pulse(hz(midi(f"{t}4")) * d, n, duty=0.5) for t in tones[:3] for d in (0.996, 1.004))
            place("pad", t0, pad * env(n, a=0.2, d=0.4, s=0.7, r=0.25) * 0.016)

            if bar == 0:
                place("drums", t0, crash())


def echo(x, delay, feedback=0.3, mix=0.25):
    d = int(round(delay * SR))
    y = x.copy()
    for i in range(d, len(y), d):
        y[i:i + d] += y[i - d:i][: len(y[i:i + d])] * feedback
    return x * (1 - mix) + y * mix


def main(out):
    render()
    lead = echo(TRACKS["lead"], 0.3)
    arp = echo(TRACKS["arp"], 0.2, 0.22, 0.18)
    mix = lead + arp + TRACKS["stab"] + TRACKS["bass"] + TRACKS["drums"] + TRACKS["pad"]
    loop = mix[LOOP:2 * LOOP]
    loop = np.tanh(loop * 1.15)
    loop *= 0.89 / np.max(np.abs(loop))
    pcm = (loop * 32767).astype(np.int16)
    with wave.open(out, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    print(f"{out}: {len(pcm)} samples, {len(pcm) / SR:.4f} s (4/4, 3/4, 7/8)")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "Hymn_of_the_Summit.wav")
