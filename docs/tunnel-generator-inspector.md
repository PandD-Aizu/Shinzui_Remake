# Tunnel Generator インスペクター設定一覧

`TunnelGenerator` をアタッチしたオブジェクトのインスペクターで設定できる項目を、表示されるグループ順に説明する

## 読み方と反映方法

- 寸法の単位は Unity のワールド単位、回転の単位は度。通常は 1 単位を 1 m として扱う
- 初期値はスクリプトで定義された値。既存のシーンやPrefabに保存されている値とは異なる場合がある
- 設定はマップ生成時に読み取られる。再生中に値だけを変更しても、生成済みのマップは自動で作り直されない
- 確認時は再生を停止して設定を変更し、再度再生する。実行中の再生成は `GenerateTunnelLifetimeScope.Regenerate(seed)` を使用する
- 生成の実行は `GenerateTunnelLifetimeScope` が担当する。`TunnelGenerator` をアタッチするだけでは生成は開始されない
- 以下の最小値は主にインスペクターの入力制限。生成時にさらに補正される項目は説明欄に記載する

## 1. Generation — 基本生成

| 表示名 / フィールド名 | 初期値 | 最小値 | 内容 |
| --- | --- | --- | --- |
| Tunnel Count / `tunnelCount` | 8 | 5 | 各階層で生成する基本トンネル数の目標。開始トンネルや特殊トンネル、傾斜へ変換するトンネルも含む。上下階を両方生成すると、目標総数は通常階の3倍になる。配置条件によっては目標数に届かない、または生成が成立しない場合がある |
| Seed / `seed` | 2777 | 制限なし | ランダム生成の種。同じ設定・同じモデル・同じ実装で同じ値を使うと、同じレイアウトを再現できる。上階・下階では、この値から派生した別の種を使用する。実行時のシード上書きや再生成の引数が優先される場合がある |
| Placement Attempts Per Tunnel / `placementAttemptsPerTunnel` | 80 | 1 | トンネル1本の接続・配置を試す最大回数。大きくすると配置候補を多く試せるが、生成時間も増える。衝突条件や寸法の矛盾を必ず解決できる設定ではない |

## 2. Upper Floor — 上階

| 表示名 / フィールド名 | 初期値 | 最小値 | 内容 |
| --- | --- | --- | --- |
| Generate Upper Floor / `generateUpperFloor` | オフ | — | オンにすると通常階より上に1階層を追加し、通常階の基本トンネルの一部を傾斜させて接続する |
| Upper Floor Height / `upperFloorHeight` | 12 | 8 | 通常階から上階までの床高の差。上階を生成するときだけ有効。実際の差は少なくとも「生成に使う Tunnel Height + 3」になるため、入力値より高くなる場合がある |

上階への高低差を大きくするほど、傾斜を収めるために長いトンネルが必要になる。配置できない場合は、高低差・トンネル長・占有範囲を併せて見直す

## 3. Lower Floor — 下階

| 表示名 / フィールド名 | 初期値 | 最小値 | 内容 |
| --- | --- | --- | --- |
| Generate Lower Floor / `generateLowerFloor` | オフ | — | オンにすると通常階より下に1階層を追加し、通常階から下る傾斜トンネルで接続する。上階とは独立して切り替えられる |
| Lower Floor Depth / `lowerFloorDepth` | 12 | 8 | 通常階から下階までの床高の差を正の値で指定する。下階を生成するときだけ有効。実際の差は少なくとも「生成に使う Tunnel Height + 3」になる |

## 4. Floor Connections — 落下口

| 表示名 / フィールド名 | 初期値 | 最小値 | 内容 |
| --- | --- | --- | --- |
| Drop Opening Count / `dropOpeningCount` | 2 | 0 | 隣接する階層間に生成する落下口の数。2なら通常階↔上階に2か所、下階↔通常階に2か所で、上下階が有効なら合計4か所。0なら落下口を生成しない。現在の配置探索では、0や1でも少なくとも2か所分の安全な重なり候補を必要とする |
| Drop Opening Size / `dropOpeningSize` | 3 | 2 | 正方形の落下口の一辺。生成時は Tunnel Width - 1 を上限として補正される。実際に床を確保でき、接続口などと干渉しない場所に配置されるため、大きくするほど条件が厳しくなる |

落下口の囲いの下端は下側トンネルのアーチ天井に沿って生成される。囲いの突出量を別途設定する項目はない

## 5. Small Rooms — 小部屋

| 表示名 / フィールド名 | 初期値 | 最小値 | 内容 |
| --- | --- | --- | --- |
| Small Room Count / `smallRoomCount` | 1 | 0 | 各階層で小部屋を組み込む接続の数。0で無効。接続可能な数までに制限され、配置の成立状況によって実際の数が変わる場合がある |
| Small Room Width / `smallRoomWidth` | 8 | 1 | 小部屋の横幅。生成時は Corridor Width 以上になる。モデル指定時は主に配置判定と床の当たり判定の寸法であり、モデル本体の横幅は Small Room Model Scale でも合わせる必要がある |
| Small Room Length / `smallRoomLength` | 6 | 1 | 通路の進行方向に沿った小部屋の長さ。小部屋の前後には通路も確保するため、接続全体の長さはこの値より長くなる |

## 6. Layout Size — 長さ・幅・配置間隔

| 表示名 / フィールド名 | 初期値 | 最小値 | 内容 |
| --- | --- | --- | --- |
| Tunnel Length / `tunnelLength` | 153.12695 | 4 | 基本トンネルの基準長。接続口の間隔を各トンネルの長さへ換算する際などに使用する。個々の実際の長さは Min / Max Tunnel Length で指定する。モデル実測が有効な場合は、本体の実測長へ置き換わる |
| Min Tunnel Length / `minTunnelLength` | 153.12695 | 4 | 基本トンネルのランダム長の下限。Max と同じなら固定長になる。上下の指定が逆の場合は、小さい方を下限として扱う |
| Max Tunnel Length / `maxTunnelLength` | 153.12695 | 4 | 基本トンネルのランダム長の上限。長いほど探索距離や配置範囲が増える。傾斜トンネルの最終的な床に沿った長さは、元の水平長より長くなる場合がある |
| Connection Point Spacing / `connectionPointSpacing` | 51.042316 | 0 | 左右各3か所の接続口について、中央の口から前後の口までの基準間隔。手動設定時は Basic Tunnel Model Scale のX倍率も掛かる。各トンネルの長さに比例して換算され、長さの45%以内に制限される。モデル実測が有効なら実測値が優先される |
| Tunnel Width / `tunnelWidth` | 14.800003 | 3 | 配置計算で使う基本トンネルの幅。モデル実測時は、左右の側面通路の先端も含めた外寸になる。主トンネル内部の通行幅や Corridor Width と同じ意味ではない |
| Tunnel Height / `tunnelHeight` | 5 | 2 | 基本トンネルの占有高や階層間隔の計算に使う高さ。モデル実測時は入力値と実測高の大きい方を使う。接続通路の天井にはそのまま使わず、側面口に合う高さを別途計測する |
| Corridor Length / `corridorLength` | 8 | 1 | 接続通路の基準長。通常の直線接続では、おおむねこの値の1〜3倍を選ぶ。L字接続や小部屋の確保によってさらに長くなる場合がある。ワープ通路の長さにも使用する。モデル実測がオンでも、この設定は維持される |
| Corridor Width / `corridorWidth` | 3 | 1 | 生成通路の内幅。対応モデルではトンネルの側面口もこの幅へ補正する。モデル実測がオンでも、この設定は維持される |
| Placement Margin / `placementMargin` | 2 | 0 | 重なり判定に追加する余裕。大きくすると構造物同士を離しやすくなるが、配置できる候補は減る。正確な壁間距離を一律に指定する値ではない |

### 基準長とランダム長の注意

モデル実測によって Tunnel Length が変わった場合、Min / Max Tunnel Length が変更前の Tunnel Length と同値なら、それぞれ実測した基準長へ補正される

長さの範囲を明示したい場合は Min / Max を設定する。例えば基準長40、Min 60、Max 76なら、個々の水平な基本トンネルは60〜76の範囲で生成される

## 7. Overlap Prevention — 重なり判定

| 表示名 / フィールド名 | 初期値 | 最小値 | 内容 |
| --- | --- | --- | --- |
| Tunnel Overlap Size Multiplier / `tunnelOverlapSizeMultiplier` | 1 | 0.1 | 基本トンネルの重なり判定用サイズの倍率。1より大きいと安全側に広く判定し、小さいと近接配置を許容しやすくなる。見た目のモデルサイズは変わらない |
| Corridor Overlap Size Multiplier / `corridorOverlapSizeMultiplier` | 1 | 0.1 | 通路の重なり判定用サイズの倍率。小さくしすぎると実際の壁が重なる可能性がある |
| Small Room Overlap Size Multiplier / `smallRoomOverlapSizeMultiplier` | 1 | 0.1 | 小部屋の重なり判定用サイズの倍率。部屋モデルが配置判定より大きい場合などに余裕を増やせるが、モデル寸法そのものを補正する値ではない |

倍率と Placement Margin は別に適用される。これらだけを小さくして生成を成立させると、見た目や当たり判定が重なる場合がある

## 8. Scene References — シーン参照

| 表示名 / フィールド名 | 初期値 | 内容 |
| --- | --- | --- |
| Map View / `mapView` | 未設定 | 生成オブジェクトを構築する `TunnelMapView` の参照。同じシーンのコンポーネントを指定する。LifetimeScope側の Map View 指定がある場合はそちらが優先され、どちらも未指定なら同じシーンで探索される |

生成先の Map Root、俯瞰用カメラ、デバッグ表示などは `TunnelMapView` 側の項目であり、`TunnelGenerator` の設定ではない

## 9. Models — 使用モデル

| 表示名 / フィールド名 | 初期値 | 内容 |
| --- | --- | --- |
| Basic Tunnel Model / `basicTunnelModel` | 未設定 | 基本トンネルのモデルまたはPrefab。現在の接続口・床・ループゲートの補正は既存の `TunnelBaseModel` 構成を前提とする。未指定時は Addressables の `TunnelBaseModel` を取得する |
| Corridor Model / `corridorModel` | 未設定 | 通路の見本モデル。項目は残っているが、現在の通常通路・小部屋前後の通路は床・壁・天井を寸法から直接生成するため、このモデルを引き伸ばして表示することはない。ワープ専用モデルが空欄の場合も現在は直接生成する |
| Small Room Model / `smallRoomModel` | 未設定 | 小部屋に配置するモデル。未指定時は小部屋の床のみを生成する。指定時の見た目は専用の Scale / Rotation で調整し、Small Room Width / Length の配置範囲に収める |
| Warp Corridor Model / `warpCorridorModel` | 未設定 | ワープ通路専用のモデル。未指定時は通常通路と同様に寸法から生成する。指定時は専用の Scale / Rotation を使用し、長さ方向にも倍率を適用するため、実際の長さ・床・接続口の整合を確認する |

任意のモデルへ差し替えるだけで、すべての補正が自動対応するわけではない。既存モデルの床・側面口・カバー・ゲートの構成と座標系を維持する必要がある

## 10. Ceiling Lights — 天井照明

| 表示名 / フィールド名 | 初期値 | 最小値 | 内容 |
| --- | --- | --- | --- |
| Ceiling Light Spacing / `ceilingLightSpacing` | 5 | 1.5 | トンネルや生成通路の天井に配置する蛍光灯の間隔。小さくすると灯数が増え、大きくすると灯数が減る。両端の余白や実際の天井の有無により配置数が決まるため、必ず長さ÷間隔の個数になるわけではない |

照明Prefabは Resources の `TunnelFluorescentLight` を使用する。Prefab参照や明るさを直接変更する項目はこのコンポーネントにはない。影の距離・影を描く灯数は `TunnelMapView` 側で設定する

## 11. Model Scale — モデル倍率

| 表示名 / フィールド名 | 初期値 | 内容 |
| --- | --- | --- |
| Basic Tunnel Model Scale / `basicTunnelModelScale` | (1, 1, 1) | Xは既存モデルの長さ方向の基準倍率。Yは断面全体の倍率で、Zも必ずYと同じ倍率になる。高さと横幅の比率を維持し、長さだけを独立して調整できる。個々の長さはさらに Min / Max で変化する |
| Corridor Model Scale / `corridorModelScale` | (1, 1, 1) | 見本の Corridor Model に対する倍率。現在の通常通路は直接生成するため、この値では見た目の幅や長さを変更できない。通常通路の調整には Corridor Width / Length を使用する |
| Small Room Model Scale / `smallRoomModelScale` | (1, 1, 1) | 指定した小部屋モデルのX・Y・Z倍率。Small Room Model が未指定なら表示モデルがないため効果はない。レイアウト上の部屋寸法は別設定 |
| Warp Corridor Model Scale / `warpCorridorModelScale` | (1, 1, 1) | ワープ専用モデルのX・Y・Z倍率。Warp Corridor Model が指定されている場合に使用する。未指定の直接生成では使用しない |

倍率の0は取得時に1へ補正される。負の倍率は反転の原因になるため、通常の調整では正の値を使用する

## 12. Model Rotation — モデル回転

| 表示名 / フィールド名 | 初期値 | 内容 |
| --- | --- | --- |
| Basic Tunnel Model Euler / `basicTunnelModelEuler` | (0, 90, 0) | 基本モデルを生成用の座標系へ合わせる回転。既存モデルでは長手方向がXのため、Yを90度回してトンネルの進行軸へ合わせる。レイアウト全体の向きを指定する項目ではない |
| Corridor Model Euler / `corridorModelEuler` | (0, 0, 0) | 見本の Corridor Model の回転設定。現在の通常通路は直接生成するため、この値で通常通路の方向は変わらない |
| Small Room Model Euler / `smallRoomModelEuler` | (0, 0, 0) | 小部屋モデルのローカル回転。部屋が横を向く、上下が逆などの場合にモデルの向きを補正する |
| Warp Corridor Model Euler / `warpCorridorModelEuler` | (0, 0, 0) | ワープ専用モデルのローカル回転。モデルの長軸判定による向き補正も加わる。専用モデルを指定した場合のみ使用する |

基本トンネルを斜めの方角へ配置する設定ではない。基本レイアウトは縦横の2方向で、階層へつなぐ傾斜は生成処理が別途決定する

## 13. Model Dimensions — 自動計測と接続口

| 表示名 / フィールド名 | 初期値 | 内容 |
| --- | --- | --- |
| Use Model Bounds For Layout / `useModelBoundsForLayout` | オン | Model Scale を反映したモデルの寸法から、Tunnel Length・Tunnel Width・Tunnel Height・Connection Point Spacing を補正する。Tunnel Height は入力値との大きい方を使う。Corridor Length / Width は上書きしない。通常はオンを維持する |
| Configure Basic Tunnel Exit Parts / `configureBasicTunnelExitParts` | オン | 使用する側面口を開き、未使用の口を既存カバーの複製で閉じる。オフにするとこの開閉処理を省略し、モデル側の有効・無効状態に依存する。配置計算や接続口の寸法補正まで無効にする設定ではない |

Use Model Bounds For Layout をオフにすると、配置計算の基準寸法を手動管理する必要がある。表示モデルの寸法や接続位置と異なる値を入れると、隙間や重なりが発生する場合がある

## インスペクターにない設定

ワープ通路のペア相手、到着方向、ループゲートの位置、通路天井の高さ、落下口の囲いの下端は生成処理で自動決定される。これらを直接入力する `TunnelGenerator` の項目はない
