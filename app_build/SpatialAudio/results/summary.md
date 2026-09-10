# Organic Reverb 段階2：3D音源管理の検証結果

2026-09-10。3D音源管理の実装と、Unity CLIによるEditor／Windows IL2CPP Playerでの検証が完了しました。

| 検証 | Editor Play Mode | Windows IL2CPP Player |
|---|---|---|
| 実行チェック（30回の再生・停止を含む） | 72項目合格 | 72項目合格 |
| 録音・解放状態の解析 | 7項目合格 | 7項目合格 |
| FMODイベントインスタンスの作成／解放 | 41／41 | 41／41 |
| シーン終了後のFMODイベント残存数 | 0 | 0 |
| 録音のクリッピング | なし | なし |

同じイベントの複数地点での同時再生、位置・速度の追従、一時停止と再開、優先度による音源の入れ替え、古いハンドルの無効化、プールの再利用、追従対象とシーンの破棄を確認しました。FMODが残響を処理している間は所有権を保ち、実際に停止した後にインスタンスを解放します。

ApplicationにはUnity／FMOD／Steam Audioへの参照を追加せず、独立した純粋C#アセンブリに要求DTOとサービス契約を配置しました。具体的な再生処理とTransform追従はInfrastructure、VContainerの登録と更新はDIが担当します。正本とUnity側のソース一致、GUID、アセンブリ依存関係の検査にも合格しています。

**試聴：同じ短い音を2地点で同時再生した残響（約6.7秒）**

![2音源の残響](D:/Pandd/ShinShinzui/app_build/SpatialAudio/results/two-voices-tail.wav)

**試聴：移動・一時停止・再開するループ音（約3.6秒）**

![移動するループ音](D:/Pandd/ShinShinzui/app_build/SpatialAudio/results/moving-loop.wav)

いずれもWindows Playerから取得した48 kHz／16-bitステレオの録音で、音量補正はしていません。

現在の実装は各音源の再生前に最低250msの準備時間を設けています。足音などの本編接続では、音響計算の事前準備と許容遅延の調整が必要です。今回の最大音源設定は8、試験時のサービス上限は3です。多音源時の性能保証や総メモリの長時間計測は、段階5で扱います。

本編イベントの変換、自動生成トンネルの音響形状、動的な扉、ワープ境界の伝播には未接続です。次の工程は、生成トンネルの簡略化した音響形状を登録・解除する仕組みと、この音源管理APIの接続です。

- [構成・API・再実行手順](D:/Pandd/ShinShinzui/app_build/SpatialAudio/README.md)
- [Editor実行結果](D:/Pandd/ShinShinzui/app_build/SpatialAudio/results/editor-report.json) / [録音解析](D:/Pandd/ShinShinzui/app_build/SpatialAudio/results/editor-analysis.json)
- [Player実行結果](D:/Pandd/ShinShinzui/app_build/SpatialAudio/results/player-report.json) / [録音解析](D:/Pandd/ShinShinzui/app_build/SpatialAudio/results/player-analysis.json)
- [Windows実行ファイル](D:/Pandd/ShinShinzui/Builds/SpatialAudioProbe/SpatialAudioProbe.exe)（隣接するDataフォルダとDLLを含むビルドフォルダ一式で使用）
- [検証したソースのSHA-256](D:/Pandd/ShinShinzui/app_build/SpatialAudio/results/source-sha256.json)

Unityログと生録音は `D:/Pandd/ShinShinzui/Logs/SpatialAudio` に保存しています。検証Playerのビルド成功は、本編Addressablesを含む製品全体のビルド成功を意味しません。ビルド時に自動変更された描画設定と既存FMODキャッシュは元に戻し、音響の設定と専用Bankを保持しています。
