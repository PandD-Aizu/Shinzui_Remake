# 深い隧道 — Windows HDRP validation

Branch: `codex/hdrp-graphics-overhaul`. Unity `6000.5.8f1`, HDRP `17.5.0`, Windows x64 IL2CPP development Player.

The source HEAD was `72c028009adcae065dac217be09c06bf9dd0c444`. Its existing dirty work was preserved in local baseline commit `f903b3c`; all 197 snapshotted dirty files remain unchanged. No push, PR, merge or purchase was performed.

## Outcome

The production Title and addressable StageTemp run in HDRP, with recursive portal views, transactional graphics settings, native Windows execution and verified GPU ray tracing. **Ultra does not meet the approximately 60 FPS target on the tested RTX 3080.** A tested Custom configuration exceeds 60 FPS on average in the fixed render workload, with real RT and portals enabled. This is not a guarantee for live gameplay or every procedural seed.

## Implemented

- Title controls: Low/Medium/High/Ultra/Custom; supported resolution/refresh; exclusive fullscreen/windowed/borderless; VSync/FPS cap; texture/filter/mesh quality; AA; shadows; indirect lighting/reflections; volumetric fog; RT request; AO/contact shadows; reflection strength; bloom, DOF, blur, grain, distortion, brightness and portal resolution. HDR output is exposed when the display reports support.
- Apply/Cancel/Defaults operate on drafts. Graphics drafts do not resize or auto-save. Audio/camera/accessibility preview their own categories. Display candidates require confirmation within 15 unscaled seconds, reverting on rejection, timeout or focus loss. Atomic persistence and failed-save rollback preserve confirmed settings. Runtime overrides apply to newly loaded cameras/scenes.
- Four HDRP production assets; dynamic GI/reflections; physical light conversion; concrete normal/mask textures; controlled exposure, tone mapping and fog. Ultra requests RTGI/RTR only when DX12, hardware, resources and frame settings permit, with high raster settings otherwise. Capability text separates hardware support from configured effects.
- Production materials, flashlight, VFX, web burning, supernatural body/eye/lens shaders and gameplay feedback. Procedural generation and regeneration remain dynamic; no single-seed bake was introduced.
- Portal cameras use the normal rendering schedule, two recursion levels, depth occlusion and user stencil bit 64. Isolated virtual flashlights replace unsafe global lighting mutations. No nested HDRP camera render request is used.
- Opt-in capture, Windows performance, GPU-ray and display-transaction harnesses; inactive during ordinary gameplay.

## Verification

| Check | Result / evidence |
|---|---|
| Title/settings EditMode | 26/26 passed; `title-tests-latest.json` |
| Procedural PlayMode | 4/4 passed; `tunnel-tests-latest.json` |
| Actual Title UI / stage transition | Passed; `scene-smoke.json`: draft retained across tabs, close cancels, defaults do not save, production addressable transition, seed 9182 regeneration preserves camera and 16 gates |
| Windows Player | Final cold build succeeded with zero errors and 33 warnings in 240 seconds; `windows-build.json` |
| DX12 raster / DX11 fallback | Native captures render correctly; DX11 reports RT unavailable and selects RayMarching |
| Actual traced work | GPU texture readback proves RTGI and reflection rays; raw/native evidence below |
| RT-off control | RT frame disabled, GI/reflections RayMarching, no ray-counter texture allocated; `raw-gpu-rays-off.json` |
| Source preservation | All 197 original dirty files unchanged; `source-preservation.json` |

Detection noise and death dissolve passed actual gameplay visual probes after correcting the HDRP scratch texture layout. Evidence: `after-noise-array-1080p.png` and `after-death-array-1080p.png`. Native display transactions passed all eight checks: draft isolation, cancel, actual candidate window, unsaved candidate, timeout rollback, focus-loss signal rollback and confirmed save. A separate Player process passed both restart checks and opened at the confirmed 1280 x 720 window size. See `player-display-transactions.json` and `player-display-restart.json`. The focus-loss test invokes the application signal; physical Alt-Tab remains untested.

## Performance

**NVIDIA GeForce RTX 3080, 10 GB.** StageTemp, seed **2777**, native **1920 × 1080**, render scale **1.0**, camera `(0, 2.2228, 0.248)`, rotation zero, flashlight on. VSync/cap off. Simulation frozen; DOF off because frozen gameplay cannot update autofocus. At least ten seconds/120 frames warmup, then at least 300 samples. These are development-player rendering workloads, not release traversal benchmarks.

| Configuration | API | Samples | Mean wall | P95 | FPS from mean |
|---|---|---:|---:|---:|---:|
| Ultra RT, final rebuilt executable | DX12 | 1142 | 26.28 ms | 28.70 ms | 38.05 |
| Ultra RT, earlier controlled run | DX12 | 1129 | 26.59 ms | 27.97 ms | 37.60 |
| High raster fallback | DX12 | 1396 | 21.53 ms | 23.15 ms | 46.45 |
| High raster fallback | DX11 | 1514 | 19.83 ms | 21.26 ms | 50.44 |
| Medium RT lighting, otherwise Ultra | DX12 | 1448 | 20.74 ms | 22.46 ms | 48.22 |
| Ultra RT, portals disabled — diagnostic only | DX12 | 1772 | 16.94 ms | 18.41 ms | 59.04 |
| Custom: medium RT lighting/fog, 50% portals | DX12 | 1971 | **15.23 ms** | **16.91 ms** | **65.66** |

The portal-off comparison attributes approximately **9.66 ms** to portal rendering in this view; it is not a shippable configuration. Only the main camera and two portal cameras are active. Reducing RT lighting quality saves about **5.86 ms** in this comparison. The supported Custom controls provide a faster option while Ultra retains higher quality. Custom P95 remains slightly above the 16.67 ms budget.

DX12's reported GPU timer is implausibly small (about 0.006 ms), so it is excluded from conclusions. Wall time and CPU frame time agree. DX11 reports a plausible average **19.76 ms GPU time**. No reliable per-pass GPU profile was obtained; cost conclusions use controlled Player comparisons. Raw samples and CPU/timer values are in the player JSON and `player-performance-summary.json`.

## Ray tracing proof

The final rebuilt Windows Player directly read **1,960,985 deferred GI rays** and **1,960,814 deferred reflection rays** in its sampled 1080p frame (`player-final-verified-rt-1080p.json`). Both full-resolution effects, pipeline support and the camera RT frame flag were enabled. The final screenshot renders successfully; the portal illumination discontinuity remains visible.

The high-quality 1080p editor GPU texture contains **1,961,684 deferred GI rays** and **1,961,520 deferred reflection rays** (`raw-gpu-rays-on.json`). Native medium RT confirms **490,355 GI / 455 reflection rays**; Custom confirms **490,385 GI / 448 reflection rays** in their sampled frames. Reduced quality changes sampling and effect coverage, rather than merely relabelling full quality.

HDRP 17.5's aggregate debug reduction dispatches the wrong kernel during its clear step, producing invalid totals and a missing-input diagnostic. Those totals are rejected. The retained evidence reads the actual per-pixel `R16_UInt` GPU texture asynchronously and sums by ray type on the CPU. Counter sampling occurs after timing. Intermediate aggregate-counter results are excluded from delivery.

## Visual evidence

- `before-native-1080p.png`: URP baseline, same seed/camera/native target.
- `after-lens-delta-1080p.png`: comparable HDRP editor view.
- `player-final-verified-rt-1080p.png`: final rebuilt native Windows Ultra output.
- `player-final-rt-1080p.png`: earlier controlled native Ultra comparison.
- `player-custom-rt-1080p.png`: faster Custom output.
- `player-final-raster-1080p.png`, `player-final-dx11-1080p.png`: fallback output.
- `title-options-final-1080p.png`: actual Title options hierarchy with existing preferences preserved.
- `portal-flashlight-depth-1080p.png`: isolated orientation/depth/flashlight proof.

Baseline timing is editor timing and must not be compared directly with Player FPS as an optimization claim. Screenshots have not been retouched to hide defects.

## Reproduce

Executable: `Builds/GraphicsValidation/Shinzui.exe`, relative to the worktree. `GraphicsWindowsBuild.Build()` builds Title and addressables; `GraphicsSceneSmoke.Start()` exercises the real transition/regeneration.

```text
-force-d3d12 -screen-fullscreen 0 -screen-width 1920 -screen-height 1080
--graphics-validation --graphics-label <name> --graphics-output <absolute-folder>
```

Add `--graphics-raster` for raster, or use `-force-d3d11` for RT-unavailable fallback. `--graphics-count-rays` reads GPU counters after timing. Custom options: `--graphics-lighting-quality 2 --graphics-fog-quality 2 --graphics-portal-scale 0.5`. `--graphics-no-portals` is diagnostic only. Avoid Player batch mode here: it failed to initialize the render pipeline.

For isolated native display checks use `--graphics-display-validation --graphics-output <absolute-folder>`, then a second process with the same arguments plus `--graphics-display-restart`. It uses `display-test-settings.json` in the evidence folder, never the user's preferences.

## Remaining limits

- Ultra's 60 FPS goal is not achieved on this RTX 3080. Full traversal, worst-case seeds, long sessions, release builds and other GPU tiers need coverage. HDR display behavior was unavailable on this display. Exclusive fullscreen/multi-monitor recovery needs manual hardware testing.
- Portal views use raster lighting; indirect illumination differs visibly from the RT main view. Rays do not traverse the non-Euclidean connection. Eight virtual-light slots support four visible portals at two recursion levels. Portal boundaries need more art/performance work.
- The deformed/dithered black-hole body is deliberately excluded from RT geometry. Raster body/eyes/lens remain visible. Webs and supernatural effects are stylized. Existing geometry and art coverage do not establish AAA fidelity or full photorealism.
- Non-production authoring scenes retain legacy URP content; legacy URP resources remain referenced but are not used by the HDRP gameplay path. Existing NavMesh startup warnings and a small native-allocation warning at Player shutdown remain outside this graphics change.
- Final build warnings concern retained URP shaders (including expected pipeline-tag stripping), disabled Pipeline runtime tooling and IL2CPP-generated code. One long-running Editor crashed in native shader stripping during a rebuild; a fresh Editor completed the final build and both native display tests passed. Ordinary gameplay does not enable the faulty HDRP debug counter reduction.
