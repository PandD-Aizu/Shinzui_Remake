# 段階4 検証結果

実施日: 2026-09-10。Unity 6000.5.8f1 / FMOD Unity 2.03.20 / FMOD Studio 2.03.12 / Steam Audio 4.8.1。Unity CLIで実行し、EditorのGUIは使用していません。

周波数別の壁越しの透過、入口・扉での段階的な遮蔽、反射音の方向と音量、残響の後半を調整しました。本編の足音を管理するTunnelAudioRuntimeへ設定を接続しています。

| 検証 | Editor CLI | Windows IL2CPP |
|---|---:|---:|
| 録音条件 | 19 | 19 |
| 実行チェック | 127 / 127 | 127 / 127 |
| 録音解析 | 14 / 14 | 14 / 14 |
| 扉の境界における最大音量変化の減少 | 36.7% | 40.3% |
| 左入口の初期反射・左−右エネルギー差 | +0.92 dB | +0.92 dB |
| 右入口の初期反射・左−右エネルギー差 | -1.93 dB | -1.88 dB |

音量変化は10msごとのRMS差を各録音の最大RMSで正規化し、その最大値を単一音線と複数音線で比較したものです。聴感の改善率やクリックの発生確率を表す数値ではありません。左右差は同じ音源・リスナー位置で入口だけを左右に変え、録音1.00〜1.35秒の反射音を測定しています。

## 比較録音

すべてFMODマスター出力の48kHz・16bit・ステレオ録音です。音量を個別に揃える正規化は行わず、条件間に1秒の無音を挿入しています。

- [部屋・扉・曲がり角・入口の比較](D:/Pandd/ShinShinzui/app_build/Propagation/results/room-comparison.wav)
- [扉の移動・入口を歩く比較](D:/Pandd/ShinShinzui/app_build/Propagation/results/boundary-comparison.wav)

部屋の比較順は「直接音のみ → 反射音のみ → 本編の配分 → 閉じた木製扉 → L字通路 → 左入口 → 右入口」です。境界の比較順は「単一音線 → 複数音線 → 入口を歩いて通過」です。

## 確認した結果

- 距離2/4/8mで、距離が2倍になると直接音の振幅が約半分になることを確認。
- コンクリート・金属・木材で直接の音線を遮り、透過を有効にした場合だけ小さい音が通過。木材が金属・コンクリートより多く通す初期設定を確認。
- 300Hz・1800Hz・10000Hzの信号で、壁越しの高音が低音より強く減衰することを確認。小さい信号の数値には16bit録音の量子化の影響を含みます。
- 反射回数16では残響後半が短かったため64へ調整。直接音にはない後半成分を確認し、本編では反射音量0.35を適用。
- 開いた中央入口と閉じた木製扉で音量差を確認。左右の入口に応じて初期反射の左右エネルギー差の符号が変化。
- L字通路では直接の音線が塞がれていても反射音が到達。
- 動く扉で部分的な遮蔽を確認し、入口を歩いて通過する連続音でも10ms窓の音切れを検出せず、急な音量変化を基準内に抑制。
- 足元の追加条件で、床による不自然な直接音の遮蔽がないことを確認。
- 音源のサンプル数がSteam Audio側の上限を超える設定はSDKへ渡す前に拒否。
- 検証後は管理する音源・音響形状を解放。FMODバックエンドエラーと録音のクリッピングなし。

## 回帰検証と証跡

現行コード・設定で、段階3のEditor実行43項目＋解析6項目、段階2のEditor実行72項目＋解析7項目に合格しました。準備した足音の開始は、段階3のEditor回帰録音で約46msを維持しています。これはFMODマスター録音上の時間で、OS・出力機器の遅延は含みません。

- [Editorチェック](D:/Pandd/ShinShinzui/app_build/Propagation/results/editor-report.json) / [解析](D:/Pandd/ShinShinzui/app_build/Propagation/results/editor-analysis.json)
- [Windowsチェック](D:/Pandd/ShinShinzui/app_build/Propagation/results/player-report.json) / [解析](D:/Pandd/ShinShinzui/app_build/Propagation/results/player-analysis.json)
- [段階3回帰](D:/Pandd/ShinShinzui/app_build/Propagation/results/phase3-regression-report.json) / [解析](D:/Pandd/ShinShinzui/app_build/Propagation/results/phase3-regression-analysis.json)
- [段階2回帰](D:/Pandd/ShinShinzui/app_build/Propagation/results/phase2-regression-report.json) / [解析](D:/Pandd/ShinShinzui/app_build/Propagation/results/phase2-regression-analysis.json)
- [ソース・設定・バンクのSHA-256](D:/Pandd/ShinShinzui/app_build/Propagation/results/source-sha256.json)

実行ファイルは [PropagationProbe.exe](D:/Pandd/ShinShinzui/Builds/PropagationProbe/PropagationProbe.exe) です。検証専用のWindows IL2CPPビルドで、本編全体のビルドではありません。

## 残る範囲

反射による方向表現を検証した段階であり、波動としての回折や開口部を通る最短経路を保証するものではありません。独自の仮想音源とPathingは追加していません。ワープポータル越しの継続伝播、全生成形状に対する聴感、最終の足音素材は別途扱います。

段階5で同時音源数・CPU/DSP・メモリ・長時間の生成と破棄、BGM/UI・設定・ポーズなどの全体回帰を検証します。今回の反射回数増加を含む設定は品質確認用の初期値で、製品の性能保証値ではありません。

[設定と再実行手順](D:/Pandd/ShinShinzui/app_build/Propagation/README.md) / [全体計画](D:/Pandd/ShinShinzui/app_build/docs/organic-reverb-plan.md)
