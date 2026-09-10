"""Publish only completed, passing Editor and Player evidence into app_build."""
import hashlib
import json
import pathlib
import shutil

root = pathlib.Path(__file__).resolve().parents[3]
destination = root / 'app_build/SpatialAudio/results'
reports = {}
for environment in ('Editor', 'Player'):
    source = root / 'Logs/SpatialAudio' / environment
    report = json.loads((source / 'report.json').read_text(encoding='utf-8-sig'))
    analysis = json.loads((source / 'analysis.json').read_text(encoding='utf-8-sig'))
    if not report['passed'] or not analysis['passed']:
        raise SystemExit(f'{environment} has not passed; results were not exported.')
    reports[environment] = (source, report, analysis)
destination.mkdir(parents=True, exist_ok=True)
for environment, (source, _, _) in reports.items():
    for filename in ('report.json', 'analysis.json'):
        shutil.copy2(source / filename, destination / (environment.lower() + '-' + filename))
for filename in ('two-voices-tail.wav', 'moving-loop.wav'):
    shutil.copy2(reports['Player'][0] / filename, destination / filename)
hashes = {str(path.relative_to(root)).replace('\\', '/'): hashlib.sha256(path.read_bytes()).hexdigest()
    for path in (root / 'app_build/SpatialAudio').rglob('*') if path.is_file() and path.suffix in ('.cs', '.asmdef', '.xml')}
(destination / 'source-sha256.json').write_text(json.dumps(hashes, indent=2), encoding='utf-8')
print('Exported passing Editor/Player reports, unmodified Player recordings, and source hashes.')
