# ステージのトンネル生成

新しい本番シーンには `Assets/Shinzui/Prefabs/StageTunnelGenerator.prefab` を1つ配置する。
シーン名は任意（`Stage`、`StageTemp` など）。シーン名をコードへ登録する必要はない。
プレイヤーやアイテムの LifetimeScope は既存のものを併用する。

## Inspector

- `TunnelGenerator`: 個数、Seed、寸法、モデル、モデルの Scale / Rotation。
- `GenerateTunnelLifetimeScope`: 同じシーンの Generator / Map View を参照する。
- `TunnelMapView.Map Root`: 生成先。Prefab では子の MapRoot を設定済み。別シーンの参照は禁止。
- `TunnelMapView.Overview Camera`: 俯瞰表示が必要な検証シーンのみ設定する。本番では空欄にしてプレイヤーカメラを維持する。
- `Show Debug Geometry`: 出入口マーカーとワープトリガーの可視化。通常は無効。

モデルは Inspector にアセットを直接設定する。設定済み Prefab ではトンネル／通路の両方を参照しており、Editor 専用 AssetDatabase 検索やシーン内の同名オブジェクトに依存しない。
既存のモデルスケール仕様は維持している。`Use Model Bounds For Layout` は未拡縮モデルの寸法を使い、`Model Scale` は表示モデルを拡縮する。接続点の間隔もモデルに合わせて設定する。

## 起動と破棄

`GenerateTunnelLifetimeScope` が Domain のレイアウト生成、Application の UseCase、Presenter、View、Infrastructure の NavMesh Builder を登録する。
VContainer の `IStartable` から、シーンの Awake／Steam Audio の sceneLoaded 初期化後に一度だけ生成する。
順序はモデル配置 → NavMesh → GeometryReady（音響）→ アイテム配置。アイテムは同じシーンの Manager / Container と、MapRoot 配下の有効な WeightedSpawnSurface を使う。
シーンを閉じると生成オブジェクト、実行時 NavMesh、ワープ入力の購読を破棄し、再読み込み時は新しいコンテナで生成する。

旧 static Bootstrapper と GenerateTunnelTestBootstrap は廃止済み。
`StageTemp`、`LatestStageGenerateTemp`、`StageTemp_DoorMoveCopy`、`GenerateTunnelTest` は配置済みの LifetimeScope を使う。
旧固定トンネルとの二重表示を避けるため、移行先の旧ステージオブジェクトは無効化して保存している。
DoorMoveCopy の旧単一メッシュは接続部品を持たないため、本番用 TunnelBaseModel を参照し、Scale 設定は引き継いでいる。

## 検証

Unity Test Runner の PlayMode で `StageTunnelGenerationTests` を実行する。
任意名のシーンでの起動、一度限りの生成、カメラ維持、加算シーンの分離、NavMesh 解放、再読み込み、ワープ購読の破棄を検証する。
レイアウトの既存テストは EditMode の `TunnelLayoutGeneratorTests`。
