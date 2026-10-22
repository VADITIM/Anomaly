"""Synthesizes every SFX in #Assets/Audio. numpy only.

Design rules, against the old chiptune set:
- No raw square or saw waves. Tones are sines, FM with low index, or filtered noise.
- Fundamentals sit low: impacts have 30-120 Hz bodies; chimes stay under ~700 Hz.
- Every sound is layered: a short transient, a body, and a tail.
- Soft tanh saturation adds weight without fizz.
- A stereo room or hall is baked in, with the left and right impulse responses decorrelated.
- A final 9 kHz roll-off keeps the whole set dark and out of the ear-piercing range.

Run: python "#Tools/gen_audio.py"
"""
import os
import wave

import numpy as np

RATE = 44100
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "#Assets", "Audio")
rng = np.random.default_rng(11)


# ---------- primitives ----------

def n_of(d):
    return int(RATE * d)


def t(d):
    return np.arange(n_of(d)) / RATE


def noise(d):
    return rng.standard_normal(n_of(d)) * 0.5


def env(d, attack=0.003, curve=3.0, hold=0.0):
    n = n_of(d)
    x = np.arange(n) / RATE
    a = np.clip(x / max(attack, 1e-4), 0, 1)
    rel = np.clip((x - attack - hold) / max(d - attack - hold, 1e-4), 0, 1)
    return a * (1 - rel) ** curve


def swell(d, peak=0.6, curve_in=2.0, curve_out=2.0):
    x = np.linspace(0, 1, n_of(d))
    up = np.clip(x / peak, 0, 1) ** curve_in
    down = (1 - np.clip((x - peak) / (1 - peak), 0, 1)) ** curve_out
    return up * down


def sine(freq, d, phase=0.0):
    f = np.broadcast_to(np.asarray(freq, dtype=float), (n_of(d),))
    return np.sin(2 * np.pi * np.cumsum(f) / RATE + phase)


def sweep(f0, f1, d):
    return np.geomspace(f0, f1, n_of(d))


def fm(carrier, ratio, index, d):
    c = np.broadcast_to(np.asarray(carrier, dtype=float), (n_of(d),))
    mod = np.sin(2 * np.pi * np.cumsum(c * ratio) / RATE)
    return np.sin(2 * np.pi * np.cumsum(c) / RATE + index * mod)


def warm_saw(freq, d, cutoff=400.0):
    """Band-limited-ish saw tamed by a steep low-pass: a growl, not a chip tone."""
    f = np.broadcast_to(np.asarray(freq, dtype=float), (n_of(d),))
    ph = np.cumsum(f) / RATE
    saw = 2 * (ph % 1.0) - 1
    return lowpass(saw, cutoff, order=4)


def sat(x, drive=2.0):
    return np.tanh(x * drive) / np.tanh(drive)


def pad_to(a, n):
    return np.pad(a, (0, max(0, n - len(a))))[:n]


def mix(*parts):
    n = max(len(p) for p in parts)
    return sum(pad_to(p, n) for p in parts)


def at(sig, offset_s, total=None):
    pad = np.zeros(n_of(offset_s))
    out = np.concatenate([pad, sig])
    return pad_to(out, total) if total else out


# ---------- filters (zero-phase FFT; butterworth magnitude, so no ringing) ----------

def _gain(freqs, kind, lo, hi, order):
    f = np.maximum(freqs, 1e-3)
    g = np.ones_like(f)
    if kind in ("lp", "bp"):
        g = g / np.sqrt(1 + (f / hi) ** (2 * order))
    if kind in ("hp", "bp"):
        g = g / np.sqrt(1 + (lo / f) ** (2 * order))
    return g


def _static(sig, kind, lo=None, hi=None, order=2):
    spec = np.fft.rfft(sig)
    freqs = np.fft.rfftfreq(len(sig), 1 / RATE)
    return np.fft.irfft(spec * _gain(freqs, kind, lo, hi, order), len(sig))


def lowpass(sig, cutoff, order=2):
    if np.ndim(cutoff) == 0:
        return _static(sig, "lp", hi=cutoff, order=order)
    return _varying(sig, "lp", None, cutoff, order)


def highpass(sig, cutoff, order=2):
    if np.ndim(cutoff) == 0:
        return _static(sig, "hp", lo=cutoff, order=order)
    return _varying(sig, "hp", cutoff, None, order)


def bandpass(sig, lo, hi, order=2):
    if np.ndim(lo) == 0 and np.ndim(hi) == 0:
        return _static(sig, "bp", lo=lo, hi=hi, order=order)
    n = len(sig)
    lo = np.broadcast_to(np.asarray(lo, dtype=float), (n,))
    hi = np.broadcast_to(np.asarray(hi, dtype=float), (n,))
    return _varying(sig, "bp", lo, hi, order)


def _varying(sig, kind, lo, hi, order, size=1024, hop=256):
    """Time-varying filter by overlap-add STFT; cutoffs are per-sample arrays sampled per frame."""
    n = len(sig)
    window = np.hanning(size)
    padded = np.pad(sig, (size, size + hop))
    out = np.zeros_like(padded)
    norm = np.zeros_like(padded)
    freqs = np.fft.rfftfreq(size, 1 / RATE)
    for start in range(0, len(padded) - size, hop):
        centre = np.clip(start - size + size // 2, 0, n - 1)
        frame = padded[start:start + size] * window
        g = _gain(freqs, kind,
                  None if lo is None else lo[centre],
                  None if hi is None else hi[centre], order)
        out[start:start + size] += np.fft.irfft(np.fft.rfft(frame) * g, size) * window
        norm[start:start + size] += window ** 2
    return (out / np.maximum(norm, 1e-6))[size:size + n]


def resonator(freqs, decays, d, amps=None, strike=0.004):
    """Struck modal resonance (bells, metal, wood): decaying sines with a soft strike transient."""
    amps = amps or [1.0] * len(freqs)
    x = t(d)
    out = sum(a * np.sin(2 * np.pi * f * x) * np.exp(-x / dec) for f, dec, a in zip(freqs, decays, amps))
    return out * np.clip(x / strike, 0, 1)


# ---------- space ----------

def _impulse(decay, damp, seed, predelay):
    local = np.random.default_rng(seed)
    d = decay * 1.6
    x = t(d)
    ir = local.standard_normal(len(x)) * np.exp(-6.9 * x / decay)
    ir = _varying(ir, "lp", None, np.geomspace(damp, damp * 0.25, len(x)), 1)
    ir[: n_of(0.004)] *= np.linspace(0, 1, n_of(0.004))
    ir = np.concatenate([np.zeros(n_of(predelay)), ir])
    return ir / np.sqrt(np.sum(ir ** 2))


def space(sig, decay=0.5, wet=0.25, damp=5000, predelay=0.012, seed=0):
    """Returns a stereo (n, 2) array: dry in the centre, decorrelated reverb on each side."""
    channels = []
    for side in range(2):
        ir = _impulse(decay, damp, seed * 2 + side + 100, predelay + side * 0.0037)
        size = len(sig) + len(ir) - 1
        fft_n = 1 << (size - 1).bit_length()
        tail = np.fft.irfft(np.fft.rfft(sig, fft_n) * np.fft.rfft(ir, fft_n), fft_n)[:size]
        channels.append(pad_to(sig, size) + tail * wet)
    longest = max(len(c) for c in channels)
    return np.stack([pad_to(c, longest) for c in channels], axis=1)


# ---------- output ----------

def save(name, sig, peak=0.82, tone=9000):
    sig = np.asarray(sig, dtype=float)
    if sig.ndim == 1:
        sig = np.stack([sig, sig], axis=1)
    sig = np.stack([lowpass(sig[:, c], tone, order=2) for c in range(2)], axis=1)
    sig -= sig.mean(axis=0)
    level = np.max(np.abs(sig)) or 1.0
    sig = sig / level * peak
    fade = min(n_of(0.01), len(sig))
    sig[-fade:] *= np.linspace(1, 0, fade)[:, None]
    data = (np.clip(sig, -1, 1) * 32767).astype(np.int16)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(data.tobytes())
    print(f"{name:16s} {len(sig) / RATE:5.2f}s")


# ---------- building blocks ----------

def whoosh(d, lo0, lo1, width=1.8, body=0.0, attack_at=0.45):
    """Air moving past a blade: a band of noise that sweeps and swells, with an optional low body."""
    lo = sweep(lo0, lo1, d)
    s = bandpass(noise(d), lo, lo * width, order=2) * swell(d, attack_at, 1.6, 2.4)
    if body:
        s = s + sine(sweep(110, 55, d), d) * swell(d, attack_at, 2, 3) * body
    return s


def thump(f0, f1, d, drive=1.8, curve=4.0):
    return sat(sine(sweep(f0, f1, d), d) * env(d, 0.002, curve), drive)


def click(d=0.006, lo=1500, hi=4500):
    return bandpass(noise(d), lo, hi) * env(d, 0.0005, 2)


def crunch(d, lo, hi, curve=5.0):
    return bandpass(noise(d), lo, hi) * env(d, 0.001, curve)


# ---------- the set ----------

def build():
    os.makedirs(OUT, exist_ok=True)

    # Scythe swings: low air, the third swing longer and heavier.
    for i, (d, lo0, lo1, body) in enumerate([(0.2, 260, 900, 0.15), (0.19, 300, 1000, 0.15), (0.3, 180, 700, 0.3)]):
        s = whoosh(d, lo0, lo1, 1.7, body)
        save(f"swing{i + 1}", space(s, 0.35, 0.18, 4000, seed=i))

    hit = mix(click(0.006, 1200, 3500) * 0.35,
              thump(120, 46, 0.16, 2.2) * 1.0,
              crunch(0.09, 350, 1300, 6) * 0.55)
    save("hit", space(hit, 0.32, 0.16, 3500, seed=3))

    heavy = mix(click(0.008, 900, 2800) * 0.3,
                thump(85, 30, 0.42, 2.6, 3.5) * 1.1,
                crunch(0.2, 200, 900, 4) * 0.6,
                lowpass(noise(0.35), 300) * env(0.35, 0.002, 3) * 0.5)
    save("hit_heavy", space(heavy, 0.6, 0.22, 3000, seed=4))

    ring = resonator([311, 467, 742, 1013], [0.5, 0.35, 0.22, 0.14], 0.8, [1, 0.6, 0.35, 0.18])
    weak = mix(hit, ring * 0.32)
    save("hit_weak", space(weak, 0.7, 0.24, 3500, seed=5))

    tear = lowpass(noise(0.6), sweep(2200, 160, 0.6)) * env(0.6, 0.002, 2.5)
    ghost = sine(sweep(110, 52, 0.7), 0.7) * swell(0.7, 0.15, 1, 2) * 0.35
    kill = mix(thump(90, 28, 0.55, 2.4, 3) * 1.1, tear * 0.7, ghost)
    save("kill", space(kill, 0.9, 0.28, 2800, seed=6))

    save("dash", space(whoosh(0.24, 600, 220, 1.6, 0.25, 0.25), 0.3, 0.14, 3500, seed=7))

    shimmer = sum(sine(f * (1 + 0.003 * np.sin(2 * np.pi * 0.7 * t(0.9) + k)), 0.9) for k, f in enumerate([110, 165, 220, 277]))
    dodge = mix(shimmer * swell(0.9, 0.12, 1.5, 2.2) * 0.4, whoosh(0.5, 900, 250, 1.6, 0, 0.1) * 0.6)
    save("perfect_dodge", space(dodge, 1.6, 0.45, 3000, seed=8))

    save("jump", space(mix(whoosh(0.14, 250, 700, 1.6, 0, 0.3) * 0.8, thump(140, 90, 0.07, 1.5) * 0.3), 0.25, 0.1, 3000, seed=9))

    land = mix(thump(95, 38, 0.16, 2.2), lowpass(noise(0.12), 650) * env(0.12, 0.001, 5) * 0.8)
    save("land", space(land, 0.3, 0.12, 2500, seed=10), 0.7)

    # Footstep: soft packed earth, a few tiny grains on top of a dull pat.
    step = mix(lowpass(noise(0.07), 900) * env(0.07, 0.001, 6), thump(110, 70, 0.05, 1.2) * 0.4)
    for k in range(3):
        step += at(crunch(0.012, 900, 2200, 4) * 0.12, 0.004 + k * 0.009, n_of(0.07))
    save("step", space(step, 0.18, 0.06, 2500, seed=11), 0.45)

    growl = sat(warm_saw(sweep(150, 70, 0.35), 0.35, 420) * env(0.35, 0.004, 2.5), 2.0)
    hurt = mix(thump(110, 40, 0.3, 2.4) * 0.9, growl * 0.5, crunch(0.08, 300, 1100, 5) * 0.4)
    save("hurt", space(hurt, 0.45, 0.2, 3000, seed=12))

    chord = sum(sine(f * (1 + 0.002 * np.sin(2 * np.pi * 0.4 * t(1.4) + k)), 1.4) * (0.9 ** k) for k, f in enumerate([110, 165, 220, 277.2, 330]))
    heal = chord * swell(1.4, 0.3, 1.5, 2.5) + highpass(noise(1.4), 2500) * swell(1.4, 0.4, 2, 3) * 0.012
    save("heal", space(heal, 1.8, 0.5, 4000, seed=13), 0.7)

    bell = resonator([196, 392.5, 588, 784.8], [1.2, 0.6, 0.35, 0.2], 1.4, [1, 0.45, 0.25, 0.12])
    save("mark_ready", space(bell, 1.6, 0.4, 3500, seed=14), 0.6)

    rise = noise(1.3)
    roar = sat(bandpass(rise, sweep(80, 260, 1.3), sweep(300, 900, 1.3), 2) * swell(1.3, 0.35, 1.2, 1.5) * 3, 2.5)
    sub = sine(sweep(30, 55, 1.3), 1.3) * swell(1.3, 0.3, 1, 1.4)
    save("aetheriac_on", space(mix(roar * 0.6, sub * 0.9, thump(70, 28, 0.6, 3) * 0.8), 1.4, 0.35, 2500, seed=15))

    exhale = lowpass(noise(0.8), sweep(1600, 90, 0.8)) * env(0.8, 0.02, 2)
    save("aetheriac_off", space(mix(exhale, sine(sweep(70, 35, 0.8), 0.8) * env(0.8, 0.01, 2) * 0.6), 1.0, 0.3, 2500, seed=16))

    beat = mix(thump(58, 34, 0.16, 2.6, 4), at(thump(52, 30, 0.2, 2.4, 4) * 0.75, 0.2))
    save("heartbeat", space(lowpass(beat, 400), 0.4, 0.12, 1500, seed=17))

    spin = whoosh(0.36, 380, 900, 1.5, 0.1, 0.3) * (0.65 + 0.35 * np.sin(2 * np.pi * 16 * t(0.36)))
    save("throw", space(spin, 0.4, 0.18, 3500, seed=18))

    grab = mix(click(0.005, 800, 2400) * 0.5, resonator([262, 413, 690], [0.12, 0.08, 0.05], 0.25, [1, 0.5, 0.25]) * 0.6, thump(140, 90, 0.06, 1.4) * 0.5)
    save("catch", space(grab, 0.3, 0.14, 3000, seed=19), 0.6)

    # Telegraph: a rising inhale with a low tremor, a warning you feel more than hear.
    tele = bandpass(noise(0.32), sweep(140, 420, 0.32), sweep(400, 1100, 0.32)) * swell(0.32, 0.85, 1.4, 1) * (0.7 + 0.3 * np.sin(2 * np.pi * 22 * t(0.32)))
    tele += sine(sweep(70, 95, 0.32), 0.32) * swell(0.32, 0.8, 1.5, 1) * 0.5
    save("telegraph", space(tele, 0.35, 0.18, 2500, seed=20), 0.5)

    lunge = mix(bandpass(noise(0.24), 180, 700) * swell(0.24, 0.3, 1.5, 2.2),
                sat(warm_saw(75 * (1 + 0.04 * np.sin(2 * np.pi * 28 * t(0.24))), 0.24, 380) * env(0.24, 0.01, 2), 2.2) * 0.5)
    save("enemy_attack", space(lunge, 0.35, 0.15, 2800, seed=21))

    squelch = mix(lowpass(noise(0.16), sweep(1400, 300, 0.16)) * env(0.16, 0.002, 3) * 0.7,
                  sine(sweep(110, 190, 0.12), 0.12) * env(0.12, 0.004, 2) * 0.6)
    save("slime_hop", space(squelch, 0.25, 0.1, 2500, seed=22), 0.6)

    spit = mix(bandpass(noise(0.2), 500, 2200) * env(0.2, 0.002, 3) * 0.8, thump(160, 80, 0.06, 1.5) * 0.5,
               sine(sweep(420, 180, 0.15), 0.15) * env(0.15, 0.003, 3) * 0.2)
    save("spit", space(spit, 0.3, 0.14, 3000, seed=23), 0.65)

    brk = thump(150, 55, 0.3, 2.2) * 0.8 + crunch(0.3, 250, 1100, 4) * 0.5
    for k in range(6):
        brk += at(crunch(0.03, 500, 1800, 3) * (0.5 - k * 0.06), 0.01 + k * 0.035 + rng.uniform(0, 0.01), len(brk))
    save("break", space(brk, 0.45, 0.2, 3000, seed=24))

    soul = sine(sweep(294, 392, 0.3), 0.3) * swell(0.3, 0.2, 1.5, 2.5) * 0.7 + sine(sweep(588, 784, 0.3), 0.3) * swell(0.3, 0.2, 1.5, 3) * 0.15
    save("soul", space(soul, 0.9, 0.4, 3500, seed=25), 0.45)

    # Interface: soft wooden ticks and a low two-note bell, nothing beeps.
    tick = mix(bandpass(noise(0.025), 700, 1800) * env(0.025, 0.0008, 5), sine(180, 0.03) * env(0.03, 0.001, 4) * 0.4)
    save("ui_move", space(tick, 0.15, 0.08, 2500, seed=26), 0.35)
    confirm = mix(resonator([196, 392], [0.25, 0.12], 0.4, [1, 0.3]), at(resonator([294, 588], [0.35, 0.15], 0.5, [1, 0.3]), 0.07))
    save("ui_confirm", space(mix(confirm * 0.7, tick * 0.4), 0.5, 0.25, 3000, seed=27), 0.45)

    armor = resonator([151, 233, 347, 529], [0.6, 0.4, 0.25, 0.15], 1.0, [1, 0.7, 0.4, 0.2])
    shards = sum(at(crunch(0.025, 900, 3000, 3) * 0.3, 0.01 + k * 0.02, n_of(1.0)) for k in range(5))
    tb = mix(thump(90, 28, 0.7, 2.8, 3) * 1.0, armor * 0.45, shards)
    save("tenacity_break", space(tb, 1.1, 0.3, 3000, seed=28))

    fall = lowpass(noise(0.9), sweep(1300, 140, 0.9)) * swell(0.9, 0.2, 1.2, 1.5) + sine(sweep(90, 40, 0.9), 0.9) * env(0.9, 0.05, 1.5) * 0.4
    save("fall", space(fall, 0.9, 0.3, 2500, seed=29))

    drone = sum(sine(sweep(f, f * 0.5, 2.2), 2.2) * (0.8 ** k) for k, f in enumerate([55, 82.4, 110]))
    death = mix(thump(70, 24, 0.8, 3, 2.5), drone * swell(2.2, 0.1, 1, 1.6) * 0.5,
                lowpass(noise(2.0), sweep(900, 60, 2.0)) * env(2.0, 0.02, 1.5) * 0.4)
    save("death", space(death, 2.4, 0.45, 2200, seed=30))

    boom = mix(thump(60, 20, 1.6, 3.5, 2), lowpass(noise(3.0), sweep(3000, 50, 3.0)) * env(3.0, 0.01, 1.4) * 0.8,
               sum(sine(sweep(f, f * 0.6, 3.0), 3.0) for f in [55, 58.3, 82.4]) * swell(3.0, 0.1, 1, 1.3) * 0.35)
    save("void_rupture", space(sat(boom, 1.6), 3.0, 0.5, 2000, seed=31))

    # The Voice: a breathy low murmur with two vowel formants and a held drone underneath.
    breath = noise(1.1)
    formants = bandpass(breath, 260, 420) * 1.0 + bandpass(breath, 820, 1100) * 0.5
    voice = formants * swell(1.1, 0.25, 1.5, 2) * (0.8 + 0.2 * np.sin(2 * np.pi * 5.5 * t(1.1))) + sine(98, 1.1) * swell(1.1, 0.3, 2, 2) * 0.35
    save("voice", space(voice, 1.6, 0.45, 2500, seed=32), 0.55)

    up = sum(sine(f, 1.3) * (0.85 ** k) for k, f in enumerate([110, 165, 220, 330]))
    level = mix(up * swell(1.3, 0.22, 1.4, 2.4), whoosh(0.6, 200, 900, 1.6, 0, 0.8) * 0.5, at(resonator([220, 441], [0.8, 0.4], 1.0, [1, 0.3]) * 0.5, 0.28))
    save("level_up", space(level, 1.8, 0.45, 3500, seed=33), 0.75)

    ambience()


def ambience():
    """A 32 s seamless bed: two beating low drones, slow wind, and the odd distant groan of stone."""
    d = 32.0
    x = t(d)
    loop = 2 * np.pi / d
    drone = np.zeros_like(x)
    for k, f in enumerate([55.0, 55.4, 82.4, 110.2]):
        f_lfo = f * (1 + 0.0015 * np.sin(loop * (k + 1) * x + k))
        drone += sine(f_lfo, d) * (0.55 + 0.45 * np.sin(loop * (k + 2) * x + k * 1.7) ** 2) * (0.9 ** k)
    drone = lowpass(drone, 240)
    wind = bandpass(noise(d), 120 + 80 * (1 + np.sin(loop * 3 * x)), 380 + 320 * (1 + np.sin(loop * 2 * x + 1.3)))
    wind *= 0.6 + 0.4 * np.sin(loop * 4 * x + 0.4) ** 2
    bed = drone * 0.55 + wind * 1.1
    for start in (5.5, 17.0, 26.5):
        groan = lowpass(warm_saw(sweep(46, 38, 2.6), 2.6, 160), 200) * swell(2.6, 0.4, 2, 2) * 0.35
        bed += at(groan, start, len(bed))
    wet = space(bed, 3.0, 0.35, 1500, seed=40)
    tail = len(wet) - len(x)
    wet[:tail] += wet[len(x):]
    wet = wet[: len(x)]
    save("ambience", wet, 0.5, tone=6000)


if __name__ == "__main__":
    build()
