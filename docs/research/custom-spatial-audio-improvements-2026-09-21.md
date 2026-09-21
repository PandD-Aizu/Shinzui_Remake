# 独自空間音響：段階Aの改善と伝播・両耳処理の追加

2026-09-21 / Unity 6000.5.8f1 / Windows x64

## 変更内容

前回の[段階A](custom-spatial-audio-phase-a-implementation-2026-09-21.md)を正規ソース上で拡張した。過去の測定結果は上書きせず、新しい出力は `Artifacts/CustomSpatialAudio/Improved` に保存する。

- FDNへの入力と出力を4次Butterworthフィルターで帯域制限し、低域の長い残響が高域の減衰測定に混入する量を減らした。直達と一次反射の相補分割は維持している。精密な周波数ごとの減衰制御や論文の完全再現ではない。
- 20 msのクロスフェード完了直後に次の更新を開始する。音声ブロックの長さで更新の待ち時間が変わる不具合を修正した。
- Room/Portalグラフと経路探索を追加した。L字通路では最終開口から音が届く方向を使い、距離は全区間を加算する。閉じた扉・非接続の部屋には直接音を出さない。
- 実測MIT KEMAR HRIRを読み込み、レート変換、方位／仰角補間、頭部姿勢変換、経路ごとの左右FIR畳み込みを追加した。HRIRの左右差にさらにパンを掛ける二重処理は行わない。
- 実測で全7経路のHRIR処理が重いことを確認し、既定は直達のみHRIR、反射はステレオパンとした。`maxHrirPaths` で最大7経路まで選択でき、追加枠は強い反射から割り当てる。分数遅延とゲインは制御スレッドでFIR係数へ畳み込み、帯域差のない直達は生のモノラル履歴から左右2本を計算する。
- 試験Playerだけに `SHINZUI_CUSTOM_AUDIO_PROBE` を渡し、SteamAudioManagerの自動起動を抑止する。ゲームビルドと通常のEditor Playの起動処理は維持する。これは既存ゲームBankの改修ではない。
- FMODの非推奨channelmask引数を0にし、専用経路の警告を解消する。
- 実FMODでの残響の消音・ポーズ・再生成、複数音源の処理時間とメモリ割り当てを検証対象に追加した。

## 使用方法

Unityメニュー `Shinzui > Audio > Custom Spatial Audio > Validate and record` は、矩形室6条件、L字通路の開放／25%開放／閉鎖、HRIRの前／後／上／右を記録する。同じメニューの `Build Windows IL2CPP probe` で専用Playerをビルドできる。

```powershell
unity test D:/Pandd/ShinShinzui --mode EditMode --filter Shinzui.Tests.CustomSpatialAudio --output D:/Pandd/ShinShinzui/Artifacts/CustomSpatialAudio/Improved/editmode.xml --timeout 600 -- -nographics
unity run D:/Pandd/ShinShinzui --timeout 1200 -- -executeMethod Shinzui.CustomSpatialAudio.Probe.Editor.CustomSpatialAudioProbeBuilder.ValidateAndBuild
python docs/research/tools/analyze_custom_spatial_audio.py Artifacts/CustomSpatialAudio/Improved/Editor
```

HRIRは `Assets/StreamingAssets/CustomSpatialAudio/Kemar/mit-kemar-compact.zip` に原配布ZIPを保存した。ロードと補間は制御スレッドで行い、不変係数だけをDSPへ渡す。初期ロードは各レートにつき1回とし、音源ごとにZIPを再ロードしない。

```csharp
var dataset = HrirDataset.LoadMitKemarCompact(zipPath, sampleRate);
var parameters = SpatialDspParameters.Create(response, sampleRate,
    directGain: 1, earlyGain: 1, lateGain: .25f,
    hrirDataset: dataset, listenerForward: forward, listenerUp: up,
    maxHrirPaths: 1); // 既定値。全一次反射を含む品質比較時は7。
processor.SetParameters(parameters);
```

`PortalRoomAcoustics.Calculate(...).ToRoomResponse()` は既存DSPへ渡せる。グラフは共有面で接する、障害物のない軸平行の凸セルに限る。開口中心を通る1本の経路を、距離＋明示的な透過損失ペナルティで選ぶ。ポータルの開口率と帯域別透過率はエネルギー率で、振幅へは平方根を掛ける。これは回折シミュレーションでも、室間残響モデルでもない。3秒を超える経路はDSP側で明示的に拒否し、遅延を切り詰めて先に鳴らすことはしない。

## 範囲と制限

Applicationはエンジン非依存のDTOのみを持ち、計算・ファイル読み込み・FMOD接続はInfrastructureに置く。View/Presentation/Domainへ新しい依存は追加していない。`app_build` へのコード複製はしていない。

KEMARは非個人化された小耳介データで、仰角は-40度未満を端の測定値へ制限する。欠けた左右半球は配布説明に従って左右耳を交換する。時間領域の補間には音色変化があり得る。レート変換は共通の因果フィルター遅延を加え、左右の相対遅延を保持する。後期残響は既存の拡散ステレオ出力で、方向別のHRTF残響にはしていない。前後・上下の信号差があることと、人が正しく定位できることは分けて評価する。

多音源測定は純粋DSPを1／8／16個直列に処理する局所測定であり、FMOD全体やゲームのCPU予算ではない。全7経路HRIRは1音源の品質比較用にも測定する。共有残響Return、音源優先度と休止、畳み込みの高速化には引き続き評価が必要。

本編の生成トンネル／ワープへのRoom/Portal変換、専用dry Studioイベント／Bank、既存SE/Master設定UseCaseとの接続、同一素材・音量での既存方式とのA/B比較、複数人での試聴評価はこの比較試作の外に残る。既存本編の音響方式を切り替える前に、これらの接続と回帰検証を行う。

## 根拠

- [MIT KEMAR配布元](https://sound.media.mit.edu/resources/KEMAR.html)：実測データと引用条件。W. G. Gardner / K. D. Martin, “HRTF measurements of a KEMAR,” JASA 97(6), 3907–3908 (1995)。
- [FMOD DSP::setChannelFormat](https://www.fmod.com/docs/2.03/api/core-api-dsp.html#dsp_setchannelformat)：入力チャンネル形式。
- [Unity BuildPlayerOptions.extraScriptingDefines](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/BuildPlayerOptions-extraScriptingDefines.html)：Playerビルドだけに適用する追加define。

## 今回の実測結果

録音はDSP直後のpre-faderで、機器の物理出力の録音ではない。以下は前回と同じインパルス・室形状・48 kHzでの、125／1000／8000 Hz帯域通過後のT20外挿値。目標値はFDN内部の各帯域成分の値で、測定フィルターの帯域とは一致しない。

| 測定帯域 | 前回 | 改善後 | FDN設定 |
| --- | ---: | ---: | ---: |
| 125 Hz | 0.986秒 | 0.994秒 | 1.008秒 |
| 1000 Hz | 0.643秒 | 0.453秒 | 0.448秒 |
| 8000 Hz | 0.424秒 | 0.307秒 | 0.216秒 |

高域の広帯域測定にはまだ差がある。別途、狭帯域のバースト励振を使う回帰テストでは3帯域とも設定値の±15%以内を確認する。これを実測60 dB減衰や、すべての周波数での精密一致とは扱わない。[Editor帯域解析](../../Artifacts/CustomSpatialAudio/Improved/Editor/analysis.json)

L字の音は1830フレームで到着し、独立計算値1830.824フレームと分数遅延の範囲内で一致した。25%開口の振幅は全開時の0.5倍、閉鎖時は完全な無音。直達＋反射＋残響の合成は分離録音の和と一致し、全境界開放は直達のみと一致した。実測HRIRの右方向では右耳のピークが左耳より34サンプル先行し、右耳のエネルギーが大きい。前／後／上も異なる信号になった。これは信号検証であり、聴感上の正答率を測った結果ではない。[波形の独立照合](../../Artifacts/CustomSpatialAudio/Improved/Editor/signal-verification.json)

Unity EditModeの最終テストは **59件成功、失敗0、スキップ0**。幾何・扉・閉鎖経路、実測HRIR読込、座標と左右耳、レート変換、最大遅延＋512タップ、分割ブロック、連続更新、実FMODの消音／ポーズ／再生成を含む。[テスト結果](../../Artifacts/CustomSpatialAudio/Improved/editmode.xml)

Editorの13条件録音とWASAPIの2条件（室内音／右方向HRIR）も成功し、callback error／capture overflowは0だった。[録音結果](../../Artifacts/CustomSpatialAudio/Improved/Editor/report.json)、[実時間結果](../../Artifacts/CustomSpatialAudio/Improved/Editor/realtime-report.json)

Editorでの純粋DSP負荷（512フレーム、48 kHz、ブロック時間10.667 ms、ウォームアップ後64ブロック）：

| モード | 音源数 | 平均 | p95 |
| --- | ---: | ---: | ---: |
| ステレオ | 1 | 0.463 ms | 0.557 ms |
| 直達HRIR | 1 | 0.978 ms | 1.148 ms |
| 直達HRIR | 8 | 7.670 ms | 8.235 ms |
| 直達HRIR | 16 | 15.318 ms | 16.086 ms |
| 全7経路HRIR | 1 | 4.373 ms | 4.965 ms |

全7経路HRIRの最適化前は1音源で平均17.270 msだった。分数遅延係数の事前合成と一様帯域の高速経路で改善した。既定の直達のみHRIRは、反射をパンにする品質／計算量の選択も含む。**16音源の直達HRIRはEditorの時間枠を超える。8音源も本編での保証値ではない。** 上表は係数固定時で、クロスフェード中は2組の経路を処理する。共有残響、音源数制限、実ゲーム負荷を加えた計測が必要。1音源につき追加のモノラル履歴は48 kHzで約0.58 MB。全7測定ケースの処理中割り当ては0 bytes。[最適化前](../../Artifacts/CustomSpatialAudio/Improved/performance-before-budget.json)、[最適化後](../../Artifacts/CustomSpatialAudio/Improved/Editor/performance.json)

Windows IL2CPP Development Playerもビルド・実行成功、終了コード0。13条件の録音とWASAPIの2条件がすべて通り、callback error／capture overflowは0だった。13録音のEditorとの最大サンプル差は **2.235×10⁻⁸**。専用Playerのログには、前回の `Missing DSP plugin 'Steam Audio Spatializer'`、既存Bankロード失敗、Listener未配置、channelmaskの警告は出ていない。通常ゲームのBank構成を修正したという意味ではない。[Player結果](../../Artifacts/CustomSpatialAudio/Improved/Windows/report.json)、[実時間結果](../../Artifacts/CustomSpatialAudio/Improved/Windows/realtime-report.json)、[Editorとの比較](../../Artifacts/CustomSpatialAudio/Improved/Windows/signal-verification.json)

IL2CPPの直達HRIRは、1／8／16音源で平均 **0.271／2.184／4.186 ms**、p95は **0.338／2.710／5.178 ms**。全7経路のHRIRは1音源で平均1.495 ms、p95 1.856 msだった。全ケースの割り当ては0 bytes。条件は上のEditor測定と同じで、これは本編の上限音源数を保証する数値ではない。[Player負荷測定](../../Artifacts/CustomSpatialAudio/Improved/Windows/performance.json)

新規ファイル／フォルダのmeta欠落0、対象34 GUIDの重複0、Applicationのエンジン参照とレイヤー参照0を確認した。ビルドが自動生成した既存の描画／画質設定等の差分はバックアップ後に取り除いた。正式ソースと検証用ファイル、KEMARデータ、専用ビルドの初期化ガードだけを変更として残している。[アセンブリ確認](../../Artifacts/CustomSpatialAudio/Improved/architecture-check.json)、[GUID確認](../../Artifacts/CustomSpatialAudio/Improved/guid-check.json)
