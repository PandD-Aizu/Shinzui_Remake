"""Check source parity, GUID uniqueness and the new feature's allowed assembly dependencies."""
import json
import pathlib
import re

root = pathlib.Path(__file__).resolve().parents[3]
mapping = {layer: f'Assets/Shinzui/Src/{layer}/SpatialAudio' for layer in ('Application', 'Infrastructure', 'DI')}
mapping.update({'Tests/Runtime': 'Assets/Shinzui/Tests/SpatialAudio/Runtime', 'Tests/Editor': 'Assets/Shinzui/Tests/SpatialAudio/Editor'})
allowed = {
    'Shinzui.SpatialAudio.Application': set(),
    'Shinzui.SpatialAudio.Infrastructure': {'Shinzui.SpatialAudio.Application', 'FMODUnity', 'SteamAudioUnity', 'SteamAudioFMODStudio'},
    'Shinzui.SpatialAudio.DI': {'Shinzui.SpatialAudio.Application', 'Shinzui.SpatialAudio.Infrastructure', 'VContainer'},
    'Shinzui.SpatialAudio.Tests': {'Shinzui.SpatialAudio.Application', 'Shinzui.SpatialAudio.Infrastructure', 'Shinzui.SpatialAudio.DI', 'Shinzui.AudioProbe.Infrastructure', 'FMODUnity', 'VContainer'},
    'Shinzui.SpatialAudio.Tests.Editor': {'Shinzui.SpatialAudio.Tests', 'Shinzui.SpatialAudio.Infrastructure', 'Shinzui.AudioProbe.Infrastructure', 'FMODUnity', 'FMODUnityEditor', 'SteamAudioUnity', 'Unity.Addressables.Editor'},
}
failures = []
guids = set()
for source_dir, target_dir in mapping.items():
    target = root / target_dir
    for source in (root / 'app_build/SpatialAudio' / source_dir).iterdir():
        if source.is_file() and (target / source.name).read_bytes() != source.read_bytes():
            failures.append(f'Unsynchronized: {source}')
    for path in [target, *target.rglob('*')]:
        if path.suffix == '.meta':
            continue
        meta = pathlib.Path(str(path) + '.meta')
        if not meta.exists():
            failures.append(f'Missing meta: {path}')
            continue
        match = re.search(r'^guid: ([0-9a-f]{32})$', meta.read_text(), re.M)
        if not match or match[1] in guids:
            failures.append(f'Missing or duplicate GUID: {meta}')
        else:
            guids.add(match[1])
        if path.suffix == '.asmdef':
            data = json.loads(path.read_text())
            if set(data['references']) != allowed[data['name']]:
                failures.append(f'Invalid assembly dependency: {path}')
            if data['name'].endswith('.Application') and not data.get('noEngineReferences'):
                failures.append('Application must have no engine references')
            if data['name'].endswith('.Editor') and data.get('includePlatforms') != ['Editor']:
                failures.append('Editor tests must be Editor-only')
for path in (root / 'app_build/SpatialAudio/Application').glob('*.cs'):
    if re.search(r'\busing\s+(UnityEngine|UnityEditor|FMOD|FMODUnity|SteamAudio|VContainer|Shinzui\.(Infrastructure|Presentation|View|DI))\b', path.read_text()):
        failures.append(f'Application has external layer/engine dependency: {path}')
print(json.dumps({'passed': not failures, 'failures': failures}, indent=2))
raise SystemExit(1 if failures else 0)
