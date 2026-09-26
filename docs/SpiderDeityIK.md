# 蜘蛛ラスボスのUnity IK

接地・自動歩行の追加工程は [SpiderDeityWalk.md](SpiderDeityWalk.md) を参照。

## すぐに確認する

1. `Assets/Shinzui/Scenes/SpiderDeityIKPreview.unity`を開いてPlayする。
2. 8本の脚が足先ターゲットに追従して交互に持ち上がる。
3. 手動操作する場合は、`SpiderDeity_IK`の **Spider Leg Ik Preview Motion** コンポーネントをオフにする。
4. Hierarchyの `SpiderDeity_IK / LegRig / Targets / IK_Leg_L01` を選び、移動ツールで動かす。
5. 他の脚も `IK_Leg_L02`〜`IK_Leg_L04`、`IK_Leg_R01`〜`IK_Leg_R04` で操作できる。

メニュー `Tools > Shinzui > Spider Deity > Open IK Preview` からも開ける。
通常のEditモードでは、ターゲットの配置を編集できるがIKの常時評価は行わない。追従の確認はPlayモードで行う。

## ゲームへ配置する

`Assets/Shinzui/Prefabs/SpiderDeity/SpiderDeity_IK.prefab`をシーンへドラッグする。
本体Prefabには確認用の自動足運びコンポーネントを含めていない。ゲーム側から各ターゲットのTransform位置を更新して使う。

- RootのAnimatorとSpider Deity Ik Rigを有効にする。RigBuilderは開始時に参照を初期化した後、自動で有効になる。
- `LegRig`のRig Weightは1。
- 足先ターゲットはボーンの子ではなく、独立した制御用階層に置いている。
- `Constraints/Leg_L01_IK`などで脚ごとのWeightを0〜1に調整できる。
- モデルを大きくする場合はPrefabのRootを均等にスケールしてからPlayする。
- 実行中のスケール変更はボーン長キャッシュの再構築が必要なので、今回の対象外。

## 構成

Unity公式 `com.unity.animation.rigging@1.4.1` の **Chain IK Constraint** を8個使用。
各脚は `Upper → Lower → Foot → Tip` の3節チェーン。Two Bone IKではなく3節をまとめて解く。

- Root：`Leg_L01_Upper`等
- Tip：`Leg_L01_Tip`等
- Target：`IK_Leg_L01`等
- Chain Rotation Weight：1
- Tip Rotation Weight：0（足先の位置だけを制御）
- Maintain Target Offset：オフ
- Max Iterations：40
- Tolerance：0.0001

AnimatorはRigBuilderの実行基盤として使用し、Animator Controllerは設定していない。足先ターゲットはシーン側のTransformで制御する。脚ごとのWeightを0にすると、その脚へのIK適用を停止して入力姿勢へ戻す。将来アニメーションクリップを重ねる場合は、ターゲットの位置や制約の参照をAnimatorが上書きしないよう別途バインディングを調整する。
`SpiderDeityIkRig`は初期化時にモデル内の実Transformへ参照を結び直してからRigBuilderを起動する。
元のFBXをネストして参照し、メッシュやボーン、ウェイトは複製・再編集していない。

## 今回の範囲

足先を指定位置に追従させるIKと、操作確認用のシーンを実装した。
プレビューの上下運動は動作確認用であり、地面判定・接地固定・移動速度に応じた踏み出し・攻撃動作ではない。
次の工程では、Raycastで接地点を決め、接地中の足先をワールド座標で固定し、歩幅を超えた脚だけ踏み出す制御を追加する。

Chain IKは初期の曲がりに基づくFABRIKで、関節角度制限やPole/Hint制御を持たない。通常の小さな歩幅から調整を始め、脚を完全に伸ばし切った状態で反対側へ動かすような極端なターゲット配置は避ける。届かないターゲットに対しても骨の長さは変えない。

## ソースとレイヤー

- 実際のIK：UnityのAnimation Riggingコンポーネント。
- 初期化：`Assets/Shinzui/Src/Infrastructure/Animation/SpiderDeityIkRig.cs`。
- 確認用ターゲット移動：`Assets/Shinzui/Src/Infrastructure/Animation/SpiderLegIkPreviewMotion.cs`。
- Prefab/確認シーン作成：`Assets/Shinzui/Editor/SpiderDeity/SpiderDeityIkAssetBuilder.cs`。
- テスト：`Assets/Shinzui/Tests/SpiderDeityIK/Editor/SpiderDeityIkTests.cs`。

確認用移動処理はUnity Transformを操作するInfrastructure層に配置。Domain/Application/Presentation/View/DI間の参照は変更していない。
作成ツールの再実行は生成済みのIK Prefabと確認シーンを更新するため、手動調整後は内容を確認してから実行する。

## 検証の実行

```powershell
unity test 'D:/Pandd/ShinShinzui' --mode EditMode --filter 'Shinzui.Tests.SpiderDeityIK' --output 'Artifacts/SpiderDeityIK/test-results.xml' --timeout 360 -- -force-d3d11
unity test 'D:/Pandd/ShinShinzui' --mode PlayMode --filter 'Shinzui.Tests.SpiderDeityIK.Runtime' --output 'Artifacts/SpiderDeityIK/playmode-results.xml' --timeout 240 -- -force-d3d11
```

全8脚のターゲット追従・メッシュ変形・他脚/胴体の独立性、到達不能位置での骨長維持、Weight 0、Rig再構築、0.5倍/3倍スケール、Playモードでの自動/手動操作を検証する。

参考：[Unity公式 Chain IK](https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.4/manual/constraints/ChainIKConstraint.html)
