"""Verify phase-4 sources, metadata, assembly boundaries and acoustic budgets."""
import json
import pathlib
import re
import subprocess
import sys

root=pathlib.Path(__file__).resolve().parents[3]
subprocess.run([sys.executable,str(root/'app_build/TunnelAcoustics/tools/audit_sources.py')],check=True)
failures=[]
expected={
    'Shinzui.Propagation.Tests': {'Shinzui.SpatialAudio.Application','Shinzui.SpatialAudio.Infrastructure','Shinzui.TunnelAcoustics.Infrastructure','Shinzui.AudioProbe.Infrastructure','FMODUnity','SteamAudioUnity'},
    'Shinzui.Propagation.Tests.Editor': {'Shinzui.Propagation.Tests','Shinzui.SpatialAudio.Infrastructure','Shinzui.TunnelAcoustics.Infrastructure','FMODUnity','FMODUnityEditor','SteamAudioUnity','Unity.Addressables.Editor'},
}
for layer in ('Runtime','Editor'):
    target=root/'Assets/Shinzui/Tests/Propagation'/layer
    for source in (root/'app_build/Propagation/Tests'/layer).iterdir():
        if source.read_bytes()!=(target/source.name).read_bytes():failures.append('Unsynchronized: '+str(source))
        if source.suffix=='.asmdef':
            data=json.loads(source.read_text())
            if set(data['references'])!=expected[data['name']]:failures.append('Invalid assembly references: '+data['name'])
            if layer=='Editor' and data.get('includePlatforms')!=['Editor']:failures.append('Editor code is not Editor-only.')
guids=set()
folder=root/'Assets/Shinzui/Tests/Propagation'
for path in [folder,*folder.rglob('*')]:
    if path.suffix=='.meta':continue
    meta=pathlib.Path(str(path)+'.meta')
    if not meta.exists():failures.append('Missing metadata: '+str(path));continue
    match=re.search(r'^guid: ([a-f0-9]{32})$',meta.read_text(),re.M)
    if not match or match[1] in guids:failures.append('Invalid/duplicate GUID: '+str(meta))
    else:guids.add(match[1])
settings=(root/'Assets/Plugins/SteamAudio/Resources/SteamAudioSettings.asset').read_text()
config=(root/'Assets/Shinzui/Audio/Resources/SpatialAudio/TunnelAudioConfiguration.asset').read_text()
def value(text,name):return float(re.search(r'^\s*'+name+r': ([0-9.]+)',text,re.M)[1])
if value(settings,'maxOcclusionSamples')<value(config,'OcclusionSamples'):failures.append('Occlusion source count exceeds scene capacity.')
if value(settings,'realTimeMaxSources')<value(config,'MaxVoices'):failures.append('Voice budget exceeds scene capacity.')
standard=(value(settings,'realTimeRays')==8192 and value(settings,'realTimeBounces')==64 and value(settings,'realTimeDuration')==3 and abs(value(settings,'simulationUpdateInterval')-.1)<1e-5)
economy=(value(settings,'realTimeRays')==4096 and value(settings,'realTimeBounces')==32 and value(settings,'realTimeDuration')==1.5 and abs(value(settings,'simulationUpdateInterval')-.15)<1e-5)
if not (standard or economy):failures.append('Expected a validated Standard or Economy profile.')
print(json.dumps({'passed':not failures,'failures':failures},indent=2))
raise SystemExit(1 if failures else 0)
