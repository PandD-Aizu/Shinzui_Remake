"""Validate lifecycle checks and actual recorded output, including the short sound's tail."""
import array
import json
import math
import pathlib
import sys
import wave

directory = pathlib.Path(sys.argv[1])
report = json.loads((directory / 'report.json').read_text(encoding='utf-8-sig'))
measurements = {}
for name in ('two-voices-tail', 'moving-loop'):
    with wave.open(str(directory / (name + '.wav')), 'rb') as audio:
        assert audio.getsampwidth() == 2
        channels, rate = audio.getnchannels(), audio.getframerate()
        values = [value / 32768 for value in array.array('h', audio.readframes(audio.getnframes()))]
    onset = next((i // channels for i, value in enumerate(values) if abs(value) > .001), 0)
    tail = values[(onset + int(.2 * rate))*channels:(onset + int(2.5 * rate))*channels]
    measurements[name] = {
        'seconds': len(values)/(rate*channels), 'channels': channels, 'sampleRate': rate,
        'peak': max(map(abs, values), default=0),
        'rms': math.sqrt(sum(value*value for value in values)/max(1,len(values))),
        'tailRms': math.sqrt(sum(value*value for value in tail)/max(1,len(tail))),
        'clippedSamples': sum(abs(value) >= .999 for value in values),
    }
checks = {
    'runtime_passed': report['passed'] and all(check['passed'] for check in report['checks']),
    'all_instances_released': report['createdInstances'] == report['releasedInstances'] and report['createdInstances'] >= 30,
    'valid_stereo_captures': all(value['channels'] == 2 and value['seconds'] > 3.5 for value in measurements.values()),
    'no_clipping': all(value['clippedSamples'] == 0 for value in measurements.values()),
    'both_recordings_audible': all(value['peak'] > .001 for value in measurements.values()),
    'short_sound_has_reflection_tail': measurements['two-voices-tail']['tailRms'] > .00001,
    'loop_has_sustained_audio': measurements['moving-loop']['rms'] > .0001,
}
result = {'passed': all(checks.values()), 'runtimeCheckCount': len(report['checks']), 'checks': checks, 'measurements': measurements}
(directory / 'analysis.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
print(json.dumps(result, indent=2))
raise SystemExit(0 if result['passed'] else 1)
