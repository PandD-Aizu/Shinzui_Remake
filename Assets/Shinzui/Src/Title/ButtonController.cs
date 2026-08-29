using LitMotion;
using LitMotion.Extensions;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Shinzui.Src.Title
{
    public class ButtonController : MonoBehaviour
    {
        [Header("依存関係")]
        [SerializeField, Tooltip("タイトルのフェードコントローラ")] private FadeController titleFadeController;
        //[SerializeField, Tooltip("オプションのフェードコントローラ")] private FadeController optionFadeController;

        [Header("STARTボタン")]
        [SerializeField] private string gameSceneAddress;
        [SerializeField, Tooltip("画面全体を覆う画像")] private Image screenBackgroundImage;
        [SerializeField, Tooltip("フェード秒数")] private float fadeDuration = 0.5f;

        /*[Header("CREDITSボタン")]
        [SerializeField] private CreditScreenPresenter creditScreenPresenter;
        [SerializeField, Tooltip("画面全体を覆うパネル")] private GameObject creditsPanel;
        [SerializeField, Tooltip("フェード秒数")] private float creditsFadeDuration = 0.5f;
        [SerializeField, Tooltip("Escでスキップ案内テキストのCanvasGroup（パネル外に置いた場合に指定）")]
        private CanvasGroup creditsSkipHintCanvasGroup;*/

        [Header("パネル")]
        [SerializeField] private GameObject titlePanel;
        [SerializeField] private GameObject optionPanel;

        /*[Header("オプションに表示するオブジェクトグループ")]
        [SerializeField] private GameObject controlOptionObject;
        [SerializeField] private GameObject cameraOptionObject;
        [SerializeField] private GameObject gameSettingOptionObject;
        [SerializeField] private GameObject graphicOptionObject;
        [SerializeField] private GameObject audioOptionObject;
        [SerializeField] private GameObject languageOptionObject;
        [SerializeField] private GameObject accessibilityOptionObject;

        private GameObject currentOptionObject;*/

        private MotionHandle _screenFadeMotion;
        private MotionHandle _creditsFadeMotion;
        private MotionHandle _creditsSkipHintFadeMotion;

        private void Start()
        {
            //currentOptionObject = controlOptionObject;
            optionPanel.SetActive(false);
            screenBackgroundImage.gameObject.SetActive(false);
        }

        public void StartGame()
        {
            screenBackgroundImage.gameObject.SetActive(true);

            var c = screenBackgroundImage.color;
            c.a = 0f;
            screenBackgroundImage.color = c;

            var target = c;
            target.a = 1f;

            _screenFadeMotion = LMotion.Create(
                                           c.a,
                                           target.a,
                                           fadeDuration
                                       )
                                       .WithOnComplete(() =>
                                       {
                                           AsyncOperationHandle handle =
                                               Addressables.LoadSceneAsync(
                                                   gameSceneAddress,
                                                   LoadSceneMode.Single
                                               );

                                           handle.Completed += OnSceneLoaded;
                                       })
                                       .Bind(
                                           screenBackgroundImage,
                                           (alpha, image) =>
                                           {
                                               var color = image.color;
                                               color.a = alpha;
                                               image.color = color;
                                           }
                                       );
        }

        // CREDITSのフェードイン（案内テキストも一緒にフェード）
        /*public void StartCredits()
        {
            creditsPanel.SetActive(true);

            var cg = creditsPanel.GetComponent<CanvasGroup>();

            if (cg == null)
            {
                cg = creditsPanel.AddComponent<CanvasGroup>();
            }

            // 前回のフェードを停止
            if (_creditsFadeMotion.IsActive())
            {
                _creditsFadeMotion.Cancel();
            }

            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = true;

            // 案内テキストがパネル外にある場合のみ個別フェード
            if (creditsSkipHintCanvasGroup != null)
            {
                creditsSkipHintCanvasGroup.gameObject.SetActive(true);

                if (_creditsSkipHintFadeMotion.IsActive())
                {
                    _creditsSkipHintFadeMotion.Cancel();
                }

                creditsSkipHintCanvasGroup.alpha = 0f;
                creditsSkipHintCanvasGroup.interactable = false;
                creditsSkipHintCanvasGroup.blocksRaycasts = false;

                _creditsSkipHintFadeMotion = LMotion.Create(
                                                        0f,
                                                        1f,
                                                        creditsFadeDuration
                                                    )
                                                    .Bind(
                                                        creditsSkipHintCanvasGroup,
                                                        (alpha, canvasGroup) =>
                                                        {
                                                            canvasGroup.alpha = alpha;
                                                        }
                                                    );
            }

            creditScreenPresenter.StartCreditsForButton();

            _creditsFadeMotion = LMotion.Create(
                                            0f,
                                            1f,
                                            creditsFadeDuration
                                        )
                                        .WithOnComplete(() =>
                                        {
                                            cg.interactable = true;
                                        })
                                        .Bind(
                                            cg,
                                            (alpha, canvasGroup) =>
                                            {
                                                canvasGroup.alpha = alpha;
                                            }
                                        );
        }

        public void OpenOptions()
        {
            optionPanel.SetActive(true);
            titlePanel.SetActive(false);
            optionFadeController.Play();
        }

        public void CloseOptions()
        {
            optionPanel.SetActive(false);
            titlePanel.SetActive(true);
            titleFadeController.Play();
        }*/

        public void QuitGame()
        { 
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }

        private void OnSceneLoaded(AsyncOperationHandle handle)
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                Debug.Log("Load Complete");
            }
            else
            {
                Debug.LogError("Load Failed");
            }
        }

        /*public void AlignControlOption() => ShowOptionObject(controlOptionObject);
        public void AlignCameraOption() => ShowOptionObject(cameraOptionObject);
        public void AlignGameSettingOption() => ShowOptionObject(gameSettingOptionObject);
        public void AlignGraphicOption() => ShowOptionObject(graphicOptionObject);
        public void AlignAudioOption() => ShowOptionObject(audioOptionObject);
        public void AlignLanguageOption() => ShowOptionObject(languageOptionObject);
        public void AlignAccessibilityOption() => ShowOptionObject(accessibilityOptionObject);

        private void ShowOptionObject(GameObject targetObject)
        {
            currentOptionObject?.SetActive(false);
            targetObject.SetActive(true);
            currentOptionObject = targetObject;
        }*/

        private void OnDisable()
        {
            if (_screenFadeMotion.IsActive())
            {
                _screenFadeMotion.Cancel();
            }

            if (_creditsFadeMotion.IsActive())
            {
                _creditsFadeMotion.Cancel();
            }

            if (_creditsSkipHintFadeMotion.IsActive())
            {
                _creditsSkipHintFadeMotion.Cancel();
            }
        }
    }
}
