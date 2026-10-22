# 人型イベント敵

`StageTemp` をPlayすると、走りの案内が変色し、左側の叫び声、転倒、敵の接近へ進む
案内中は移動可能。倒れた後は「何かキーを押して立ち上がる」の表示を待ち、新しくキーを押すと起立する
押しっぱなしのキーは起立に使わない。マウスボタン・ゲームパッドのボタンも利用可能
転倒後0.28秒で接近経路の88％を進み、全身を見せながら近距離でゆっくり迫る。入力受付は登場直後から6.5秒間で、時間切れになると0.2秒でカメラ直前へ飛びつき、0.12秒後にゲームオーバー。イベントの時間切れは死亡回避アイテムの対象外
転倒と二段の突進にカメラの揺れ・反動・一時的なモーションブラーを同期。入力待ちの溜めでは揺れを止め、起立・終了時に通常のポスト処理へ戻す。演出中は追跡時と共通の弱い砂嵐を適用
起立後2.5秒の猶予を挟み、登場した個体を既存の敵AIへ戻す

- `Assets/Shinzui/Prefabs/StageIntroEvent.prefab`：各段階の時間、叫び声の音量・左右定位、文字色、揺れとブラーの強度を調整
- `Assets/Shinzui/Prefabs/HumanEventEnemy.prefab`：NavMeshの移動設定、骨格、待機・歩行、接地IK
- `Artifacts/HumanEnemy/HumanEnemyRig.blend`：骨格と歩行を編集できるBlenderソース
- 元モデル：ユーザー提供の `HumanEnemyType1.glb`。21本の骨格とウェイト、Idle/Walkクリップを追加
- 音声：ユーザー提供の `alex_jauk-monstrous-scream-187949.mp3` を `Assets/StreamingAssets/EventEnemy/MonstrousScream.mp3` として使用。FMODのSEバスへ接続

作成メニューは `Shinzui > Enemies > Human Event`。既存Prefabの手調整は上書きしない
