"""Archive passing production-budget evidence, including stress failures above the selected cap."""
from pathlib import Path
import hashlib, json, shutil
r=Path(__file__).resolve().parents[3];dst=r/'app_build/AcousticPerformance/results';dst.mkdir(parents=True,exist_ok=True)
records={}
for label in ['Editor-full','Player-full','Player-economy']:
    folder=r/'Logs/AcousticPerformance'/label
    report=json.loads((folder/'report.json').read_text(encoding='utf-8-sig'));analysis=json.loads((folder/'analysis.json').read_text())
    assert report['passed'] and analysis['passed'],label+' failed the selected production checks'
    records[label]=(report,analysis)
    for name in ['report.json','analysis.json']:shutil.copy2(folder/name,dst/(label.lower()+'-'+name))
    names=[p.name for p in folder.glob('*.wav')] if label=='Player-full' else ['voices-12.wav','voices-32.wav'] if label=='Player-economy' else []
    for name in names:shutil.copy2(folder/name,dst/(label.lower()+'-'+name))
full,fa=records['Player-full'];economy,ea=records['Player-economy'];editor,_=records['Editor-full']
regressions=[]
for suite in ['SpatialAudio','TunnelAcoustics','Propagation']:
    folder=r/'Logs'/suite/'Editor'
    report=json.loads((folder/'report.json').read_text(encoding='utf-8-sig'));analysis=json.loads((folder/'analysis.json').read_text())
    assert report['passed'] and analysis['passed'],suite+' regression failed'
    regressions.append((suite,len(report['checks']),len(analysis['checks'])))
    for name in ['report.json','analysis.json']:shutil.copy2(folder/name,dst/('regression-'+suite.lower()+'-'+name))
lines=['# 段階5 検証結果','',
    '標準12音源・軽量8音源を初期設定として採用。Unity CLIのEditor検証とWindows IL2CPP Development版の検証・録音解析を実施した。32音源は製品上限を超えるストレス条件として結果を保存し、採用条件の合否とは分けている。','',
    f"計測機: {full['cpu']} / {full['logicalCores']}論理コア、{full['gpu']}、RAM {full['ramMB']}MB。Unity {full['unityVersion']}。960×540、120fps上限の生成トンネル音響シーン。",'',
    '## Windowsでの定常再生','',
    '| 品質 | 音源 | フレームp95 ms | DSP平均 / 最大 % | デバイス欠落警告 | 録音秒数（実時間8秒） |',
    '| --- | ---: | ---: | ---: | ---: | ---: |']
for label,(report,analysis) in records.items():
    if not label.startswith('Player'):continue
    for m in report['measurements']:
        seconds=analysis['audio'][m['name']]['seconds']
        lines.append(f"| {report['quality']} | {m['voices']} | {m['frameP95']:.2f} | {m['dspMean']:.1f} / {m['dspMax']:.1f} | {m['outputStarvations']} | {seconds:.2f} |")
lines+=['','DSPは音声スレッドの負荷指標。32音源で録音秒数が実時間より短い場合、リアルタイム出力に追いついていない。CPU・フレーム・音声の生データはJSONに保存。初期予算はp95 16.67ms以下、DSP最大80%未満、採用上限内の出力欠落なし。','',
    '## 再生成・シーン破棄','',
    '| 実行環境 | Private Bytes 最後の10回の変動 MB | Unity割当 最後の10回の変動 MB | FMOD割当 MB |',
    '| --- | ---: | ---: | ---: |']
for label,(_,analysis) in records.items():
    m=analysis['memory'];lines.append(f"| {label} | {m['processPrivateMB']['last_10_range']:.3f} | {m['unityAllocatedMB']['last_10_range']:.3f} | {m['fmodAllocatedMB']['last']:.3f} |")
lines+=['','各30回の生成・破棄で、音源インスタンスと音響形状が解放され、マテリアル数が安定し、生成NavMeshが1個に保たれることを確認。最後のAdditiveシーンUnloadでは生成階層・音響形状・音源・所有NavMeshがなくなることを確認した。有限回の観測であり、長時間プレイの保証ではない。','',
    '## 回帰確認','']
for label,(report,analysis) in records.items():
    lines.append(f"- {label}: 実行チェック {len(report['checks'])}件、解析チェック {len(analysis['checks'])}件が合格（採用範囲）。")
for suite,runtime,analysis in regressions:lines.append(f"- {suite} Editor回帰: 実行 {runtime}件、録音解析 {analysis}件が合格。")
lines+=[f"- Windows標準設定: SE音量0.5で足音の振幅比は {fa['amplitude_half_ratio']:.4f}。SE / Master / WorldSEミュート、ポーズで無音。BGMミュートで足音は消えない。",
    '- 既存BGMとボタンSEの再生・音量設定、ポーズ後の個別停止状態の維持、発音準備中のポーズ、シーンUnload後の既存音声再生も検証した。',
    '- 既存SEバスとBGMイベントの設定は維持。足音は専用WorldSEバスを通り、SE音量に追従し、従来のリバーブSendを重ねない。','',
    '## 残る製品作業','',
    'この実装で実ゲームへ接続済みなのは生成トンネルの足音（仮素材）。敵・衝突音などの位置付きAPIへの移行、最終音素材への差替え、製品の対象PC・フルシーン・長時間プレイでの試聴と計測は別途必要。全体FPSやBodycamの内部実装との一致を保証するものではない。',
    '', '再現手順と品質切替は [README](../README.md) を参照。']
(dst/'summary.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
paths=[]
for directory in ['OrganicReverb','SpatialAudio','TunnelAcoustics','Propagation','AcousticPerformance']:
    paths.extend(p for p in (r/'app_build'/directory).rglob('*') if p.suffix in ['.cs','.asmdef','.py','.ps1','.js'] and 'results' not in p.parts)
paths.extend([r/'app_build/View/GenerateTunnel/TunnelMapView.cs',r/'Assets/Plugins/SteamAudio/Resources/SteamAudioSettings.asset',
    r/'Assets/Shinzui/Audio/Resources/SpatialAudio/TunnelAudioConfiguration.asset',r/'Assets/Plugins/FMOD/Resources/FMODStudioSettings.asset',
    r/'Shinzui/Metadata/Event/{c5318595-038f-4def-a65f-d4b1c5ffe2c8}.xml'])
paths.extend((r/'Shinzui/Metadata/Group').glob('*.xml'));paths.extend((r/'Shinzui/Build/Desktop').glob('*.bank'))
(dst/'source-sha256.json').write_text(json.dumps({p.relative_to(r).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(set(paths))},indent=2))
print('Archived reports, source hashes and unnormalized WAV files:',dst)
