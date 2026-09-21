# カスタム空間音響の本編統合（2026-09-21）

## 起動方法

`Assets/Shinzui/Scenes/LatestStageGenerateTemp.unity` をPlayすると、ステージ生成後にカスタム音響が自動で構築される。プレイヤーの移動・接地から足音を準備し、接地タイミングで鳴らす。旧 `TunnelAudioBinding` は生成エントリポイントから起動しないため、旧方式との二重発音を防ぐ。

設定は `Assets/Shinzui/Audio/Resources/SpatialAudio/CustomSpatialAudioConfiguration.asset`。既定は実測HRIR有効、8音声、位置応答更新20 Hz。ドライのモノラル音源を `StreamingAssets` 配下へ置き、設定のIDと相対パスを登録すると、Applicationの `ISpatialAudioService` / `SpatialAudioUseCase` から再生できる。今回のゲームプレイ接続はプレイヤーの足音。既存のBGM・UIイベントは従来のStudioイベントを維持している。

Editorメニュー `Shinzui > Audio > Custom Spatial Audio > Build integrated Windows game` から、本編シーンを起動するWindows IL2CPP検証ビルドを作成できる。出力は `Builds/CustomSpatialAudioGame/Shinzui.exe`。通常起動では本編を表示し、`-customAudioGameCheck` を付けた場合だけ自動検証後に終了する。正式なBuild Profileのシーン一覧は変更しない。

## 接続とライフサイクル

- Application: エンジン非依存の音響DTO・扉開口状態と、既存の再生／足音ユースケース。
- Infrastructure: `GeneratedAcousticWorld` が生成済み `TunnelMapDto` を音響セル・開口へ変換する。`CustomSpatialAudioService` がネイティブDSPと音声プールを管理し、`CustomTunnelAudioRuntime` がFMOD、リスナー、扉、ステージ寿命を結ぶ。
- DI: `CustomTunnelAudioBinding` と `TunnelGenerationEntryPoint` が既存Viewの入力・寿命イベントを購読する。ステージコンテナからも同じ `ISpatialAudioService` を解決できる。
- ViewからPresenterや音響サービスへの依存、PresenterからDomain／Infrastructureへの直接依存は追加していない。

音声は本編の `RuntimeManager.CoreSystem` で再生し、カスタムDSPを通してStudioの `bus:/WorldSE` に入る。別のFMOD Systemを本編用に作らない。WorldSEバスのロックは追加ロード中のステージ間で共有し、最後のステージが破棄された時点で解放する。

準備中の足音は無音で、発音後の音源位置は足から切り離す。ドライ音が終わった後も残響を維持し、休止中は残響の寿命も止める。音声上限時は低優先度の音声、または同優先度でドライ音が終了した古い残響を再利用する。新しい足音のためにDSPを再作成しない。ワープ、再生成、シーン破棄時に古い音声と残響を破棄する。

## 本編で見つかった不整合への修正

1. モデル寸法を使う設定では、トンネル本体だけでなく6入口の間隔も実メッシュから求める。従来の要求値（入口間隔約51 m）と本編モデル（長辺約41 m）の組み合わせでは通常通路が音響セルへ接続できなかった。描画・生成DTO・音響で同じ寸法を使うようにした。
2. セル境界の浮動小数点誤差をミリメートル単位で揃え、最大約2 mmの差を同一境界へまとめる。シード42などで起きた微小な部屋の重なりを防ぐ。
3. 設定適用のフォールバックを `vca:/Master`・`vca:/SE`・`vca:/BGM` に揃えた。`bus:/SE` だけを変更しても、その兄弟バスWorldSEの音量は変更されない。未定義の `FMOD_UNITY` 条件で実処理が除外される問題も解消した。
4. 既存Studio Bankが参照する `phonon_fmod` をFMOD設定のプラグイン一覧へ登録し、Bankより先にロードする。既存イベントを壊さずカスタム音響を共存させるための設定であり、本編でSteam Audioの自動初期化を禁止していない。

## 音響の範囲

実際に隣接するセル面だけを音響開口にする。ワープ先のペアIDは音響的な接続に使わない。物理的な扉は `TempDoor` の移動・回転進行度から開口率を受け取り、経路が扉を通る場合に減衰させる。

同一セル内は直接音・一次反射・帯域別残響・HRIR、別セル間は開口経由の最短経路による伝播を使う。これは軸に沿った矩形セルの近似であり、複数室の残響結合、任意形状の詳細な回折、閉じた物理扉を避ける別経路の再探索までは実装していない。扉の開口率は経路ゲインの近似で、扉材質別の透過や残響場の再計算はしない。

生成ルートは1 Unity unit = 1 m・等倍を前提とする。セルは75 m以下に分割し、既存グラフ上限（64セル・128開口）、DSP経路遅延上限（3秒）の範囲で動作する。未知の部屋や到達できない経路は無音にする。

## 検証記録

- 空間音響EditMode: 77件成功。複数シードの実モデル寸法、開口・扉、音声上限、準備音声、残響、休止、音量を含む。
- 本編PlayMode: 実シーンで21セル・20開口、初回生成1回、足音発音成功、DSPコールバックエラー0。SE／Masterミュートおよび休止の出力ピーク0、BGMミュート時は足音が継続。旧BGM／UIイベント起動、シード42での再生成、アンロード時の音声破棄を確認。
- 既存ステージ生成PlayMode: 4件成功。追加ロード・片側アンロード・再読み込み、NavMesh所有、明示的再生成、ワープ購読を確認。追加ロード時のFMOD二重ロックをこの回帰テストで検出し、共有所有管理へ修正した。
- Windows IL2CPP: 本編を含むDevelopmentビルド成功、自動検証Playerの終了コード0。21セル・20開口、DSP 8個、再生成後の構築回数2、コールバックエラー0、実行エラー0。WorldSE出力ピーク約0.05685、SE／Masterミュート・休止時は0、BGMミュート時は約0.05684。ワープとアンロード時の音声破棄も成功。

成果物: `Artifacts/CustomSpatialAudio/Integration/` 内の `final-editmode-api.json`、`final-playmode-api.json`、`generation-playmode-api.json`、`main-scene-report.json`、`main-player-report.json`。

Editorテストは合計82件成功。ログは `Logs/CustomSpatialAudioIntegration/`。ビルド検証で自動更新されたURPのプリフィルタ・描画設定は、変更前のスナップショットへ戻した。並行作業のタイトル画面・敵関連の変更は保持している。

Player起動ログには、検証ランチャーから本編へ移る間のリスナー未登録警告、NavMesh生成前のAgent警告、Studioのコマンドバッファ拡張／音源開始遅延警告が残る。定常時の出力・休止・破棄検証は成功しているが、起動フレームの負荷平準化は今後の調整項目。`Missing DSP plugin` は発生していない。

音量バランス・定位の自然さ・酔いや聞き疲れについての複数人の試聴評価は、数値検証とは別に必要。
