# 独自空間音響：段階Aの実装と検証

2026-09-21 / Unity 6000.5.8f1 / Windows x64

追記：この文書は初回試作時点の記録。後続の減衰精度修正、Room/Portal伝播、実測HRIR両耳処理、追加検証は[改善記録](custom-spatial-audio-improvements-2026-09-21.md)を参照。

## 今回の到達範囲

調査資料を受け、矩形室の独立した比較試作を実装した。正式ソースは `Assets/Shinzui/Src`、試験コードは `Assets/Shinzui/Tests/CustomSpatialAudio` に配置した。既存のゲーム音響、Steam Audio設定、Studioイベント／Bank、シリアライズ済みのゲーム用シーンは置換していない。`app_build` への複製・同期も行っていない。

最初にFMOD Coreの独立したSystemと短いPCMを使い、カスタムDSPのABI、ステレオ出力、残響寿命を検証する。調査で提案した専用Studioイベント／Bankへの接続は未実装であり、段階A全体の製品統合完了を意味しない。

## 構成

| 配置 | 内容 |
| --- | --- |
| `Application/CustomSpatialAudio` | Unity非依存の座標、3帯域値、壁／開口、矩形室、7経路、残響応答DTO |
| `Infrastructure/CustomSpatialAudio/RectangularRoomAcoustics.cs` | 直達＋6面の一次鏡像反射、距離と到来方向、帯域別反射振幅、残響時間推定 |
| `Infrastructure/CustomSpatialAudio/Dsp` | 制御スレッドで係数作成、分数遅延、3帯域の8本FDN、20 msの固定タップ間クロスフェード |
| `Infrastructure/CustomSpatialAudio/FmodRoomAcousticProcessor.cs` | FMODカスタムDSP、ステレオ形式交渉、atomicな係数公開、音源より長寿命のChannelGroup |
| `Tests/CustomSpatialAudio` | NUnitテスト、独立Core経路の録音、Editorメニュー、IL2CPP試験用シーン生成／ビルド |

Applicationのasmdefは外部レイヤー参照なし・`noEngineReferences: true`。Infrastructureは独自ApplicationとFMODUnityのみを参照する。Presentation、View、Domain、既存DIへの依存追加はない。試験用EntryPointは専用Playerの構成を組み立てる。

## 計算と近似

- 軸に平行な箱の6面に対する一次鏡像法。直達と反射の遅延は距離÷音速、振幅は参照距離で上限を持つ1/r。各帯域の反射振幅は `sqrt((1-吸音率)*(1-開口率))`。
- 壁面積で重み付けした損失によるEyring型RT60を0.05–10秒に制限する。開口率はその面の平均損失であり、窓の位置や開口経由の経路を表すものではない。長いトンネルでの拡散音場近似の妥当性は未確定。
- 壁がすべて開放／完全吸音なら反射と新規残響入力は0。帯域ごとの残響入力にも反射可能なエネルギーを反映する。完全開放の試験は境界なしの近似で、地面反射を持つ屋外モデルではない。
- 3帯域それぞれの8本の遅延線に直交Householder散乱を用い、振幅フィードバックは `10^(-3*m/(fs*T60))`。室の残響エネルギー量は校正前のヒューリスティックであり、FDNとSDNの比較は未実施。
- 250 Hz／4 kHzの一次相補フィルターで入力を3帯域に分ける。帯域が重なるため、125 Hz／1 kHz／8 kHz等で測る出力の減衰時間は設定された各FDN成分のRT60とは一致しない。論文の精密な減衰フィルター設計を再現したとは扱わない。
- 後期残響への入力は最初の有効な壁反射以降に遅延させ、その後FDNの遅延を加える。大きい部屋で壁反射より先に残響が発生することを防ぐ。
- 方向表現は等電力ステレオパンのみ。HRTF、前後／上下の定位、頭部回転の知覚評価は未実装。経路更新は固定タップのクロスフェードで、意図しない連続遅延変化を避けるが、移動中の音質評価は残る。

根拠：[Allen & Berkleyの鏡像法](https://doi.org/10.1121/1.382599)、[Schlecht & HabetsのFDN減衰制御](https://dafx.de/paper-archive/2017/papers/DAFx17_paper_11.pdf)、[FMOD DSP API](https://www.fmod.com/docs/2.03/api/plugin-api-dsp.html)。公開方式に基づく独立実装であり、Bodycam内部の再現ではない。外部HRIRデータや論文付属コードは同梱していない。

## スレッド・寿命

幾何計算、三角関数、フィードバック係数、配列確保は制御スレッドで実施する。音声コールバックは固定バッファを使い、Unity API、形状探索、I/O、待機ロックを呼ばない。DTOから作成した不変係数への参照をatomicに渡し、DSP状態を変更するのは音声スレッドだけとする。

FMODグラフは「短い音源 → 独自DSP → Group Fader → 親Group」。音源が終わってもGroupが反射／残響を保持する。無音音源で寿命を延長しない。この試作のDSPは空Groupでも稼働し続けるため、所有者が明示的にDisposeする必要がある。同時音源の共有Return、自動的な末尾検出／省電力化は今後の対象。

Group音量は保持中の尾にも適用され、GroupのPauseは処理を停止する。既存ゲームのMaster／SEバスや設定UseCaseへの接続は未実装。プローブの録音はDSP直後のpre-faderであり、最終デバイス出力の録音やマイク収録ではない。

DSPを除去・解放する際だけ制御スレッドからFMODのmixer fenceを使い、解放が成功するまでGCHandleを保持する。コールバック関数はstatic＋AOT属性を持つ。録音データをファイルに出すのはコールバック停止後の制御スレッド。

## 実行方法

Unityのメニュー `Shinzui > Audio > Custom Spatial Audio` に以下を追加した。

1. `Validate and record`：同じ短いパルスを使い、小部屋の直達／反射／尾／合成、直線トンネル、全境界開放を録音する。
2. `Audition and record (3 seconds)`：Windows WASAPI出力で短いノイズ音と尾を3秒間再生・記録する。
3. `Create probe scene`：専用の試験シーンを追加生成する。編集中のシーンは保存・変更しない。
4. `Build Windows IL2CPP probe`：既存Standalone設定がIL2CPPであることを確認して、専用シーンだけをビルドする。

出力は `Artifacts/CustomSpatialAudio/Editor` と `Builds/CustomSpatialAudioProbe`。Playerには `-customAudioOutput <絶対パス>` と `-customAudioRealtime` を指定できる。後者がなければNOSOUND_NRTによるオフライン処理だけを行う。

```powershell
unity test D:/Pandd/ShinShinzui --mode EditMode --filter Shinzui.Tests.CustomSpatialAudio --output D:/Pandd/ShinShinzui/Artifacts/CustomSpatialAudio/editmode.xml --timeout 600 -- -nographics
unity run D:/Pandd/ShinShinzui --timeout 1200 -- -executeMethod Shinzui.CustomSpatialAudio.Probe.Editor.CustomSpatialAudioProbeBuilder.ValidateAndBuild
python docs/research/tools/analyze_custom_spatial_audio.py Artifacts/CustomSpatialAudio/Editor
```

## 検証記録

Unity Editorの実コンパイル後、NUnit **22件成功／失敗0／スキップ0**（幾何10件、DSP12件）。解析的な到着時刻、吸音と開口、無効入力、大部屋の因果順、残響寿命、分割ブロックの一致、切り替えの連続性を確認した。[テスト結果](../../Artifacts/CustomSpatialAudio/editmode.xml)

EditorのFMOD Core経路では **6条件すべて成功**。各録音は48 kHz・145,920フレーム（3.04秒）、callback errorとoverflowは0。パルス音源の終了後も小部屋／トンネルの尾が残り、直達のみ・全境界開放では0.2秒以降の尾のエネルギーは0だった。[Editor結果](../../Artifacts/CustomSpatialAudio/Editor/report.json)

小部屋で最初の非ゼロサンプルは直達312、一次反射523、後期残響2022。各経路の理論遅延をサンプル化した順序と整合し、後期残響は最短反射と最短FDN遅延1499サンプルの後に現れた。

Windows WASAPIを指定したEditorの3秒再生も成功（48 kHz、143,872記録フレーム、ピーク0.0993、callback error／overflow 0）。これはデバイス出力経路とDSP応答の検証であり、人による定位・音質の試聴評価や物理出力のループバック測定ではない。[実時間結果](../../Artifacts/CustomSpatialAudio/Editor/realtime-report.json)、[試聴用WAV](../../Artifacts/CustomSpatialAudio/Editor/small-room-audition.wav)

小部屋の「後期残響だけ」の録音を125／1000／8000 Hzのバンドパスで解析した結果：

| 測定帯域 | T20から外挿したRT60 | 回帰R² |
| --- | ---: | ---: |
| 125 Hz | 0.986秒 | 0.998 |
| 1000 Hz | 0.643秒 | 0.987 |
| 8000 Hz | 0.424秒 | 0.992 |

独立FDN成分への設定は1.008／0.448／0.216秒だった。測定帯域とFDN分割帯域は同じものではなく、特に高域で設定より長い尾が見える。フィルター設計とレベル校正を次段階で改善する。これは録音から-5〜-25 dBを回帰した**T20外挿**であり、実測60 dB減衰と呼ばない。短い直達やフィルターの尾は部屋のRT60評価対象から除外した。[解析JSON](../../Artifacts/CustomSpatialAudio/Editor/analysis.json)、[CSV](../../Artifacts/CustomSpatialAudio/Editor/analysis.csv)、[再解析スクリプト](tools/analyze_custom_spatial_audio.py)

追加の独立.NET／実FMODライブラリ検証では、空Groupの尾、Pause時のcallback停止とMaster出力0、Group音量0で尾も消音、Dispose後の再生成、部分初期化失敗の後始末を確認した。純粋DSPのウォームアップ後1,024,000フレーム＋係数切替の呼び出しスレッド割り当ては0 bytes。これらはUnity全体のGCや多音源CPU予算の測定ではない。[ネイティブ接続検証](../../Artifacts/CustomSpatialAudio/adapter-native-review.txt)

**Windows IL2CPP Development Playerのビルド・実行成功、終了コード0**。6条件のNOSOUND_NRT録音とWASAPIの3秒出力がすべて通り、callback error／overflowは0だった。EditorとPlayerの7録音を比較した最大サンプル差は約2.24×10⁻⁸（同じフレーム数）。[Player結果](../../Artifacts/CustomSpatialAudio/Windows/report.json)、[Player実時間結果](../../Artifacts/CustomSpatialAudio/Windows/realtime-report.json)、[Editor／IL2CPP比較](../../Artifacts/CustomSpatialAudio/editor-il2cpp-comparison.json)

小部屋の直達＋反射＋尾と合成出力の最大差は3.13×10⁻¹⁷未満、全境界開放と直達のみは完全一致。[成分比較](../../Artifacts/CustomSpatialAudio/Editor/component-check.json)

Playerの既存FMOD自動起動では `Missing DSP plugin 'Steam Audio Spatializer'`、既存Bankのロード警告、Studio Listener未配置の警告を観測した。独立Core側の7ケースは通っているが、Player全体の既存音響初期化が正常と検証できたわけではない。また、自作アダプターの`setChannelFormat`にはFMODの廃止予定channelmaskに関する警告が出るが、形式交渉と出力は成功した。これらを含め、既存Studio Bankの整理と初期化経路の選択は製品統合時の残課題とする。

## 次の工程

1. 専用のdry Studioイベント／Bankとゲーム音量／Pauseへの接続、同一素材・同一音量による既存方式とのA/B比較。
2. HRIRの読み込み、サンプルレート変換、方向補間、左右耳の畳み込みと複数人による定位評価。
3. TunnelMapDtoに基づくRoom／Portalの定義、L字・隣室・扉の伝播。ワープは物理的な扉と分けて仕様化する。
4. 多音源の共有残響、CPU測定、屋外／境界移動の連続性、SDN比較。

本番の生成トンネル、ワープ、再生成、Unload、BGM/UI音量の回帰試験は、ゲームへ接続する段階で実施する。既存SteamAudioManagerはPlayer開始時に自動初期化するため、専用試験シーンでも別系統のSteam Audioがロードされ得る。この試作が確認するのは独自信号経路での非依存であり、プロセス全体のSteam Audio DLL未ロードではない。
