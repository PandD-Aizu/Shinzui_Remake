# 敵AI「二つの脳」化 実装計画（Alien: Isolation 方式）

実装担当: Codex（このドキュメントを指示書として使う）
コーディング規約は `CLAUDE.md` に従う（XMLコメント・ブロックごとのコメント・既存関数の変更は最小限・過剰な例外処理をしない・テストは必要最小限）

参考: Creative Assembly「Building Fear in Alien: Isolation」(GDC 2015) と公開されている設計解説。原作の内部実装の再現ではなく、公開されている設計思想をこのプロジェクト向けに再設計したもの

---

## 0. 目的と設計思想

Alien: Isolation の敵AIの特徴は次の4点。これを本作に導入する

| # | 原作の仕組み | 本作での形 |
|---|---|---|
| A | **二つの脳**。全知の「Director」と、知覚だけで動く「敵本体」を分離。Directorはプレイヤー位置を知っているが、敵へは「大体この辺」というヒントしか渡さない | `EnemyDirectorUseCase`（全知）と新規 `EnemyBrain`（知覚のみ）に分離。**`EnemyBrain` の入力にプレイヤー座標を構造的に含めない** |
| B | **Menace Gauge**。敵が近い・見えている・追跡中だと上昇。上限に達すると敵を「バックステージ（ダクト）」へ下げて休息を作る | 既存の `Pressure` を `MenaceGauge` に置き換え。上限で敵を **Backstage（不可視・非接触の移動状態）** に退避させ、プレイヤーから見えない地点で再出現 |
| C | **行動ツリー + 行動アンロック**。行動ツリーのノードの多くは最初ロックされていて、プレイヤーの戦術（隠れる、火炎放射器を使う等）に応じて解放され、敵が「学習した」ように見える | 小型のビヘイビアツリーを実装し、`Gate` デコレータでノードをロック。プレイヤーの戦術（ストロボ多用・石の陽動・しゃがみ接近・ロッカーに隠れるなど）を数えてアンロック |
| E | **隠れ場所**。ロッカーに隠れてやり過ごせるが、多用すると敵がロッカーを調べ始める。近くで嗅がれたら身を引いて息を潜める | 保守用ロッカーをギミックとして自動配置。隠れる・息を止める操作を追加し、敵は `CheckHidingSpots` アンロック後にロッカーを調べる（§7.5） |
| D | **段階的な知覚**。視覚・聴覚は即発見ではなく「疑い」が溜まる。距離・視野中心/周辺・明るさ・姿勢で溜まり方が変わる | `SuspicionMeter` を敵ごとに持ち、視覚サンプルとノイズイベントで加算。`Unaware → Suspicious → Alerted → Engaged` の段階を持つ |

### 絶対に守るルール（テストで担保する）
1. `EnemyBrain` はプレイヤー座標を受け取らない。プレイヤー位置を知る手段は「視認したときの `SightSample.SeenPosition`」「ノイズイベントの発生位置」「Directorのヒント（ぼかし済み）」の3つだけ
2. Directorは敵に「追え」と命令しない。追跡は `EnemyBrain` が知覚に基づいて自分で決める
3. Directorは追跡中（Engaged）の敵をBackstageへ下げない。視線が切れてから下げる
4. 再出現地点はプレイヤーのカメラから見えない位置に限る
5. 隠れているプレイヤーを敵が知る手段は「隠れる瞬間を目撃した」「ロッカーの前で気配（息・物音）を感じた」の2つだけ。Directorは隠れている事実を知っているが、それを敵へ渡さない
6. 既存の挙動を壊さない: 接触死亡（遮蔽判定込み）、人型の捕獲演出、ストロボ停止/減速、ループトンネルの `WarpZ`、`StageIntroPresenter` による演出中停止、`IsPlayerFound` / `IsEncounterRecognized` の発見演出

---

## 1. 現状把握（調査結果）

### 既存の敵AIパイプライン
- `PlayerLifetimeScope.RegisterEnemies` が `EnemyDirectorSettings`・`EnemyDirectorUseCase`・敵ごとの `UnityEnemyRuntime` を登録
- `EnemyPresenter.Tick()`
  1. 各 `EnemyController.CreateReport()` で `EnemyReport`（位置・`CanSeePlayer`・`IsVisibleToPlayer`）を作成
  2. 足音から `PlayerNoiseRadius` を計算（歩き4m・走り13m・しゃがみ0）
  3. `EnemyDirectorUseCase.DecideCommands(world, dt)` → 敵ごとの `EnemyCommand`
  4. `EnemyController.Tick(..., command)` が `IEnemyRuntime` で移動・死亡判定
- `EnemyDirectorUseCase` は既に Ambient / BuildUp / Hunt / Search / Recovery のフェーズと `Pressure` を持つ「Alien: Isolation 風」実装（`docs/enemy-director.md`）

### 既存実装と原作方式のギャップ
| 項目 | 現状 | 問題点 |
|---|---|---|
| 意思決定 | Directorが `ChasePlayer` / `InvestigatePosition` を直接命令 | 敵本体に「脳」がなく、Directorが全部操っている（二つの脳になっていない） |
| 視覚 | 視野角+距離+Raycastの二値。0.3秒連続で発見 | 距離・周辺視野・ライト・しゃがみの差が出ない |
| 聴覚 | 足音の半径だけ。石・火災報知器などの音は未入力 | 陽動ができない |
| 休息 | Recoveryで近くの敵を18m離すだけ | 敵が見えたまま離れていくので「ダクトに消えた」緊張感がない |
| 学習 | なし | 同じ戦術が永遠に効く |
| 捜索 | 最終地点の周りを黄金角スパイラル | 小部屋・横穴を調べない |
| 隠れ場所 | なし | 見つかった後の逃げ場がなく、やり過ごす遊びができない |

### 利用できる既存資産
- `IEnemyRuntime` / `UnityEnemyRuntime`: NavMesh移動・視線Raycast・`WarpZ`
- `EnemyView`: `IsVisibleFrom(camera, root, mask)`、`FadeOut()`、Gizmo
- `FlashlightUseCase.IsOn` / ストロボ（`StrobeTarget` → `EnemyController.ApplyStrobeEffect`）
- `PlayerMoveUseCase.IsRunning` / `IsCrouching` / `CurrentHeight`
- `PlayerThrowUseCase` + `View/StoneCollisionHandler.cs`（石の着弾 → ノイズ源）
- `View/Interaction/FireAlarm.cs`（ノイズ源）
- `TunnelMapDto`（小部屋・側面口・落下口 `DropOpenings`・上階 `TunnelUpperFloorBuilder`）→ 捜索点・再出現点の候補
- テスト: `Assets/Shinzui/Tests/EnemyDirector/Editor/DirectorTests.cs`（EditMode 18件）、`Runtime/RuntimeTests.cs`（PlayMode 5件）

---

## 2. 全体アーキテクチャ

```
                ┌───────────────────── EnemyPresenter.Tick ─────────────────────┐
 Player状態 ──▶ │ ① NoiseEventHub.Drain()   ② 各敵のSightSample採取              │
 (位置/走り/    │ ③ EnemyDirectorUseCase.Update(全知: プレイヤー座標 + 各BrainReport)│
  しゃがみ/     │      → 敵ごとの DirectorDirective（ヒント/Backstage/出現）        │
  ライト)       │ ④ EnemyBrain.Tick(知覚 + Directive)  ※プレイヤー座標なし        │
                │      → EnemyIntent（Chase/MoveTo/Search/LookAround/Backstage…）  │
                │ ⑤ EnemyController が Intent を IEnemyRuntime / EnemyView へ反映  │
                │ ⑥ PlayerTacticTracker → BehaviorUnlockEntity（行動アンロック）   │
                └──────────────────────────────────────────────────────────────┘
```

### レイヤー配置（既存規約に合わせる）
- **Domain**（Unity非依存・純C#）: `MenaceGauge`、`SuspicionMeter`、`BehaviorUnlockEntity`、各種enum
- **Application**（`UnityEngine.Vector3` 可・MonoBehaviour不可）: ビヘイビアツリー基盤、`EnemyBrain`、Director、知覚計算、ノイズハブ、戦術トラッカー、DTO・設定
- **Infrastructure**: `UnityEnemyRuntime` 拡張（視覚サンプル・出現地点探索・Backstage切替）、捜索点プロバイダ、FMODフィードバック
- **View**: `EnemyView.SetBackstage(bool)`、デバッグ表示、ノイズ源のイベント公開
- **Presentation**: `EnemyPresenter` / `EnemyController` の配線変更
- **DI**: `PlayerLifetimeScope` に設定と登録を追加

---

## 3. 新規・変更ファイル一覧

### Domain（新規）
| ファイル | 内容 |
|---|---|
| `Domain/ValueObjects/Enemy/EnemyAwarenessLevel.cs` | `enum EnemyAwarenessLevel { Unaware, Suspicious, Alerted, Engaged }` |
| `Domain/ValueObjects/Enemy/EnemyBehaviorFlags.cs` | `[Flags] enum EnemyBehaviorFlags`（§7 参照） |
| `Domain/ValueObjects/Enemy/PlayerTacticKind.cs` | `enum PlayerTacticKind`（§7 参照） |
| `Domain/Entities/Enemy/MenaceGauge.cs` | Menaceの加算・減衰・閾値・Backstage最短時間の管理（§6） |
| `Domain/Entities/Enemy/SuspicionMeter.cs` | 疑いの値0〜1、段階判定、減衰、Alerted保持時間（§4） |
| `Domain/Entities/Enemy/BehaviorUnlockEntity.cs` | 戦術回数の集計、ルール評価、アンロック済みフラグ保持（§7） |

### Application（新規）
| ファイル | 内容 |
|---|---|
| `Application/AI/BehaviorTree/BtStatus.cs` ほか | `BtNode`, `BtSelector`, `BtSequence`, `BtCondition`, `BtAction`, `BtGate`, `BtCooldown`, `BtTimeLimit`（§5） |
| `Application/AI/Enemy/EnemyBrain.cs` | 敵1体の脳。黒板・ツリー構築・`Tick` → `EnemyIntent` |
| `Application/AI/Enemy/EnemyBlackboard.cs` | 最終目撃地点、最終目撃時刻、調査対象、捜索点キュー、Directive など |
| `Application/AI/Enemy/EnemyBrainTreeFactory.cs` | ツリー構造の組み立て（§5の優先順位） |
| `Application/DTOs/Enemy/SightSample.cs` | 視覚サンプル（§4） |
| `Application/DTOs/Enemy/NoiseEvent.cs` | ノイズイベント + `enum NoiseKind { Footstep, Impact, Alarm, Door, Strobe, Breath }` |
| `Application/DTOs/Enemy/EnemyBrainInput.cs` | Brainへの入力（**プレイヤー座標を持たない**） |
| `Application/DTOs/Enemy/EnemyIntent.cs` | Brainの出力 + `enum EnemyIntentType` |
| `Application/DTOs/Enemy/DirectorDirective.cs` | Director→Brain。`None / HintArea / GoBackstage / EmergeAt` + 位置 + 半径 |
| `Application/DTOs/Enemy/EnemyBrainSettings.cs` | 知覚・捜索・速度の調整値（Serializable） |
| `Application/DTOs/Enemy/MenaceSettings.cs` | Menace・Backstage・出現距離の調整値（Serializable） |
| `Application/DTOs/Enemy/BehaviorUnlockRuleDto.cs` | アンロックルールのInspector用DTO |
| `Application/Interfaces/Enemy/INoiseEventSink.cs` | `void Emit(NoiseEvent e)`（ノイズ源から呼ぶ） |
| `Application/Interfaces/Enemy/IEnemySearchPointProvider.cs` | 捜索点・再出現点の取得 |
| `Application/UseCases/Enemy/NoiseEventHub.cs` | `INoiseEventSink` 実装。フレームごとにバッファしてDrain |
| `Application/UseCases/Enemy/EnemyPerception.cs` | `SightSample` とノイズから疑いの増分を計算する純関数群 |
| `Application/UseCases/Enemy/PlayerTacticTracker.cs` | 戦術イベントの検出 → `BehaviorUnlockEntity` へ通知 |

### Application（変更）
| ファイル | 変更内容 |
|---|---|
| `UseCases/Enemy/EnemyDirectorUseCase.cs` | 直接の追跡命令を廃止し、Directive出力へ。`Pressure` → `MenaceGauge`。Featured Enemy選定。Backstage/出現制御 |
| `DTOs/Enemy/EnemyReport.cs` | `Awareness`、`IsBackstage`、`IsEngaged` を追加 |
| `DTOs/Enemy/EnemyWorldState.cs` | `PlayerIsCrouching`、`PlayerIsRunning`、`FlashlightOn` を追加（Director用。Brainには渡さない） |
| `Interfaces/IEnemyRuntime.cs` | §8 のメソッド追加 |
| `Interfaces/IEnemyEncounterFeedback.cs` | `OnEnemyBackstageMove(Vector3)`、`OnEnemyEmerge(Vector3)` を追加（ダクト音など） |
| `DTOs/Enemy/EnemyCommand*.cs` | 移行完了後に削除（Phase 2 の最後） |

### Infrastructure
| ファイル | 内容 |
|---|---|
| `Services/UnityEnemyRuntime.cs`（変更） | `SampleSight`、`SetBackstage`、`TryFindEmergePoint`、`WarpTo` を追加 |
| `Enemy/NavMeshSearchPointProvider.cs`（新規） | NavMeshサンプリング + 生成マップの小部屋入口/側面口/落下口から候補点を返す |
| `Services/FMODEnemyEncounterFeedback.cs`（変更） | Backstage移動音・出現音。イベント未作成なら空実装で可 |

### View
| ファイル | 内容 |
|---|---|
| `View/Enemy/EnemyView.cs`（変更） | `SetBackstage(bool)`: Renderer/Collider/Animator の有効切替。Gizmoに疑いの色・視野を表示 |
| `View/Enemy/EnemyAIDebugView.cs`（新規） | Menace・フェーズ・各敵の Awareness / 実行中ノード / アンロック一覧を画面表示（開発ビルドのみ） |
| `View/StoneCollisionHandler.cs`（変更） | 着弾時に `event Action<Vector3, Vector3> Impacted`（位置, 飛来方向）を公開 |
| `View/Interaction/FireAlarm.cs`（変更） | 鳴動時に `event Action<Vector3> Rang` を公開 |

### Presentation
| ファイル | 内容 |
|---|---|
| `Enemy/EnemyPresenter.cs`（変更） | §2 の①〜⑥の順で駆動。足音を `NoiseEvent` として Hub に流す |
| `Enemy/EnemyController.cs`（変更） | `EnemyCommand` の switch を `EnemyIntent` の switch に置換。死亡判定・ストロボ・WarpZはそのまま |
| `Enemy/EnemyNoisePresenter.cs`（新規） | Viewのノイズイベントを購読し `INoiseEventSink` へ転送 |
| `Enemy/EnemyAIDebugPresenter.cs`（新規） | Director/Brainの状態を DebugView へ渡す |
| `Flashlight/FlashlightPresenter.cs`（変更） | ストロボ命中時に `PlayerTacticTracker.Report(StrobeHitEnemy)` と `NoiseKind.Strobe` を発行 |

### DI
- `DI/PlayerLifetimeScope.cs`: `EnemyBrainSettings`・`MenaceSettings`・アンロックルール配列を `[SerializeField]` で追加し登録。`NoiseEventHub`（`.As<INoiseEventSink>().AsSelf()`）、`BehaviorUnlockEntity`、`PlayerTacticTracker`、`IEnemySearchPointProvider` を Scoped 登録。`EnemyNoisePresenter`・`EnemyAIDebugPresenter` を EntryPoint 登録

---

## 4. 知覚システム（Phase 1）

### SightSample（Infrastructureで採取、毎フレーム）
```csharp
public readonly struct SightSample
{
    public bool InRange { get; }          // 最大視認距離内
    public float Distance { get; }
    public float AngleFromForward { get; } // 水平角(度)
    public float VisibleRatio { get; }     // 頭・胴・足の3点Raycastの通過率 0〜1
    public Vector3 SeenPosition { get; }   // VisibleRatio > 0 のときだけ有効
    public Vector3 SeenVelocity { get; }   // 同上（PredictiveChase用）
}
```
- 3点Raycast（頭・胴・足）は既存 `HasClearContact` のロジックを流用。しゃがむと頭の点が下がり遮蔽物に隠れやすくなる
- `SeenPosition` / `SeenVelocity` は可視時のみ値を入れ、それ以外は `default`

### 疑いの増加量（`EnemyPerception`、純関数）
```
sightGain/秒 = baseSightRate(既定 2.5)
             × distanceFactor   … 近距離(≤4m)=1、最大距離で0へ線形
             × angleFactor      … 中心視(±30°)=1、周辺視(〜fov/2)=0.35、それ以外=0
             × VisibleRatio
             × postureFactor    … 走り1.5 / 歩き1.0 / しゃがみ0.55 / 静止しゃがみ0.35
             × lightFactor      … ライトON 1.6、ライトが敵を照らしている 2.2
```
- 姿勢とライトはDirector経由ではなく `EnemyPresenter` が `EnemyBrainInput.PlayerSignature`（姿勢・ライト状態のみ、座標なし）として渡す。見えているときだけ意味を持つ値なので情報漏れにならない
- 近距離即発見: `Distance ≤ instantDetectDistance(既定 2.5m)` かつ `VisibleRatio ≥ 0.34` なら即 Engaged
- ライトの光芒検出（任意）: プレイヤーのライトが敵の視野内を照らしていたら、プレイヤーが見えていなくても `Suspicious` に上げ、光源方向を調査対象にする

### ノイズ
```csharp
public readonly struct NoiseEvent
{
    public NoiseKind Kind { get; }
    public Vector3 Position { get; }
    public float Radius { get; }       // 聞こえる最大距離(m)
    public Vector3 Direction { get; }  // Impactの飛来方向など。不要なら zero
}
```
| 発生源 | 半径 | 発行元 |
|---|---|---|
| 足音（歩き/走り） | 既存値 4m / 13m | `EnemyPresenter`（既存の `GetFootstepNoiseRadius` を流用） |
| 石の着弾 | 10m | `StoneCollisionHandler.Impacted` → `EnemyNoisePresenter` |
| 火災報知器 | 30m | `FireAlarm.Rang` → `EnemyNoisePresenter` |
| ストロボ | 8m | `FlashlightPresenter` |

- 聞こえる判定: `距離 ≤ Radius × (遮蔽ありなら 0.6)`。遮蔽は1本のRaycastで十分（`IEnemyRuntime.HasLineTo(Vector3)` を追加）
- 疑いの増分: `noiseGain = noiseBase(既定 0.6) × (1 - 距離/有効半径)`。**音だけでは Engaged にならない**（最大 Alerted）
- 聞こえたノイズの位置を黒板の `InvestigateTarget` に入れる（より大きい増分のものを優先）

### SuspicionMeter（Domain）
- 値 0〜1。閾値: `Suspicious ≥ 0.3`、`Alerted ≥ 0.65`、`Engaged = 1.0`（視覚由来のみ到達可能）
- 刺激がないと `decayPerSecond`（既定0.12）で減衰。ただし Engaged から視線を失った後は `alertedHoldTime`（既定 8秒）の間 Alerted を下回らない
- `API`: `AddSight(float)`, `AddNoise(float)`, `Tick(float dt, bool hasStimulus)`, `Level`, `Value`, `ForceEngage()`

### Phase 1 の完了条件
- Directorの「`_sightTimers` による0.3秒判定」を `SuspicionMeter.Level == Engaged` に置き換える（この段階ではまだDirectorが命令を出す構造のまま）
- しゃがみ＋ライトOFFで遠距離・周辺視野なら発見されにくく、走り＋ライトONなら素早く発見されることをPlayで確認
- 石を投げると近くの敵が着弾点を調べに行く

---

## 5. 敵の脳: ビヘイビアツリー（Phase 2）

### 基盤（`Application/AI/BehaviorTree`）
- 構築時にのみ new し、Tick中はアロケーションしない
- `BtSelector` は**毎フレーム先頭から再評価**（優先度の高い行動が割り込める「リアクティブ」型）
- `BtSequence` は実行中の子を記憶
- `BtGate(EnemyBehaviorFlags flag, BtNode child)` はアンロックされていなければ `Failure`（＝原作の「ロックされたノード」）
- `BtCooldown(seconds, child)`、`BtTimeLimit(seconds, child)`
- デバッグ用に `EnemyBrain.ActivePath`（実行中ノード名を `/` で連結した文字列）を公開。名前はノード生成時に付ける

### ツリー構造（上が高優先）
```
Root (Selector)
├─ [1] Backstage           Directive が GoBackstage/EmergeAt、または黒板.IsBackstage
│     └─ Sequence: 出口へ移動(プレイヤーから見えない位置) → Backstage化 → 待機 → EmergeAt で出現
├─ [2] Stunned              ストロボ停止中 → Idle
│     └─ Gate(StrobeResistance): 停止解除後、ストロボ発生方向を調査対象にして Alerted へ
├─ [2.5] PullOut            隠れる瞬間を目撃した / ロッカー前で Engaged に達した（§7.5）
│     └─ Sequence: ロッカー前へ全速 → OpenHidingSpot → 捕獲
├─ [3] Chase                Level == Engaged かつ（可視 または 見失って < chaseGraceTime 1.5秒）
│     ├─ 可視: SeenPosition へ全速
│     └─ 見失い直後: 最終目撃地点へ
│           └─ Gate(PredictiveChase): 最終目撃地点 + SeenVelocity × 1.5秒 へ
├─ [4] Search               Level == Alerted（見失った後 or 大きな音）
│     └─ TimeLimit(searchDuration 20秒) Sequence:
│           最終目撃/音の地点へ → LookAround(2秒) → 捜索点を順に巡回（半径を徐々に拡大）
│           ├─ Gate(SearchSideRooms): 近くの小部屋・側面口を捜索点の先頭に入れる
│           ├─ Gate(SlowStalk): 移動速度0.4倍、各点で3秒立ち止まって聞き耳（聴覚半径×1.3）
│           └─ Gate(CheckHidingSpots): 捜索半径内のロッカーを近い順に InspectHidingSpot（§7.5）
├─ [5] Investigate          Level == Suspicious
│     └─ Sequence: その場で刺激方向を向く(1秒) → 歩き(0.5倍速)で調査対象へ → LookAround
│           └─ Gate(DecoyWariness): 調査対象が Impact なら、着弾点の後に飛来方向の逆側を5〜8m辿る
├─ [6] FollowHint           Directive が HintArea
│     └─ ヒント半径内のランダム点を巡回（ヒントが変わるまで）
├─ [6.5] FakeLeave          Gate(LingerAfterSearch): 捜索終了時、ロッカーから8m離れて立ち止まり15秒聞き耳を立ててから去る
├─ [7] Backtrack            Gate(Backtrack): 捜索打ち切り後 10〜20秒経ったら一度だけ最終地点へ戻る
└─ [8] Wander               既存 `Wander(radius)` と同じ
```

### EnemyIntent（Brainの出力）
```csharp
public enum EnemyIntentType { Idle, Wander, MoveTo, Chase, LookAround, GoBackstage, Hidden, Emerge,
    InspectHidingSpot, OpenHidingSpot }   // 後ろ2つは Phase 5 で追加。対象は EnemyIntent.HidingSpotId
public readonly struct EnemyIntent
{
    public EnemyIntentType Type { get; }
    public Vector3 Target { get; }
    public float SpeedMultiplier { get; }   // Chase=1、Search=0.8、Investigate=0.5 など設定値
}
```
- `EnemyController` は `Chase` のとき既存の人型の追跡ポーズ (`SetChasePose`) とループトンネルのダミー座標処理をそのまま使う
- `LookAround` は停止して向きだけ変える（`IEnemyRuntime.FaceTowards(Vector3)` を追加）

### EnemyReport の追加情報（Brain → Director）
- `Awareness`、`IsEngaged`、`IsBackstage`、`ActiveIntent`

### 発見演出との互換
- `IsPlayerFound` = いずれかの敵が Engaged
- `IsEncounterRecognized` / `IEnemyEncounterFeedback.OnPlayerFound()` = Engaged の敵をプレイヤーが視認したとき（既存条件と同じ）。Directorがこれまで通り判定する

### Phase 2 の完了条件
- Directorは `ChasePlayer` を一切出さない。`EnemyCommand` / `EnemyCommandType` を削除
- Brainの入力型にプレイヤー座標が存在しないことをコードレビューで確認できる
- 既存の DirectorTests のうち、Directorが追跡命令を出す前提のテストは Brain のテストへ移すか削除する（§10）

---

## 6. Director: Menace Gauge と Backstage（Phase 3）

### Featured Enemy
- 原作は敵1体だが本作は複数。**「主役」1体だけ**をDirectorが積極的にプレイヤーへ寄せる
- 選定: Backstageでなく、プレイヤーへのNavMesh距離が最も近い敵。Engaged の敵がいればその敵。頻繁に交代しないよう最低保持20秒
- 主役以外は Directive なし（自分の知覚で徘徊・反応はする）。Menace上限時は主役以外も Backstage 候補にする（主役優先、プレイヤーから20m以内の敵）

### MenaceGauge（Domain）
```
上昇/秒:
  主役が Engaged                               +0.10
  主役をプレイヤーが視認（12m以内）           +0.06
  主役がプレイヤーから menaceRadius(15m) 以内  +0.04 × (1 - 距離/menaceRadius)
減少/秒:
  上記どれも無し                               -0.05
  Backstage 中                                 -0.12
```
- `Value ≥ 1.0` かつ 主役が Engaged でない → 主役に `GoBackstage`
- Engaged のまま上限に達した場合は保持し、視線が切れて `chaseGraceTime` 経過後に `GoBackstage`
- Backstage は最短 `backstageMinDuration`（20〜35秒の乱数）かつ `Value ≤ 0.3` で解除

### 既存フェーズとの対応
| 既存フェーズ | 新しい意味 |
|---|---|
| Ambient | 主役はFrontstageで自由行動。ヒントなし |
| BuildUp | 主役に `HintArea` を出す。ヒントは既存の「12mセル + オフセット」方式を流用し、`hintInterval` ごとに更新 |
| Hunt | 誰かが Engaged（Directorは観測するだけ） |
| Search | 誰かが Alerted（観測するだけ） |
| Recovery | 主役が Backstage 中 |

- 「離れすぎ防止」: 主役がFrontstageで `leashDistance`（35m）より遠い状態が `leashTime`（25秒）続いたら BuildUp に移ってヒントを出す（原作の「敵が常に近くにいる」感覚）

### Backstage の実装
1. Brainが `GoBackstage` を受けると、プレイヤーのカメラから見えない最寄りの退出点（落下口・小部屋の奥・曲がり角の先など `IEnemySearchPointProvider.GetExitPoints`）へ移動
2. 退出点到着時、`EnemyView.IsVisibleFrom(camera)` が false なら `SetBackstage(true)`: Renderer/Collider無効、NavMeshAgent無効、接触死亡判定停止
3. Backstage 中の敵は「抽象位置」を持ち、Directorが指定した方向へ一定速度（3m/秒）で直線移動させる。`IEnemyEncounterFeedback.OnEnemyBackstageMove(抽象位置)` を数秒おきに呼び、天井裏・壁の中を這う音を鳴らす（**プレイヤーに「まだ近くにいる」と伝えるのが重要**）
4. 解除時、Directorが出現点を選ぶ: プレイヤーから `emergeMinDistance`（15m）〜`emergeMaxDistance`（30m）、NavMesh上、プレイヤーカメラから不可視、プレイヤーとNavMesh経路で繋がっている点。候補が無ければ距離を広げて再試行
   - `Gate(AmbushFromAbove)` がアンロック済みなら落下口/上階の点も候補に入れる
5. `EmergeAt` を受けたBrainは `WarpTo` → `SetBackstage(false)` → 出現音 `OnEnemyEmerge` → Alerted 状態でヒント地点を捜索開始
- `StageIntroPresenter` の演出中停止・死亡時の他敵 `FadeOut` は Backstage 中の敵にも適用する（`FadeOut` は Backstage なら何もしない）

### Phase 3 の完了条件
- 追跡を振り切ってしばらくすると敵がプレイヤーの見ていない所で消え、這う音だけがする → 一定時間後に見えない位置から現れる
- 追跡中・視認中に消えることは絶対にない
- 敵が長時間遠くに行ったままにならない

---

## 7. 行動アンロック（Phase 4）

### PlayerTacticKind と検出方法（`PlayerTacticTracker`）
| 戦術 | 検出条件 |
|---|---|
| `SneakNearEnemy` | しゃがみ状態で敵から8m以内、かつ その敵が Unaware/Suspicious の時間が累計10秒で1回 |
| `DecoyThrow` | 石の着弾ノイズで敵が Investigate を開始した（Brainが通知） |
| `StrobeHitEnemy` | ストロボが敵に命中した |
| `EscapedSearch` | Search が時間切れで終わった（プレイヤーを再発見できなかった） |
| `BrokeSightAtCorner` | Engaged の敵が8m以内で視線を失い、そのまま Search に移行した |
| `HidDuringSearch` | 敵が Alerted で15m以内にいる間にロッカーへ隠れ、その捜索が時間切れで終わった（Phase 5） |

### アンロックルール（Inspectorで調整可能。既定値）
| フラグ `EnemyBehaviorFlags` | 条件 | 効果 |
|---|---|---|
| `SlowStalk` | `SneakNearEnemy` × 3 | 捜索中ゆっくり歩き立ち止まって聞く。聴覚半径×1.3 |
| `DecoyWariness` | `DecoyThrow` × 3 | 着弾音による疑い増分×0.5、着弾後に飛来方向を逆に辿る |
| `StrobeResistance` | `StrobeHitEnemy` × 3 | ストロボ停止時間×0.5、解除後に光源方向を調査 |
| `SearchSideRooms` | `EscapedSearch` × 2 | 捜索で小部屋・側面口を優先して調べる |
| `Backtrack` | `EscapedSearch` × 3 | 捜索終了後に一度だけ戻ってくる |
| `PredictiveChase` | `BrokeSightAtCorner` × 3 | 見失った後、進行方向の先を予測して追う |
| `AmbushFromAbove` | ステージ進行（階層 ≥ 2） | 出現点に落下口・上階を含める |
| `CheckHidingSpots` | `HidDuringSearch` × 2 | 捜索中に近くのロッカーを調べる（§7.5） |
| `LingerAfterSearch` | `HidDuringSearch` × 4 | 捜索終了時に去ったふりをして近くで待ち伏せる |

- `BehaviorUnlockEntity` は全敵で共有（「敵が学習した」体験を一貫させる）
- 1回のアンロックの後 `unlockCooldown`（60秒）は次のアンロックをしない（急激に賢くならないように）
- アンロックはプレイヤーに通知しない（原作同様、気付かせない）。デバッグ表示とログのみ
- 保存: Phase 4 では1ラン内のみ保持。死亡リトライで引き継ぐかは後で決める（`GameSaveData` への追加は今回は行わない）

### Phase 4 の完了条件
- 同じ戦術を繰り返すと、対応する行動が現れることをデバッグ表示で確認できる
- 初期状態では全てのGate付き行動が発動しない

---

## 7.5 隠れ場所: ロッカー（Phase 5）

### 遊びの流れ
1. 敵に追われる、または気配を感じたら、近くのロッカーに入る（インタラクト）
2. 中ではスリット越しに外が見える。移動・ライト・アイテム使用はできない。足音ノイズも出ない
3. 敵がロッカーの前で立ち止まって嗅ぎ始めたら、**息を止めるボタンを押し続けて身を引く**。息ゲージが尽きるとあえぎ声（ノイズ）が出る
4. 敵が去ったら出る。出るときのドア音は小さなノイズになる

### 敵が隠れているプレイヤーに気付く条件（これ以外では絶対に気付かない）
| 条件 | 結果 | ロック |
|---|---|---|
| 目撃: 敵が Suspicious 以上で、プレイヤーが入る瞬間（ドアが閉じるまで）を `VisibleRatio > 0` で見ていた | そのロッカーIDを黒板の `WitnessedHidingSpotId` に記録 → PullOut | なし（常に有効） |
| 入る音: 走ったまま入った（ドアを叩きつける） | ロッカー位置に `NoiseKind.Door` 半径8m | なし |
| 気配: `InspectHidingSpot` 中に1.5m以内で、息を止めていない | 疑い +0.35/秒。1.0 で PullOut | `CheckHidingSpots` 解放後のみ発生 |
| あえぎ: 息ゲージが尽きた | ロッカー位置に `NoiseKind.Breath` 半径6m（Engaged 判定には使わないが、近くの敵は Inspect に来る） | なし |
| 中で息を止めていても、敵が調べているときにランダムで開ける | 確率 `blindOpenChance`（既定0.15。`LingerAfterSearch` 解放後0.3） | `CheckHidingSpots` 解放後のみ |

- `CheckHidingSpots` 解放前の敵はロッカーを調べない（原作同様、最初は隠れれば安全）
- 隠れている間 `SampleSight` は常に `VisibleRatio = 0` を返す
- Directorは隠れていることを知っており、Menaceは通常通り距離で増減する。ヒントは既存の12mセル方式なので、ロッカーの位置はそのまま漏れない

### InspectHidingSpot の挙動
- ロッカー正面の立ち位置（`HidingSpotDto.StandPosition`）へ歩き、ロッカーの方を向いて `inspectDuration`（3秒）止まる
- 気配判定（上表）を毎フレーム評価。1.0 に達するか `blindOpenChance` に当たったら OpenHidingSpot、それ以外は次の捜索点へ
- 同じロッカーは `inspectCooldown`（30秒）の間は再検査しない

### OpenHidingSpot と捕獲
- ドアを開ける（`HidingLockerView.OpenByEnemy()`）。中に誰もいなければ空振りで捜索続行
- 中にプレイヤーがいれば、既存の `EnemyPresenter.CapturePlayer(EnemyView)` → `PlayerDeathUseCase.TryKillPlayer` を通して死亡。死亡回避（アイテム等）が成立した場合はプレイヤーを外に押し出し、敵は Engaged で追跡開始

### 配置（マップ生成への追加）
- `TunnelGimmickKind` / `TunnelGimmickKindDto` に `HidingLocker` を追加
- `TunnelGenerationConfig` / `TunnelGenerationRequestDto` / `TunnelGenerator`（Inspector）に `HidingLockerCount`（既定4）、`HidingLockerMinSpacing`（既定25m）を追加
- `TunnelGimmickPlanner.Plan` の最後に**別ブロックとして**追加（既存の巣・報知器のブロックは変更しない）
  - 候補: 小部屋の壁際を優先し、足りなければ通常トンネルの側壁（報知器と同じ条件で開口部・落下口・ゴール端・スタートトンネルを除外し、報知器から3m以上離す）
  - 乱数は既存の `random` を引き続き使う（既存ギミックの配置結果を変えないよう、必ず既存処理の後に引く）
- `TunnelMapView.Gimmicks.cs` に `CreateHidingLocker(...)` を追加（`CreateFireAlarm` と同じく壁へRaycastして設置）
- `GenerateTunnelPresenter` の gimmick ループに分岐を1つ追加し、生成したロッカーを `IHidingSpotRegistry` へ登録

### 新規・変更ファイル
| 層 | ファイル | 内容 |
|---|---|---|
| Domain | `ValueObjects/Player/BreathGauge.cs` | 息ゲージ（最大4秒、回復 0.5倍速、尽きたら2秒間止められない） |
| Application | `DTOs/Enemy/HidingSpotDto.cs` | `Id`, `Position`, `StandPosition`, `Forward` |
| Application | `Interfaces/Enemy/IHidingSpotRegistry.cs` | `Register / Clear / GetWithin(center, radius, buffer)` |
| Application | `UseCases/Player/PlayerHidingUseCase.cs` | `Enter(id, wasRunning)` / `Exit()` / `IsHidden` / `CurrentSpotId` / `IsHoldingBreath` / 息ゲージ更新。入退出とあえぎのノイズを `INoiseEventSink` へ発行 |
| Application | `UseCases/Enemy/HidingSpotRegistry.cs` | Registry 実装（Scoped） |
| Application | `Interfaces/IInputService.cs` | `HoldBreathHeld` を追加 |
| Infrastructure | `Services/UnityInputService.cs` | `HoldBreath` アクションを読む（Input Action アセットに1つ追加。既定キー: 右クリック / LT） |
| View | `View/HidingSpot/HidingLockerView.cs` | ドア開閉（LitMotion）、内部カメラアンカー、出口位置、`OpenByEnemy()` |
| View | `View/HidingSpot/HidingSpotInteractable.cs` | `InteractableComponent` 派生。入る/出るのメッセージ切替 |
| View | `View/HidingSpot/BreathGaugeView.cs` | 息ゲージUI（隠れている間だけ表示） |
| View | `View/Player/PlayerView.cs`（変更） | 隠れ中のカメラ移動・操作無効・身を引く演出（カメラを0.15m後退） |
| Presentation | `Player/PlayerHidingPresenter.cs` | インタラクト → `PlayerHidingUseCase`、入力・カメラ・ゲージの仲介。隠れ中はライト強制OFF、`PlayerMoveUseCase` の移動を停止 |
| Presentation | `Enemy/EnemyController.cs`（変更） | `InspectHidingSpot` / `OpenHidingSpot` の実行 |
| Presentation | `Enemy/EnemyPresenter.cs`（変更） | 隠れ中は足音ノイズを出さない。Brainへ「目撃」「気配」の感覚入力を渡す（`EnemyBrainInput.HidingSense`: 対象ID・気配の有無のみ） |
| Editor | `Editor/HidingLockerAssets.cs` | 仮モデル（Cubeの組み合わせ）で `Prefabs/HidingLocker.prefab` を生成するメニュー。既存の `*Assets.cs` と同じ方式 |
| DI | `PlayerLifetimeScope.cs` | Registry・UseCase・Presenter・ゲージViewの登録。手置きのロッカーは初期化時にシーンから収集して Registry に登録 |

### Phase 5 の完了条件
- 生成マップにロッカーが配置され、入る・出る・息を止める操作ができる
- 初期状態では、見られずに入れば敵は絶対にロッカーを開けない
- 入る瞬間を見られると引きずり出される
- ロッカーで捜索をやり過ごすのを2回繰り返すと、敵がロッカーを調べ始める。息を止めていればほぼ助かる
- ロッカーが無い既存の手置きシーンでもエラーなく動く

---

## 8. `IEnemyRuntime` 追加メソッド

```csharp
SightSample SampleSight(Vector3 playerPosition, float playerHeight, Vector3 playerVelocity);
bool HasLineTo(Vector3 position);              // ノイズの遮蔽判定
void FaceTowards(Vector3 position);            // LookAround / 刺激方向を向く
void SetBackstage(bool backstage);             // Agentの有効/無効切替
bool WarpTo(Vector3 position);                 // NavMesh上にサンプリングしてWarp
float PathDistanceTo(Vector3 position);        // Featured選定用。経路が無ければ float.MaxValue
```
- `SampleSight` は `playerPosition` を受け取るが、返すのは `SightSample` だけ。**Presenterは `SightSample` 以外をBrainへ渡さない**
- 出現点探索は `IEnemySearchPointProvider` 側に置く（Runtimeに置かない）

```csharp
public interface IEnemySearchPointProvider
{
    int GetSearchPoints(Vector3 center, float radius, bool includeSideRooms, Span<Vector3> buffer);
    int GetExitPoints(Vector3 from, Span<Vector3> buffer);
    bool TryFindEmergePoint(Vector3 playerPosition, float minDistance, float maxDistance,
        bool includeUpperFloor, Func<Vector3, bool> isVisibleToPlayer, out Vector3 point);
}
```
- 生成マップ（`TunnelMapDto`）が無いシーンでは NavMesh のランダムサンプリングだけで動くこと

---

## 9. 実装フェーズと順序

各フェーズ終了時にPlay可能な状態を保つこと。フェーズごとにコミットを分ける

| Phase | 内容 | 主な対象 |
|---|---|---|
| 1 | 知覚（SightSample・SuspicionMeter・NoiseEventHub・石/火災報知器のノイズ） | Domain, Application, Infrastructure, View, Presentation |
| 2 | ビヘイビアツリー基盤とEnemyBrain。Directorから追跡命令を除去。`EnemyCommand` 削除 | Application, Presentation |
| 3 | MenaceGauge・Featured Enemy・Backstage/出現・離れすぎ防止 | Domain, Application, Infrastructure, View |
| 4 | 行動アンロック（Tracker・Entity・Gate接続） | Domain, Application, Presentation, DI |
| 5 | 隠れ場所（ロッカー配置・隠れる/息を止める・目撃/気配・CheckHidingSpots/LingerAfterSearch） | 全層 + Editor |
| 6 | デバッグ表示・Gizmo・`docs/enemy-director.md` 更新・調整値の初期チューニング | View, Presentation, docs |

### 調整値の置き場所
- 既存と同じく Serializable DTO を `PlayerLifetimeScope` の Inspector に置く（`[Header("Enemy Brain")]`, `[Header("Menace")]`, `[Header("Behavior Unlock")]`）
- 非正値・非有限値は既存の `EnemyDirectorSettings.Positive` と同じ方式で既定値へフォールバック
- 使わなくなる `EnemyDirectorSettings` のフィールド（`detectionTime`, `chasePressurePerSecond` など）は削除せず `[Obsolete]` コメントも付けず、そのまま残して未使用にする（シーンのシリアライズ差分を避けるため）。Phase 6 で整理するか判断

---

## 10. テスト方針（必要最小限）

合計で既存込み 40件以内を目安。EditMode中心

### 追加（EditMode）
- `MenaceGauge`: 上昇・減衰 / 上限到達後に最短時間未満では解除されない（2件）
- `SuspicionMeter`: 音だけでは Engaged にならない / Engaged 喪失後 alertedHoldTime の間 Alerted を保持（2件）
- `EnemyPerception`: しゃがみ+ライトOFFは走り+ライトONより増分が小さい（1件）
- `BehaviorUnlockEntity`: 閾値でアンロック / クールダウン中はアンロックしない（2件）
- `BtSelector` + `BtGate`: ロック中は次の子へ、アンロック後は割り込む（1件）
- `EnemyBrain`:
  - 視覚なし・ノイズなしでは Chase しない（1件）
  - ノイズでは Investigate/Search になり Chase にならない（1件）
  - 見失った後の目標は最終目撃地点であり、その後のプレイヤー移動に追従しない（1件）
  - Engaged 中は `GoBackstage` Directive を無視する（1件）
- `EnemyDirectorUseCase`:
  - HintArea の位置がプレイヤー座標と一致しない（1件）
  - Menace上限かつ非Engagedで主役に GoBackstage を出す（1件）
  - 出現点がプレイヤーから見える場合は採用しない（1件、Providerのスタブで確認）
- 隠れ場所（Phase 5）:
  - `TunnelGimmickPlanner`: ロッカーが開口部・報知器から離れ、既存ギミックの配置結果が変わらない（1件。既存の TunnelGeneration テストに追加）
  - `EnemyBrain`: `CheckHidingSpots` 未解放なら目撃なしでは OpenHidingSpot しない / 目撃ありなら解放前でも PullOut する（2件）
  - 息を止めている間は気配の疑いが増えない（1件）
  - `BreathGauge`: 尽きるとあえぎノイズが発行される（1件）

### 既存テストの扱い
- `DirectorTests.cs` のうち `ChasePlayer` 命令を前提にするテストは、同じ意図（「近いだけでは追わない」「連続視認が必要」など）を `EnemyBrain` のテストとして書き直し、元は削除
- `RuntimeTests.cs`（PlayMode）は維持。Backstage 切替でRendererとAgentが無効になる確認を1件追加

---

## 11. Codexへの作業指示（コピペ用）

```
docs/enemy-ai-two-brain-plan.md を読み、Phase 1 から順に実装してください。

- コーディング規約は CLAUDE.md に従う（XMLコメント<summary>/<param>/<returns>、
  処理ブロックごとのコメント、文末の句点なし、です・ます調禁止、既存関数の変更は最小限、過剰な例外処理をしない）
- 既存のレイヤー構成（Domain/Application/Infrastructure/View/Presentation/DI）と asmdef の参照方向を守る。
  Domain に UnityEngine を持ち込まない
- §0「絶対に守るルール」を満たすこと。特に EnemyBrain の入力にプレイヤー座標を含めない
- 既存の接触死亡・捕獲演出・ストロボ・ループトンネルWarpZ・StageIntro停止を壊さない
- Scene/Prefab の変更は最小限。新しい設定は PlayerLifetimeScope の Inspector に既定値付きで追加する
- テストは §10 の範囲に限定する。100件を超えない
- 1フェーズごとに作業を止め、変更内容・テスト結果・未確認事項を報告する
- バグ修正の記録をドキュメントに残さない。docs の更新は Phase 6 で enemy-director.md を簡潔に書き換えるのみ
```

---

## 12. 未決事項（実装前に決めておくと良いもの）

1. **ロッカーの正式モデル**: Phase 5 は仮モデルで実装する。トンネルの雰囲気に合う外観（保守用ロッカー・配電盤キャビネットなど）とドアの開閉音は別途用意が必要
2. **アンロックの永続化**: 死亡リトライ・次の階層へ引き継ぐか（推奨: 同一ラン内は引き継ぐ、タイトルに戻るとリセット）
3. **Backstage 移動音のFMODイベント**: サウンド担当にイベント（這う音・ダクトを叩く音・出現音）の作成を依頼する必要がある
4. **複数敵の扱い**: 主役1体＋その他、で良いか。BlackHoleEnemy など特殊な敵を Director 管理から外すか
