# 蜘蛛ラスボス：脚リグ

元モデル：`C:/Users/koton/Downloads/spider deity 3d model.glb`（変更なし）

## 納品ファイル

- `SpiderDeity_Rigged.blend`：編集用。脚ボーン、ウェイト、Blender内の確認用IKターゲット付き。
- `SpiderDeity_Rigged.glb`：スキニングと骨階層を含む汎用モデル。
- Unity用FBX：`Assets/Shinzui/3DModels/SpiderDeity/SpiderDeity_Rigged.fbx`
- `rigged_pose_test.png`：2本の脚を持ち上げた変形確認画像。

## 骨の構成

合計34本：`Root`、`Body`、8本の脚 × 4本。

```
Root
└─ Body
   ├─ Leg_L01_Upper → Leg_L01_Lower → Leg_L01_Foot → Leg_L01_Tip
   ├─ Leg_L02_Upper → Leg_L02_Lower → Leg_L02_Foot → Leg_L02_Tip
   ├─ Leg_L03_Upper → Leg_L03_Lower → Leg_L03_Foot → Leg_L03_Tip
   ├─ Leg_L04_Upper → Leg_L04_Lower → Leg_L04_Foot → Leg_L04_Tip
   └─ Leg_R01〜Leg_R04（同じ構成）
```

- L/Rはモデル自身から見た左右。01は前脚、04は後脚。
- 各脚は3本の変形用ボーンと、足先位置を示すTipで構成。
- `Root`と`Tip`はウェイトを持たない補助骨。変形用はBodyを含む25本。
- 人型の上半身・腹部はBodyに固定。腕、顔、髪を個別に動かす骨は今回の対象外。
- 原寸維持。元モデルの高さは約1 Unity unit。ボスのゲーム内サイズはPrefab側で設定する。
- 元モデルにテクスチャ/UVは含まれていないため、グレーの外観を維持。

## Blenderで脚を動かす

1. `.blend`を開く。
2. `SpiderDeity_Rig`を選び、Pose Modeにする。
3. 例：`Leg_L01_Foot`のBone Constraintsで `Preview IK (enable influence)` のInfluenceを **1** にする。
4. Object Modeに戻り、`IK_Controls_Blender_Only`内の`IK_Leg_L01`を移動する。
5. 対応する脚が3本の骨で追従する。

保存時は全IKのInfluenceを0にして、元の形状を保っている。確認用IKはストレッチ無効。曲げ方向の厳密な制限やPole制御は歩行実装時に調整する。

## Unityで使う

- FBXはGenericとして読み込み、Optimize Game Objectsはオフにして骨Transformを保持する。
- シーンにFBXを配置するとSkinnedMeshRendererで脚を変形できる。
- 今回の骨は3節なので、足先まで全体を解く場合はChain IKまたは3節対応のソルバーを使う。
- Chain IKのRootは`Leg_L01_Upper`、Tipは`Leg_L01_Tip`（他の脚も同様）。
- BlenderのIK制約はFBX/GLBには移植されない。Unity側のIK、Raycast接地、踏み出し制御は次の工程。
- 装飾は取り付け付近の骨に追従する。鎖や布の独立した物理揺れは未設定。

## 検証

- `validation.json`：全頂点のウェイト、ニュートラル形状、各脚のIK追従、胴体/反対側の脚への影響を検証。
- `deformation_validation.json`：各脚の足先を高さ約0.09、前方約0.025動かした際のメッシュ辺の伸びを確認。
- `unity_validation.json`：UnityでのFBX読み込み、8本のTip、スキニング、各脚の変形を確認（生成された場合）。
- 最終的な歩幅や攻撃ポーズが決まったら、その可動域で追加調整する。

## 再生成

`inspect_mesh.py` → `prepare_weights.py` → Blenderバックグラウンドで`build_rig.py`。
`rig_definition.json`に配置座標、`weights.npz`に正規化済みウェイトを保存。

Blenderはローカルキャッシュのポータブル版4.5.9 LTSを使用。ゲームのランタイムコードやレイヤー依存は変更していない。
