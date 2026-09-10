"""Keep measured evidence and an unnormalized A/B recording outside Unity's disposable logs."""
import json
import pathlib
import shutil
import wave

root = pathlib.Path(__file__).resolve().parents[3]
destination = root / 'app_build/OrganicReverb/results'
destination.mkdir(parents=True, exist_ok=True)
for environment in ('Editor', 'Player'):
    source = root / 'Logs/OrganicReverb' / environment
    for filename in ('report.json', 'analysis.json'):
        data = json.loads((source / filename).read_text(encoding='utf-8-sig'))
        if not data['passed']:
            raise SystemExit(f'{environment}/{filename} has not passed; results were not exported.')
        shutil.copyfile(source / filename, destination / (environment.lower() + '-' + filename))
cases = ['small-room-dry', 'small-room-wet', 'corridor-wet', 'outdoor-wet']
with wave.open(str(destination / 'comparison.wav'), 'wb') as result:
    reference = None
    for case in cases:
        with wave.open(str(root / 'Logs/OrganicReverb/Player' / (case + '.wav')), 'rb') as source:
            audio_format = (source.getnchannels(), source.getsampwidth(), source.getframerate())
            if reference is None:
                reference = audio_format
                result.setnchannels(reference[0]); result.setsampwidth(reference[1]); result.setframerate(reference[2])
            if audio_format != reference:
                raise ValueError('Capture formats differ; no resampling or normalization is performed.')
            result.writeframes(source.readframes(source.getnframes()))
(destination / 'comparison-order.txt').write_text('Order: dry -> small room reverb -> corridor reverb -> outdoor reverb.\nEach segment is approximately 5 seconds. Original capture gain preserved.\n', encoding='utf-8')
print(destination)
