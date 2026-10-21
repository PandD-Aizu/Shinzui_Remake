# 蜘蛛の接地・プロシージャル歩行

移動AIと胴体の高さ・傾き補正を含むPrefabは [SpiderDeityAI.md](SpiderDeityAI.md) を参照。

## 確認方法

`Assets/Shinzui/Scenes/SpiderDeityWalkPreview.unity` を開いてPlay。
メニュー `Tools > Shinzui > Spider Deity > Open Walk Preview` からも開ける。
胴体が前後移動・旋回し、接地中の足先をワールド座標に保ちながら、交互の4脚グループで踏み出す。

`Spider Walk Preview Motion` を無効にするとデモ移動が止まる。
その状態でルートをゆっくり移動・Y軸回転させても、脚は追従して踏み替える。
足先を手動操作する場合は `Spider Procedural Walk` も無効にする。
従来の `SpiderDeityIKPreview.unity` は手動IK確認用として維持している。

## ゲーム側で使う

`Assets/Shinzui/Prefabs/SpiderDeity/SpiderDeity_Walk.prefab` を配置する。
これは既存IK Prefabを継承したVariantで、デモ移動は含まない。
地形にColliderを付け、`Ground Layers`を地形のレイヤーに設定する（初期値はDefault）。
自身の子ColliderとTriggerは接地判定から除外する。
移動AI等はルートTransformを更新する。歩行コンポーネントは足先だけを制御し、移動方向を決定しない。
移動処理は実行順序 -100 より前のUpdateで行う。デモは -200。

## 調整項目

| 項目 | 初期値 | 用途 |
| --- | --- | --- |
| Step Distance | 0.065 | 接地位置と次の着地候補のずれがこの距離を超えると踏み出す |
| Step Duration | 0.28秒 | 一歩の時間 |
| Step Height | 0.055 | 持ち上げ高さ |
| Probe Height / Depth | 0.2 / 0.3 | 基準足先の上方から下へ地面を探す範囲 |
| Max Slope | 50度 | 着地点として許可する地面の傾斜 |
| Foot Offset | 0.002 | 足先を地面からわずかに浮かせる距離 |
| Teleport Distance | 0.6 | 1フレームで超えると接地点を再取得する移動量 |

距離はPrefabの均等スケールに追従する。スケールはPlay開始前に設定する。
同時に踏み出すのは最大4脚。同じ側の隣接脚と反対側の対応脚は別グループ。
地面が見つからない・傾斜が急すぎる・脚が届かない候補には踏み出さない。
必要ならテレポート直後に `ResetContacts()` を呼んで接地を明示的にリセットできる。

## 範囲と制約

今回の対象は静的な地面上での低速歩行と足先の接地。
移動AI、経路探索、胴体の高さ・傾き補正、壁歩き、動く床への追従、攻撃モーションとの切替は含まない。
地面がなくなった場合は最後の接地点を保持するため、崖を渡れるかどうかは移動側で判断する。
移動が速すぎると接地足がIKの到達範囲外になるので、まずデモ程度の低速（約0.10単位/秒以下）から調整する。
踏み出し先は開始時に確定するため、歩行中に変形・移動する地形は対象外。
IK自体の関節角制限は従来どおりない。

Unity物理判定・Transform操作はInfrastructure層に配置し、他レイヤーとasmdefの依存関係を追加していない。
既存のFBX・ウェイト・IK PrefabとIK確認シーンは変更していない。

## 検証

`SpiderWalkPlayModeTests` が接地固定、実際の足先追従、最大同時踏み出し数、停止後の着地、地面なし、テレポートを確認する。

2026-09-26、Unity 6000.5.8f1でPlayMode計5件（既存IK確認1件＋歩行4件）が成功。
デモの前後移動・旋回を含む7秒間の足先追従と描画も確認。
結果は `Artifacts/SpiderDeityIK/walk-playmode-results.xml`、画像は `Artifacts/SpiderDeityIK/unity_walk_preview.png`。

```powershell
unity test 'D:/Pandd/ShinShinzui' --mode PlayMode --filter 'Shinzui.Tests.SpiderDeityIK.Runtime' --output 'Artifacts/SpiderDeityIK/walk-playmode-results.xml' --timeout 240 -- -force-d3d11
```

生成ツール `Create or Update Walk Assets` は歩行用Prefabとシーンを再作成する。手動調整後は再実行による上書きに注意する。
