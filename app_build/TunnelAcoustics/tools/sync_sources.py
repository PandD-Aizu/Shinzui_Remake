"""Deploy phase-3 sources and explicit legacy integration changes without changing existing GUIDs."""
import pathlib
import subprocess
import sys
import uuid

root=pathlib.Path(__file__).resolve().parents[3]
subprocess.run([sys.executable,str(root/'app_build/SpatialAudio/tools/sync_sources.py')],check=True)
mapping={layer:f'Assets/Shinzui/Src/{layer}/TunnelAcoustics' for layer in ('Infrastructure','DI')}
mapping.update({'Tests/Runtime':'Assets/Shinzui/Tests/TunnelAcoustics/Runtime','Tests/Editor':'Assets/Shinzui/Tests/TunnelAcoustics/Editor'})
def meta(path):
    target=pathlib.Path(str(path)+'.meta')
    if not target.exists():target.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n'+('folderAsset: yes\n' if path.is_dir() else ''),encoding='utf-8')
for source,destination in mapping.items():
    target=root/destination;target.mkdir(parents=True,exist_ok=True)
    current=target
    while current!=root/'Assets':meta(current);current=current.parent
    for file in (root/'app_build/TunnelAcoustics'/source).iterdir():
        if file.is_file():deployed=target/file.name;deployed.write_bytes(file.read_bytes());meta(deployed)
for relative in ['View/GenerateTunnel/TunnelMapView.cs','View/Player/PlayerView.cs','DI/GenerateTunnel/GenerateTunnelBootstrapper.cs','DI/GenerateTunnel/GenerateTunnelLifetimeScope.cs','DI/DILayer.asmdef']:
    (root/'Assets/Shinzui/Src'/relative).write_bytes((root/'app_build'/relative).read_bytes())
print('Tunnel acoustics sources and explicit integration changes synchronized.')
