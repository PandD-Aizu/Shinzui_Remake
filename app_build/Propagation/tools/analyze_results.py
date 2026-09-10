"""Analyze measured PCM, rather than treating DSP settings as evidence of audible propagation.

Requires NumPy; see README for the bundled interpreter used for these results.
"""
import json
import math
import pathlib
import sys
import wave
import numpy as np

directory = pathlib.Path(sys.argv[1])
report = json.loads((directory / 'report.json').read_text(encoding='utf-8-sig'))
measurements = {}
recordings = {}

def rms(values):
    return float(np.sqrt(np.mean(values * values))) if values.size else 0.0

for case in report['samples']:
    with wave.open(str(directory / (case['name'] + '.wav')), 'rb') as recording:
        assert recording.getsampwidth() == 2
        rate, channels = recording.getframerate(), recording.getnchannels()
        values = np.frombuffer(recording.readframes(recording.getnframes()), dtype='<i2').astype(np.float64).reshape(-1, channels) / 32768
    recordings[case['name']] = values
    power = np.sum(values[int(rate):int(1.35 * rate)] ** 2, axis=0)
    block = int(.01 * rate)
    envelope = np.array([rms(values[i:i+block]) for i in range(0, len(values)-block, block)])
    spectrum = np.abs(np.fft.rfft(values * np.hanning(len(values))[:, None], axis=0))
    frequencies = np.fft.rfftfreq(len(values), 1 / rate)
    bands = [float(np.sqrt(np.sum(spectrum[(frequencies > f-15) & (frequencies < f+15)] ** 2))) for f in (300,1800,10000)]
    measurements[case['name']] = {
        'sampleRate': rate, 'channels': channels, 'seconds': len(values) / rate,
        'peak': float(np.max(np.abs(values))), 'rms': rms(values),
        'clippedSamples': int(np.sum(np.abs(values) >= .999)),
        'earlyRms': rms(values[int(rate):int(1.35*rate)]),
        'lateRms': rms(values[int(1.4*rate):int(3.5*rate)]),
        'earlyLeftMinusRightDb': float(10*np.log10((power[0]+1e-20)/(power[1]+1e-20))),
        'toneBandAmplitudes': bands,
        'maxNormalized10msEnvelopeChange': float(np.max(np.abs(np.diff(envelope))) / max(float(np.max(envelope)), 1e-12)),
        'min10msRms': float(np.min(envelope)),
    }

def m(name, field='rms'):
    return measurements[name][field]

band_ratios = {kind: (np.array(m('wall-'+kind,'toneBandAmplitudes')) / np.maximum(m('distance-2m','toneBandAmplitudes'),1e-20)).tolist() for kind in ('concrete','metal','wood')}
checks = {
    'runtime_passed': report['passed'] and len(report['samples']) == 19 and all(c['passed'] for c in report['checks'])
        and any(c['name']=='foot_contact_is_not_self_occluded_by_floor' for c in report['checks']),
    'stereo_captures_without_clipping': all(v['channels']==2 and v['seconds']>1 and v['clippedSamples']==0 for v in measurements.values()),
    'distance_doubling_halves_amplitude': .4 < m('distance-4m')/m('distance-2m') < .6 and .4 < m('distance-8m')/m('distance-4m') < .6,
    'wall_transmission_is_quiet_but_nonzero': 1e-6 < m('wall-concrete') < m('distance-2m')*.1,
    'wood_transmits_more_than_metal_and_concrete': m('wall-wood') > m('wall-metal')*1.5 and m('wall-metal') > m('wall-concrete')*1.2,
    'transmission_off_removes_direct_leak': m('wall-no-transmission') < max(1e-7,m('wall-concrete')*.05),
    'wall_filters_attenuate_high_more_than_low': all(v[0]>v[1]*1.2 and v[1]>v[2]*1.2 for v in band_ratios.values()),
    'reflections_have_late_tail': m('room-reflections','lateRms') > 1e-5 and m('room-direct','lateRms') < m('room-reflections','lateRms')*.1,
    'production_mix_reduces_tail': .1 < m('room-mix','lateRms')/max(m('room-reflections','lateRms'),1e-12) < .7,
    'closed_door_quieter_than_open': m('room-closed-door') < m('room-mix')*.4,
    'bend_has_indirect_audio': m('bend-reflections') > 1e-5 and m('bend-reflections') > m('bend-direct')*3,
    'offset_openings_change_early_direction': m('opening-left','earlyLeftMinusRightDb') > .5 and m('opening-right','earlyLeftMinusRightDb') < -.5,
    'volumetric_boundary_reduces_largest_level_step': m('boundary-volumetric','maxNormalized10msEnvelopeChange') < m('boundary-raycast','maxNormalized10msEnvelopeChange')*.7,
    'walking_through_doorway_has_continuous_audio': m('walk-through-doorway','min10msRms') > .001 and m('walk-through-doorway','maxNormalized10msEnvelopeChange') < .1,
}
result = {'passed':all(checks.values()),'runtimeCheckCount':len(report['checks']),'checks':checks,'measurements':measurements,'transmissionBandAmplitudeRatios':band_ratios}
(directory/'analysis.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps({'passed':result['passed'],'checks':checks,'transmissionBandAmplitudeRatios':band_ratios,
                 'openingDirectionDb':{n:m(n,'earlyLeftMinusRightDb') for n in ('opening-left','opening-right')},
                 'boundaryLevelStep':{n:m(n,'maxNormalized10msEnvelopeChange') for n in ('boundary-raycast','boundary-volumetric')}},indent=2))
raise SystemExit(0 if result['passed'] else 1)
