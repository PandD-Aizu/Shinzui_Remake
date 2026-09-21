# ブラックホール眼の敵

`Assets/Shinzui/Prefabs/BlackHoleEnemy.prefab` を `StageTemp` に追加。タイトルのゲーム開始先はこのシーンです。

黒いフードと裂けた裾を持つ浮遊型の敵で、左右の目に黒い事象の地平面、青白い光輪、中心に流れ込む螺旋を描画します。左右でサイズ・回転方向・位相を変えています。行動は既存の `EnemyView` / `EnemyPresenter` / `EnemyController` に接続され、巡回、追跡、接触時の死亡判定、ストロボの影響を引き継ぎます。

## 確認

- `Assets/Shinzui/Scenes/BlackHoleEnemyPreview.unity` を開き、Playすると浮遊アニメーションと目の渦を確認できます。プレビューではNavMeshAgentとEnemyViewを無効にしてあります。
- `StageTemp` をPlayすると、既存の敵よりトンネルの奥へ6m離した位置に追加した敵が既存AIで動作します。
- 他のゲームシーンでは、NavMesh上にPrefabを配置してください。前方はローカル +Z です。既存の `PlayerLifetimeScope.enemyViews` が空なら起動時に自動検出します。配列を明示指定しているシーンではこの敵のEnemyViewを配列へ追加します。

## 調整

`Assets/Shinzui/Art/BlackHoleEnemy/EyeLeft.mat` と `EyeRight.mat` のInspectorで変更できます。

| パラメータ | 内容 |
| --- | --- |
| Photon Ring / Accretion Disk | 光輪と渦の色 |
| Emission | 発光強度。Bloomなしでも可視 |
| Event Horizon Radius | 黒い中心の大きさ |
| Photon Ring Width | 光輪の太さ |
| Orbit Speed | 回転速度。負数で逆回転 |
| Inward Flow Speed | 中心へ流れる速度 |
| Spiral Winding | 螺旋の密度 |
| Eye Phase | 左右の位相差 |

目の**周辺**の歪みは `Assets/Shinzui/Art/BlackHoleEnemy/SurroundingLens.mat` で調整します。

| パラメータ | 内容 |
| --- | --- |
| Inward Pull | 顔・背景を目の中心へ引き込む強さ。初期値2.6 |
| Spiral Distortion | 引き込みに加えるねじれ。左右で逆方向 |
| Distortion Radius | 光輪の外側まで広がる歪みの範囲 |
| Suction Wave Speed | 外側から目へ進む吸引波の速度 |
| Suction Wave Strength | 吸引波による強弱の変化 |
| Distortion Blend | 歪みの混合量。0で周辺の歪みを無効化 |

背景の屈折はURPのOpaque Textureを使用します。低・中・高の各品質設定で有効にしています。Opaque Textureが利用できない別の描画設定では歪みを省略し、黒い中心・光輪・螺旋のみ表示します。透明物体はOpaque Textureに含まれません（[Unity公式リファレンス](https://docs.unity3d.com/ja/6000.0/Manual/urp/universalrp-asset.html)）。

## 配置と保守

- コードは正式な `Assets/Shinzui` 配下のみ。追加のランタイムC#、asmdef参照変更、Domain / Applicationへの機能追加はありません。
- シェーダーは `Shaders/BlackHoleEye.shader`。透過のプリマルチプライ方式で中心を黒く遮蔽し、矩形の四隅を透明にします。深度テストで壁越しには描画されません。
- `Shaders/BlackHoleLens.shader` は両目の吸引を一枚の描画面に合成します。顔と背景を歪ませてから光輪を描くため、左右の効果の重なりで目が消えません。変位を実際の目の面から投影するため、距離や視野角に応じて自然に縮小します。
- 身体メッシュ、マテリアル、浮遊AnimationClipは保存済みアセット。実行時の自動生成やマテリアル複製は行いません。
- `Shinzui > Enemies > Black Hole > Create Assets and Preview` は欠けているアセットだけを作成します。作成済みPrefabやマテリアルの手調整を上書きしません。
- `Add to Gameplay Stage` は `StageTemp` に一体だけ配置する冪等操作です。未保存の同シーンが開かれている場合は保存を要求して停止します。
- 旧Prefabへ周辺の歪みを追加する場合は `Enable Surrounding Distortion` を実行します。既存の目や身体の設定を保持し、歪み面だけを追加します。

## 検証結果

Unity 6000.5.8f1 / URP 17.5.0で確認。

- Unityの再コンパイル完了、エラーなし。実際の描画後もBlackHoleEyeのシェーダーエラーなし。
- Prefabには左右2つの目、EnemyView 1個、明示参照されたNavMeshAgent、接触用CapsuleCollider、浮遊AnimationClipを保持。Missing Scriptなし。
- StageTempのPlayで既存敵と追加敵の計2体がEnemyPresenterへ登録されることを確認。
- Play中のみ追加敵をプレイヤー前方5mのNavMeshへ移し、1秒後に1.108mの追跡移動、速度1.2m/s、PathCompleteを確認。テスト時の移動はシーンへ保存していません。
- 浮遊アニメーションの再生と高さの変化を確認。
- 画像: `Artifacts/BlackHoleEnemy/preview.png`。移動記録: `Artifacts/BlackHoleEnemy/runtime-verification.json`。
- 周辺の歪みは低品質設定のUnity描画で有効・無効を比較。光輪の外側でも背景格子と顔の輪郭が変形し、画面端の変化は0ピクセル。両シェーダーのコンパイルエラーなし。
- 比較画像: `Artifacts/BlackHoleEnemy/lens-before.png` / `lens-after.png`。歪みの描画順は2990、光輪は3000。
