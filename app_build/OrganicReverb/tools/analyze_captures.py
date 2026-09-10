"""Measure actual FMOD output; fail on silent playback, missing tails or failed occlusion."""
import array
import json
import math
import pathlib
import sys
import wave

root = pathlib.Path(sys.argv[1])
report = json.loads((root / 'report.json').read_text(encoding='utf-8-sig'))
measurements = {}

def rms(values):
    return math.sqrt(sum(x*x for x in values) / max(1, len(values)))

for case in report['cases']:
    with wave.open(str(root / (case['name'] + '.wav')), 'rb') as wav:
        rate, channels = wav.getframerate(), wav.getnchannels()
        assert wav.getsampwidth() == 2
        samples = array.array('h', wav.readframes(wav.getnframes()))
    floats = [x/32768 for x in samples]
    peak = max(map(abs, floats), default=0)
    # Anchor to the first audible sample instead of assuming a render/scheduler start time.
    onset = next((i // channels for i, v in enumerate(floats) if abs(v) > .001), 0)
    tail = floats[(onset + int(.2 * rate))*channels:(onset + int(2.5*rate))*channels]
    measurements[case['name']] = {
        'seconds': len(floats)/(channels*rate), 'sample_rate': rate, 'channels': channels,
        'peak': peak, 'rms': rms(floats), 'tail_rms': rms(tail),
        'clipped_samples': sum(abs(v) >= .999 for v in floats),
        'occlusion': case['occlusion'], 'dsp_cpu_percent': case['dspCpuPercent']
    }

def value(case, key):
    return measurements[case][key]

checks = {
    'runtime_report_passed': report['passed'],
    'eight_cases': len(measurements) == 8,
    'valid_captures': all(v['seconds'] >= 4.5 and v['channels'] == 2 for v in measurements.values()),
    'no_clipping': all(v['clipped_samples'] == 0 for v in measurements.values()),
    'all_unoccluded_audible': all(value(s+'-'+m, 'peak') > .001 for s in ['small-room','corridor','outdoor'] for m in ['dry','wet']),
    'room_tail': value('small-room-wet','tail_rms') > max(1e-6, 10*value('small-room-dry','tail_rms')),
    'corridor_tail': value('corridor-wet','tail_rms') > max(1e-6, 10*value('corridor-dry','tail_rms')),
    'room_vs_outdoor_tail': value('small-room-wet','tail_rms') > 2*value('outdoor-wet','tail_rms'),
    'wall_occludes_direct': value('wall-occluded','peak') < .1*value('small-room-dry','peak'),
    'occlusion_simulation': value('wall-occluded','occlusion') < .1 and value('small-room-dry','occlusion') > .9,
    'wall_indirect_audible': value('wall-occluded-wet','peak') > .001,
}
result = {'passed': all(checks.values()), 'checks': checks, 'measurements': measurements}
(root / 'analysis.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
print(json.dumps(result, indent=2))
sys.exit(0 if result['passed'] else 1)
