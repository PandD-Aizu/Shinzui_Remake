"""Unpack trusted Unity packages, preserving GUIDs and validating every asset path."""
import pathlib
import sys
import tarfile

destination = pathlib.Path(sys.argv[1]).resolve()
for package in sys.argv[2:]:
    with tarfile.open(package, "r:gz") as archive:
        members = {m.name: m for m in archive.getmembers() if m.isfile()}
        count = 0
        for name in members:
            if not name.endswith('/pathname'):
                continue
            asset_path = archive.extractfile(members[name]).read().decode('utf-8').strip()
            relative = pathlib.PurePosixPath(asset_path)
            if relative.is_absolute() or '..' in relative.parts or relative.parts[0] != 'Assets':
                raise ValueError(f'Unexpected asset path: {asset_path}')
            target = (destination / asset_path).resolve()
            target.relative_to(destination)
            prefix = name.rsplit('/', 1)[0]
            for entry, output in [('asset', target), ('asset.meta', pathlib.Path(str(target) + '.meta'))]:
                member = members.get(prefix + '/' + entry)
                if member:
                    output.parent.mkdir(parents=True, exist_ok=True)
                    data = archive.extractfile(member).read()
                    if output.exists() and output.read_bytes() != data:
                        raise FileExistsError(f'Refusing to replace different content: {output}')
                    output.write_bytes(data)
            count += 1
        print(f'{pathlib.Path(package).name}: {count} assets')
