"""Deploy the approved acoustic implementation and isolated propagation tests; retain GUIDs."""
import pathlib
import subprocess
import sys
import uuid

root=pathlib.Path(__file__).resolve().parents[3]
subprocess.run([sys.executable,str(root/'app_build/TunnelAcoustics/tools/sync_sources.py')],check=True)
def meta(path):
    target=pathlib.Path(str(path)+'.meta')
    if not target.exists():
        target.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n'+('folderAsset: yes\n' if path.is_dir() else ''),encoding='utf-8')
for layer in ('Runtime','Editor'):
    target=root/'Assets/Shinzui/Tests/Propagation'/layer;target.mkdir(parents=True,exist_ok=True)
    current=target
    while current!=root/'Assets':meta(current);current=current.parent
    for file in (root/'app_build/Propagation/Tests'/layer).iterdir():
        destination=target/file.name;destination.write_bytes(file.read_bytes());meta(destination)
print('Propagation test sources synchronized.')
