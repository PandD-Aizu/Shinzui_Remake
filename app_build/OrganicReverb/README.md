# Organic Reverb 技術検証

Bodycamの公開説明を参考にした、FMOD + Steam Audioの環境適応型音響の検証です。Bodycamと同一のアルゴリズムという意味ではありません。承認された段階1のみを対象とします。

## 構成

- Unity 6000.5.8f1 / Windows x64 IL2CPP
- FMOD Unity Integration 2.03.20 / FMOD Studio CLI 2.03.12
- Steam Audio 4.8.1（配布アーカイブのSHA-256は `versions.json`）
- リアルタイム反射: 8,192 rays / 16 bounces / 3秒 / Ambisonics order 1 / 1音源 / 更新0.1秒
- シーン: `Assets/Shinzui/Tests/OrganicReverb/OrganicReverbProbe.unity`
- イベント: `event:/OrganicReverbProbe`（専用Bank）
- 検証ソースの正本: `app_build/OrganicReverb/Infrastructure` と `Editor`
- Unity側の配置先: `Assets/Shinzui/Tests/OrganicReverb/Infrastructure` と `Editor`

新規検証アセンブリはFMOD/Steam AudioとUnity APIだけを扱います。既存Domain/Application/Presentation/Viewへの参照追加はありません。Editor側のシーン構築コードが検証用コンポーネントを結合します。製品側SEサービスやトンネル生成との接続は段階2以降です。

## 試験内容

同一の短いモノラルノイズパルスを、小部屋・両端の開いた長い廊下・屋外の3箇所で再生します。それぞれ残響OFF/ONを録音し、小部屋の仕切り壁に直接音を遮らせたOFF/ONを加えて8条件にします。残響が処理される間は、イベント内の無音区間でDSPを生存させます。

FMODのマスター音声経路にステレオのパススルーDSPを接続して48 kHz/16-bit WAVへ保存します。実行環境のサンプルレートが異なる場合は、その実測レートをWAVヘッダーに記録します。音声スレッドでは固定長バッファにコピーするだけで、ファイル保存は再生後に行います。

`analyze_captures.py` は、録音長、無音、クリッピング、残響の尾、室内/屋外の差、直接音の遮蔽、遮蔽時にも残る間接音を判定します。DSPが存在するだけでは合格にしません。音源接続の成功は録音された音と音響シミュレーション値の両方で確認します。

## CLIから再実行

プロジェクトルート `D:/Pandd/ShinShinzui` で実行します。Unityは起動していない状態にしてください。Unityライセンスとビルドキャッシュへアクセスできる通常ユーザー権限が必要です。

```powershell
# 正本のコードをUnity側へ反映（既存.metaは保持）
./app_build/OrganicReverb/tools/sync_probe.ps1

# テストシーンと音響用形状を生成
./app_build/OrganicReverb/tools/run_unity_probe.ps1 -Action Create

# GUIを開かずEditor Play Modeで実行・録音
./app_build/OrganicReverb/tools/run_unity_probe.ps1 -Action RunEditor
python ./app_build/OrganicReverb/tools/analyze_captures.py Logs/OrganicReverb/Editor

# Windows IL2CPPビルドとPlayer側の実行・録音
./app_build/OrganicReverb/tools/run_unity_probe.ps1 -Action Build
./app_build/OrganicReverb/tools/run_player_probe.ps1
python ./app_build/OrganicReverb/tools/analyze_captures.py Logs/OrganicReverb/Player
```

ビルド先は `Builds/OrganicReverbProbe/OrganicReverbProbe.exe`。直接起動した場合も8条件を順番に再生します。CLI試験は `-organicReverbOutput` で出力先を指定すると終了コードを返して自動終了します。直接起動時の録音先はUnityのpersistentDataPath内の `OrganicReverbProbe` です。

検証シーンはAddressablesを使わないため、Build処理の間だけ本編Addressablesの同時生成を止め、終了時に元の設定へ戻します。本編コンテンツのビルド成功を主張するものではありません。

FMOD側を再作成する場合は、`generate_probe_audio.py`、`create_fmod_probe.js` を使用します。Studio CLIでスクリプトを実行し、続いて `-build -banks OrganicReverbProbe -platforms Desktop` を実行してください。`Shinzui/Plugins` のDLL/JSが必要です。元のゲームイベントは変更せず、専用イベントとBankだけを作成/更新します。Master.strings.bankはイベント登録のため更新されます。

## 検証で分かった注意点

- `ApplyOccl=1` がシミュレーション値の使用、`2` は手動値です。手動値のままでは壁を検出しても直接音が減衰しません。
- Steam Audio 4.8.1の `getInt(SIMULATION_OUTPUTS_HANDLE)` は実装上常に `-1` を返します。これを接続成功判定には使用できません。
- FMODマスター経路は6chになる場合があるため、録音DSP側は2chを明示します。イベントのSteam Audio出力形式はFinal Outputを選択します。
- FMODイベントの残響はSpatializer内で直接ミックスします。この1音源検証では共有Mixer Returnを追加していません。
- Steam AudioのFMODブリッジは型名による動的生成を使うため、IL2CPP用の `link.xml` を付属します。
- Unityの `Temp` は起動・終了で消えるため、検証ログと録音は `Logs/OrganicReverb` に保存します。

## 既存コードのビルド互換性修正

Playerビルドを通すために以下を修正しています。対応するソースをapp_buildにも保存しています。

1. `InventoryEntity`: 未使用の `UnityEditor.Search` と `FMOD` のusingを削除。
2. `TunnelGateView`: Editorでは従来のライト設定を参照し、Playerでは `bakingOutput.lightmapBakeType` を参照。
3. `ButtonController`: Playerの終了処理を `UnityEngine.Application.Quit()` と明示。

## 判定範囲

この試験は1音源・固定形状・1材質による技術成立性の確認です。Bodycam相当の完成度、多音源時の負荷、材質ごとの調整、自動生成形状、動的な扉、ポータル越しの連続伝播、持続音や実際の足音への適用は未検証です。

FMOD DSP CPU値はケース終了時の瞬間値であり、別スレッドで実行される形状シミュレーションの総CPU負荷やフレーム時間の上限を表しません。対象PC/FPSの性能予算と人による最終試聴は本実装の前に決める必要があります。

## 参照

- [Steam Audio 4.8.1](https://github.com/ValveSoftware/steam-audio/releases/tag/v4.8.1)
- [FMOD連携ガイド](https://valvesoftware.github.io/steam-audio/doc/fmod/getting-started.html)
- [Steam Audio DSP実装](https://github.com/ValveSoftware/steam-audio/blob/v4.8.1/fmod/src/spatialize_effect.cpp)

配布ライセンスは `Assets/Plugins/SteamAudio/LICENSE.md` と `THIRDPARTY.md` に保持し、検証Playerにもコピーします。
