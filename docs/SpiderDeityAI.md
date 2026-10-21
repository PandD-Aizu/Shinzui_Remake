# 蜘蛛ボスの移動AIと胴体の接地補正

## 確認する

`Assets/Shinzui/Scenes/SpiderDeityAiPreview.unity` を開き、Playする。
7度の斜面で蜘蛛が巡回し、胴体が地面に沿って傾く。
Hierarchyの `Player Target - move this to test detection` を蜘蛛の前方へ移動すると追跡し、接近すると停止する。
障害物で視線を遮ると最後に見た位置を探し、2秒後に巡回へ戻る。
ターゲットを削除・非アクティブにしても巡回へ戻る。
デモのカプセルは目印であり、プレイヤー操作・ダメージ判定は含まない。

## ゲームに配置する

`Assets/Shinzui/Prefabs/SpiderDeity/SpiderDeity_AI.prefab` をNavMesh上に配置する。

- ルート：NavMeshAgentとSpider Navigation Driver。経路移動とY軸旋回を担当。
- 子：既存SpiderDeity_WalkとSpider Body Grounding。見た目の高さ・傾きを補正し、その後に8脚の足先を更新。
- DriverのTargetにプレイヤーのルートTransform、Patrol Pointsに巡回地点を順番に指定する。
- Target未設定なら巡回のみ。巡回地点も空なら待機。
- 対象の床にColliderを付け、Body GroundingとProcedural WalkのGround Layersを一致させる（初期値Default）。
- 視線を遮る壁などはSight Blocking Layersに含める。自身とターゲットの子Collider、Triggerは視線判定から除外。

検知は距離と視野角、遮蔽物の有無で行う。未検知のプレイヤー位置へ追跡しない。
移動中の目標は0.25秒ごとに再経路検索する。完全な経路が取れない場合は停止し、巡回の場合は1秒後に次の地点を試す。
NavMeshがまだない場合は0.5秒ごとに開始位置近辺を確認し、生成後に移動を開始する。
停止・無効化でAgentの経路を解除する。OffMesh Linkによるジャンプや落下は対象外。

## 調整値（スケール1）

| 項目 | 初期値 | 説明 |
| --- | --- | --- |
| Speed | 0.08 | 脚の踏み替えに合わせた低速移動 |
| Turn Speed | 12度/秒 | 胴体のY軸旋回速度 |
| Detection Distance / Field Of View | 2 / 220度 | 視認範囲 |
| Stop Distance | 0.65 | プレイヤーの手前で停止する距離 |
| Memory Seconds | 2秒 | 最後に見た位置を探す時間 |
| Sample Radius | 0.3 | 目的地・開始位置をNavMeshへ投影する範囲 |
| Max Tilt | 18度 | 胴体の傾きの上限 |
| Max Height Offset | 0.15 | 移動ルートからの見た目の上下補正上限 |
| Smoothing | 6 | 姿勢補正の追従速度 |

胴体は8脚の初期配置に対応する地面を調べ、地面の法線を平均して姿勢を決める。
脚のアニメーション結果を姿勢判定へ戻さないので、足を上げるたびに胴体が振動しにくい。
地面が3点未満しか見つからないときは滑らかに基準姿勢へ戻る。
水平移動はNavMeshAgent、見た目の補正はその子で行い、NavMesh上の座標を姿勢補正で上書きしない。

## NavMesh

確認シーンにはSurface 1個とAgent 1個を用意し、既存のAgent Type 0（Humanoid）を使用する。
SurfaceはNavigation Terrainの子Colliderのみからベイクし、データは `SpiderDeityPreviewNavMesh.asset` に保存する。
既存のナビゲーション設定・ゲームシーンのSurfaceは変更していない。
障害物は静的なColliderとしてベイク済み。動的な物体に対応する場合は適切なNavMeshObstacle等が別途必要。
床や障害物を変更したらNavMeshSurfaceのInspectorで再ベイクする。
蜘蛛を拡大する場合、歩行の距離だけでなくAgentの半径・高さ、対応するNavMeshのベイク設定、検知距離・停止距離も調整する。
実行中のスケール変更と非均等スケールは対象外。

## レイヤー

- Application：`SpiderMovementUseCase`。Unity依存なしの巡回・追跡・探索・停止の状態判断。
- Infrastructure：`SpiderNavigationDriver`。Transform、視線Raycast、NavMeshの具体的な操作。
- Infrastructure：`SpiderBodyGrounding`。地面判定と見た目の姿勢補正。
- Editor：`SpiderAiAssetBuilder`。専用Prefab・シーン・NavMeshの作成。

Presentation→Domain、View→Presentationの依存は追加していない。
既存のEnemyDirectorは複数敵の遭遇演出を含むため、この単体ボスの移動へは結合していない。
このPrefabに別の移動コントローラーを同時に付けないこと。

## 検証

`SpiderMovementUseCaseTests`：視認・記憶・停止のヒステリシス、ターゲット消失、到達不能な巡回地点の切替。
`SpiderAiPlayModeTests`：斜面での姿勢とIK追従、追跡・接近停止・遮蔽、障害物迂回、到達不能経路、NavMesh未生成、地面消失。

2026-09-26、Unity 6000.5.8f1で新規の実動作テスト5件が成功。
状態判断テスト2件もEditor上で成功し、既存IK・歩行のPlayModeテスト5件も通過。
実動作結果は `Artifacts/SpiderDeityIK/ai-playmode-results.json`。
検証は保存済みのPrefab・シーンを読み直して行っている。

攻撃、ダメージ、ボスのフェーズ変化、崖からの落下、壁歩き、動く床への追従は今回の範囲に含まない。
地面補正は静的な低～中傾斜の地形を対象とし、大きな段差を登る専用モーションは別途必要。
