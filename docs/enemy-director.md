# Enemy Director

既存の `EnemyDirectorUseCase → EnemyPresenter → EnemyController` を拡張した、緊張と休息を管理する敵AIです。Alien: Isolationのような「管理AIが遭遇の頻度を調整し、敵自身が知覚から追跡する」遊びを目指しています。原作内部実装の再現ではありません。

## 使い方

`PlayerLifetimeScope` と `EnemyView` / `NavMeshAgent` が配置済みのゲームシーンは、そのままPlayできます。`PlayerLifetimeScope` の **Enemy Director** を展開して調整してください。`enemyViews` が空ならシーン内の敵を自動取得します。配列を指定している場合は、その配列が管理対象です。

`EnemyView.agentOverride` に明示的にAgentを設定するか、同一オブジェクト・親・子に配置してください。他の敵のAgentを距離だけで選ぶ動作は廃止しました。同じAgentを二重に登録すると警告して後続をスキップします。無効なView、Agent、NavMesh未接続、停止ストロボ中の敵は追跡担当になりません。ランダムマップのNavMeshが後から生成された場合も接続を再試行します。

既存の `TempPatrolChaseEnemy` は別系統の試作AIであり、本管理AIの対象には含みません。ゲーム中の追加スポーンは現在の固定登録配列に含まれないため、別途ライフサイクル登録が必要です。

## 行動

| 状態 | 動作 | 標準値 |
| --- | --- | --- |
| Ambient | 徘徊。敵は独自に視認・足音を検出できる | 12秒 |
| BuildUp | 1体に曖昧な探索地点を提示。地点は一定時間固定 | 最大35秒、ヒント更新8秒 |
| Hunt | 視界内で連続視認が成立した1体が追跡 | 16m、110度、0.3秒で発見確定 |
| Search | 最終視認地点または音がした地点、その周辺を捜索 | 最大14秒、3秒ごとに捜索点更新、半径最大7m |
| Recovery | 近い敵を離す。自動ヒントと足音による捜索を抑制 | 最低18秒、緊張度35%以下で再開 |

- 管理AIの探索ヒントは12mセルとオフセットを使い、現在位置の正確な連続追尾にならないよう保持します。
- 視界判定は距離・水平視野角・物理遮蔽を確認します。失踪後の捜索地点には、見えていないプレイヤーの現在位置を使いません。
- 追跡担当は視認中に頻繁に交代しません。他の近い敵は離れるよう指示します。
- 緊張度は追跡中や近距離で上昇し、休息中に下がります。上限でも視認中の敵が突然見失うことはありません。視線を切ると休息へ移ります。
- 休息中でも実際に視認されれば追跡が再開します。無敵時間ではありません。
- 歩きは4m、走りは13mの範囲に足音の手掛かりを出します。足音は位置移動を確認して0.65秒間隔で発生します。しゃがみ移動は静かです。音だけでは追跡や発見演出を開始しません。
- 聴覚は距離ベースのゲーム用近似です。FMOD/Steam Audioの音響伝播結果や投擲物の音はまだ入力していません。
- 接触死亡にも遮蔽判定を入れ、壁越しの距離だけで死亡しないようにしました。停止ストロボ中は接触死亡を試みません。
- NavMesh上で到達できる地点だけを採用し、到達不能なら古い経路を破棄します。経路計算は最大約0.3秒間隔です。休息中もテレポートで敵を消しません。

## 調整

`EnemyDirectorSettings` はApplicationのSerializable DTOで、DIのInspectorに保持しています。まず `ambientDuration`、`buildUpDuration`、`recoveryDuration`、`sightDistance`、`runningNoiseRadius` を調整すると遭遇頻度を変えやすくなります。時間・半径などの管理AIパラメーターに非正値・非有限値を与えると既定値へフォールバックします。

`sightBlockingLayers` は視界を遮るレイヤーのビットマスクです。既定の -1 は全レイヤーを判定し、敵自身、プレイヤー自身、Triggerは除外します。`eyeHeight` は敵のAgent位置からの目の高さです。プレイヤー側の注視点は現在のキャラクター高さの半分に追従します。

`Phase`、`Pressure`、`FocusEnemyId`、`IsPlayerFound` を `EnemyDirectorUseCase` から取得でき、デバッグ表示や演出へ接続できます。`DecideCommands(world, deltaTime)` の戻り値はレポートと同順の再利用バッファで、次の呼び出しまで有効です。停止中は呼び出し側も更新を止めます。

## レイヤー

- Domain: `EnemyEntity` の移動状態は純粋なC#。R3/Unity依存を除去。
- Application: 管理AI、設定・命令・知覚DTO、`IEnemyRuntime`、`IEnemyEncounterFeedback`。
- Infrastructure: `UnityEnemyRuntime` がRaycast/NavMesh、`FMODEnemyEncounterFeedback` が既存の発見音を担当。
- View: Unity部品への参照とGizmo表示。PresenterやUseCaseには依存しない。
- Presentation: 入力・足音の報告、命令とViewの仲介、既存の死亡・ストロボとの接続。Domain/Infrastructureへ直接依存しない。
- DI: `PlayerLifetimeScope` が設定、各敵のランタイム、発見演出を登録。

正式な `Assets/Shinzui` 内だけに実装しています。既存asmdefの参照先やGUID、Prefab/Sceneのシリアライズは変更していません。

## 検証

Unity 6000.5.8f1の実Editorで `Shinzui.EnemyDirector.Tests`（EditMode）15件と `Shinzui.EnemyDirector.PlayModeTests`（PlayMode）6件、計21件が通過しました。結果は `Artifacts/EnemyDirector/editmode-results.json` と `playmode-results.json` に保存しています。

StageTempのPlayでも既存EnemyとBlackHoleEnemyの2体が登録され、NavMesh接続と移動、視認後のHunt、接触死亡、ストロボの停止・時間経過後の解除を確認しました。この実行区間のConsole Errorは0件です。検証後はPlayを終了し、元のTitleシーンへ戻しました。実機ビルドや長時間の難易度評価は実施していません。

参考: Creative Assembly, [Building Fear in Alien: Isolation — GDC 2015](https://gdcvault.com/play/1021852/Building-Fear-in-Alien)。本実装の具体的な状態遷移・数値は、このプロジェクト用の独自設計です。