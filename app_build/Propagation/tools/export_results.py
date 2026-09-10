"""Archive passing measured results and preserve absolute playback levels in comparisons."""
import array
import hashlib
import json
import pathlib
import shutil
import wave

root=pathlib.Path(__file__).resolve().parents[3]
destination=root/'app_build/Propagation/results'
sources={}
for environment in ('Editor','Player'):
    source=root/'Logs/Propagation'/environment
    report=json.loads((source/'report.json').read_text(encoding='utf-8-sig'))
    analysis=json.loads((source/'analysis.json').read_text())
    if not report['passed'] or not analysis['passed'] or not any(c['name']=='foot_contact_is_not_self_occluded_by_floor' and c['passed'] for c in report['checks']):
        raise SystemExit(environment+' did not pass the complete current test set; no results exported.')
    sources[environment]=(source,report,analysis)
destination.mkdir(parents=True,exist_ok=True)
for environment,(source,_,_) in sources.items():
    for name in ('report.json','analysis.json'):shutil.copy2(source/name,destination/(environment.lower()+'-'+name))
for case in sources['Player'][1]['samples']:
    name=case['name']+'.wav';shutil.copy2(sources['Player'][0]/name,destination/name)
comparisons={
    'room-comparison': ['room-direct','room-reflections','room-mix','room-closed-door','bend-mix','opening-left','opening-right'],
    'boundary-comparison': ['boundary-raycast','boundary-volumetric','walk-through-doorway'],
}
for name,cases in comparisons.items():
    blocks=[]
    for case in cases:
        with wave.open(str(destination/(case+'.wav')),'rb') as recording:
            assert recording.getframerate()==48000 and recording.getnchannels()==2 and recording.getsampwidth()==2
            blocks.append(recording.readframes(recording.getnframes()))
        blocks.append(bytes(48000*2*2))
    with wave.open(str(destination/(name+'.wav')),'wb') as result:
        result.setnchannels(2);result.setsampwidth(2);result.setframerate(48000);result.writeframes(b''.join(blocks))
    (destination/(name+'-order.txt')).write_text('\n'.join(cases)+'\nAbsolute gains preserved; one second of silence between cases.\n',encoding='utf-8')
paths=[]
for directory in ('app_build/Propagation','app_build/SpatialAudio','app_build/TunnelAcoustics'):
    paths.extend(p for p in (root/directory).rglob('*') if p.is_file() and p.suffix in ('.cs','.asmdef'))
paths.extend(root/p for p in ('Assets/Plugins/SteamAudio/Resources/SteamAudioSettings.asset','Assets/Plugins/FMOD/Resources/FMODStudioSettings.asset',
    'Assets/Shinzui/Audio/Resources/SpatialAudio/TunnelAudioConfiguration.asset','Shinzui/Build/Desktop/PropagationProbe.bank',
    'Shinzui/Assets/PropagationTone.wav','Shinzui/Metadata/Event/{ecad8cc8-1a5b-45ff-892b-5809cfde489c}.xml'))
hashes={p.relative_to(root).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in paths}
(destination/'source-sha256.json').write_text(json.dumps(hashes,indent=2),encoding='utf-8')
print('Exported passing evidence, original recordings, comparisons and source hashes.')
