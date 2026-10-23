using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;

namespace Shinzui.View
{
    /// <summary>独立した検証場で敵Prefabのモーションと導入イベントを繰り返し再生</summary>
    public sealed class HumanEnemyMotionTest : MonoBehaviour
    {
        public enum TestMode { Idle, Walk, Crawl, Lunge, Introduction, Capture, Chase }

        [SerializeField] private PlayerView player;
        [SerializeField] private StageIntroView introductionPrefab;
        [SerializeField] private EnemyView[] enemyPrefabs;
        private GameObject _instance;
        private StageIntroView _introduction;
        private HumanEnemyCaptureView _capture;
        private HumanEnemyLocomotion _motion;
        private NavMeshAgent _agent;
        private NavMeshData _navMesh;
        private NavMeshDataInstance _navMeshInstance;
        private TestMode _mode;
        private int _enemyIndex;
        private float _elapsed;
        private bool _lunging;
        private bool _walkRight;
        private string _result = "";

        /// <summary>床に対応するNavMeshを構築して待機プレビューを開始</summary>
        private void Start()
        {
            // Unity Audioを使わないFMODの背後音を検証カメラで聞く
            if (player.MainCamera.GetComponent<FMODUnity.StudioListener>() == null)
                player.MainCamera.gameObject.AddComponent<FMODUnity.StudioListener>();
            var sources = new List<NavMeshBuildSource>
            {
                new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Box,
                    transform = Matrix4x4.TRS(new Vector3(0f, -.25f, 0f), Quaternion.identity, Vector3.one),
                    size = new Vector3(24f, .5f, 24f)
                }
            };
            _navMesh = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), sources,
                new Bounds(Vector3.zero, new Vector3(26f, 6f, 26f)), Vector3.zero, Quaternion.identity);
            _navMeshInstance = NavMesh.AddNavMeshData(_navMesh);
            Play(TestMode.Idle);
        }

        /// <summary>直前の検証を終了し、選択したモーションを初期状態から再生</summary>
        /// <param name="mode">再生する検証項目</param>
        public void Play(TestMode mode)
        {
            // 本番の終了処理を通してカメラと音声を解放
            if (_introduction != null) _introduction.Cancel();
            if (_capture != null) _capture.Cancel();
            if (_instance != null)
            {
                _instance.SetActive(false);
                Destroy(_instance);
            }
            _introduction = null;
            _capture = null;
            _motion = null;
            _agent = null;
            _mode = mode;
            _elapsed = 0f;
            _lunging = false;
            _walkRight = true;
            _result = "";
            var camera = player.MainCamera;
            camera.fieldOfView = 55f;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            camera.transform.SetPositionAndRotation(new Vector3(3f, 1.6f, -4f),
                Quaternion.LookRotation(new Vector3(-3f, -.65f, 6f)));

            // 導入イベントは本番Prefabと同じ入力受付・時間切れ処理を使用
            if (mode == TestMode.Introduction)
            {
                camera.transform.SetPositionAndRotation(new Vector3(0f, 1.65f, 0f), Quaternion.identity);
                _introduction = Instantiate(introductionPrefab);
                _instance = _introduction.gameObject;
                _introduction.Failed += ShowFailure;
                _introduction.Finished += ShowSuccess;
                _introduction.Begin(player);
            }
            else
            {
                var enemy = Instantiate(enemyPrefabs[_enemyIndex], new Vector3(0f, 0f, 2f), Quaternion.Euler(0f, 180f, 0f));
                _instance = enemy.gameObject;
                _agent = enemy.ResolveAgent();
                _motion = enemy.GetComponent<HumanEnemyLocomotion>();
                _agent.Warp(_instance.transform.position);
                _agent.speed = mode == TestMode.Crawl ? .65f : 1.25f;
                if (mode == TestMode.Lunge)
                    camera.transform.SetPositionAndRotation(new Vector3(0f, .35f, 0f), Quaternion.identity);
                if (mode == TestMode.Capture)
                {
                    camera.transform.SetPositionAndRotation(new Vector3(0f, 1.65f, 0f), Quaternion.identity);
                    _capture = enemy.gameObject.AddComponent<HumanEnemyCaptureView>();
                    _capture.Begin(player);
                }
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>往復移動と飛びつきの再生時間を更新</summary>
        private void Update()
        {
            _elapsed += Time.deltaTime;
            if (_capture != null && !_capture.IsPlaying) _result = "GAME OVER - Replay to retry";
            if (_agent == null || !_agent.isOnNavMesh) return;
            if (_mode == TestMode.Walk || _mode == TestMode.Crawl || _mode == TestMode.Chase)
            {
                if (!_agent.pathPending && (!_agent.hasPath || _agent.remainingDistance < .2f))
                {
                    _agent.SetDestination(new Vector3(_walkRight ? 2f : -2f, 0f, 2f));
                    _walkRight = !_walkRight;
                }
            }
        }

        /// <summary>骨格評価前に四つ這いと飛びつきの進行を指定</summary>
        private void LateUpdate()
        {
            if (_motion == null || _mode == TestMode.Capture) return;
            _motion.SetChasePose(_mode == TestMode.Chase, player.MainCamera.transform.position);
            _motion.SetIntroPose(_mode == TestMode.Crawl || _mode == TestMode.Lunge ? 1f : 0f, player.MainCamera);
            if (_mode != TestMode.Lunge || _elapsed < .6f) return;
            if (!_lunging)
            {
                _motion.BeginLunge(player.MainCamera);
                _lunging = true;
            }
            _motion.SetLungeProgress((_elapsed - .6f) / .2f);
        }

        /// <summary>検証中の時間切れを記録し、ゲーム全体を停止せず再試行を可能にする</summary>
        private void ShowFailure() => _result = "TIMEOUT / GAME OVER - Replay to retry";

        /// <summary>起立後のイベント完了を記録</summary>
        private void ShowSuccess() => _result = "STAND UP / ESCAPE COMPLETE";

        /// <summary>敵選択、モーション再生、イベントの進行状況を表示</summary>
        private void OnGUI()
        {
            if (_navMesh == null) return;
            GUILayout.BeginArea(new Rect(Mathf.Max(16f, Screen.width - 356f), 16f, 340f, 430f), GUI.skin.box);
            GUILayout.Label("ENEMY MOTION TEST");
            GUILayout.Label("Enemy prefab");
            for (int index = 0; index < enemyPrefabs.Length; index++)
            {
                if (GUILayout.Button(enemyPrefabs[index].name))
                {
                    _enemyIndex = index;
                    Play(TestMode.Idle);
                }
            }
            GUILayout.Space(8f);
            if (GUILayout.Button("Idle")) Play(TestMode.Idle);
            if (GUILayout.Button("Walk / patrol")) Play(TestMode.Walk);
            GUI.enabled = enemyPrefabs[_enemyIndex].GetComponent<HumanEnemyLocomotion>() != null;
            if (GUILayout.Button("Run / reach toward player")) Play(TestMode.Chase);
            if (GUILayout.Button("Crawl / approach")) Play(TestMode.Crawl);
            if (GUILayout.Button("Lunge at camera")) Play(TestMode.Lunge);
            if (GUILayout.Button("Caught: breath > turn > jumpscare")) Play(TestMode.Capture);
            GUI.enabled = true;
            if (GUILayout.Button("Human event: fall > rush > timeout")) Play(TestMode.Introduction);
            if (GUILayout.Button("Replay current test")) Play(_mode);
            GUILayout.Space(8f);
            GUILayout.Label("Mode: " + _mode);
            if (_introduction != null) GUILayout.Label("Phase: " + _introduction.Phase);
            if (_capture != null) GUILayout.Label("Phase: " + _capture.Phase);
            GUILayout.Label(_result);
            GUILayout.Label("Event: press a key when prompted to stand up.\nWait without input to test the final jumpscare.\nPanel clicks also count as event input.");
            GUILayout.EndArea();
        }

        /// <summary>検証専用のNavMeshとイベント占有を解放</summary>
        private void OnDestroy()
        {
            if (_introduction != null) _introduction.Cancel();
            if (_capture != null) _capture.Cancel();
            if (_navMeshInstance.valid) _navMeshInstance.Remove();
            if (_navMesh != null) Destroy(_navMesh);
        }
    }
}
