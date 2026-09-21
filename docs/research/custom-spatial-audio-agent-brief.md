# 独自空間音響：次エージェントへの調査引き継ぎ

2026-09-21作成。調査時HEADは `a20fc1e`。今回は調査資料だけを追加し、実装は行っていない。

実装後の追記：上記は調査時点の記述。矩形室・FDNの[段階A](custom-spatial-audio-phase-a-implementation-2026-09-21.md)に続き、Room/Portal伝播、実測HRIR、減衰・CPU改善を追加した。[最新の実装・検証と残課題](custom-spatial-audio-improvements-2026-09-21.md)を先に確認し、既存の正式ソースへ継続して作業すること。

本編統合後の追記：生成ステージの足音をカスタムDSPへ接続した。現在の接続経路・設定・寿命管理・近似の限界は[本編統合メモ](custom-spatial-audio-game-integration-2026-09-21.md)を参照すること。以下の「先に確認する箇所」は当初調査時点の情報であり、現在の生成エントリポイントは `STEAMAUDIO_ENABLED` に関係なくカスタム音響を起動する。

最初に[調査本編](D:/Pandd/ShinShinzui/docs/research/bodycam-spatial-audio-research-2026-09-21.md)と、開始時点の会話・[agents.md](D:/Pandd/ShinShinzui/agents.md)を読む。この資料は方式選定の材料であり、最終仕様への承認を表さない。

## ユーザーの意図

既存のOrganic ReverbはSteam Audioへの依存が大きい。Bodycamを参考に、環境の形状・材質・開口に応じて変化する立体音響を自作したい。今回の担当は調査、実装は次の担当が行う。

## 事実と未確認事項

- Bodycam公式はOrganic Reverbの実時間環境解析、開口や廊下を通る音、遮蔽、上下定位改善、独自技術の開発を説明している。[Devlog #2](https://steamcommunity.com/games/2406770/announcements/detail/675126551624287627)
- ただし内部のレイ／波動方式、FDN、HRTF、ミドルウェア採否は確認できていない。紹介した論文をBodycam採用論文と呼ばない。
- 公式動画の英語自動字幕で、発砲周囲の部屋寸法・天井高・開口・閉鎖度の分析を確認した。本編に時刻リンクがある。音素材・ミックス・ボディカメラのマイクを意識した音作りも関係する。音色の演出と伝播計算を分ける。

## 調査からの推奨

1. FMODを再生基盤として使い、伝播・反射・残響・両耳化を独立させる案を検証する。FMOD維持は提案であり、ユーザーが全音声基盤の撤去を指定した場合は再検討する。
2. 矩形室の一次反射（本編P1）＋帯域別FDN（P2）を比較基準とし、形状に直結するSDN（P3）を代案として比較する。
3. HRTF/HRIRの両耳処理（P9）を別に実装・評価する。左右パンや残響量の変更だけで前後・上下を完成扱いにしない。
4. L字通路と複数室はRoom/Portalグラフ（P4）、必要ならCoupled SDN（P5）へ進む。最短経路を物理回折と呼ばない。
5. 専用のイベント・Bank・試験シーンで音声経路を成立させ、既存方式と同一素材・同一音量で比較してから本編へ移す。

## このリポジトリで先に確認する箇所

- [ISpatialAudioService](D:/Pandd/ShinShinzui/Assets/Shinzui/Src/Application/SpatialAudio/ISpatialAudioService.cs) と [IAcousticSceneService](D:/Pandd/ShinShinzui/Assets/Shinzui/Src/Application/SpatialAudio/IAcousticSceneService.cs) は再利用候補。
- [TunnelMapDto](D:/Pandd/ShinShinzui/Assets/Shinzui/Src/Application/DTOs/Tunnel/TunnelMapDto.cs) は接続・開口・寸法を持つ。ただし完成した音響グラフではない。
- [FmodSpatialAudioService](D:/Pandd/ShinShinzui/Assets/Shinzui/Src/Infrastructure/SpatialAudio/FmodSpatialAudioService.cs) と [TunnelAudioRuntime](D:/Pandd/ShinShinzui/Assets/Shinzui/Src/Infrastructure/TunnelAcoustics/TunnelAudioRuntime.cs) はSteam Audioに直接依存。
- [TunnelGenerationEntryPoint](D:/Pandd/ShinShinzui/Assets/Shinzui/Src/DI/GenerateTunnel/TunnelGenerationEntryPoint.cs) と音響asmdefは `STEAMAUDIO_ENABLED` に依存。defineの削除だけでは起動経路まで消える。
- FMODイベントのSteam Audio Spatializer、Bank、DLL設定も移行対象。C#の差し替えだけでは完了しない。
- 古いREADMEの `GenerateTunnelBootstrapper` は現在のHEADに存在しない。過去の同期スクリプトを無条件で実行しない。コードの正式配置は開始時点のユーザー指示とリポジトリ規則に従う。

## 設計・実装で維持する条件

Domainは外部依存なし、Applicationは契約とDTO、Unity/FMODとの接続はInfrastructure、組み立てはDI。Presentation→Domain/Infrastructure、View→Application/Presentationを追加しない。既存.metaとasmdef GUID、シリアライズ互換性を保持する。

音声コールバックでUnity API・形状探索・I/O・割り当て・待機ロックを実行しない。短い発音と長い残響の寿命を分ける。形状・扉・HRIR切替は連続性を検証する。UI/BGM、SE/Master音量、ポーズ、再生成、ワープ、Unloadを回帰対象とする。

試験では初期反射時刻、帯域別減衰、L字通路の到来方向、扉開閉、視点回転、前後・上下誤定位、多音源時のCPU/DSP負荷を記録する。EditorだけでなくWindows IL2CPPの音声出力まで確認する。過去の性能値を今回の測定値として再掲しない。

Planeverbには公開READMEに特許の記載があり、初期実装の既定案にはしない。HRIRはMIT KEMARが引用条件を確認できた候補。論文・データ・実装コードの利用条件を混同しない。

## 最初の成果物として適切なもの

小部屋・直線トンネル・L字通路の比較試作、使用論文と近似の説明、録音と測定結果、変更ファイル、残課題。まず実装担当が最小構成と完了条件を具体化し、必要な仕様承認はその時点のユーザー指示と役割規則に従う。
