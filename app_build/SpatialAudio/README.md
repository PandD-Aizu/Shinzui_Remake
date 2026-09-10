# 3D音源管理（Organic Reverb 段階2）

再生ごとのハンドルを使って、FMODイベントの位置・移動・停止・残響を含む寿命を管理します。同じイベントを複数地点で同時に再生できます。段階1のFMOD + Steam Audio経路を使用します。

EditorとWindows IL2CPP Playerの双方で実行72項目と録音解析7項目に合格しました。[検証結果と試聴](D:/Pandd/ShinShinzui/app_build/SpatialAudio/results/summary.md)

| 配置先 | 内容 |
|---|---|
| Application/SpatialAudio | 外部SDKに依存しない座標DTO、要求、ハンドル、サービス契約、呼び出し元単位の寿命管理 |
| Infrastructure/SpatialAudio | FMODイベント対応表、インスタンス管理、Steam Audioオブジェクトの再利用、Unity Transformの追従 |
| DI/SpatialAudio | VContainer登録、毎フレームの更新、シーンスコープの破棄 |
| Tests/SpatialAudio | CLI用の固定シーン、ライフサイクル試験、録音 |

コードの正本は `D:/Pandd/ShinShinzui/app_build/SpatialAudio` です。`tools/sync_sources.py` がUnity側の `Assets/Shinzui/Src` と `Assets/Shinzui/Tests` へ同期し、既存の `.meta` とGUIDを保持します。Applicationの新規アセンブリは `noEngineReferences: true`、参照アセンブリなしです。段階2ではPresentation/Viewに変更・参照追加はありません。段階3のViewイベント接続はTunnelAcoustics側で管理します。

## 管理規則

- 1回の再生に1つのGUIDハンドルを発行します。オブジェクトを再利用しても以前のハンドルでは操作できません。
- `Play` の `Accepted` は要求の登録成功を表します。初期状態は `Preparing` で、準備が済むと `Playing` になります。
- 優先度は0〜255で、大きいほど優先します。上限到達時には、要求より低い優先度の音源を停止して置き換えます。同順位の候補は古いものを選びます。新しい要求と同じ優先度しかない場合は要求を拒否します。
- 準備中・一時停止中・フェードアウト中の音も上限に含みます。イベント終了後にFMODインスタンスをreleaseし、音響GameObjectをプールへ戻します。
- `UpdatePose` で明示座標へ切り替えると、追従プロバイダーとの接続を解除します。向きは正規化し、速度はFMODへ渡します。
- `TransformAudioPoseSource` はInfrastructureでのみUnity Transformを扱います。追従対象を破棄すると音も停止します。速度の算出は任意で、大きい位置ジャンプでは速度をゼロにします。
- `SpatialAudioUseCase.Dispose()` は、その呼び出し元が開始した音だけを停止します。サービス全体のDisposeはすべての音源とプールを破棄します。
- APIはUnityのメインスレッドで使用します。

段階5でInfrastructureの `SetSuspended` を追加しました。シーン全体のポーズを個別の `SetPaused` と別に保持し、復帰時に個別停止を解除しません。`Ready` は発音せず、停止中のフェードアウト期限もポーズ時間を除外します。計測に基づく上限・品質プリセット・音量バス構成は [AcousticPerformance](D:/Pandd/ShinShinzui/app_build/AcousticPerformance/README.md) を参照してください。

## 本編で接続するとき

音声用のシーンスコープに `SpatialAudioLifetimeScope` を配置し、`SpatialAudioCatalog` を割り当てます。Catalogにはゲーム側の文字列IDとFMOD EventReferenceを対応させます。接続先では `ISpatialAudioService` または `SpatialAudioUseCase` をDIから受け取ります。新しいApplication APIへFMOD型やTransformを渡す必要はありません。

```csharp
var result = spatialAudio.Play(new SpatialAudioRequest(
    "footstep.concrete", AudioPose.At(x, y, z), SpatialAudioCategory.Footstep, priority: 80));

if (result.Accepted)
{
    spatialAudio.UpdatePose(result.Handle, AudioPose.At(nextX, nextY, nextZ));
    spatialAudio.Stop(result.Handle, SpatialAudioStopMode.AllowFadeOut);
}
```

`footstep.concrete` は接続例のIDです。今回のCatalogに登録した実イベントは試験用の `pulse` と `loop` です。段階3では別Catalogの `footstep.prototype` を生成トンネルとプレイヤーのリスナーへ接続しました。

Catalogに登録するイベントにはSteam Audio Spatializerが必要です。短い音は、残響を処理できる無音区間または適切なイベント設計をFMOD側に用意してください。C#のハンドル保持だけでは、既に停止したDSPの残響は延長できません。試験用パルスは6秒のイベント内に短い音と無音区間を含み、持続音は1秒ループと500msのリリースエンベロープを使用します。

新しく割り当てた音源は、FMODサンプルとDSPの準備、および音響シミュレーションの初期更新を待ってからタイムラインを開始します。既定では最低250ms、かつシミュレーション更新間隔の2倍以上です。段階3で追加した `Prepare` / `StartPrepared` は、この初期準備を接地前に済ませます。実装と計測は [TunnelAcoustics](D:/Pandd/ShinShinzui/app_build/TunnelAcoustics/README.md) を参照してください。この待ち時間はシミュレーション計算完了の厳密な通知ではありません。

段階2ではSteam Audioの最大音源数を1から8へ設定し、試験サービスは上限3で検証しました。段階3の現在値は16です。この値は性能保証ではありません。シーン内で複数サービスや別のSteamAudioSourceを併用する場合、総音源数がSteam Audioの設定内に収まるようスコープと予算を構成してください。

## CLIで再実行

プロジェクトルートで、Unity Editorが起動していない状態で実行します。

```powershell
python ./app_build/SpatialAudio/tools/sync_sources.py
./app_build/SpatialAudio/tools/run_probe.ps1 -Action Create
./app_build/SpatialAudio/tools/run_probe.ps1 -Action RunEditor
./app_build/SpatialAudio/tools/run_probe.ps1 -Action Build -TimeoutSeconds 1200
./app_build/SpatialAudio/tools/run_probe.ps1 -Action RunPlayer -TimeoutSeconds 180
python ./app_build/SpatialAudio/tools/audit_sources.py
```

検証結果は `D:/Pandd/ShinShinzui/Logs/SpatialAudio`、実行ファイルは `D:/Pandd/ShinShinzui/Builds/SpatialAudioProbe/SpatialAudioProbe.exe` に保存します。検証シーンはAddressablesを使用しないため、ビルド中だけ本編Addressablesの同時生成を止め、終了時に設定を戻します。

FMODの試験イベントは `tools/create_loop_event.js` で作成します。既存本編イベントは変更しません。Bankは `SpatialAudioProbe`、Master.strings.bankはイベント登録のため更新されます。

## 参照

- [Steam Audio FMODガイド](https://valvesoftware.github.io/steam-audio/doc/fmod/guide.html)
- [FMOD StudioのマーカートラックAPI](https://www.fmod.com/docs/2.03/studio/scripting-api-reference-project-model-track.html)

前工程の構成・配布ライセンスは [Organic Reverb README](D:/Pandd/ShinShinzui/app_build/OrganicReverb/README.md) を参照してください。
