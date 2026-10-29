using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Shinzui.View.Interaction;
using Shinzui.Temp.BotamochiStealth;

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

        public override string DisplayName => TempMessages.EscapeExit.Prompt;
        public override string InteractMessage => TempEscapeKey.HasKey() ? TempMessages.EscapeExit.Unlocked : TempMessages.EscapeExit.Locked;

        public override void ExecuteInteractEffect()
        {
            base.ExecuteInteractEffect();
            if (TempEscapeKey.HasKey())
            {
                StartCoroutine(Escape());
            }
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
