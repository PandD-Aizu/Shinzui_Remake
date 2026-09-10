"""Check native runtime assertions and measure FMOD master-bus onset from PCM captures."""
import array
import json
import math
import pathlib
import sys
import wave

directory = pathlib.Path(sys.argv[1])
report = json.loads((directory / 'report.json').read_text(encoding='utf-8-sig'))
measurements = {}
for name in ('prepared-step', 'cold-step'):
    with wave.open(str(directory / (name + '.wav')), 'rb') as recording:
        assert recording.getsampwidth() == 2
        channels, rate = recording.getnchannels(), recording.getframerate()
        samples = array.array('h', recording.readframes(recording.getnframes()))
    if sys.byteorder != 'little':
        samples.byteswap()
    values = [sample / 32768 for sample in samples]
    first = next((i // channels for i, sample in enumerate(values) if abs(sample) > .001), None)
    measurements[name] = {
        'seconds': len(values) / (rate * channels),
        'channels': channels, 'sampleRate': rate,
        'onsetMilliseconds': first * 1000 / rate if first is not None else None,
        'peak': max(map(abs, values), default=0),
        'rms': math.sqrt(sum(sample * sample for sample in values) / max(1, len(values))),
        'clippedSamples': sum(abs(sample) >= .999 for sample in values),
    }
prepared = measurements['prepared-step']['onsetMilliseconds']
cold = measurements['cold-step']['onsetMilliseconds']
improvement = cold - prepared if prepared is not None and cold is not None else None
checks = {
    'runtime_checks_passed': report['passed'] and len(report['checks']) >= 30 and all(c['passed'] for c in report['checks']),
    'three_generated_seeds': len(report['seeds']) == 3 and all(n > 100 for n in report['triangleCounts']),
    'valid_stereo_captures': all(v['channels'] == 2 and v['seconds'] >= .75 for v in measurements.values()),
    'audible_without_clipping': all(v['peak'] > .001 and v['clippedSamples'] == 0 for v in measurements.values()),
    'prepared_onset_under_100ms': prepared is not None and prepared < 100,
    'preparation_removes_at_least_150ms': improvement is not None and improvement > 150,
}
result = {'passed': all(checks.values()), 'runtimeCheckCount': len(report['checks']), 'checks': checks,
          'measurements': measurements, 'onsetReductionMilliseconds': improvement,
          'measurementScope': 'FMOD master capture from contact request; excludes OS/device/headphone latency.'}
(directory / 'analysis.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
print(json.dumps(result, indent=2))
raise SystemExit(0 if result['passed'] else 1)
