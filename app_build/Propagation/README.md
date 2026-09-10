# 音の伝播と演出調整（段階4）

本編の生成トンネル音響へ、体積を持つ音源の遮蔽、周波数別の透過、方向変化時のHRTF補間、直接音と反射音の配分を追加します。既存イベントの音素材は維持し、シーンの設定をDSPへ適用します。

## 本編設定

[TunnelAudioConfiguration.asset](D:/Pandd/ShinShinzui/Assets/Shinzui/Audio/Resources/SpatialAudio/TunnelAudioConfiguration.asset) のPropagationで変更できます。サービス生成時に設定をコピーします。実行中のInspector変更を継続して反映する仕組みではありません。

| 項目 | 初期設定 | 意図 |
|---|---|---|
| VolumetricOcclusion / SourceRadius | 有効 / 0.35m | 入口や扉の端で遮蔽を段階的に変える |
| OcclusionSamples | 32 | 音源周囲の複数点で遮蔽を判定 |
| Transmission / MaxTransmissionSurfaces | 有効 / 4 | 面の材質に従って壁越しの低・中・高音を減衰 |
| DirectGain | 1 | 直接音を基準にする |
| ReflectionGain | 0.35 | 初期反射と残響を約9dB抑え、直接音を聞き取りやすくする |

FMODのSteam Audio Spatializerには、距離減衰Physics-Based、HRTF補間Bilinear、Transmission Type=Frequency Dependentを適用します。距離減衰はこのDSP内で行い、追加の手動減衰は重ねません。音響計算だけをUnity側で有効にして、FMOD側の透過が無効のままになる状態を避けています。

Steam Audio全体の設定はMax Occlusion Samples=32、Real Time Bounces=64、Real Time Max Sources=16です。短かった残響後半を伸ばすため、反射回数を16から増やしました。IRの上限時間3秒、8192本の反射レイ、更新間隔0.1秒は維持します。反射回数の増加は計算量を増やすため、段階5で製品の負荷予算を確定します。

音源のOcclusionSamplesがシステム上限を超える場合は、SDKへ渡す前に設定エラーとして拒否します。Propagationをnullにしたサービスでは、従来のFMODイベント設定をそのまま使います。既存の段階2テストはこの経路です。

## 材質と方向

壁の係数は前工程のAcousticSurface（Concrete / Metal / Wood）を使用します。高音が低音より強く減衰する係数です。これらは面ごとの初期値で、特定の建材・壁厚を実測した値ではありません。音響メッシュの厚みや重なりによって通過する面数が変わるため、Collider形状と重複面も確認してください。

L字通路や離れた開口部では、直接音とSteam Audioの幾何形状に基づく反射音を分けて測定します。直接の音線が壁で塞がれていても反射音が届くこと、入口の左右で初期反射の左右エネルギーが変わることを検証します。独自の仮想音源やPathingを加算しません。

この方式は反射による方向表現です。波動としての回折や、入口を通る最短経路を保証する方式ではありません。ワープポータルをまたぐ音の継続は対象外で、段階3の旧足音解除・移動先での準備・速度ゼロの仕様を維持します。

## 検証

検証専用の19条件で、距離2/4/8m、材質別の閉じた壁、中央・左右の入口、閉じた扉、L字通路、移動する扉の境界を録音します。パルスの直接音・反射音・本編設定での合成音を分け、低音300Hz・中音1800Hz・高音10000Hzの測定用信号で透過を確認します。足元の高さで床に近い音源についても、直接音が不自然に遮られないことを確認します。

反射音の遅い成分、開口部による左右差、境界移動の10msごとの音量変化は録音から計測します。試験は個別の形状・位置における信号の確認であり、全プレイヤーの聴感や全生成形状を保証するものではありません。最終的な素材・好みの残響量は比較録音で確認できます。

## 再実行

正本は `app_build/SpatialAudio`、`app_build/TunnelAcoustics` と本ディレクトリです。同期スクリプトは既存.metaのGUIDを維持します。Application / View / Presentationの新たな依存関係はありません。

```powershell
python ./app_build/Propagation/tools/sync_sources.py
python ./app_build/Propagation/tools/generate_tone.py
& 'C:/Program Files/FMOD SoundSystem/FMOD Studio 2.03.12/fmodstudiocl.exe' -script ./app_build/Propagation/tools/create_tone_event.js ./Shinzui/Shinzui.fspro
& 'C:/Program Files/FMOD SoundSystem/FMOD Studio 2.03.12/fmodstudiocl.exe' -build -banks PropagationProbe -platforms Desktop ./Shinzui/Shinzui.fspro
./app_build/Propagation/tools/run_probe.ps1 -Action Create
./app_build/Propagation/tools/run_probe.ps1 -Action RunEditor
./app_build/Propagation/tools/run_probe.ps1 -Action Build -TimeoutSeconds 1200
./app_build/Propagation/tools/run_probe.ps1 -Action RunPlayer -TimeoutSeconds 300
```

録音解析にはNumPyを使用します。今回の環境では次のPythonを使いました。別環境ではNumPyの入ったPythonで実行できます。

```powershell
$propagationPython = 'C:/Users/koton/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
& $propagationPython ./app_build/Propagation/tools/analyze_results.py ./Logs/Propagation/Editor
& $propagationPython ./app_build/Propagation/tools/analyze_results.py ./Logs/Propagation/Player
python ./app_build/Propagation/tools/audit_sources.py
python ./app_build/Propagation/tools/export_results.py
```

以前の段階のCreateを実行した場合は、最後に本段階のCreateで音響の上限・反射回数・本編設定を適用してください。前段階の回帰試験はCreateせずRunEditorで実行できます。

[検証結果と録音](D:/Pandd/ShinShinzui/app_build/Propagation/results/summary.md)

仕様の参照: [Steam Audio Source](https://valvesoftware.github.io/steam-audio/doc/unity/source.html)、[FMOD Spatializer](https://valvesoftware.github.io/steam-audio/doc/fmod/spatializer.html)、[Steam Audio Settings](https://valvesoftware.github.io/steam-audio/doc/unity/settings.html)。
