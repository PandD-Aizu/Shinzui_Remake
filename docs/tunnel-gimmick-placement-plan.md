# ランダムマップへのギミック自動配置 実装計画

対象ギミック: 蜘蛛の巣 (`SpiderWeb`) / 火災報知器 (`FireAlarm`)
実装担当: Codex（このドキュメントを指示書として使う）
コーディング規約は `CLAUDE.md` に従う（XMLコメント・ブロックごとのコメント・既存関数の変更は最小限）

---

## 0. 現状把握（調査結果）

### 生成パイプライン
1. `TunnelGenerationEntryPoint.Start()` → `GenerateTunnelPresenter.ExecuteGeneration()`
2. `GenerateTunnelUseCase` が `TunnelLayoutGenerator`（Domain）で `TunnelLayoutResult` を作り `TunnelMapDto` に変換
3. `GenerateTunnelPresenter.BuildSceneHierarchy()` が `TunnelMapView` を使って
   トンネル → 通常通路 → 小部屋 → ワープ通路 → 落下口 → ゴール → カメラ の順に実体化
4. `_navigation.BuildNavMesh()` → `_mapView.NotifyGeometryReady()`
5. EntryPoint で `_audio.Rebuild()` → `_items.OnMapReady()`（アイテム生成）

### 座標系
- トンネルルート: `localRotation = LookRotation(forward)` → **ローカルZ = 長さ方向、ローカルX = 横（壁方向）**
- トンネル長は `TunnelNodeDto.Length`、中心が原点。Z ∈ [-Length/2, +Length/2]
- 側面口は `TunnelEntranceMarkerDto`（`LocalPosX` の符号で左右、`LocalPosZ` で位置、`IsOpen` で使用中）
- 再生成時は `PrepareBuild()` が `_geometryRoot` ごと破棄するので、ギミックも `_geometryRoot` 配下に置けば自動で消える

### 既存の除外すべき場所
- `Id == 0` のトンネル = スタートトンネル（ゴール候補からも除外されている）
- `IsSpecial` / `Slope != 0` のトンネル
- ゴールトンネルの `endSign` 側の端（`GenerateTunnelPresenter` 6. で `System.Random(mapDto.Seed ^ 0x47504c)` で決定）
- 落下口 (`DropOpenings`) 周辺
- ワープ通路（トリガーがある）

### ギミックの性質
| | 蜘蛛の巣 `SpiderWeb` | 火災報知器 `FireAlarm` |
|---|---|---|
| 効果 | 触れると減速・引っ張り（**通行不能にはならない**）。マッチで燃やせる | 半径 `detectionRadius`(5m) に入ると警報・赤色灯。[E]で停止、`cooldownSeconds`(30s)後に再作動 |
| 形状 | ローカルXY平面に生成、ローカルZが法線。`width`/`height`（既定 5.5×4.0）、`seed` | 壁掛けモデル。`modelVariants` から `Awake` で `UnityEngine.Random` 選択 |
| Prefab | **存在しない**（`StageTemp.unity` などにシーン直置き） | `Assets/Shinzui/Prefabs/FireAlarm.prefab` |
| 注意 | `[ExecuteAlways]`、`RequireComponent` で `SpiderWebInteractable` 等が自動付与 | プレイヤーは自動検索するので参照設定不要 |

---

## 1. 配置ルール（ゲームデザイン）

### 蜘蛛の巣
- **通常通路（`NormalCorridors`）の断面をふさぐ形**で配置する（通路の進行方向が巣の法線）
  - 通路幅 3m 程度なので、巣の `width = CorridorWidth - 0.2`、`height = passageHeight - 0.2` 程度に合わせる
  - 通路長の中央付近（両端から 1.5m 以上離す）に置く
- 除外: `OpenLeft` / `OpenRight` が true の通路（側面が開いていて断面が閉じないため）、ワープ通路
- 1本の通路に最大1枚
- 枚数は `spiderWebCount`（既定 3）。候補が足りなければある分だけ
- 通行不能にならないので、経路の到達性チェックは不要
- （任意・Phase 2）小部屋の通路部分 (`SmallRooms` の `PassageLength`) も候補に含める

### 火災報知器
- **平坦な通常トンネルの側壁**に壁掛けで配置する
- 候補トンネル: `Slope == 0 && !IsSpecial && Id != 0`
- 1トンネル最大1個
- トンネル内のZ位置の制約
  - 両端から `endMargin`（既定 12m）以上内側
  - 同じ側の開いている側面口 (`IsOpen && sign(LocalPosX) == side`) の `LocalPosZ` から `mouthMargin`（既定 4m）以上離す
  - 落下口の中心から `DropOpening.Size + 2m` 以上離す
  - ゴールトンネルでは `endSign` 側の端から 20m 以上離す
- 報知器同士のワールド距離が `fireAlarmMinSpacing`（既定 40m、検知半径の 2 倍より十分大きく）以上
- 高さは床から `fireAlarmHeight`（既定 1.4m）
- 個数は `fireAlarmCount`（既定 3）

### 決定論
- すべて `mapDto.Seed` 由来の `System.Random` で決定する（同じシードなら同じ配置）
- 既存のゴール決定と干渉しないよう別の定数で XOR する（例: `Seed ^ 0x47494d4b`）
- 蜘蛛の巣の見た目の `seed` も同じ乱数から与える

---

## 2. アーキテクチャ

既存のクリーンアーキテクチャ（Domain → Application → Presentation → View）に合わせる。
「どこに置くか」の論理判定は Domain（テスト可能・Unity非依存）、
「壁の正確な位置」は View で Raycast して決める。

```
Domain   : TunnelGimmickPlanner        … レイアウトから配置スロットを決める（純C#）
Application: GenerateTunnelUseCase     … 結果を TunnelMapDto.Gimmicks に詰める
Presentation: GenerateTunnelPresenter  … 7. ギミック配置 ステップを追加
View     : TunnelMapView.Gimmicks.cs   … Prefab生成、Raycastで壁へスナップ
```

---

## 3. 実装ステップ

### Step 1. ゴール決定を UseCase に移す（前準備・挙動は完全に同一）
ギミック計画がゴール位置を知る必要があるため。

- `TunnelMapDto` に `int? GoalTunnelId` と `int GoalEndSign` を追加
- `GenerateTunnelUseCase.ConvertToDto` で、現在 Presenter の 6. にある計算をそのまま移植
  ```csharp
  var goalCandidates = tunnels.Where(t => t.Slope == 0 && !t.IsSpecial && t.Id != 0).ToArray();
  var random = new System.Random(unchecked(seed ^ 0x47504c));
  goalId = candidates[random.Next(n)].Id; endSign = random.Next(2) == 0 ? -1 : 1;
  ```
  **乱数の呼び出し順を変えないこと**（既存ステージのゴール位置が変わる）
- Presenter 6. は DTO の値を使うだけに変更
- `TunnelGoalGenerationTests` が通ることを確認

### Step 2. 設定値の追加
- `TunnelGenerationConfig` に init プロパティを追加（コンストラクタ引数は増やさない）
  - `SpiderWebCount`(3), `FireAlarmCount`(3), `FireAlarmMinSpacing`(40f), `FireAlarmEndMargin`(12f), `FireAlarmMouthMargin`(4f)
- `TunnelGenerationRequestDto` に同名プロパティを追加し、`ToDomainConfig()` でオブジェクト初期化子で渡す
- `TunnelGenerator`（Inspector）に `[Header("Gimmicks")]` を追加
  - 上記数値 + `GameObject spiderWebTemplate` + `GameObject fireAlarmPrefab` + `float fireAlarmHeight = 1.4f`
  - 公開プロパティを追加
- `TunnelGenerationRequestFactory.Create` で数値を詰める
- ⚠ **`GenerateTunnelPresenter.ExecuteGeneration` の `adjustedRequest` は全項目を手で詰め直している**。新項目のコピーを忘れないこと

### Step 3. Domain: 配置計画
新規ファイル
- `Domain/ValueObjects/Tunnel/TunnelGimmickPlacement.cs`
  ```csharp
  public enum TunnelGimmickKind { SpiderWeb, FireAlarm }
  public sealed class TunnelGimmickPlacement
  {
      public TunnelGimmickKind Kind { get; }
      public int HostId { get; }        // SpiderWeb: NormalCorridor.ConnectionIndex / FireAlarm: Tunnel.Id
      public float LocalZ { get; }      // ホストのローカルZ（中心原点）
      public int Side { get; }          // FireAlarm のみ: -1 / +1（ローカルX方向）
      public int VisualSeed { get; }
  }
  ```
- `Domain/DomainServices/Tunnel/ITunnelGimmickPlanner.cs` / `TunnelGimmickPlanner.cs`
  ```csharp
  IReadOnlyList<TunnelGimmickPlacement> Plan(TunnelLayoutResult layout, TunnelGenerationConfig config,
                                            int? goalTunnelId, int goalEndSign);
  ```
  処理の流れ
  1. `System.Random(unchecked(layout.Seed ^ 0x47494d4b))`
  2. 蜘蛛の巣: 条件を満たす通路を列挙 → シャッフル（Fisher–Yates）→ 先頭 `SpiderWebCount` 本。
     `LocalZ` は `[-L/2 + 1.5, L/2 - 1.5]` の一様乱数（L < 3 ならスキップ）
  3. 火災報知器: 候補トンネルをシャッフル → 各トンネルで最大 N 回（例 8 回）`(Side, LocalZ)` を試行し、
     セクション1の制約と、既に採用した報知器とのワールド距離をチェック。
     ワールド座標は `Position + Forward(XZ) * LocalZ + Right * Side * (TunnelWidth/2)` の近似でよい
  4. 個数に達したら終了
- 通路の向き: `NormalCorridorLayout.Direction` の方向に Z 軸が向いている前提（View 側の `CreateNormalCorridor` で確認すること）

### Step 4. Application: DTO 変換
- `Application/DTOs/Tunnel/TunnelMapDto.cs` に `TunnelGimmickDto`（Kind は int か Application 側 enum、HostId, LocalZ, Side, VisualSeed）を追加し、
  `TunnelMapDto.Gimmicks`（既定は空配列）を追加
- `GenerateTunnelUseCase` のコンストラクタに `ITunnelGimmickPlanner` を追加（null なら `new TunnelGimmickPlanner()`、既存の書き方に合わせる）
- `Generate()` 内でレイアウト生成後に `Plan()` を呼び、`ConvertToDto` で詰める
- `GenerateTunnelLifetimeScope.Configure` に `builder.Register<TunnelGimmickPlanner>(Lifetime.Scoped).As<ITunnelGimmickPlanner>();`

### Step 5. View: 生成処理
新規 `View/GenerateTunnel/TunnelMapView.Gimmicks.cs`（既存の partial 分割に合わせる）

- `public SpiderWeb CreateSpiderWeb(Transform corridor, float localZ, float width, float height, int seed)`
  1. `spiderWebTemplate` があれば `Instantiate(template, corridor, false)`、無ければ `new GameObject("SpiderWeb").AddComponent<SpiderWeb>()`
     **生成直後に `SetActive(false)` で止めてから設定する**（`[ExecuteAlways]` / `OnEnable` での生成を1回にするため）
  2. ローカル位置 `(0, floorY + height/2, localZ)`、回転 `Quaternion.identity`（XY面が断面、Zが通路方向）
  3. `SpiderWeb` に `public void ConfigureShape(float width, float height, int seed)` を**新規追加**して設定
     （既存フィールドを書き換えるだけ。Regenerate はアクティブ化時に任せる。必要なら呼ぶ）
  4. `SetActive(true)`
  5. 床の高さは通路内で下向き Raycast するか、既存の `SampleTunnelFloor` 相当を使う
- `public FireAlarm CreateFireAlarm(Transform tunnel, float localZ, int side, float height)`
  1. `Physics.SyncTransforms()`（同フレーム生成のコライダーに Raycast するため。ステップの最初に1回でよい）
  2. 起点 `tunnel.TransformPoint(0, floorY + height, localZ)`、方向 `tunnel.right * side` に Raycast（距離 `TunnelWidth`）
     - 自分のトリガーや他ギミックに当たらないよう `QueryTriggerInteraction.Ignore`
     - 外れたら生成せず `false` 相当を返す（警告ログ1行のみ、例外は投げない）
  3. `Instantiate(fireAlarmPrefab, tunnel)`、位置 `hit.point + hit.normal * 0.02f`、
     回転は「Prefab の正面が `hit.normal` を向く」ように設定
     ⚠ **FireAlarm.prefab の正面軸（+Z か -Z か）を Prefab を開いて確認すること**
- 生成物はすべてホスト（トンネル/通路ルート＝ `_geometryRoot` 配下）の子にする → 再生成時に自動破棄

### Step 6. Presenter: 配置ステップ追加
`GenerateTunnelPresenter.BuildSceneHierarchy`
- 2. 通常通路の生成で `CreateNormalCorridor` の戻り値を `Dictionary<int, Transform> corridorRoots`（キー: `ConnectionIndex`）に保存
- 6. ゴールの後に「7. ギミック配置」を追加し、`mapDto.Gimmicks` を走査して `tunnelRoots` / `corridorRoots` から親を引き、View のメソッドを呼ぶ
- 既存の 7. カメラフレーミングは 8. に繰り下げ

NavMesh への影響
- 蜘蛛の巣のコライダーはトリガーなので NavMesh には影響しない想定。火災報知器は壁に埋まる小物なので問題ない想定
- `BuildNavMesh()` より前に配置される（`BuildSceneHierarchy` 内）ので、もし NavMesh に穴が出るなら報知器のレイヤーを NavMesh 対象外にする

アイテム生成との関係
- アイテムは `OnMapReady` 後に物理チェック付きで生成されるため、ギミックを先に置けば報知器との重なりは自動で避けられる

### Step 7. Editor 上の作業（人間が行う）
- `StageTemp.unity` 等にある調整済みの蜘蛛の巣を **Prefab 化**（`Assets/Shinzui/Prefabs/SpiderWeb.prefab`）
  - `manualAnchors` / `attachmentSurface` は空にしておく（生成先で参照が切れるため）
- 各ステージシーンの `TunnelGenerator` に `spiderWebTemplate` / `fireAlarmPrefab` と個数を設定

---

## 4. テスト（最小限）

`Assets/Shinzui/Tests/TunnelGeneration/TunnelGimmickPlannerTests.cs`（EditMode、既存 asmdef に追加）

1. 同じシードで2回計画すると配置が完全一致する
2. 火災報知器が `Id == 0` / 特殊 / 傾斜トンネルに置かれない、かつ1トンネル1個以下
3. 火災報知器同士の距離がすべて `FireAlarmMinSpacing` 以上
4. 蜘蛛の巣が `OpenLeft/OpenRight` 通路に置かれず、1通路1枚以下
5. 個数を 0 にすると何も置かれない

加えて既存の `TunnelGoalGenerationTests` / `StageTunnelGenerationTests` が通ること（Step 1 の回帰確認）

PlayMode で `GenerateMapTest.unity`（`Scenes/DevScenes/`）を数シード回し、目視で
- 巣が通路断面に収まっている / 報知器が壁に貼り付いている / スタート直後に警報が鳴らない
ことを確認する

---

## 5. 変更ファイル一覧

| 種別 | ファイル |
|---|---|
| 新規 | `Src/Domain/ValueObjects/Tunnel/TunnelGimmickPlacement.cs` |
| 新規 | `Src/Domain/DomainServices/Tunnel/ITunnelGimmickPlanner.cs` |
| 新規 | `Src/Domain/DomainServices/Tunnel/TunnelGimmickPlanner.cs` |
| 新規 | `Src/View/GenerateTunnel/TunnelMapView.Gimmicks.cs` |
| 新規 | `Tests/TunnelGeneration/TunnelGimmickPlannerTests.cs` |
| 変更 | `Src/Domain/ValueObjects/Tunnel/TunnelGenerationConfig.cs`（プロパティ追加） |
| 変更 | `Src/Application/DTOs/Tunnel/TunnelGenerationRequestDto.cs` |
| 変更 | `Src/Application/DTOs/Tunnel/TunnelMapDto.cs`（Gimmicks, GoalTunnelId, GoalEndSign） |
| 変更 | `Src/Application/UseCases/Tunnel/GenerateTunnelUseCase.cs` |
| 変更 | `Src/Presentation/GenerateTunnel/GenerateTunnelPresenter.cs`（adjustedRequest, 6., 7.） |
| 変更 | `Src/View/GenerateTunnel/TunnelGenerator.cs`（Inspector項目） |
| 変更 | `Src/DI/GenerateTunnel/TunnelGenerationRequestFactory.cs` |
| 変更 | `Src/DI/GenerateTunnel/GenerateTunnelLifetimeScope.cs`（Planner登録） |
| 変更 | `Src/View/SpiderWeb/SpiderWeb.cs`（`ConfigureShape` 追加のみ） |

---

## 6. スコープ外 / 将来検討
- 火災報知器の警報で敵を呼び寄せる連携（現状 `FireAlarm` は敵システムと未接続）
- `FireAlarm.Awake` の外観選択を `UnityEngine.Random` からシード由来にする（完全再現性が必要なら）
- トンネル本体の壁・天井の角への装飾的な蜘蛛の巣
- 難易度やフロア階層に応じた個数の変化
