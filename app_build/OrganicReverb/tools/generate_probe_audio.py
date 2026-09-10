"""Deterministic mono impulse plus silence keeps the FMOD event alive for its tail."""
import array
import math
import pathlib
import random
import wave

output = pathlib.Path(__file__).resolve().parents[3] / 'Shinzui/Assets/OrganicReverbProbe.wav'
if output.exists():
    raise SystemExit('Probe audio already exists; kept unchanged.')
rng = random.Random(20260910)
rate = 48000
samples = array.array('h', [0]) * (rate * 6)
for i in range(2400):
    samples[rate + i] = int(12000 * rng.uniform(-1, 1) * math.exp(-i / 600))
with wave.open(str(output), 'wb') as wav:
    wav.setnchannels(1)
    wav.setsampwidth(2)
    wav.setframerate(rate)
    wav.writeframes(samples.tobytes())
print(output)
