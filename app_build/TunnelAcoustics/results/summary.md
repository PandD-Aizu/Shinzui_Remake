# 段階3 検証結果

実施日: 2026-09-10。Unity 6000.5.8f1 / FMOD Unity 2.03.20 / FMOD Studio 2.03.12 / Steam Audio 4.8.1。Unity EditorのGUIを開かずCLIで実行。

生成トンネルへの接続と足音の事前準備を実装し、EditorおよびWindows IL2CPP Playerの双方で合格しました。

| 検証 | Editor CLI | Windows IL2CPP |
|---|---:|---:|
| 実行チェック | 43 / 43 | 43 / 43 |
| 録音・結果解析 | 6 / 6 | 6 / 6 |
| 通常要求からの足音開始 | 280.83 ms | 302.17 ms |
| 事前準備済みの足音開始 | 46.17 ms | 46.17 ms |
| 開始遅延の短縮 | 234.67 ms | 256.00 ms |

開始時間は接地要求直前からFMODマスターバスを録音し、振幅が0.001を超える最初のPCMサンプルで測定しています。各環境1回の比較で、OS・音声デバイス・ヘッドホンの出力遅延は含みません。録音は48kHz・16bit・ステレオ、クリッピングなしです。

## 確認した内容

- 実際の生成器とPrefabでシード2777/42/1234を生成。登録三角形数は3,402 / 3,198 / 3,198。
- 各シードで実メッシュの面を横切る音線は遮蔽シミュレーションの出力0（直接音を遮断）、通路の実際の開口部を横切る音線は1（通過）。音響形状の解除後は1に戻り、旧形状が残らない。
- 剛体の扉を閉じる・移動して開く・無効化・再有効化・破棄する場合の音響登録と遮音を確認。
- 足音を2音源事前準備し、要求したフレームで再生状態へ遷移。発音後は残響を接地点に保持。
- 空中・静止では歩行音を発生せず、接地して移動した距離から1歩を発音。
- ワープで旧地点の音と予約を解除し、移動先で準備。移動先の音源位置と速度ゼロをFMODの属性で確認。
- マップ破棄後は管理するFMODインスタンスをすべて解放し、ネイティブ音響オブジェクト数は0。FMODバックエンドエラーなし。
- 既存の段階2も現行コードでEditor CLIの回帰試験を実行し、実行72項目と録音解析7項目に合格。
- 正本とAssetsの一致、.meta、追加アセンブリの依存方向、Viewへの他レイヤー参照がないことを検証。

## 録音と証跡

短い合成足音による接続試験です。冒頭の無音時間を含む未加工のPlayer録音を保存しています。

- [事前準備した足音](D:/Pandd/ShinShinzui/app_build/TunnelAcoustics/results/prepared-step.wav)
- [通常要求から開始した足音](D:/Pandd/ShinShinzui/app_build/TunnelAcoustics/results/cold-step.wav)
- [Editor実行チェック](D:/Pandd/ShinShinzui/app_build/TunnelAcoustics/results/editor-report.json) / [解析](D:/Pandd/ShinShinzui/app_build/TunnelAcoustics/results/editor-analysis.json)
- [Player実行チェック](D:/Pandd/ShinShinzui/app_build/TunnelAcoustics/results/player-report.json) / [解析](D:/Pandd/ShinShinzui/app_build/TunnelAcoustics/results/player-analysis.json)
- [段階2回帰チェック](D:/Pandd/ShinShinzui/app_build/TunnelAcoustics/results/phase2-regression-report.json) / [解析](D:/Pandd/ShinShinzui/app_build/TunnelAcoustics/results/phase2-regression-analysis.json)
- [ソース・設定・バンクのSHA-256](D:/Pandd/ShinShinzui/app_build/TunnelAcoustics/results/source-sha256.json)

Player実行ファイル: [TunnelAcousticsProbe.exe](D:/Pandd/ShinShinzui/Builds/TunnelAcousticsProbe/TunnelAcousticsProbe.exe)。検証専用ビルドです。

## 残る工程

足音は仮素材で、材質別素材やアニメーション接地との同期は未調整です。今回の試験はサンプル点での音響境界を確認しており、すべての生成パターン・全表面の一致を保証するものではありません。本編Addressablesの読み込みを含む全ゲーム動線も検証対象外です。

段階4では開口部・L字通路・扉を通る聞こえ方、壁越しの透過、境界の連続性を調整します。ポータルをまたぐ継続伝播は未実装です。段階5では性能予算、同時音源数、長時間生成・破棄、BGM/UI・設定・ポーズ等を検証します。

[構成と再実行手順](D:/Pandd/ShinShinzui/app_build/TunnelAcoustics/README.md) / [実装計画](D:/Pandd/ShinShinzui/app_build/docs/organic-reverb-plan.md)
