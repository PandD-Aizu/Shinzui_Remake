"""Deploy canonical app_build sources, preserving existing Unity GUIDs."""
import pathlib
import uuid

root = pathlib.Path(__file__).resolve().parents[3]
source_root = root / 'app_build/SpatialAudio'
mapping = {layer: f'Assets/Shinzui/Src/{layer}/SpatialAudio' for layer in ('Application', 'Infrastructure', 'DI')}
mapping.update({'Tests/Runtime': 'Assets/Shinzui/Tests/SpatialAudio/Runtime', 'Tests/Editor': 'Assets/Shinzui/Tests/SpatialAudio/Editor'})

def metadata(path):
    meta = pathlib.Path(str(path) + '.meta')
    if meta.exists():
        return
    folder = 'folderAsset: yes\n' if path.is_dir() else ''
    meta.write_text(f'fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\n{folder}', encoding='utf-8')

for source_dir, destination in mapping.items():
    target = root / destination
    target.mkdir(parents=True, exist_ok=True)
    current = target
    while current != root / 'Assets':
        metadata(current)
        current = current.parent
    for source in (source_root / source_dir).iterdir():
        if source.is_file():
            deployed = target / source.name
            deployed.write_bytes(source.read_bytes())
            metadata(deployed)
print('Spatial audio sources synchronized; existing .meta files preserved.')
