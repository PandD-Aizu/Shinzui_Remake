# Skill: Refactor temp code to structured

## Objective
`Assets/Shinzui/Src/Temp` フォルダ内にある一時的なスクリプトやコードを、クリーンアーキテクチャのレイヤー構造（Application, Domain, Infrastructure, Presentation, View）に従ってリファクタリングし、適切な各フォルダ（`Assets/Shinzui/Src/` 配下）へ配置・分類します。

## Rules of Engagement (ルールと制約)
- **フォルダ構成の遵守**:
  コードはクリーンアーキテクチャの定義に基づき、以下のレイヤーのいずれかに分類して移動してください。
  - **Domain**: エンティティ、値オブジェクト、ビジネスルール、リポジトリなどのインターフェース
  - **Application**: ユースケース、アプリケーションサービス、DTO、およびその関連ロジック
  - **Infrastructure**: DB、ネットワーク、外部サービス、Unity固有のデータアクセスなどの具象実装
  - **Presentation**: コントローラー、プレゼンター、ViewModel、外部からの入力を受け取る部分
  - **View**: UIコンポーネント、UnityのUI表示処理、描画ロジック
- **Unityのメタデータ（.meta）の考慮**:
  Unityプロジェクトのため、ファイルを移動する際はC#スクリプトだけでなく、対応する `.meta` ファイルの扱いにも注意し、スクリプトの参照が壊れないようにしてください。
- **後片付け**:
  リファクタリング完了後、`Temp` フォルダ内の移動済みコードは適切にクリーンアップしてください。

## Instructions (手順)
1. **一時コードの確認**:
   `Assets/Shinzui/Src/Temp` フォルダ配下に存在するソースコードを読み込み、それぞれのコードの役割や依存関係を分析します。
2. **アーキテクチャ設計と分類**:
   各コードをどのレイヤー（Domain, Application, Infrastructure, Presentation, View）に移行すべきか分類計画を立てます。必要に応じて、密結合なコードをインターフェースと実装に分離します。
3. **リファクタリングと配置**:
   - `Assets/Shinzui/Src/` の対応する各フォルダ（`Domain/`, `Application/`, `Infrastructure/`, `Presentation/`, `View/`）にコードを配置または新規作成します。
   - レイヤー間の依存関係ルール（内側のレイヤーは外側のレイヤーに依存しない）を厳格に守ります。
4. **名前空間と参照の修正**:
   移動後のフォルダ構成に合わせて `namespace` を適切に修正し、プロジェクト全体でコンパイルエラーが発生しないようにコンパイルと参照の整合性を確認します。
5. **クリーンアップ**:
   移行が完了し、正しく動作することが確認できたら、`Assets/Shinzui/Src/Temp` から不要になった移行元のコードファイルを削除します。
