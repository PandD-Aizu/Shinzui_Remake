using System.Collections;
using LitMotion;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FMODUnity;

namespace Shinzui.Src.Title
{
    public class CreditScreenPresenter : MonoBehaviour
    {
        [Header("References")] [SerializeField]
        private CreditScreenView view;

        [SerializeField] private StudioEventEmitter eventEmitter;
        [SerializeField] private CreditData creditData;

        [SerializeField, Tooltip("親のクレジットパネル（終了時に非表示にする）")]
        private GameObject creditsPanel;

        [SerializeField, Tooltip("Escでスキップ案内テキストのCanvasGroup（パネル外に置いた場合に指定）")]
        private CanvasGroup skipHintCanvasGroup;

        [Header("Scene Transition")] [SerializeField]
        private string nextSceneName = "";

        [SerializeField] private bool loopCredits = false;
        [SerializeField] private bool allowSkip = true;
        [SerializeField] private KeyCode skipKey = KeyCode.Escape;

        [Header("Start Options")] [SerializeField]
        private bool autoStart = false;

        [Header("Fade")] [SerializeField, Tooltip("クレジット終了時のフェードアウト秒数")]
        private float fadeOutDuration = 0.5f;

        private CreditScreenModel model;
        private Coroutine endDelayCoroutine;
        private Coroutine initRoutine;

        private MotionHandle _skipHintFadeMotion;
        private MotionHandle _creditsPanelFadeMotion;

        private void Awake()
        {
            if (view == null)
            {
                view = GetComponent<CreditScreenView>();

                if (view == null)
                {
                    view = GetComponentInChildren<CreditScreenView>(true);
                }
            }

            if (creditsPanel == null && transform.parent != null)
            {
                creditsPanel = transform.parent.gameObject;
            }

            model = new CreditScreenModel();
        }

        private void OnEnable()
        {
            if (autoStart)
            {
                StartCreditsInternal();
            }
        }

        private void OnDisable()
        {
            eventEmitter.Stop();

            CancelFadeMotions();
        }

        public void StartCreditsForButton()
        {
            StartCreditsInternal();
        }

        private void StartCreditsInternal()
        {
            if (initRoutine != null)
            {
                StopCoroutine(initRoutine);
            }

            initRoutine = StartCoroutine(InitializeRoutine());
        }

        private IEnumerator InitializeRoutine()
        {
            if (creditData == null)
            {
                Debug.LogError("CreditData is not assigned!");
                yield break;
            }

            if (view == null)
            {
                view = GetComponent<CreditScreenView>()
                    ?? GetComponentInChildren<CreditScreenView>(true);

                if (view == null)
                {
                    Debug.LogError("CreditScreenView is not found!");
                    yield break;
                }
            }

            model.Initialize(creditData);
            view.SetCreditText(creditData);

            yield return new WaitForEndOfFrame();
            Canvas.ForceUpdateCanvases();

            view.ResetScrollPosition();
            view.EnableInteraction(false);

            model.ResetScroll();
            model.StartScrolling();

            eventEmitter.Play();

            initRoutine = null;
        }

        private void Update()
        {
            if (model == null || !model.IsScrolling) return;

            model.UpdateScrollPosition(Time.deltaTime);

            float maxScroll = view.GetMaxScrollPosition();

            if (maxScroll > 0f)
            {
                float normalizedPosition =
                    model.CurrentScrollPosition / maxScroll;

                view.UpdateScrollPosition(normalizedPosition);

                if (model.HasReachedEnd(maxScroll))
                {
                    OnCreditsEnd();
                }
            }

            if (allowSkip && Input.GetKeyDown(skipKey))
            {
                SkipCredits();
            }
        }

        private void OnCreditsEnd()
        {
            model.StopScrolling();

            if (endDelayCoroutine != null)
            {
                StopCoroutine(endDelayCoroutine);
            }

            endDelayCoroutine = StartCoroutine(EndDelayCoroutine());
        }

        private IEnumerator EndDelayCoroutine()
        {
            yield return new WaitForSeconds(creditData.delayAfterEnd);

            eventEmitter.Stop();

            if (loopCredits)
            {
                RestartCredits();
            }
            else if (!string.IsNullOrEmpty(nextSceneName))
            {
                LoadNextScene();
            }
            else
            {
                DeactivateCreditsPanelWithFade();
            }
        }

        private void RestartCredits()
        {
            model.ResetScroll();
            view.ResetScrollPosition();
            model.StartScrolling();

            eventEmitter.Play();
        }

        private void SkipCredits()
        {
            if (endDelayCoroutine != null)
            {
                StopCoroutine(endDelayCoroutine);
            }

            model.StopScrolling();

            eventEmitter.Stop();

            if (!string.IsNullOrEmpty(nextSceneName))
            {
                LoadNextScene();
            }
            else if (loopCredits)
            {
                RestartCredits();
            }
            else
            {
                DeactivateCreditsPanelWithFade();
            }
        }

        private void LoadNextScene()
        {
            if (!string.IsNullOrEmpty(nextSceneName))
            {
                SceneManager.LoadScene(nextSceneName);
            }
            else
            {
                DeactivateCreditsPanelWithFade();
            }
        }

        // CanvasGroupで全体（テキスト含む）をフェードアウトし、案内も一緒にフェード
        private void DeactivateCreditsPanelWithFade()
        {
            // 案内テキストがパネル外なら個別フェード
            if (skipHintCanvasGroup != null && skipHintCanvasGroup.gameObject.activeInHierarchy)
            {
                CancelSkipHintFade();

                skipHintCanvasGroup.interactable = false;
                skipHintCanvasGroup.blocksRaycasts = false;

                _skipHintFadeMotion = LMotion.Create(
                                                 skipHintCanvasGroup.alpha,
                                                 0f,
                                                 fadeOutDuration
                                             )
                                             .Bind(
                                                 skipHintCanvasGroup,
                                                 (alpha, canvasGroup) =>
                                                 {
                                                     canvasGroup.alpha = alpha;

                                                     if (alpha <= 0f)
                                                     {
                                                         canvasGroup.gameObject.SetActive(false);
                                                     }
                                                 }
                                             );
            }

            if (creditsPanel == null)
            {
                if (transform.parent != null)
                {
                    transform.parent.gameObject.SetActive(false);
                }

                return;
            }

            if (!creditsPanel.activeSelf)
            {
                creditsPanel.SetActive(true);
            }

            var cg = creditsPanel.GetComponent<CanvasGroup>();

            if (cg == null)
            {
                cg = creditsPanel.AddComponent<CanvasGroup>();
            }

            CancelCreditsPanelFade();

            cg.alpha = 1f;
            cg.interactable = false;
            cg.blocksRaycasts = false;

            _creditsPanelFadeMotion = LMotion.Create(
                                                 1f,
                                                 0f,
                                                 fadeOutDuration
                                             )
                                             .Bind(
                                                 cg,
                                                 (alpha, canvasGroup) =>
                                                 {
                                                     canvasGroup.alpha = alpha;

                                                     if (alpha <= 0f)
                                                     {
                                                         creditsPanel.SetActive(false);
                                                     }
                                                 }
                                             );
        }

        private void CancelSkipHintFade()
        {
            if (_skipHintFadeMotion.IsActive())
            {
                _skipHintFadeMotion.Cancel();
            }
        }

        private void CancelCreditsPanelFade()
        {
            if (_creditsPanelFadeMotion.IsActive())
            {
                _creditsPanelFadeMotion.Cancel();
            }
        }

        private void CancelFadeMotions()
        {
            CancelSkipHintFade();
            CancelCreditsPanelFade();
        }

        public void SetCreditData(CreditData data)
        {
            creditData = data;
            StartCreditsInternal();
        }

        public void PauseCredits()
        {
            model?.StopScrolling();
        }

        public void ResumeCredits()
        {
            model?.StartScrolling();
        }
    }
}