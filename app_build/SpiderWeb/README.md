# 蜘蛛の巣の見た目改善

2026-09-20 / `codex/realistic-spiderweb`

正式ソースは `Assets/Shinzui/` 配下にあります。このフォルダには比較画像と検証結果のみを保存しています。

## 変更内容

- 手動アンカー8点の物理構造を維持したまま、描画上は32本の放射糸へ補間。
- 横糸を支え糸の55%の幅とし、4区間の曲線による小さなたわみ、局所的な破れ、間隔の揺らぎを追加。
- 糸の接線を使った反射とPoint/Spot Lightへの対応。暗所の常時発光を除去。
- 半透明の滑らかな輪郭と、最小1.5pxの描画幅に応じた被覆率補正。High/Midium/Lowで確認。
- カメラごとに糸の向きとBoundsを更新。画面外では頂点更新を省略し、毎フレームの配列生成を回避。
- 無効化・再有効化後にメッシュが戻らない既存問題も修正。
- `LatestStageGenerateTemp` と `StageTemp` へ表示設定を保存。新規作成時と旧StageTempの太い糸の初期幅は1.5mm、LatestStageGenerateTempの既存0.5mm設定は維持。

## 比較画像

同じ実シーンのアンカー配置・seed・糸幅を検証用の空シーンにコピーし、同じカメラとライトで撮影しました。物理シミュレーションを止めたEditModeの画像です。比較シーンでは各画質のURP Renderer Featureを外し、糸そのものを評価しています。

| 条件 | 変更前 | 変更後 |
| --- | --- | --- |
| 左から懐中電灯 | [前](results/before-High-flash-left.png) | [後](results/after-High-flash-left.png) |
| 右から懐中電灯 | [前](results/before-High-flash-right.png) | [後](results/after-High-flash-right.png) |
| ライトなし | [前](results/before-High-dark.png) | [後](results/after-High-dark.png) |
| 近距離 | [前](results/before-High-close.png) | [後](results/after-High-close.png) |
| 斜め | [前](results/before-High-oblique.png) | [後](results/after-High-oblique.png) |
| 遠距離・霧 | [前](results/before-High-far-fog.png) | [後](results/after-High-far-fog.png) |
| Low画質 | [前](results/before-Low-flash-left.png) | [後](results/after-Low-flash-left.png) |

[実ステージ背景での確認画像](results/stage-after.png)も保存しています。これは検証用カメラとライトを一時配置した画像で、プレイヤーの通常カメラからのゲームプレイ画像ではありません。検証用カメラ・ライトはシーンへ保存していません。

## 検証

Unity 6000.5.8f1 / URP 17.5 / NVIDIA GeForce RTX 3080。

- C#コンパイル成功、実描画したシェーダーにコンパイルエラーなし。
- 関連テスト13件すべて合格（追加9件、既存4件）。
- 固定seed、8固定点と32表示放射、物理生成の不変、曲線接点、Bounds、カメラ別の被覆率、配列再利用、再有効化、画面外からの再表示を回帰テスト。
- Play Modeへ移行して複数接触、減速と引き戻し、段階的解除、燃焼時の即時解放、フェードと最終破棄を検証。接触通知は実Colliderを渡して既存コールバックを呼び、物理エンジンの接触タイミングからは切り離しています。
- マッチ棒装備時・未装備時の既存InteractionUseCaseテストも実行。
- 結果は [tests.json](results/tests.json)。Pipelineの結果一覧はPlay Modeのドメインリロードで途中まで省略されますが、集計結果は全テストを含みます。

| 測定項目 | 変更前 | 変更後 |
| --- | ---: | ---: |
| 物理ノード | 145 | 145 |
| 物理制約 | 286 | 286 |
| コライダー | 8 | 8 |
| 描画頂点 | 1,144 | 7,762 |
| 三角形 | 572 | 5,480 |
| メッシュ更新単体・300回平均 | 0.041 ms | 1.626 ms |
| メッシュ更新のGC割り当て | 0 B | 0 B |

同じ145ノード・32放射設定の画面外判定は約0.011ms、GC割り当て0 Bでした。[記録](results/offscreen-performance.json)

処理時間はEditor上でのCPU計測で、Playerのフレーム時間やGPU時間ではありません。曲線・密度増加により画面内の描画更新負荷は増えています。多数の巣を同時に表示するシーンでは追加の負荷確認が必要です。Low画質には輪郭の階段状の粗さが残ります。静止画比較のため、移動中の時間方向のちらつきは今回の確認対象外です。

## 再確認

Unity Test Runnerで `SpiderWeb` をフィルターして実行できます。

比較画像は `Shinzui.Editor.SpiderWebVisualProbe.Capture("after")`、実ステージ画像は `CaptureStage()` で再生成できます。専用の検証用Editorで実行してください。これらは開いているシーンを切り替えます。`ApplyStageAppearance()` は対象2シーンの表示設定を明示的に保存する処理であり、通常の撮影では呼ぶ必要はありません。

主な調整項目はSpiderWeb Inspectorの `Capture Thread Width Ratio`、`Capture Thread Sag`、`Minimum Thread Pixels`、`Ring Phase Variation` です。
