"""Analyze measured performance, output continuity, mixer gain and memory plateau. Requires NumPy."""
from pathlib import Path
import json, sys, wave
import numpy as np

folder=Path(sys.argv[1]); report=json.loads((folder/'report.json').read_text(encoding='utf-8-sig'))
checks={}; audio={}; stress_checks={}
def read(name):
    with wave.open(str(folder/(name+'.wav')),'rb') as w:
        assert w.getnchannels()==2 and w.getsampwidth()==2
        rate=w.getframerate(); data=np.frombuffer(w.readframes(w.getnframes()),dtype='<i2').astype(float).reshape(-1,2)/32768
    return rate,data
for sample in report['measurements']:
    name=sample['name']; rate,data=read(name)
    chunk=rate//100
    rms=np.sqrt(np.mean(data[:len(data)//chunk*chunk].reshape(-1,chunk,2)**2,axis=(1,2)))
    # 10 ms bins detect output gaps. This does not prove every short transient is perceptually clean.
    threshold=max(1/32768,float(np.median(rms))*.05)
    gaps=rms<threshold
    audio[name]={'seconds':len(data)/rate,'rms':float(np.sqrt(np.mean(data**2))),
        'min_10ms_rms':float(rms.min()),'median_10ms_rms':float(np.median(rms)),
        'near_silent_10ms_bins':int(gaps.sum()),'peak':float(np.abs(data).max())}
    if sample['voices']:
        target=checks if sample['voices']<=12 else stress_checks
        target[name+'_no_output_gaps']=not bool(gaps.any())
        target[name+'_eight_seconds']=len(data)/rate>=7.9
        target[name+'_no_device_starvations']=sample['outputStarvations']==0
for name in ['foot-se-1.0','foot-se-0.5','foot-se-0.0','foot-master-muted','foot-bus-muted','paused','foot-bgm-muted','bgm-volume-1','bgm-volume-0','button-volume-1','button-volume-0']:
    _,data=read(name);audio[name]={'energy':float(np.sum(data**2)),'peak':float(np.abs(data).max())}
full=audio['foot-se-1.0']['energy'];half=audio['foot-se-0.5']['energy']
ratio=float(np.sqrt(half/full)) if full else 0
checks['se_slider_halves_amplitude']=.45<ratio<.55
checks['foot_audible']=full>1e-3
for name in ['foot-se-0.0','foot-master-muted','foot-bus-muted','paused']:checks[name+'_silent']=audio[name]['peak']<.0001
checks['bgm_control_does_not_mute_footsteps']=.9<np.sqrt(audio['foot-bgm-muted']['energy']/full)<1.1
for prefix in ['bgm','button']:
    checks[prefix+'_audible']=audio[prefix+'-volume-1']['peak']>.001
    checks[prefix+'_muted']=audio[prefix+'-volume-0']['peak']<.0001
memory={}
for field in ['processPrivateMB','unityAllocatedMB','fmodAllocatedMB']:
    values=np.array([m[field] for m in report['memory']]);tail=values[-10:]
    slope=float(np.polyfit(np.arange(len(tail)),tail,1)[0]) if len(tail)>1 else float('inf')
    memory[field]={'first':float(values[0]),'last':float(values[-1]),'peak':float(values.max()),
        'last_10_range':float(np.ptp(tail)),'last_10_slope_MB_per_cycle':slope}
    tolerance=.5 if field=='processPrivateMB' else .1
    checks[field+'_plateau']=len(values)==30 and bool(np.all(values>0)) and slope<tolerance and np.ptp(tail)<(8 if field=='processPrivateMB' else 2)
checks['runtime']=report['passed'] and all(c['passed'] for c in report['checks'])
checks['material_count_stable']=len({m['materials'] for m in report['memory'][-10:]})==1
checks['single_owned_navmesh']=all(m['navMeshes']==1 for m in report['memory'])
eligible=[m['voices'] for m in report['measurements'] if m['frameP95']<=1000/60 and m['dspMax']<80 and m['virtualVoices']==0 and m['outputStarvations']==0 and (m['voices']==0 or audio[m['name']]['near_silent_10ms_bins']==0)]
if report['environment'].startswith('Windows'):checks['production_12_voice_frame_and_DSP_budget']=12 in eligible
checks={name:bool(value) for name,value in checks.items()}
result={'passed':all(checks.values()),'checks':checks,'amplitude_half_ratio':ratio,'audio':audio,'memory':memory,
    'stress_checks_above_production_capacity':stress_checks,
    'counts_with_p95_under_16_67ms_and_DSP_below_80pct':eligible,
    'performance_scope':'960x540, generated tunnel probe, 120 fps cap, one stationary listener; not full-game target-hardware certification'}
(folder/'analysis.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps({'passed':result['passed'],'failed':[k for k,v in checks.items() if not v],'frame_budget_counts':eligible,'half_ratio':ratio,'memory':memory},indent=2))
sys.exit(0 if result['passed'] else 1)
