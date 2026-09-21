# タイトル画面の構成

`Assets/Shinzui/Scenes/Title.unity` の `TitleLifetimeScope` が、タイトル・オプション・クレジットをまとめて構築します。VContainerSettings の RootLifetimeScope を親とし、画面に関係するサービスと Presenter は `Lifetime.Scoped` で登録しています。

| 配置 | 役割 |
| --- | --- |
| `Src/DI/TitleLifetimeScope.cs` | シーン内の View を明示的に登録し、設定アセットを Application の DTO に変換する |
| `Src/Presentation/Title/TitlePresenter.cs` | ボタンイベントの購読、表示切替、フェード、非同期読込のエラー処理。VContainer が初期化・更新・破棄する |
| `Src/Application/Title/` | シーン遷移のインターフェース、表示設定の DTO、クレジットの再生状態 |
| `Src/Infrastructure/Title/` | Addressables / SceneManager / 終了処理と、クレジットの編集用 ScriptableObject |
| `Src/View/Title/` | UI参照、入力イベント、描画・アニメーション。Presenter・UseCase・Domain へ依存しない |

既存 View のクラス名・名前空間・`.meta` GUID・ボタン用メソッド名はシリアライズ互換性のため保持しています。`ButtonController` は旧名称ですが、実体は入力イベントを公開する View です。`CreditData` も旧名前空間を保持し、Infrastructure アセンブリへ移しています。

## オプションとの接続

設定サービスの登録は `SettingsRegistration` に共通化しています。タイトルでは `TitleLifetimeScope` がこれを呼び出すため、同じシーンに `SettingsLifetimeScope` を重ねて配置しません。独立したオプション画面では従来の `SettingsLifetimeScope` を利用できます。

`UnitySettingsApplier` の任意の音声サービスは、登録済みなら注入し、未登録なら既存のFMOD直接適用を使います。VContainerは省略可能なコンストラクター引数も解決するため、DI側のファクトリーでこの選択を明示しています。

タイトルの `OptionWindowView.GenerateCategoryTabs` は無効です。既存のタブと UnityEvent を使用し、7カテゴリ・8設定コントロールを Inspector 上で接続します。カメラ速度・マウス感度の Slider 範囲は既存の設定モデルに合わせて 1～100 です。開くたびに編集を開始し、閉じると保存してタイトルへ戻ります。

保存には既存パッケージの Newtonsoft.Json を使用します。Domain の設定値はプロパティであるため、Unity の JsonUtility では保存されません。旧形式の空データや欠けた設定項目は初期値で読み込みます。

## 寿命と失敗時の動作

- 開始ボタンの連打は1回のシーン読込にまとめます。読込失敗時は黒い画面を解除し、再操作できます。
- フェード・クレジット更新は Presenter が管理し、Scope の破棄後は進みません。ボタン購読と設定の購読・Debounce も破棄します。
- クレジットは開始待機中・終了待機中も Escape でスキップできます。再表示時は再生状態を初期化します。
- Scope の必須参照が欠けている場合は、未設定のフィールド名を含むエラーで構築を中止します。

## 検証

Unity Test Runner の EditMode で `Shinzui.Title.Tests` を実行します。実際の `TitleLifetimeScope.Configure` を使い、保存とシーン遷移のアダプターだけをテスト用に差し替えて、非表示 View の登録・二重読込・失敗後の再試行・破棄・オプション再編集・クレジットの再表示を検証します。

実シーンでは、タイトル起動、全オプションタブの切替、戻る操作、クレジット再表示を確認します。
