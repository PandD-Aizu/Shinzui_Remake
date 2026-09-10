# 生成トンネル音響（Organic Reverb 段階3）

生成した壁・床・天井・通路の開口部をSteam Audioへ登録し、プレイヤーの足音をFMODで空間化します。生成完了、再生成、PlayerViewのワープ、シーン破棄に接続しています。足音は接続確認用の合成した仮素材です。

## 本編への接続

`GenerateTunnelBootstrapper` と `GenerateTunnelLifetimeScope` が `TunnelAudioBinding.Attach` を呼びます。二重に呼ばれても同じBindingを返します。Bindingは同じシーンのPlayerViewを探し、MainCameraをリスナー、プレイヤーの足元を音源の準備位置として使用します。

設定は [TunnelAudioConfiguration.asset](D:/Pandd/ShinShinzui/Assets/Shinzui/Audio/Resources/SpatialAudio/TunnelAudioConfiguration.asset) です。実行時にResourcesから読み込みます。Catalogの `footstep.prototype` はFMODの `event:/FootstepPrototypeSpatial` に対応し、`GeneratedTunnelAudio.bank` に格納しています。最終素材への置換はCatalogまたはFMODイベントで行えます。

足音は接地中の平面移動1.6mごと、最短間隔0.3秒で鳴らします。静止中・空中の移動では鳴らしません。これは距離による仮の歩行リズムで、歩行アニメーションの接地イベントや材質別の最終サウンド設計は未接続です。

## 形状と寿命

- 本編の3つのFBXから12メッシュを事前に書き出し、重複頂点を統合します。1,579頂点から861頂点になり、三角形の開口部を保持します。Playerで読み出し不可の描画メッシュにも対応します。
- 生成後の有効なMeshCollider/BoxColliderとTransformから、静的な音響メッシュを1個構築します。再生成で配置・接続が変わるため、この集約形状をSDKへ登録する方式を採用しました。非表示の歩行補助床、ワープのバックストップ、Triggerは反射面に含めません。
- Viewの基本イベント `GeometryClearing` / `GeometryReady` をDIが購読します。Unityの遅延Destroyを1フレーム待ってから登録し、再生成前には旧音源と旧メッシュを解除します。
- 動く扉にはInfrastructureの `MovingAcousticGeometry` を扉ルートに追加し、MeshColliderを使う場合はLibrary欄へエクスポート済みの `AcousticMeshLibrary.asset` を割り当てます。独立したサブシーンのInstancedMeshとして平行移動・回転・スケール、無効化、再有効化、破棄を反映します。生成マップの静的集約からは除外されます。既存の全扉へ自動追加する処理はありません。
- `AcousticSurface` でConcrete/Metal/Woodの吸収・散乱・透過係数を選べます。未指定はConcreteです。係数は初期値であり、素材固有の測定値ではありません。段階4でFMODの周波数別透過を有効化しました。現在の設定と比較録音は [Propagation](D:/Pandd/ShinShinzui/app_build/Propagation/README.md) を参照してください。
- ライブシーンのCommitはSteamAudioManagerへ予約し、SDKの反射計算と同時に直接Commitしません。新規の未使用サブシーンだけを生成時にCommitします。現在のバックエンドはDefaultです。段階4では反射回数64、遮蔽サンプル上限32を使用します。

メッシュの頂点・三角形や素材を編集した場合は再エクスポートが必要です。動的オブジェクトのTransform更新は剛体変換用であり、スキニングや実行中のトポロジ変更を追従しません。現在の生成器が床のみ作る小部屋には、存在しない壁や天井を補っていません。

## 足音の事前準備

Applicationの `Prepare` は音源を一時停止状態で用意し、準備完了後に `Ready` を返します。2個の予約音源が足元を追従し、接地タイミングの `StartPrepared` で即座に再生を開始します。発音した音源は追従を解除し、接地点に残響を残します。通常の `Play` は従来どおり初期準備を待ちます。

PlayerViewのWarp通知と4mを超える位置ジャンプでは、足音と予約を停止し、移動先で準備し直します。リスナー速度はゼロを明示し、座標ジャンプによるドップラーを防ぎます。準備できていない接地は遅延キューへ入れず、MissedCountに数えます。ワープ直後には最大で準備時間分の足音欠落があり、ポータル越しに旧地点の音を継続して伝える処理は段階4以降です。

サービス上限は予約を含め12音源、Steam Audioの上限は16です。残響が長い場合や同時発音が多い場合は足音が欠落する可能性があります。複数のサービスを併用する場合は全体の予算を調整してください。段階5で標準12音源・軽量8音源の品質プリセット、SE音量に接続するWorldSEバス、Time.timeScaleによるポーズ連携、生成マテリアルとNavMeshの解放を追加しました。負荷と回帰の結果は [AcousticPerformance](D:/Pandd/ShinShinzui/app_build/AcousticPerformance/README.md) を参照してください。

## 配置と依存関係

正本は `app_build/TunnelAcoustics`、共通音声APIは `app_build/SpatialAudio` です。既存ファイルへの接続変更も `app_build/View` / `app_build/DI` に保存しています。`tools/sync_sources.py` がAssetsへ反映し、既存の.meta GUIDを保持します。

Applicationはエンジン非依存です。InfrastructureはApplicationと同じInfrastructureの音源実装を利用します。DIがViewとInfrastructureを接続し、Viewに他レイヤーの参照は追加していません。既存Presentation/Domainの変更はありません。

## Unity CLIで検証

プロジェクトルートで、Unity Editorを終了した状態で実行します。FMOD Studio 2.03.12、Unity 6000.5.8f1を使用します。

```powershell
python ./app_build/TunnelAcoustics/tools/sync_sources.py
& 'C:/Program Files/FMOD SoundSystem/FMOD Studio 2.03.12/fmodstudiocl.exe' -script ./app_build/TunnelAcoustics/tools/create_footstep_event.js ./Shinzui/Shinzui.fspro
& 'C:/Program Files/FMOD SoundSystem/FMOD Studio 2.03.12/fmodstudiocl.exe' -build -banks GeneratedTunnelAudio -platforms Desktop ./Shinzui/Shinzui.fspro
./app_build/TunnelAcoustics/tools/run_probe.ps1 -Action Create
./app_build/TunnelAcoustics/tools/run_probe.ps1 -Action RunEditor
python ./app_build/TunnelAcoustics/tools/analyze_results.py ./Logs/TunnelAcoustics/Editor
./app_build/TunnelAcoustics/tools/run_probe.ps1 -Action Build -TimeoutSeconds 1200
./app_build/TunnelAcoustics/tools/run_probe.ps1 -Action RunPlayer -TimeoutSeconds 180
python ./app_build/TunnelAcoustics/tools/analyze_results.py ./Logs/TunnelAcoustics/Player
python ./app_build/TunnelAcoustics/tools/audit_sources.py
python ./app_build/TunnelAcoustics/tools/export_results.py
```

段階1/2のCreateは当時の音源数設定へ戻すため、実行した場合は最後に段階3のCreateを実行してください。段階2の回帰試験はCreateせずRunEditorを実行できます。

検証は本編の生成器・View・Prefabを使います。テストではテンプレート参照を明示してAddressablesのダウンロードを不要にしています。テスト用Playerは本編全体のビルドではありません。全ゲームシーン・BGM/UI・設定・ポータル伝播・性能の回帰試験は残る工程で行います。

[検証結果](D:/Pandd/ShinShinzui/app_build/TunnelAcoustics/results/summary.md) / [全体計画](D:/Pandd/ShinShinzui/app_build/docs/organic-reverb-plan.md)
