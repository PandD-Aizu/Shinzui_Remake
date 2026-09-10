"""Archive passing Editor/Player evidence, recorded samples and the exact source hashes."""
import hashlib
import json
import pathlib
import shutil

root = pathlib.Path(__file__).resolve().parents[3]
destination = root / 'app_build/TunnelAcoustics/results'
reports = {}
for environment in ('Editor', 'Player'):
    source = root / 'Logs/TunnelAcoustics' / environment
    report = json.loads((source / 'report.json').read_text(encoding='utf-8-sig'))
    analysis = json.loads((source / 'analysis.json').read_text(encoding='utf-8-sig'))
    if not report['passed'] or not analysis['passed']:
        raise SystemExit(f'{environment} did not pass. Results were not exported.')
    reports[environment] = (source, report, analysis)
destination.mkdir(parents=True, exist_ok=True)
for environment, (source, _, _) in reports.items():
    for filename in ('report.json', 'analysis.json'):
        shutil.copy2(source / filename, destination / (environment.lower() + '-' + filename))
for filename in ('prepared-step.wav', 'cold-step.wav'):
    shutil.copy2(reports['Player'][0] / filename, destination / filename)
shutil.copy2(root / 'Logs/TunnelAcoustics/mesh-export.json', destination / 'mesh-export.json')
paths = []
for directory in ('app_build/TunnelAcoustics', 'app_build/SpatialAudio', 'app_build/DI/GenerateTunnel'):
    paths.extend(p for p in (root / directory).rglob('*') if p.is_file() and p.suffix in ('.cs', '.asmdef'))
paths.extend(root / p for p in ('app_build/DI/DILayer.asmdef', 'app_build/View/GenerateTunnel/TunnelMapView.cs',
    'app_build/View/Player/PlayerView.cs', 'Shinzui/Assets/FootstepPrototypeSpatial.wav',
    'Shinzui/Build/Desktop/GeneratedTunnelAudio.bank', 'Shinzui/Metadata/Event/{c5318595-038f-4def-a65f-d4b1c5ffe2c8}.xml',
    'Assets/Plugins/SteamAudio/Resources/SteamAudioSettings.asset', 'Assets/Plugins/FMOD/Resources/FMODStudioSettings.asset'))
paths.extend((root / 'Assets/Shinzui/Audio/Resources/SpatialAudio').glob('*.asset'))
hashes = {p.relative_to(root).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest() for p in paths}
(destination / 'source-sha256.json').write_text(json.dumps(hashes, indent=2), encoding='utf-8')
print('Exported passing reports, original Player recordings, mesh statistics and source hashes.')
