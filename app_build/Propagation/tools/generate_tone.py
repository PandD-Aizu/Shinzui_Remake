"""Loop-safe low/mid/high-band calibration tone; preserves existing audio assets."""
import array
import math
import pathlib
import wave

path = pathlib.Path(__file__).resolve().parents[3] / 'Shinzui/Assets/PropagationTone.wav'
if path.exists():
    raise SystemExit('Calibration tone already exists.')
rate = 48000
samples = array.array('h', (int(4500 * sum(math.sin(2*math.pi*f*i/rate) for f in (300, 1800, 10000))) for i in range(rate)))
with wave.open(str(path), 'wb') as output:
    output.setnchannels(1); output.setsampwidth(2); output.setframerate(rate); output.writeframes(samples.tobytes())
print(path)
