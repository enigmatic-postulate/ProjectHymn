"""
Small synthesis kit shared by Project Hymn's music scripts (VS-6 on): notes and pitches, band-limited pulses, a
triangle, FM bells, a formant choir, a plucked harp, drums, ADSR envelopes, filters, a feedback echo and a small
Schroeder reverb, and a track bus that renders two loops back to back so tails wrap into the start of the loop.

Everything here is original synthesis: no samples.
"""
import wave

import numpy as np
from scipy.signal import butter, lfilter, sosfilt

SR = 44100

NOTE = {"C": 0, "C#": 1, "Db": 1, "D": 2, "D#": 3, "Eb": 3, "E": 4, "F": 5, "F#": 6, "Gb": 6, "G": 7, "G#": 8,
        "Ab": 8, "A": 9, "A#": 10, "Bb": 10, "B": 11}


def midi(name):
    pitch, octave = name[:-1], int(name[-1])
    return 12 * (octave + 1) + NOTE[pitch]


def hz(m):
    return 440.0 * 2.0 ** ((m - 69) / 12.0)


def f(name):
    return hz(midi(name))


_rng = np.random.default_rng(11)


def noise(n, rng=None):
    return (rng or _rng).uniform(-1, 1, n)


# ---------------- Envelopes and filters ----------------

def env(n, a=0.005, d=0.1, s=0.6, r=0.08, hold=None):
    t = np.arange(n) / SR
    rel = int(r * SR)
    hold = n - rel if hold is None else hold
    e = np.where(t < a, t / max(a, 1e-6), s + (1 - s) * np.exp(-(t - a) / max(d, 1e-6)))
    if hold < n:
        k = np.arange(n - hold) / max(rel, 1)
        e[hold:] *= np.clip(1.0 - k, 0, 1)
    return e


def lowpass(x, cutoff, order=2):
    sos = butter(order, min(cutoff, SR * 0.45) / (SR / 2), btype="low", output="sos")
    return sosfilt(sos, x)


def highpass(x, cutoff, order=2):
    sos = butter(order, cutoff / (SR / 2), btype="high", output="sos")
    return sosfilt(sos, x)


def bandpass(x, lo, hi, order=2):
    sos = butter(order, [lo / (SR / 2), min(hi, SR * 0.45) / (SR / 2)], btype="band", output="sos")
    return sosfilt(sos, x)


# ---------------- Oscillators and voices ----------------

def _blep(t, dt):
    dt = np.broadcast_to(dt, t.shape)
    out = np.zeros_like(t)
    a = t < dt
    x = t[a] / dt[a]
    out[a] = x + x - x * x - 1.0
    b = t > 1.0 - dt
    x = (t[b] - 1.0) / dt[b]
    out[b] = x * x + x + x + 1.0
    return out


def pulse(freq, n, duty=0.5, vibrato=0.0, vib_rate=5.5, vib_delay=0.18):
    t = np.arange(n) / SR
    fr = np.full(n, float(freq))
    if vibrato > 0:
        depth = np.clip((t - vib_delay) / 0.25, 0, 1) * vibrato
        fr = fr * 2.0 ** (depth * np.sin(2 * np.pi * vib_rate * t) / 12.0)
    dt = fr / SR
    ph = np.cumsum(dt) % 1.0
    sq = np.where(ph < duty, 1.0, -1.0)
    sq += _blep(ph, dt)
    sq -= _blep((ph - duty) % 1.0, dt)
    return sq


def triangle(freq, n):
    ph = (np.arange(n) * freq / SR) % 1.0
    return 4.0 * np.abs(ph - 0.5) - 1.0


def sine(freq, n, vibrato=0.0, vib_rate=5.0, vib_delay=0.2):
    t = np.arange(n) / SR
    fr = np.full(n, float(freq))
    if vibrato > 0:
        depth = np.clip((t - vib_delay) / 0.3, 0, 1) * vibrato
        fr = fr * 2.0 ** (depth * np.sin(2 * np.pi * vib_rate * t) / 12.0)
    return np.sin(2 * np.pi * np.cumsum(fr) / SR)


def bell(freq, n, decay=1.6, bright=1.0):
    """FM bell: a carrier with an inharmonic modulator whose index falls away."""
    t = np.arange(n) / SR
    idx = 3.0 * bright * np.exp(-t * 3.0)
    mod = np.sin(2 * np.pi * freq * 3.5 * t) * idx
    car = np.sin(2 * np.pi * freq * t + mod)
    return car * np.exp(-t * decay) * np.minimum(1, t * 600)


def harp(freq, n, bright=0.6, decay=0.996):
    """A plucked string (Karplus-Strong, one period at a time) with a soft attack."""
    period = max(2, int(round(SR / freq)))
    block = lowpass(noise(period), 2000 + 6000 * bright)
    chunks = []
    total = 0
    while total < n:
        chunks.append(block)
        total += period
        block = 0.5 * (block + np.roll(block, -1)) * decay
    out = np.concatenate(chunks)[:n]
    return out * np.minimum(1, np.arange(n) / (SR * 0.002))


def choir(freqs, n, vowel="ah", detune=(0.996, 1.0, 1.004), breath=0.02):
    """Additive voices: harmonics shaped by vowel formants, a slow vibrato each."""
    formants = {"ah": ((750, 120, 1.0), (1150, 140, 0.7), (2600, 260, 0.25)),
                "oo": ((330, 90, 1.0), (850, 120, 0.4), (2400, 260, 0.12)),
                "eh": ((530, 110, 1.0), (1850, 160, 0.55), (2500, 240, 0.25))}[vowel]
    t = np.arange(n) / SR
    out = np.zeros(n)
    for f0 in freqs:
        for k, d in enumerate(detune):
            vib = 1 + 0.006 * np.sin(2 * np.pi * (4.8 + k * 0.4) * t + k * 2.1)
            ph = 2 * np.pi * f0 * d * np.cumsum(vib) / SR
            for h in range(1, 20):
                fh = f0 * h
                if fh > 5000:
                    break
                amp = sum(a * np.exp(-((fh - fc) / bw) ** 2) for fc, bw, a in formants) + 0.015 / h
                out += np.sin(ph * h) * amp
    out += lowpass(noise(n), 3000) * breath
    return out


# ---------------- Drums ----------------

def kick(n=None, pitch=55.0, punch=1.0):
    n = n or int(0.35 * SR)
    t = np.arange(n) / SR
    fr = pitch * (1 + 2.5 * np.exp(-t * 40) * punch)
    return np.sin(2 * np.pi * np.cumsum(fr) / SR) * np.exp(-t * 9)


def snare(n=None, tone=190.0):
    n = n or int(0.25 * SR)
    t = np.arange(n) / SR
    body = np.sin(2 * np.pi * tone * t) * np.exp(-t * 25)
    hiss = highpass(noise(n), 1500) * np.exp(-t * 18)
    return body * 0.5 + hiss * 0.8


def hat(n=None, open_=False):
    n = n or int((0.25 if open_ else 0.06) * SR)
    t = np.arange(n) / SR
    return highpass(noise(n), 6500) * np.exp(-t * (10 if open_ else 70))


def tom(freq, n=None):
    n = n or int(0.4 * SR)
    t = np.arange(n) / SR
    fr = freq * (1 + 0.6 * np.exp(-t * 20))
    return np.sin(2 * np.pi * np.cumsum(fr) / SR) * np.exp(-t * 7)


def bubbles(n, rate=4.0, rng=None):
    """Underwater bubbles: short rising sine chirps scattered over n samples."""
    rng = rng or _rng
    out = np.zeros(n)
    count = int(n / SR * rate)
    for _ in range(count):
        start = int(rng.uniform(0, n - 0.1 * SR))
        m = int(rng.uniform(0.02, 0.06) * SR)
        t = np.arange(m) / SR
        f0 = rng.uniform(500, 1400)
        fr = f0 * (1 + 3.0 * t / (m / SR))
        out[start:start + m] += np.sin(2 * np.pi * np.cumsum(fr) / SR) * np.sin(np.pi * t / (m / SR)) * rng.uniform(0.2, 1)
    return out


# ---------------- Effects ----------------

def echo(x, delay_s, feedback=0.35, mix=0.3):
    d = max(1, int(round(delay_s * SR)))
    y = x.copy()
    for i in range(d, len(y), d):
        y[i:i + d] += y[i - d:i][: len(y[i:i + d])] * feedback
    return x * (1 - mix) + y * mix


def reverb(x, size=1.0, mix=0.3, damp=4500):
    """A small Schroeder reverb: four combs in parallel, two allpasses in series."""
    combs = [int(SR * s * size) for s in (0.0297, 0.0371, 0.0411, 0.0437)]
    out = np.zeros_like(x)
    for c, g in zip(combs, (0.80, 0.78, 0.76, 0.74)):
        b = np.zeros(c + 1); b[0] = 1.0
        a = np.zeros(c + 1); a[0] = 1.0; a[c] = -g
        out += lfilter(b, a, x)
    out = lowpass(out / 4.0, damp)
    for ap, g in ((int(SR * 0.005), 0.7), (int(SR * 0.0017), 0.7)):
        b = np.zeros(ap + 1); b[0] = -g; b[ap] = 1.0
        a = np.zeros(ap + 1); a[0] = 1.0; a[ap] = -g
        out = lfilter(b, a, out)
    return x * (1 - mix) + out * mix


# ---------------- Tracks ----------------

class Bus:
    """Named tracks two loops long; place() writes a sound into both loops so tails wrap around."""

    def __init__(self, loop_samples, names):
        self.loop = loop_samples
        self.tracks = {k: np.zeros(loop_samples * 2) for k in names}

    def place(self, track, at_seconds, sig):
        start = max(0, int(round(at_seconds * SR)))
        for rep in (0, 1):
            s = start + rep * self.loop
            e = min(s + len(sig), self.loop * 2)
            if e > s:
                self.tracks[track][s:e] += sig[: e - s]

    def loop_of(self, x):
        return x[self.loop:2 * self.loop]


def write(path, loop, peak=0.75, gain=None):
    """Write a mono 16-bit WAV, normalised to peak (or scaled by gain); returns the gain used."""
    if gain is None:
        m = np.max(np.abs(loop))
        gain = peak / m if m > 0 else 1.0
    pcm = (np.clip(loop * gain, -1, 1) * 32767).astype(np.int16)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    print(f"{path}: {len(pcm)} samples, {len(pcm) / SR:.4f} s")
    return gain
