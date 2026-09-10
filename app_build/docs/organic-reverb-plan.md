# 環境適応型空間音響 実装計画（段階4承認済み）

作成日: 2026-09-10

## 目的と位置づけ

BodycamのOrganic Reverbを参考に、トンネル・小部屋・開口部・材質によって反射、残響、遮蔽が変わる音響を実現する。Bodycamの内部実装や採用SDKは確認できていないため、同じアルゴリズムの再現を保証するものではない。

推奨構成は既存FMODを維持し、Steam AudioのUnity/FMOD連携を小規模に検証してから採用する方式。互換性・負荷・聞こえ方が採用条件を満たさなければ、FMODの残響制御と簡易遮蔽へ範囲を縮小し、計画を再承認する。

## 確認済みの現状

- Unity 6000.5.8f1、FMOD Unity Integration、VContainerを導入済み。
- FMODSEServiceは音源座標を受け取らず、PlaySEで3D属性を設定していない。PlayOneShotも位置を指定していない。
- IFMODSEServiceがFMODUnity.EventReferenceを公開している。新しい空間音響APIではこの外部型への依存を引き継がない。
- トンネル生成DTO、GenerateTunnelTest、LoopTunnelTestが存在する。生成形状への対応とループ移動時の音響の扱いを検証対象にする。
- Resonance Audio関連ファイルは存在するが、実際のDSP使用状況は未確認。
- 計画着手前のgit statusは変更なし。以下の工程で追加したコード・Unity設定が現在の差分に含まれる。
- .agents/agents.md は存在しない。段階1の着手時にルートの agents.md を確認し、会話で提示されたものと同じレイヤー制約を適用した。

## 初期対象

足音、物体の衝突音、敵の発声音など、位置を持つワールドSEを対象にする。最初は残響の少ないテスト用の短い音と持続音で比較する。UI/BGMは別経路を維持する。初期検証はWindows PC・単一リスナーを仮定し、製品の対象環境は採用判断前に確定する。

## 設計

| 層 | 担当 |
| --- | --- |
| Domain | 必要な音響ID・材質IDなどの純粋C#モデル。Unity/FMOD/Steam Audio型を含めない。DSPやメッシュ処理を持たせない。 |
| Application | 空間音源の再生要求DTO、再生ハンドル、ISpatialAudioService、IAcousticSceneService、開始・更新・終了のユースケース。座標はプリミティブで受け渡す。 |
| Infrastructure | FMODイベント対応表、イベント寿命と3D属性更新、Steam Audio連携、音響用形状・材質・リスナーの登録と解除。エンジンの位置取得もここで実装する。 |
| View | 音響設定UIの参照と基本入力イベントだけを公開する。音響計算を行わない。 |
| Presentation | ApplicationのDTO/ユースケースとViewを仲介する。Domain/Infrastructureを直接参照しない。 |
| DI | 実装の登録、リスナーや生成システムとの接続、ライフサイクルを構成する。 |

新規コードはapp_build/に保存する。Unityでコンパイル対象にするため、既存のAssetsへの反映手順を調査し、正本・同期方法・配置先を最初の工程で確定する。独立した二重管理や同一クラスの二重コンパイルを避け、既存.metaとasmdef GUIDを維持する。

## 実装工程と完了条件

### 1. 互換性と音声経路の検証

- FMOD Integration、FMOD Studio、Bank、Steam Audioのバージョンと対応プラットフォームを確認し固定する。
- FMOD Studioプロジェクトの所在・編集可否、既存イベントの3D設定、Listener、Spatializer、Return経路を確認する。
- 小部屋・長い廊下・屋外の固定テストシーンを用意し、1音源で反射・残響・遮蔽のON/OFF比較を行う。
- FMOD側のプラグイン配置、DSP設定、Bank再ビルドも作業範囲とする。Unityコンポーネントを追加するだけでは完了しない。
- 完了条件: EditorとWindowsビルドの双方で音が出て、プラグインエラーがなく、環境による差を確認できる。

### 2. 3D音源管理

- イベントID、位置、向き、必要なら速度、音源カテゴリ、優先度を扱うAPIを追加する。
- 短い音も空間音響処理に必要な期間はハンドルを管理する。移動音源の追従、停止、再利用、シーン破棄を扱う。
- 同じイベントの同時発音を個別に識別し、音源数の上限と優先順位を設ける。
- 完了条件: 同一SEの複数地点再生、移動追従、連続再生、破棄後のリソース解放が成立する。

### 3. 生成トンネルとの連携

- 描画メッシュとは別に簡略化した音響形状を用意し、壁・床・天井・開口部を保持する。
- 固定形状の部品は事前にエクスポートしたDynamic Objectとして生成する方式を第一候補とする。
- 実行時に頂点構造が変わる形状は別途SDK経由の登録/再構築が必要か検証する。Transform更新だけで任意のメッシュ変形に対応できるとは扱わない。
- 生成完了時に登録し、再生成時に旧形状と音源を解除する。移動する扉は独立した剛体音響オブジェクトにする。
- コンクリート、金属、木材などの吸収・透過・散乱特性を設定する。
- 完了条件: 複数シードで形状と音響境界が一致し、再生成で古い反射面や音源が残らない。

### 4. 音の伝播と演出調整

- 直接音、初期反射、残響を分けて試聴し、距離減衰・遮蔽・壁越しの透過を調整する。
- L字廊下、隣室の開いた入口、閉じた扉で、聞こえる方向と音量を検証する。残響だけで開口部経由の方向表現まで解決したとは扱わない。
- 必要な場合に限り、部屋と開口部の接続グラフによる経路推定と仮想音源を追加設計する。SDK側の間接音との二重加算を防ぐ。
- LoopTunnelのワープでは、現実の座標とゲーム内の接続関係が異なる可能性がある。初期版は移動時の音切れ・誤った補間・ドップラーを防ぐ。ポータル越しの継続伝播は別途拡張範囲として合意する。
- 完了条件: 部屋境界で不自然な切替やクリックがなく、扉・曲がり角の差が試聴で認識できる。

### 5. 負荷と回帰検証

- 同時発音数1/8/16/32で、フレーム時間、音声DSP負荷、メモリ、音切れを計測する。
- 反射の計算対象、更新間隔、形状精度を段階化する。数値予算は工程1の対象PCと計測結果で確定する。
- 初期採用基準: 目標フレーム時間を維持し、定常再生で音切れがなく、繰り返し生成・破棄でメモリが増加し続けないこと。
- 既存の音量設定、BGM/UI、シーン切替、ポーズ、ミュートを回帰確認する。
- asmdefとソース参照を確認し、特にPresentation -> Domain、View -> Presentationの依存がないことを検証する。
- Unityの実コンパイルとPlayerビルドを確認し、試聴録音と計測結果を残す。

## 採用前に確定する事項

対象PC/プラットフォームと目標FPS、優先するSE、FMOD Studioプロジェクトの編集可否、ループ境界を通る音の必要性。最初の承認範囲は工程1の技術検証とし、結果をもとに本実装の費用・負荷・品質を判断する。

## 参考資料

- Bodycam公式告知: https://steamcommunity.com/app/2406770/announcements/?l=english
- Steam Audio Unityガイド: https://valvesoftware.github.io/steam-audio/doc/unity/guide.html
- Steam Audio FMOD導入: https://valvesoftware.github.io/steam-audio/doc/fmod/getting-started.html
- Steam Audio FMODガイド: https://valvesoftware.github.io/steam-audio/doc/fmod/guide.html

2026-09-10、ユーザーが段階1の技術検証を明示承認。Unity EditorのGUIは起動せず、Unity CLIでコンパイル・Play Mode検証・Windowsビルドを実施する。この時点の承認は段階1のみで、後続工程は以下の追加承認に基づく。

段階1は完了。Editor Play ModeとWindows IL2CPP Playerの双方で8条件を実行し、各11項目の録音解析に合格した。構成・試聴・判定範囲は [段階1の検証結果](D:/Pandd/ShinShinzui/app_build/OrganicReverb/results/summary.md) を参照。

2026-09-10、ユーザーの「次の工程に進んでください」により段階2の3D音源管理を実装。Unity CLIのEditor／Windows IL2CPP Playerの双方で実行72項目と録音解析7項目に合格し、段階2を完了した。新規APIと実装は [SpatialAudio](D:/Pandd/ShinShinzui/app_build/SpatialAudio/README.md)、結果は [段階2の検証結果](D:/Pandd/ShinShinzui/app_build/SpatialAudio/results/summary.md) を参照。生成トンネルとの接続と本編音源の事前準備による遅延短縮は段階3として扱う。

2026-09-10、ユーザーの「お願いします」により、段階3の生成トンネル接続と足音の事前準備を実装。Unity CLIのEditor／Windows IL2CPP Playerで各43項目と録音解析6項目に合格し、段階3を完了した。Playerの足音開始は録音上302msから46msへ短縮。段階2のEditor回帰試験も72項目と解析7項目に合格。構成は [TunnelAcoustics](D:/Pandd/ShinShinzui/app_build/TunnelAcoustics/README.md)、証跡は [段階3の結果](D:/Pandd/ShinShinzui/app_build/TunnelAcoustics/results/summary.md) を参照。この時点で段階4・5は未着手。後続の進捗は以下を参照。

2026-09-10、ユーザーの「お願いします」により段階4の音の伝播・演出調整を開始。体積を持つ音源の遮蔽、周波数別透過、反射音の配分を本編へ接続。Unity CLIのEditor／Windows IL2CPPで、各19録音条件・127実行チェック・14解析チェックに合格し、段階4を完了した。段階3・2のEditor回帰も合格。結果と比較録音は [段階4の結果](D:/Pandd/ShinShinzui/app_build/Propagation/results/summary.md)、設定は [Propagation](D:/Pandd/ShinShinzui/app_build/Propagation/README.md) を参照。残る段階5で反射回数64を含む負荷予算と全体回帰を検証する。反射音による開口部の方向差を確認したため、この段階では独自の仮想音源を追加しない。

2026-09-10、ユーザーの「お願いします」により段階5の負荷・回帰検証を実施し、初期実装の5工程を完了。Unity CLIのEditorおよびWindows IL2CPPのStandard/Economyで各580実行チェックと、Editorでは26項目・Windowsでは27項目の解析チェックが採用範囲内で合格した。標準は12音源（8192 rays / 64 bounces / IR 3秒）、軽量は8音源（4096 rays / 32 bounces / IR 1.5秒）を採用。i7-12700 / RTX 3080で標準12音源のフレームp95は8.47ms、DSP平均45.5%、最大61.0%。標準32音源では出力欠落を確認しており、製品上限には採用しない。30回の生成・破棄でメモリが安定し、所有オブジェクトを解放することを確認した。新規足音のSE音量・ポーズ連携、既存生成処理のマテリアル・NavMesh解放も修正した。段階2・3・4のEditor回帰と録音解析もすべて合格。設定・測定条件・失敗を含むストレス結果は [段階5の結果](D:/Pandd/ShinShinzui/app_build/AcousticPerformance/results/summary.md) と [再現手順](D:/Pandd/ShinShinzui/app_build/AcousticPerformance/README.md) を参照。

完了範囲は、単一リスナー・1つの有効なトンネル音響Runtime・Windows向けの初期実装とその検証。生成トンネルに接続済みなのは足音（仮素材）で、位置付き音声APIは他のワールドSEへ展開できる。敵・衝突音など既存SEの移行、最終音素材への差替え、製品の対象PC・フルシーン・長時間プレイでの最終評価は残る。音響検証シーンの結果を製品全体のFPS保証とは扱わない。
