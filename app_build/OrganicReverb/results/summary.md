# Organic Reverb 段階1の検証結果

2026-09-10。FMOD + Steam Audioによる環境適応型音響が、このプロジェクトのEditorとWindows IL2CPP Playerで動作することを確認しました。Unity EditorのGUIを開かず、すべてUnity CLIで実施しています。

| 検証 | 結果 |
|---|---|
| テストシーンと音響形状の生成 | 成功 |
| Editor Play Mode | 8条件成功、録音解析11項目合格 |
| Windows x64 IL2CPP Developmentビルド | 成功 |
| Windows Player実行 | 8条件成功、録音解析11項目合格 |
| 配置ソースの一致・新規検証アセンブリの依存・Unityメタデータ | 合格 |

同一の短いノイズパルスを使い、小部屋・廊下・屋外の残響OFF/ONと、壁で遮られた位置の残響OFF/ONを録音しました。室内で残響の尾が残ること、屋外との違い、壁による直接音の遮断、遮断後も反射音が届くことを音声データから確認しています。全録音でクリッピングはありませんでした。

**試聴:** 残響なし → 小部屋 → 廊下 → 屋外。各約5秒、音量の個別補正なし。

![Windows Playerの残響比較](D:/Pandd/ShinShinzui/app_build/OrganicReverb/results/comparison.wav)

Player録音の残響部分のRMSは、小部屋 `0.00056549`、廊下 `0.00007219`、屋外 `0.00004460` でした。今回の形状では小部屋が屋外より約22 dB大きく、場所による残響の差を確認できました。これは今回の試験条件の測定値です。

Playerビルドに必要だった既存コードの修正は3件です。InventoryEntityの未使用Editor参照を削除し、TunnelGateViewのライト情報取得をEditor/Playerで分け、ButtonControllerの終了処理を `UnityEngine.Application.Quit()` と明示しました。ビルドが自動変更した無関係な描画設定は元に戻しています。

今回の確認範囲は、1音源・固定形状・1材質による技術成立性です。本編SE、自動生成トンネル、動的な扉、多音源時の負荷、ワープ境界への接続は未実装です。Bodycamと同一の内部実装や聴感品質を保証する結果ではありません。FPSや総CPU負荷の性能予算も次工程で検証します。

再実行手順と構成は [README](D:/Pandd/ShinShinzui/app_build/OrganicReverb/README.md)、判定の詳細は [Editor解析](D:/Pandd/ShinShinzui/app_build/OrganicReverb/results/editor-analysis.json) と [Player解析](D:/Pandd/ShinShinzui/app_build/OrganicReverb/results/player-analysis.json) に保存しています。

[Windows実行ファイル](D:/Pandd/ShinShinzui/Builds/OrganicReverbProbe/OrganicReverbProbe.exe) を直接起動すると8条件を順番に再生します。実行ファイルは隣接するDataフォルダとDLLを含むビルドフォルダ一式で使用してください。ログと各条件の録音は `D:/Pandd/ShinShinzui/Logs/OrganicReverb` にあります。

次の工程は、今回成立したFMOD/Steam Audio経路を使う3D音源管理APIの実装です。残響のための再生寿命管理、同時発音、移動追従、破棄を先に整え、その後で生成トンネルへ接続します。
