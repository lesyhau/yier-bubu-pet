"""Generate the default sound effects in assets/sounds/ (Python 3, no extra packages).

    python tools/make_sounds.py

The sounds are synthesized here, so they can be shipped with the app. The folder layout
mirrors animations/: a sound in assets/sounds/yier/click/ plays with Yier's click animations.
"""
import math
import os
import random
import struct
import wave

RATE = 22050
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "assets", "sounds")


def write(rel_path, samples):
    path = os.path.join(OUT, rel_path)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    peak = max(1e-9, max(abs(s) for s in samples))
    gain = 0.5 / peak  # normalize to -6 dBFS; the app sets the final volume
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(s * gain * 32767)) for s in samples))
    print(f"{rel_path}: {len(samples) / RATE:.2f}s")


def envelope(t, attack, length):
    """Quick fade-in, then a smooth fade-out to silence at `length`."""
    if t < attack:
        return t / attack
    x = (t - attack) / max(1e-9, length - attack)
    return max(0.0, 1 - x) ** 2


def pop(f0, f1, length=0.16):
    """A bubbly "boop": a sine whose pitch rises quickly."""
    out, phase = [], 0.0
    for i in range(int(RATE * length)):
        t = i / RATE
        f = f0 + (f1 - f0) * (1 - math.exp(-t * 30))
        phase += 2 * math.pi * f / RATE
        out.append(math.sin(phase) * envelope(t, 0.004, length))
    return out


def whee(f0, f1, length=0.7):
    """A slide whistle going up, with a little vibrato."""
    out, phase = [], 0.0
    for i in range(int(RATE * length)):
        t = i / RATE
        x = t / length
        f = f0 * (f1 / f0) ** (x ** 0.7) * (1 + 0.02 * math.sin(2 * math.pi * 7 * t))
        phase += 2 * math.pi * f / RATE
        tone = math.sin(phase) + 0.15 * math.sin(2 * phase)
        out.append(tone * envelope(t, 0.03, length))
    return out


def chime(notes, gap=0.12, length=1.3):
    """Soft bell notes, one after another (a little "aww" for hugs)."""
    out = [0.0] * int(RATE * length)
    for n, f in enumerate(notes):
        start = int(RATE * gap * n)
        for i in range(len(out) - start):
            t = i / RATE
            decay = math.exp(-t * 4.5)
            attack = min(1.0, t / 0.005)
            bell = (math.sin(2 * math.pi * f * t)
                    + 0.35 * math.sin(2 * math.pi * f * 2.0 * t) * math.exp(-t * 6)
                    + 0.12 * math.sin(2 * math.pi * f * 3.01 * t) * math.exp(-t * 9))
            out[start + i] += bell * decay * attack * 0.6
    fade = int(RATE * 0.05)
    for i in range(fade):
        out[-1 - i] *= i / fade
    return out


def snore(length=2.6):
    """A soft, breathy snore (filtered noise that swells and fades)."""
    rng = random.Random(7)
    out, lp = [], 0.0
    for i in range(int(RATE * length)):
        t = i / RATE
        breath = math.sin(math.pi * t / length) ** 2  # zero at both ends, so it loops
        noise = rng.uniform(-1, 1)
        lp += 0.06 * (noise - lp)  # low-pass: rumbly, not hissy
        rumble = 1 + 0.6 * math.sin(2 * math.pi * 32 * t)
        out.append(lp * rumble * breath)
    return out


if __name__ == "__main__":
    # Yier (the panda) is a bit higher-pitched than Bubu (the bear).
    write("yier/click/boop.wav", pop(660, 1250))
    write("bubu/click/boop.wav", pop(400, 800, length=0.18))
    write("yier/drag/whee.wav", whee(600, 1500))
    write("bubu/drag/whee.wav", whee(380, 1000))
    write("together/play/chime.wav", chime([1046.5, 1318.5, 1568.0]))
    write("together/sleep/snore.wav", snore())
