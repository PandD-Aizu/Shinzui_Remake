# 段階5: 音響の負荷予算と回帰検証

段階1～4の残響を、Windows向け生成トンネルに組み込む初期実装の検証。結果は `results/summary.md` に記録する。

## 設定

| 設定 | 管理音源上限 | Rays / Bounces | IR長 | 更新間隔 |
| --- | ---: | ---: | ---: | ---: |
| Standard（既定） | 12 | 8192 / 64 | 3秒 | 0.1秒 |
| Economy | 8 | 4096 / 32 | 1.5秒 | 0.15秒 |

両設定とも1次Ambisonics、反射ゲイン0.35、32点の体積遮蔽、単一リスナー。音響形状は同じエクスポート済み衝突メッシュを使い、入口を塞ぐような形状の粗略化は行わない。反射は管理対象のワールドSEだけに適用する。既存のBGMやボタンSEにSteam Audioの音源を追加しない。

上限には発音準備中の足音と残響が継続している音源も含む。現在の構成は同時に有効なトンネル音響Runtimeが1つであることを前提とする。同順位の要求が上限を超えた場合は拒否し、高順位の要求は低順位の音源を置き換える。

Economyでは残響の後半を短くする。Standardの入口・曲がり角の方向表現と残響は段階4の設定を維持する。品質の変更は起動前に行い、ネイティブのバッファを作り直すため次の起動から適用する。実行中の品質切替UIは追加していない。

## 実装した回帰修正

- `FootstepPrototypeSpatial` → 専用の `bus:/WorldSE` → Master。`WorldSE` は既存の `vca:/SE` に所属し、その上位Master VCAにも従う。既存SEバスのリバーブSendを通さず、Steam Audioの残響との二重加算を防ぐ。
- DIが `Time.timeScale <= 0` をシーンの一時停止として伝える。Infrastructureでイベントを停止状態にし、復帰時に個別音源のポーズ指定を保持する。準備中・発音待ちの足音を誤って再生しない。
- 生成Viewは自身で作ったマテリアルを再利用し、破棄時に解放する。生成したNavMeshDataは更新・破棄時に解放し、既存のアセットは解放しない。
- レイヤーへの新たな依存は追加していない。Viewは基本イベントを公開し、DIが音響Infrastructureへ接続する。

## 計測方法と解釈

実際の生成器・FBXテンプレートから4トンネル＋小部屋1つを作る。音響面は約3千三角形。カメラは実際の廊下内に配置。960×540、VSyncなし、120fps上限で、0/1/8/12/16/32音源を4秒安定させてから各8秒測定する。32音源は負荷の限界を調べる検証専用設定で、製品の上限ではない。

全条件を同じネイティブ32音源枠で比較し、その後の再生成検査でもプールを保持する。したがってメモリの絶対量はストレス検証プロセスの値で、通常の16音源枠による製品起動時の必要量ではない。

- フレーム時間は実時間の差からp50/p95/p99/最大を算出。60fps相当の16.67msを初期予算に用いる。
- FMOD `getCPUUsage().dsp` で音声DSPの平均と最大を記録する。出力バッファ不足の警告も音源数ごとに集計する。採用上限ではDSP最大80%未満を目安に余裕を確保する。
- プロセスCPUはCPU時間÷実時間。**100%が論理コア1つ**なので、全CPU比率にするには論理コア数で割る。Unityの描画・待機・音響シミュレーションなどを含み、DSPだけの数値とは異なる。
- Master出力をPCM16・48kHzステレオで記録する。10ms区間のRMSと録音時間から、無音区間と実時間に追いつかない出力を検出する。WASAPI側で欠落した区間がDSP内録音に無音として残るとは限らないため、録音時間とデバイス警告も確認する。
- 音量確認では実際の設定Applier/VCAサービスを使い、保存先だけをメモリに置き換える。ユーザーの保存済みオプションを変更しない。
- 30回の再生成では各回8音源を開始・解除し、GC後のUnity割当量・FMOD割当量・WindowsのPrivate Bytes、Material/NavMesh数を記録する。最後に実際にAdditiveシーンをUnloadする。実行時間が有限のため、長期のリークが存在しないことを証明する検査ではない。

Unityの `Process.PrivateMemorySize64` がこの環境で0を返し、IL2CPPでは `Process.TotalProcessorTime` が未対応だったため、[Windows GetProcessMemoryInfo](https://learn.microsoft.com/ja-jp/windows/win32/api/psapi/nf-psapi-getprocessmemoryinfo) のPrivateUsageとGetProcessTimesを使用する。Steam AudioのバッファやCPU・更新間隔の設定は[公式設定資料](https://valvesoftware.github.io/steam-audio/doc/unity/settings.html)に準拠。

この検証は音響用シーンの結果であり、製品シーン全体のGPU負荷・全敵AI・Addressables遷移を含むフレームレート保証ではない。実際に移行済みなのは生成トンネルの足音で、音素材は引き続き仮素材。敵・衝突音など既存SEを位置付きAPIへ移す作業と製品の対象PCでの最終試聴は別途必要。

## 再現手順（Unity GUI不要）

```powershell
python app_build/AcousticPerformance/tools/sync_sources.py
& 'C:/Program Files/FMOD SoundSystem/FMOD Studio 2.03.12/fmodstudiocl.exe' -script ./app_build/AcousticPerformance/tools/configure_mixer.js ./Shinzui/Shinzui.fspro
& 'C:/Program Files/FMOD SoundSystem/FMOD Studio 2.03.12/fmodstudiocl.exe' -build -banks Master,GeneratedTunnelAudio -platforms Desktop ./Shinzui/Shinzui.fspro
./app_build/AcousticPerformance/tools/run_probe.ps1 -Action Create
./app_build/AcousticPerformance/tools/run_probe.ps1 -Action RunEditor
./app_build/AcousticPerformance/tools/run_probe.ps1 -Action Build
./app_build/AcousticPerformance/tools/run_probe.ps1 -Action RunPlayer -Quality full
./app_build/AcousticPerformance/tools/run_probe.ps1 -Action RunPlayer -Quality economy
```

計測は同時実行せず、他の重い作業も止めて行う。検証用32音源設定はCLI引数がある検証実行だけで適用し、製品の設定アセットは書き換えない。NumPyのあるPythonで `tools/analyze_results.py Logs/AcousticPerformance/Player-full` などを実行する。16/32音源のストレス結果は採用条件の合否と分けて、失敗も保存する。

品質プリセットはUnityメニュー `Tools > Shinzui > Audio Quality`、またはCLIの `-Action ApplyStandard` / `-Action ApplyEconomy` から設定する。`app_build/` がソース正本で、同期は既存 `.meta` GUIDを維持する。`tools/audit_sources.py` は既存の依存監査も連鎖して実行する。
