# MIT KEMAR compact HRIR measurements

The unchanged `mit-kemar-compact.zip` archive contains the measured head-related impulse responses by **Bill Gardner and Keith Martin**, MIT Media Laboratory (1994), *HRTF Measurements of a KEMAR Dummy-Head Microphone*, Perceptual Computing Technical Report #280. Copyright 1994 MIT Media Laboratory.

Official source and usage terms: https://sound.media.mit.edu/resources/KEMAR.html

Original download: https://sound.media.mit.edu/resources/KEMAR/compact.zip

Measurement/format documentation: https://sound.media.mit.edu/resources/KEMAR/hrtfdoc.txt

Downloaded 2026-09-21. SHA-256:

`0bcd69f8e8760cf8eacff4a89594ceca7d44fb94f28ecf0bf19970003bbfb3e0`

The official source permits research and commercial use without restrictions, with attribution to the authors. Preserve this attribution when redistributing or using these measurements. This data is not described as MIT-licensed software.

## Contents and coordinates

The archive has 368 stereo PCM16 WAV files, 128 frames each at 44.1 kHz. They contain the compact speaker-equalized, symmetric small-pinna responses, with elevation -40 to +90 degrees. Azimuth 0 is front, 90 is right, 180 is back. Stereo samples are left then right. The loader mirrors the missing hemisphere by swapping ears, yielding 710 directions. No synthetic panning data is substituted for measurements.

The runtime loads the archive on the control thread, never the audio thread. No files are extracted. `HrirDataset.LoadMitKemarCompact(path, outputSampleRate)` validates its bounded ZIP/WAV inputs and returns immutable data. This path-based loader targets the Windows desktop prototype; platforms with URL-backed StreamingAssets must read bytes outside the audio thread and supply a seekable stream.

## Rendering and limitations

Azimuth interpolation wraps across 360 degrees; elevation is interpolated between rings and clamps at the measured -40/+90 limits. Listener forward/up vectors transform world arrival directions before interpolation. Interpolation blends paired time-domain responses on the control thread. It does not perform ITD alignment or spherical triangulation; sparse custom grids can colour moving sound. The measured grid's finite angular resolution and generic dummy head do not guarantee individualized front/back or elevation perception.

Rate conversion uses a 32-source-tap Hann-windowed sinc with antialiasing, shared ear timing, gain compensation and zero padding. At a different sample rate it introduces a common causal latency of 16/44100 seconds (about 0.363 ms); 44.1 kHz playback uses the original data directly. 48 kHz uses 175 taps per ear and 96 kHz uses 349. This is in addition to travel delay and the onset in the original compact response. Resampling preserves relative interaural delay and level and does not normalize ears independently.

The default `maxHrirPaths: 1` uses FIR convolution on the direct path and stereo panning on reflections. An explicit budget up to 7 adds the strongest early paths for quality comparison; `HrirPathCount` reports actual prepared paths. Their existing 20 ms output crossfade handles head/origin/filter changes. The diffuse late tail remains a decorrelated stereo approximation. Use headphones for directional evaluation. FIR cost increases with tap count and active paths; see the project improvement report for measured multi-source limits. This remains a CPU reference renderer, without partitioned convolution or shared late returns.

Custom data can be constructed with `HrirWavReader.ReadPcm16Stereo(bytes)`, `HrirMeasurement(azimuthDegrees, elevationDegrees, filter)` and `new HrirDataset(measurements)`. Each elevation ring should span a full circle; only the MIT archive loader mirrors a hemisphere. All measurements must share sample rate and tap count, with at most 512 taps per ear.
