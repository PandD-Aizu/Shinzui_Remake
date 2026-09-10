"""Check deployed sources, metadata, architecture and the dedicated dry world bus."""
from pathlib import Path
import json, re, subprocess, sys, xml.etree.ElementTree as ET
r=Path(__file__).resolve().parents[3]
subprocess.run([sys.executable,str(r/'app_build/Propagation/tools/audit_sources.py')],check=True)
failures=[];guids=set()
for layer in ['Runtime','Editor']:
    for p in (r/'app_build/AcousticPerformance/Tests'/layer).iterdir():
        target=r/'Assets/Shinzui/Tests/AcousticPerformance'/layer/p.name
        if p.read_bytes()!=target.read_bytes():failures.append('Source differs: '+str(p))
        if p.suffix=='.asmdef':
            data=json.loads(p.read_text())
            if layer=='Editor' and data.get('includePlatforms')!=['Editor']:failures.append('Editor assembly not isolated')
for p in (r/'Assets/Shinzui/Tests/AcousticPerformance').rglob('*'):
    if p.suffix=='.meta':continue
    meta=Path(str(p)+'.meta')
    if not meta.exists():failures.append('Missing meta: '+str(p));continue
    match=re.search(r'^guid: ([a-f0-9]{32})$',meta.read_text(),re.M)
    if not match or match[1] in guids:failures.append('Invalid/duplicate meta: '+str(p))
    else:guids.add(match[1])
world=None
for path in (r/'Shinzui/Metadata/Group').glob('*.xml'):
    tree=ET.parse(path);group=tree.find("object[@class='MixerGroup']")
    if group.findtext("property[@name='name']/value")=='WorldSE':world=(tree,group)
if not world:failures.append('WorldSE bus missing')
else:
    tree,group=world
    if tree.find("object[@class='MixerSend']") is not None:failures.append('WorldSE must not add a second reverb send')
    if group.findtext("relationship[@name='masters']/destination")!='{da0ab76d-61fd-4f61-aedf-374a2b7d4a88}':failures.append('SE VCA assignment missing')
    event=ET.parse(r/'Shinzui/Metadata/Event/{c5318595-038f-4def-a65f-d4b1c5ffe2c8}.xml')
    if event.find("object[@class='MixerInput']").findtext("relationship[@name='output']/destination")!=group.attrib['id']:failures.append('Footstep output does not reach WorldSE')
print(json.dumps({'passed':not failures,'failures':failures},indent=2));sys.exit(bool(failures))
