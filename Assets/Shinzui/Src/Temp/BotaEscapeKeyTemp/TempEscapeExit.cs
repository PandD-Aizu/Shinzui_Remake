using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Shinzui.View.Interaction;
using Shinzui.Temp.BotamochiStealth;
using Shinzui.View.GenerateTunnel;

namespace Shinzui.Temp
{
    /// <summary>
    /// 脱出口。鍵を持って調べるとクリアになってタイトルに戻る
    /// </summary>
    public class TempEscapeExit : InteractableComponent
    {
        [Header("Escape")]
        [SerializeField] private string titleScenePath = "Assets/Shinzui/Scenes/Title.unity";
        [Tooltip("扉が開いてから「脱出した」を出すまでの秒数")]
        [SerializeField] private float clearMessageDelaySeconds = 1.5f;
        [Tooltip("「脱出した」を出してからタイトルに戻るまでの秒数")]
        [SerializeField] private float returnDelaySeconds = 4.0f;
        private GeneratedTunnelGoal _goal;
        private Transform _goalDoor;

        /// <summary>
        /// ゴール前のドアとして使用し、開錠後はシーン遷移せずゴールを有効にする
        /// </summary>
        /// <param name="goal">開錠するゴール</param>
        public void ConfigureForGoal(GeneratedTunnelGoal goal)
        {
            _goal = goal;
            _goalDoor = GetComponentInChildren<Renderer>().transform;
        }

        public override string DisplayName => TempMessages.EscapeExit.Prompt;
        public override string InteractMessage => TempEscapeKey.HasKey() ? TempMessages.EscapeExit.Unlocked : TempMessages.EscapeExit.Locked;

        public override void ExecuteInteractEffect()
        {
            if (!CanInteract) return;
            base.ExecuteInteractEffect();
            if (TempEscapeKey.HasKey())
            {
                if (_goal != null) StartCoroutine(OpenGoalDoor());
                else StartCoroutine(Escape());
            }
        }

        /// <summary>
        /// 既存の鍵を消費し、ドアを横へ開いてゴールへの通行を許可する
        /// </summary>
        /// <returns>開錠と開扉の処理</returns>
        private IEnumerator OpenGoalDoor()
        {
            CanInteract = false;
            var useKey = TempEscapeKey.UseKeyAsync();
            yield return new WaitUntil(() => useKey.IsCompleted);
            if (useKey.IsFaulted || useKey.IsCanceled)
            {
                CanInteract = true;
                yield break;
            }

            // 扉の幅だけ移動させ、中央の通行部分を開ける
            Vector3 direction = transform.right;
            Vector3 extents = _goalDoor.GetComponent<Renderer>().bounds.extents;
            float distance = 2 * (Mathf.Abs(direction.x) * extents.x + Mathf.Abs(direction.y) * extents.y
                + Mathf.Abs(direction.z) * extents.z) + 0.2f;
            Vector3 start = _goalDoor.position;
            Vector3 end = start + direction * distance;
            float elapsed = 0;
            while (elapsed < 0.75f)
            {
                elapsed += Time.deltaTime;
                _goalDoor.position = Vector3.Lerp(start, end, Mathf.Clamp01(elapsed / 0.75f));
                yield return null;
            }
            _goalDoor.position = end;
            if (_goal != null) _goal.Unlock();
        }

        private IEnumerator Escape()
        {
            CanInteract = false;

            var useKey = TempEscapeKey.UseKeyAsync();
            yield return new WaitUntil(() => useKey.IsCompleted);

            // 演出中に捕まらないように止めておく
            foreach (var enemy in FindObjectsByType<TempPatrolChaseEnemy>())
            {
                enemy.PauseEnemy();
            }

            yield return new WaitForSecondsRealtime(clearMessageDelaySeconds);
            var messageView = FindAnyObjectByType<InteractionMessageView>();
            if (messageView != null)
            {
                messageView.ShowMessage(TempMessages.EscapeExit.Cleared);
            }

            // 次は1階から
            if (TempPastTunnelDirector.Instance != null)
            {
                TempPastTunnelDirector.Instance.ResetProgress();
            }
            TempDocumentCollector.ResetAll();
            TempTalk.ResetChosen();

            yield return new WaitForSecondsRealtime(returnDelaySeconds);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
#if UNITY_EDITOR
            // TitleはBuild Settingsに入ってないのでパスで読む
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(titleScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(System.IO.Path.GetFileNameWithoutExtension(titleScenePath));
#endif
        }
    }
}
