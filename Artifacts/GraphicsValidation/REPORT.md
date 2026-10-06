# 深い隧道 — Windows HDRP validation

Branch: `codex/hdrp-graphics-overhaul`. Unity `6000.5.8f1`, HDRP `17.5.0`, Windows x64 IL2CPP development Player.

Source HEAD: `72c028009adcae065dac217be09c06bf9dd0c444`; preserved dirty baseline: `f903b3c`; initial HDRP implementation: `17c6379`. All 197 snapshotted original dirty files remain unchanged. No push, PR, merge or purchase was performed.

## Outcome

The production Title and addressable StageTemp run in HDRP, with transactional graphics controls and measured hardware RT. Two 45-second normal-simulation Ultra runs on the RTX 3080 at native 1080p averaged **59.97 FPS** (seed 2777) and **59.50 FPS** (regenerated seed 9182), with DOF, autofocus, enemies, collision, stamina and production portal crossings active. This supports the approximately 60 FPS **average** target for these short routes. P95 was **26.69 ms** in both and 1% lows were **26.46 / 21.68 FPS**, so smooth locked 60 FPS is not established.

Windows rejected exclusive fullscreen and foreground activation during the approved visible launches. The Player rendered in borderless mode at 74.973 Hz with the saved Ultra defaults (VSync enabled, 60 FPS cap, requested 60 Hz). The actual render workload is valid, but exclusive-fullscreen/focus behavior is not validated. The previous foreground window was restored after both Players exited.

## Implemented

- Title controls: Low/Medium/High/Ultra/Custom; supported resolution/refresh; exclusive fullscreen/windowed/borderless; VSync/FPS cap; texture/filter/mesh quality; AA; shadows; indirect lighting/reflections; volumetric fog; RT request; AO/contact shadows; reflection strength; bloom, DOF, blur, grain, distortion, brightness and portal resolution. HDR output is exposed when the display reports support.
- Apply/Cancel/Defaults operate on drafts. Graphics drafts do not resize or auto-save. Audio/camera/accessibility preview their own categories. Display candidates require confirmation within 15 unscaled seconds, reverting on rejection, timeout or focus loss. Atomic persistence and failed-save rollback preserve confirmed settings. Runtime overrides apply to newly loaded cameras/scenes.
- Four HDRP production assets; dynamic GI/reflections; physical light conversion; concrete normal/mask textures; controlled exposure, tone mapping and fog. Ultra requests RTGI/RTR only when DX12, hardware, resources and frame settings permit, with high raster settings otherwise. Capability text separates hardware support from configured effects.
- Production materials, flashlight, VFX, web burning, supernatural body/eye/lens shaders and gameplay feedback. Procedural generation and regeneration remain dynamic; no single-seed bake was introduced.
- Portal cameras use the normal rendering schedule, two recursion levels, depth occlusion and user stencil bit 64. Isolated virtual flashlights replace unsafe global lighting mutations. No nested HDRP camera render request is used.
- Opt-in capture, Windows performance, GPU-ray and display-transaction harnesses; inactive during ordinary gameplay.


- Cropped portal targets shade the visible rectangle while preserving requested pixel density and two recursion levels. The fixed view uses 416 x 288 and 208 x 144 targets instead of 1440 x 810 and 720 x 405. A guard band, outward quantization and full-view fallback at the near plane preserve the aperture during movement.
- Portal indirect lighting now uses RT when configured. Matching the camera near-depth constants to its oblique clip projection removes the broad depth-lighting bands seen during migration. Portal reflection views remain raster; rays do not traverse the non-Euclidean connection. A visible illumination contrast at the doorway remains.
- Ultra uses half-resolution RTGI/RTR with rough-surface reflection coverage retained. Highest raster GI/reflection/fog is selected when RT is unavailable. Earlier full-resolution Ultra saves retain their values and appear as Custom; preferences are not silently overwritten.

## Quality and cost tradeoffs

| Feature | Earlier full-resolution Ultra | Tuned Ultra with RT |
|---|---|---|
| Main target / textures / shadows | Native 1080p / full / highest | Unchanged |
| GI rays | Full resolution, 50 m, 64 steps | Half resolution, 50 m, 48 steps |
| Reflection rays | Full resolution, 50 m, 64 iterations | Half resolution, 50 m, 64 iterations |
| Reflection surface threshold / fade | 0 / 0 | 0 / 0, including rough concrete |
| Denoising | Enabled | Enabled; full-resolution GI denoiser retained |
| Fog budget | HDRP High: 0.666 | HDRP Medium: 0.330; density/color preserved |
| Portal sampling / recursion | 75% / 37.5%, two levels | Same density and depth, cropped targets |
| Portal indirect / reflections | Raster / raster | RTGI / raster |

Half-resolution effects have less sample detail and temporal reconstruction can soften or lag during motion. Medium fog reduces spatial/depth detail. Full-resolution lighting/fog remains selectable through Custom. This is a measured sampling tradeoff, not a claim that every setting is at maximum.

## Verification

| Check | Result / evidence |
|---|---|
| Title/settings EditMode | 27/27 passed, including old-save preservation; `title-tests-latest.json` |
| Procedural PlayMode | 4/4 passed after portal changes; `tunnel-tests-latest.json` |
| Real Title / addressable transition | Passed with tuned defaults; tab draft, cancel, defaults, transition, seed 9182 regeneration, camera and 16 gates; `scene-smoke.json` |
| Windows build | Succeeded, 0 errors, 33 warnings, 219.5 seconds; `windows-build.json` |
| Native crossing / angled view / regeneration | All 11 checks passed through the production warp event, exactly one warp; `player-tuned-ultra-rt-1080p-crossing.json` |
| Native display transactions / new-process restart | Eight transaction checks and two restart checks passed; `player-display-transactions.json`, `player-display-restart.json` |
| DX11 RT-unavailable fallback | Actual rendered frame, RT unavailable, RayMarching, high quality levels 2/2 and full-resolution screen-space GI; `player-tuned-fallback-dx11-1080p.json` |
| Normal native Ultra gameplay | Two 45-second runs passed render/movement/DOF/enemy/portal checks; mean 59.97 / 59.50 FPS, uneven frame pacing; `normal-gameplay-summary.json` |
| Actual traced work | Raw GPU counters: 490,209 GI / 490,177 reflection rays in a sampled frame |
| Source preservation | All 197 original dirty files unchanged; `source-preservation.json` |

Detection noise and death dissolve passed gameplay visual probes. The display focus-loss test invokes the application signal; physical Alt-Tab remains untested. The native crossing checks observe `PlayerView.Warped` driven by `TunnelLoopPresenter`, not a direct test call to Warp. Its short movement samples are diagnostic, not a traversal benchmark, and some movement frames may exceed 16.67 ms.

## Normal gameplay performance

Windows x64 IL2CPP development Player, NVIDIA GeForce RTX 3080 10 GB, DX12, native 1920 x 1080, render scale 1.0. Production addressable StageTemp; seed 2777 and production regeneration to 9182 before warmup. Each run lasted 45 seconds with 5 seconds of ordinary-simulation warmup and about 40 seconds sampled. The Editor was closed. Screenshot encoding occurred after sampling.

The actual saved Ultra defaults were written to and reloaded from an isolated diagnostic settings file: DOF on, VSync on, 60 FPS cap, native scale and RT on. User preferences were not written. Virtual gamepad events went through production InputActions. The harness did not teleport the player, call Warp, disable enemies/death/camera, or freeze simulation. Time scale stayed 1; Cinemachine and autofocus stayed enabled. Both runs reported HDRP RT support and RT frame settings enabled; actual traced rays were independently verified in the retained fixed-render evidence.

| Seed | Samples | Mean frame | Mean FPS | P95 frame | P99 frame | 1% low FPS | Worst frame |
|---|---:|---:|---:|---:|---:|---:|---:|
| 2777 | 2399 | 16.67 ms | 59.97 | 26.69 ms | 26.69 ms | 26.46 | 53.37 ms |
| 9182 (regenerated) | 2381 | 16.81 ms | 59.50 | 26.69 ms | 40.01 ms | 21.68 | 80.03 ms |

Mean FPS is 1000 divided by mean sampled frame milliseconds. P95/P99 use nearest rank. The 1% low is 1000 divided by the mean of the slowest 1% of frames. Lowest single-frame rates were 18.74 / 12.50 FPS, respectively. These are capped measurements; they do not measure uncapped headroom. CPU-frame means were 16.67 / 16.81 ms. GPU telemetry included frequent zero samples and is retained without treating its average as reliable GPU cost.

Seed 2777 moved 101.97 m and crossed two portals; regenerated 9182 moved 102.75 m and crossed three. Neither player died. Each run observed two enabled enemies on the NavMesh in all 92 enemy observations; 71 / 59 observations recorded movement. The director entered Ambient, Hunt and Search. DOF remained active with changing physical-camera focus distances. Final screenshots retain normal detection noise and visible enemies; they are not clean beauty renders.

No runtime error/exception was captured during loading and the measured gameplay. Each log had four startup NavMesh warnings (the agents subsequently navigated), two exclusive-fullscreen failures and two borderless-fallback warnings. Foreground requests returned false, with a Steam helper observed owning foreground during the test. Actual mode was FullScreenWindow at 74.973 Hz, while saved settings requested exclusive 60 Hz. The combination of 60 FPS cap and 74.973 Hz VSync is a plausible contributor to the observed 13.34/26.68 ms cadence; no controlled causal profile was performed. The harness did not change those defaults to improve the result.

After evidence was written, each capture log recorded four InputSystem assertions from diagnostic virtual-device cleanup and a persistent-allocation warning at shutdown. The harness now leaves device disposal to InputSystem during application quit. That cleanup-only change is rebuilt; no extra visible gameplay run was performed to recheck shutdown. It does not change the measured scene or gameplay path. The two bounded approved runs are complete, and previous foreground restoration succeeded.

Evidence: `player-normal-ultra-seed2777.json`, `player-normal-ultra-regenerated9182.json`, their settings JSON and PNG files, `normal-gameplay-summary.json` and `normal-gameplay-launch.json`.

## Fixed-render comparison

NVIDIA GeForce RTX 3080, 10 GB, StageTemp seed 2777, native 1920 x 1080, render scale 1.0, camera `(0, 2.2228, 0.248)`, rotation zero, flashlight on. VSync/cap off. Simulation frozen and DOF off because autofocus does not update while frozen. At least 10 seconds/120 rendered frames warmup. Counter instrumentation runs separately after timing. Editor closed during Player measurements.

| Configuration | Samples | Mean wall | P95 | FPS from mean |
|---|---:|---:|---:|---:|
| Earlier full-resolution Ultra, original portal implementation | 1142 | 26.28 ms | 28.70 ms | 38.05 |
| Full-resolution Ultra, cropped portals + portal RTGI | 1280 | 23.44 ms | 24.61 ms | 42.66 |
| Cropped Medium candidate with rough reflections mostly excluded | 2047 | 14.66 ms | 15.85 ms | 68.20 |
| Covered-reflection candidate, same sampling as tuned Ultra | 1927 | 15.58 ms | 16.11 ms | 64.20 |
| Final tuned Ultra, default preset from rebuilt Player | 1873 | **16.03 ms** | **16.58 ms** | **62.39** |
| Final highest raster fallback, DX11 | 1575 | 19.06 ms | 21.20 ms | 52.46 |

The 68.20 FPS comparison traced only 456 reflection rays because HDRP Medium excludes most rough surfaces. It was not selected as Ultra. The covered candidate and final Ultra retain the full-quality smoothness eligibility, with about one quarter of the full-resolution ray count. Native screenshots show the corresponding appearance difference.

DX12's reported GPU timer remains implausibly small (about 0.006 ms) and is excluded from performance conclusions. Wall time and CPU frame time agree. No reliable per-pass DX12 GPU profile was obtained. Raw samples and timer values are retained in the Player JSON and `player-performance-summary.json`.

The validation harness now requires actual main-camera render callbacks. A suspended-window run that only advanced Update was rejected; its blank image and invalid timings are excluded from delivery. Start with fullscreen enabled as shown below, then the harness applies the controlled windowed target.

## Ray tracing evidence

Final native evidence reports hardware support, pipeline support, enabled RT frame settings and actual GPU rays separately. Main and both active portal cameras have RT/indirect frame settings enabled. Counters cover the main 1080p texture, not a sum of all cameras. HDRP 17.5's aggregate debug reduction produces invalid totals; retained evidence instead asynchronously reads the per-pixel `R16_UInt` texture and sums by ray type on the CPU. Ordinary gameplay does not enable this debug counter.

## Visual evidence

- `player-normal-ultra-seed2777.png`, `player-normal-ultra-regenerated9182.png`: unfrozen Ultra, normal DOF and gameplay feedback, captured after the measured route.
- `before-native-1080p.png`: preserved URP baseline, matched seed/camera/native target.
- `player-final-verified-rt-1080p.png`: earlier full-resolution Windows Ultra.
- `player-cropped-ultra-retry-1080p.png`: portal optimization at original full-resolution lighting.
- `player-cropped-medium-rt-1080p.png`: rejected limited-reflection candidate.
- `player-tuned-ultra-rt-1080p.png`: final default Ultra, actual Windows Player.
- `player-tuned-ultra-rt-1080p-after-crossing.png`, `-angled.png`, `-regenerated.png`: actual crossing and regenerated output.
- `player-tuned-fallback-dx11-1080p.png`: highest raster fallback.
- `title-options-tuned-1080p.png`: real Title controls, preserving existing preferences.
- `after-noise-array-1080p.png`, `after-death-array-1080p.png`: gameplay feedback probes.

Full-resolution local PNGs are unretouched. The portable HTML uses compressed 1280 x 720 previews. Baseline timing was Editor timing and is not compared directly with Player FPS as a speedup claim. The pre-crossing capture can be obscured by the nearby supernatural body; post-crossing and angled captures show the tunnel clearly.

## Reproduce

Executable: `Builds/GraphicsValidation/Shinzui.exe`, relative to the worktree. `GraphicsWindowsBuild.Build()` builds Title and addressables.

```text
-force-d3d12 -screen-fullscreen 1 -screen-width 1920 -screen-height 1080
--graphics-validation --graphics-label <name> --graphics-output <absolute-folder>
--graphics-count-rays --graphics-crossing-validation
```

Use `-force-d3d11` without `--graphics-raster` to verify Ultra automatically falls back when hardware/API RT is unavailable. `--graphics-raster` explicitly selects a Custom raster configuration. Full lighting/fog: `--graphics-lighting-quality 3 --graphics-fog-quality 3`. `--graphics-uncropped-portals` and `--graphics-no-portals` are diagnostic comparisons only. Avoid Player batch mode for this rendering harness.

Normal gameplay: add `--graphics-gameplay-validation --graphics-gameplay-seed 2777 --graphics-label player-normal-ultra-seed2777 --graphics-output <absolute-folder>` to the native DX12 launch, without the fixed-render `--graphics-validation` flag. Repeat with seed 9182 for production regeneration. Launch visibly after approval; allow 45 seconds plus loading.

Native display check: `--graphics-display-validation --graphics-output <absolute-folder>`, then a second process with `--graphics-display-restart`. It uses an isolated test settings file, never the user's preferences.

## Remaining limits

- Two short normal-play routes support about 60 FPS average, but P95 and 1% lows show uneven pacing. Full-game traversal, worst-case seeds, long sessions, release builds and other GPU tiers are not measured. Exclusive fullscreen and confirmed foreground behavior remain unvalidated on this host; the measured runs used the automatic borderless fallback.
- Portal boundary illumination contrast remains. Portal reflections are raster and rays do not cross the non-Euclidean connection. Eight virtual-light slots cover four visible portals at two recursion levels. More camera angles and simultaneous visible portals need art/QA coverage.
- The raster fallback is visibly brighter than the RT path; matching their indirect-light appearance needs further art tuning. Its 52.46 FPS result also misses the 60 FPS target on this measured configuration.
- The deformed/dithered black-hole body is deliberately excluded from RT geometry; raster body/eyes/lens remain visible. Webs and supernatural effects remain stylized. Existing geometry/art do not establish AAA fidelity or full photorealism.
- HDR display behavior is untested on the available non-HDR monitor. Physical Alt-Tab, exclusive-fullscreen recovery and multi-monitor changes require hardware/manual checks.
- Non-production authoring scenes retain legacy URP content/resources. Existing NavMesh startup warnings and a small native allocation warning at Player shutdown remain. Build warnings include legacy shader stripping, disabled Pipeline runtime tooling and generated IL2CPP code. A prior long-running Editor crashed during shader stripping; fresh Editor builds succeeded.
