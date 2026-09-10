"""Audit phase-3 source parity, Unity metadata and clean-architecture dependencies."""
import json
import pathlib
import re
import subprocess
import sys

root = pathlib.Path(__file__).resolve().parents[3]
subprocess.run([sys.executable, str(root / 'app_build/SpatialAudio/tools/audit_sources.py')], check=True)
mapping = {layer: f'Assets/Shinzui/Src/{layer}/TunnelAcoustics' for layer in ('Infrastructure', 'DI')}
mapping.update({'Tests/Runtime': 'Assets/Shinzui/Tests/TunnelAcoustics/Runtime',
                'Tests/Editor': 'Assets/Shinzui/Tests/TunnelAcoustics/Editor'})
legacy = ['View/GenerateTunnel/TunnelMapView.cs', 'View/Player/PlayerView.cs',
          'DI/GenerateTunnel/GenerateTunnelBootstrapper.cs', 'DI/GenerateTunnel/GenerateTunnelLifetimeScope.cs', 'DI/DILayer.asmdef']
allowed = {
    'Shinzui.TunnelAcoustics.Infrastructure': {'Shinzui.SpatialAudio.Application', 'Shinzui.SpatialAudio.Infrastructure', 'SteamAudioUnity', 'FMODUnity'},
    'Shinzui.TunnelAcoustics.DI': {'Shinzui.TunnelAcoustics.Infrastructure', 'ViewLayer'},
    'Shinzui.TunnelAcoustics.Tests': {'Shinzui.TunnelAcoustics.Infrastructure', 'Shinzui.TunnelAcoustics.DI',
        'Shinzui.SpatialAudio.Application', 'Shinzui.SpatialAudio.Infrastructure', 'Shinzui.AudioProbe.Infrastructure',
        'ApplicationLayer', 'DomainLayer', 'PresentationLayer', 'ViewLayer', 'FMODUnity', 'SteamAudioUnity'},
    'Shinzui.TunnelAcoustics.Tests.Editor': {'Shinzui.TunnelAcoustics.Tests', 'Shinzui.TunnelAcoustics.Infrastructure',
        'Shinzui.SpatialAudio.Infrastructure', 'FMODUnity', 'FMODUnityEditor', 'SteamAudioUnity', 'Unity.Addressables.Editor'},
}
failures = []
paths = []
for source_dir, target_dir in mapping.items():
    target = root / target_dir
    for source in (root / 'app_build/TunnelAcoustics' / source_dir).iterdir():
        if source.is_file() and (target / source.name).read_bytes() != source.read_bytes():
            failures.append(f'Unsynchronized: {source}')
    paths.extend([target, *(p for p in target.rglob('*') if p.suffix != '.meta')])
for relative in legacy:
    target = root / 'Assets/Shinzui/Src' / relative
    if target.read_bytes() != (root / 'app_build' / relative).read_bytes():
        failures.append(f'Unsynchronized integration: {relative}')
    paths.append(target)
for directory in ('Assets/Shinzui/Audio/Resources/SpatialAudio', 'Assets/Shinzui/Tests/TunnelAcoustics'):
    paths.extend([root / directory, *(p for p in (root / directory).rglob('*') if p.suffix != '.meta')])
guids = set()
for path in set(paths):
    meta = pathlib.Path(str(path) + '.meta')
    if not meta.exists():
        failures.append(f'Missing meta: {path}')
        continue
    match = re.search(r'^guid: ([0-9a-f]{32})$', meta.read_text(), re.M)
    if not match or match[1] in guids:
        failures.append(f'Missing/duplicate GUID: {meta}')
    else:
        guids.add(match[1])
    if path.suffix == '.asmdef':
        data = json.loads(path.read_text())
        if data['name'] in allowed and set(data['references']) != allowed[data['name']]:
            failures.append(f'Invalid dependencies: {data["name"]}')
        if data['name'].endswith('.Editor') and data.get('includePlatforms') != ['Editor']:
            failures.append(f'Editor assembly is not Editor-only: {data["name"]}')
for relative in legacy[:2]:
    text = (root / 'app_build' / relative).read_text(encoding='utf-8-sig')
    if re.search(r'\busing\s+Shinzui\.(Application|Infrastructure|Domain|Presentation|DI)\b', text):
        failures.append(f'View depends on another layer: {relative}')
print(json.dumps({'passed': not failures, 'failures': failures}, indent=2))
raise SystemExit(1 if failures else 0)
