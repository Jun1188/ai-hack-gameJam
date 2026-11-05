"""Render an original, one-shot cafe celebration cue. Python 3 + numpy only.

No downloaded samples, soundfonts, trained models, or external services are used.
The score and instrument models below are the source of the delivered WAV.
Run from any directory: python tools/create-cafe-ending-bgm.py
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path
import wave

import numpy as np

RATE = 44100
BPM = 114
TRANSPOSE = -3  # A major: a gentler register than the original C-major score.
BEAT = 60 / BPM
BARS = 32
TAIL_SECONDS = 3.5
FRAMES = round((BARS * 4 * BEAT + TAIL_SECONDS) * RATE)
ROOT = Path(__file__).resolve().parents[1]
DEFAULT_OUTPUT = ROOT / "ai-hack-test/Assets/CafeSim/Resources/CafeEnding/ending_bgm.wav"
RNG = np.random.default_rng(20261106)


def frequency(note):
    return 440 * 2 ** ((note + TRANSPOSE - 69) / 12)


def piano(note, duration, velocity):
    """Hammer attack, decaying partials, and gently detuned strings."""
    t = np.arange(round((duration + 2.4) * RATE)) / RATE
    f = frequency(note)
    y = np.zeros_like(t)
    for h in range(1, 12):
        decay = (1.65 + 35 / f) / (1 + .19 * h)
        partial = h * f * math.sqrt(1 + .00007 * h * h)
        amplitude = np.exp(-h / 3.5) / h ** .66
        shape = (1 - np.exp(-t * 650)) * np.exp(-t / decay)
        y += amplitude * shape * (
            .68 * np.sin(2 * np.pi * partial * t)
            + .17 * np.sin(2 * np.pi * partial * 1.0008 * t)
            + .15 * np.sin(2 * np.pi * partial * .9993 * t)
        )
    # Felt-like hammer attack; no hard digital click.
    noise = RNG.standard_normal(len(t))
    noise = np.convolve(noise, np.ones(7) / 7, mode="same")
    y += .055 * noise * np.exp(-t * 95) * (1 - np.exp(-t * 900))
    release = np.minimum(1, np.exp(-np.maximum(0, t - duration) * 5))
    return y * release * velocity * .36


def nylon(note, duration, velocity):
    """A plucked-string model with a soft nail transient and body resonances."""
    t = np.arange(round((duration + .9) * RATE)) / RATE
    f = frequency(note)
    y = np.zeros_like(t)
    for h in range(1, 16):
        amplitude = np.sin(np.pi * h * .22) / h ** 1.05
        damping = np.exp(-t * (1.4 + h * .34))
        y += amplitude * np.sin(2 * np.pi * f * h * t) * damping
    body = .12 * np.sin(2 * np.pi * 190 * t) * np.exp(-t * 12)
    y += body
    return y * (1 - np.exp(-t * 1100)) * np.exp(-np.maximum(0, t - duration) * 7) * velocity * .25


def bass(note, duration, velocity):
    t = np.arange(round((duration + .22) * RATE)) / RATE
    f = frequency(note)
    y = np.zeros_like(t)
    for h, a in enumerate([1, .46, .19, .1, .048, .025], 1):
        y += a * np.sin(2 * np.pi * f * h * t) * np.exp(-t * (1.5 + h * .4))
    y *= (1 - np.exp(-t * 220)) * np.exp(-np.maximum(0, t - duration) * 20)
    return y * velocity * .42


def bell(note, duration, velocity):
    t = np.arange(round((duration + 1.7) * RATE)) / RATE
    f = frequency(note)
    y = np.zeros_like(t)
    for ratio, amp, decay in [(1, 1, 1.2), (2.001, .25, .65), (3.97, .09, .24)]:
        y += amp * np.sin(2 * np.pi * f * ratio * t) * np.exp(-t / decay)
    return y * (1 - np.exp(-t * 450)) * velocity * .082


def brush(velocity, accent=False):
    t = np.arange(round(.19 * RATE)) / RATE
    noise = RNG.standard_normal(len(t))
    soft = np.convolve(noise, np.ones(11) / 11, mode="same")
    high = noise - soft
    env = (1 - np.exp(-t * 900)) * np.exp(-t * (30 if accent else 48))
    return (high * .035 + soft * (.1 if accent else .035)) * env * velocity


def kick(velocity):
    t = np.arange(round(.2 * RATE)) / RATE
    phase = 2 * np.pi * (51 * t + 18 * .025 * (1 - np.exp(-t / .025)))
    return np.sin(phase) * (1 - np.exp(-t * 600)) * np.exp(-t * 27) * velocity * .12


def render(output):
    global RNG
    RNG = np.random.default_rng(20261106)
    mix = np.zeros((FRAMES, 2), dtype=np.float64)
    notes = 0

    def add(beat, signal, pan=0):
        nonlocal notes
        notes += 1
        start = round(beat * BEAT * RATE)
        # One-shot placement: note tails decay after the final tonic cadence.
        gains = np.array([math.cos((pan + 1) * np.pi / 4), math.sin((pan + 1) * np.pi / 4)])
        signal = signal[:, None] * gains
        first = min(len(signal), FRAMES - start)
        mix[start:start + first] += signal[:first]

    # Cafe jazz/bossa: a C-major score transposed down three semitones to A major.
    # Added sixths/ninths and warm secondary dominants retain its celebratory feel.
    # Each entry is a bass root, fifth, and a compact upper voicing.
    harmony = {
        "C6": (36, 43, [52, 57, 62, 67]),
        "Cend": (36, 43, [52, 55, 57, 60]),
        "Am9": (33, 40, [48, 55, 59, 64]),
        "Dm9": (38, 45, [53, 60, 64, 69]),
        "G13": (31, 38, [53, 59, 64, 69]),
        "F9": (29, 36, [52, 57, 60, 67]),
        "Em7": (28, 35, [50, 55, 59, 64]),
        "A7": (33, 40, [49, 55, 59, 64]),
        "G7": (31, 38, [53, 59, 62, 68]),
    }
    score = ["C6", "Am9", "Dm9", "G13", "F9", "Em7", "Dm9", "G13",
             "C6", "Am9", "Dm9", "G13", "F9", "Em7", "Dm9", "G13",
             "F9", "F9", "Em7", "A7", "Dm9", "Dm9", "G13", "G7",
             "C6", "Am9", "Dm9", "G13", "F9", "Dm9", "G13", "Cend"]

    # Original four-bar rising welcome motif, answered by a descending phrase.
    # Beat positions are intentionally syncopated; durations are in beats.
    motif = [
        [(0, 67, .68), (.75, 69, .38), (1.5, 72, .9), (2.75, 76, .9)],
        [(.25, 76, .65), (1, 74, .4), (1.75, 72, .8), (3, 71, .65)],
        [(0, 69, .6), (.75, 72, .55), (1.5, 76, .75), (2.75, 74, .85)],
        [(.25, 71, .75), (1.25, 69, .4), (2, 67, 1.5)],
        [(0, 69, .75), (1, 72, .6), (2, 76, .7), (3, 79, .6)],
        [(.25, 78, .55), (1, 76, .8), (2.25, 74, 1.15)],
        [(0, 77, .65), (.75, 76, .45), (1.5, 74, .6), (2.5, 72, .6)],
        [(0, 71, .65), (1, 74, .5), (2, 69, .5), (3, 67, .7)],
    ]
    bridge = [
        [(0, 72, .7), (1, 76, .7), (2, 79, 1.4)],
        [(.25, 81, .65), (1, 79, .45), (1.75, 76, .7), (3, 74, .6)],
        [(0, 71, .75), (1, 74, .65), (2, 79, 1.25)],
        [(.25, 76, .5), (1, 73, .5), (2, 71, .7), (3, 69, .6)],
        [(0, 69, .7), (1, 72, .6), (2, 77, 1.3)],
        [(.25, 76, .55), (1, 74, .6), (2, 72, .6), (3, 69, .65)],
        [(0, 71, .65), (.75, 74, .5), (1.5, 76, .65), (2.5, 79, .6)],
        [(0, 80, .65), (1, 79, .65), (2, 77, .6), (3, 74, .65)],
    ]
    cadence = [
        [(0, 69, .75), (1, 72, .6), (2, 76, .7), (3, 79, .6)],
        [(.25, 77, .65), (1, 76, .55), (2, 74, .7), (3, 72, .6)],
        [(0, 71, .65), (1, 74, .65), (2, 69, .6), (3, 67, .6)],
        [(0, 76, .7), (1, 74, .45), (1.75, 72, 2)],
    ]

    for bar, chord in enumerate(score):
        root, fifth, voicing = harmony[chord]
        base = bar * 4
        # A quiet, rounded acoustic bass leaves space around the melody.
        add(base, bass(root, (2.8 if bar == 31 else 1.3) * BEAT, .78), -.12)
        if bar != 31:
            add(base + 2.1, bass(fifth, 1.15 * BEAT, .65), -.12)
        if bar % 4 == 3 and bar != 31:
            next_root = harmony[score[(bar + 1) % BARS]][0]
            add(base + 3.45, bass(next_root - 1, .42 * BEAT, .45), -.12)
        # Gentle bossa comping, with individual string offsets rather than blocks.
        strums = [(0, .68)] if bar == 31 else [(0, .63), (1.5, .47), (2.5, .58), (3.25, .36)]
        for pos, strength in strums:
            for string, note in enumerate(voicing):
                add(base + pos + string * .014, nylon(note, (3 if bar == 31 else .65) * BEAT, strength), -.32)
        # Piano answers the guitar on the second half of alternate bars.
        if bar % 2 == 1 and bar != 31:
            for j, note in enumerate(voicing[1:]):
                add(base + 2.8 + .018 * j, piano(note, .55 * BEAT, .2), .30)
        phrase = cadence[bar - 28] if bar >= 28 else bridge[bar - 16] if 16 <= bar < 24 else motif[bar % 8]
        for pos, note, duration in phrase:
            # The repeated theme receives small rhythmic/dynamic variations.
            vel = .76 + .055 * math.sin(bar * .9 + pos * 1.3)
            if 8 <= bar < 16 and pos == 0:
                pos += .09
            add(base + pos, piano(note, duration * BEAT, vel), .14)
        # A tiny glockenspiel glow marks the group-photo celebration; restrained.
        if bar in (0, 4, 8, 12, 24, 28):
            for pos, note in [(0, 84), (.75, 88), (1.5, 91)]:
                add(base + pos, bell(note, .7, .34), -.03)
        if bar == 31:
            add(base + 1.75, bell(84, 1.2, .23), -.03)
        for beat in range(1 if bar == 31 else 4):
            add(base + beat, brush(.75, beat % 2 == 1), .45)
            add(base + beat + .55, brush(.33), .32)
        add(base, kick(.56), 0)
        if bar != 31:
            add(base + 2, kick(.39), 0)

    # A short stereo room with no wraparound into the opening.
    dry = mix.copy()
    for delay, gain in [(.043, .12), (.079, .095), (.127, .075), (.191, .055), (.277, .038), (.401, .026)]:
        offset = round(delay * RATE)
        mix[offset:] += dry[:-offset, ::-1] * gain
    # Small speaker-friendly bass control, then conservative soft saturation.
    mix -= mix.mean(axis=0)
    mix = np.tanh(mix * 1.1)
    mix *= .105 / np.sqrt(np.mean(mix * mix))
    if abs(mix).max() > .78:
        mix *= .78 / abs(mix).max()
    # The final A6 chord and melody A4 finish before a 3.5-second room tail.
    # A short entry ramp and a 1.5-second tail fade make both file ends silent.
    ramp = round(.012 * RATE)
    mix[:ramp] *= np.linspace(0, 1, ramp)[:, None]
    fade = round(1.5 * RATE)
    mix[-fade:] *= np.linspace(1, 0, fade)[:, None] ** 2
    pcm = np.rint(np.clip(mix, -1, 1) * 32767).astype("<i2")
    output.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(output), "wb") as wav:
        wav.setnchannels(2)
        wav.setsampwidth(2)
        wav.setframerate(RATE)
        wav.writeframes(pcm.tobytes())
    report = {
        "title": "A Table for Everyone",
        "style": "warm, mellow acoustic cafe jazz / light bossa celebration",
        "bpm": BPM, "key": "A major", "bars": BARS,
        "transpose_semitones": TRANSPOSE,
        "seconds": len(pcm) / RATE, "sample_rate": RATE,
        "channels": 2, "bits": 16, "rendered_events": notes,
        "playback": "one-shot, no repeat",
        "cadence": "Dmaj9 -> Bm9 -> E13 -> A6; final melody note A4 (440 Hz)",
        "tail_seconds": TAIL_SECONDS,
        "peak_dbfs": round(20 * math.log10(float(abs(mix).max())), 3),
        "rms_dbfs": round(20 * math.log10(float(np.sqrt(np.mean(mix * mix)))), 3),
        "clipped_samples": int(np.count_nonzero(abs(pcm.astype(np.int32)) >= 32767)),
        "first_sample_pcm": pcm[0].astype(int).tolist(),
        "last_sample_pcm": pcm[-1].astype(int).tolist(),
        "sha256": hashlib.sha256(output.read_bytes()).hexdigest(),
        "source": "Original deterministic score and synthesized instruments; no external audio samples.",
        "quality_limit": "Numerical checks only; listening in the final game is still required.",
    }
    print(json.dumps(report, ensure_ascii=False, indent=2))
    return report


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    render(parser.parse_args().output)
