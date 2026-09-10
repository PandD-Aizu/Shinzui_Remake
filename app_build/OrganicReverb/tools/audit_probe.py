"""Audit deployment parity, Unity metadata and the isolated experiment's assembly edges."""
import json
import pathlib

root = pathlib.Path(__file__).resolve().parents[3]
asset_root = root / 'Assets/Shinzui/Tests/OrganicReverb'
failures = []
for layer in ('Infrastructure', 'Editor'):
    for source in (root / 'app_build/OrganicReverb' / layer).iterdir():
        if source.is_file():
            deployed = asset_root / layer / source.name
            if not deployed.exists() or source.read_bytes() != deployed.read_bytes():
                failures.append(f'Not synchronized: {source.name}')
for path in [asset_root, *asset_root.rglob('*')]:
    if path.suffix != '.meta' and not pathlib.Path(str(path) + '.meta').exists():
        failures.append(f'Missing .meta: {path.relative_to(root)}')
allowed = {
    'Shinzui.AudioProbe.Infrastructure': {'FMODUnity', 'SteamAudioUnity', 'SteamAudioFMODStudio'},
    'Shinzui.AudioProbe.Editor': {'Shinzui.AudioProbe.Infrastructure', 'FMODUnity', 'FMODUnityEditor', 'SteamAudioUnity', 'Unity.Addressables.Editor'}
}
for path in asset_root.rglob('*.asmdef'):
    definition = json.loads(path.read_text())
    if set(definition['references']) != allowed[definition['name']]:
        failures.append(f'Unexpected layer dependency: {path.name}')
    if definition['name'].endswith('.Editor') and definition['includePlatforms'] != ['Editor']:
        failures.append('Editor test assembly would be included in Player')
for source, target in (
    ('app_build/Domain/Entities/Inventory/InventoryEntity.cs', 'Assets/Shinzui/Src/Domain/Entities/Inventory/InventoryEntity.cs'),
    ('app_build/View/LoopTunnel/TunnelGateView.cs', 'Assets/Shinzui/Src/View/LoopTunnel/TunnelGateView.cs'),
    ('app_build/Title/ButtonController.cs', 'Assets/Shinzui/Src/Title/ButtonController.cs'),
):
    if (root/source).read_bytes() != (root/target).read_bytes():
        failures.append(f'Compatibility source not synchronized: {source}')
print(json.dumps({'passed': not failures, 'failures': failures}, indent=2))
raise SystemExit(1 if failures else 0)
