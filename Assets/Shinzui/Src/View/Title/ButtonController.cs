using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Shinzui.Src.Title
{
    /// <summary>Passive title view. Existing names preserve serialized button UnityEvents.</summary>
    public class ButtonController : MonoBehaviour
    {
        [SerializeField] private Image screenBackgroundImage;
        [SerializeField, Min(0f)] private float fadeDuration = 0.5f;
        [SerializeField] private GameObject creditsPanel;
        [SerializeField, Min(0f)] private float creditsFadeDuration = 0.5f;
        [SerializeField] private CanvasGroup creditsSkipHintCanvasGroup;
        [SerializeField] private GameObject titlePanel;
        [SerializeField] private GameObject optionPanel;

        [Header("Option tab labels")]
        [SerializeField] private TextMeshProUGUI controlOptionText;
        [SerializeField] private TextMeshProUGUI cameraOptionText;
        [SerializeField] private TextMeshProUGUI gameSettingOptionText;
        [SerializeField] private TextMeshProUGUI graphicOptionText;
        [SerializeField] private TextMeshProUGUI audioOptionText;
        [SerializeField] private TextMeshProUGUI languageOptionText;
        [SerializeField] private TextMeshProUGUI accessibilityOptionText;

        public event Action StartGameRequested;
        public event Action CreditsRequested;
        public event Action OpenOptionsRequested;
        public event Action CloseOptionsRequested;
        public event Action QuitRequested;
        public event Action SkipRequested;
        public event Action<int> OptionSelected;

        public Image ScreenBackgroundImage => screenBackgroundImage;
        public float FadeDuration => fadeDuration;
        public GameObject CreditsPanel => creditsPanel;
        public float CreditsFadeDuration => creditsFadeDuration;
        public CanvasGroup CreditsSkipHintCanvasGroup => creditsSkipHintCanvasGroup;
        public GameObject TitlePanel => titlePanel;
        public GameObject OptionPanel => optionPanel;

        public TMP_Text GetOptionLabel(int index) => index switch
        {
            0 => controlOptionText,
            1 => cameraOptionText,
            2 => gameSettingOptionText,
            3 => graphicOptionText,
            4 => audioOptionText,
            5 => languageOptionText,
            6 => accessibilityOptionText,
            _ => null
        };

        public void StartGame() => StartGameRequested?.Invoke();
        public void StartCredits() => CreditsRequested?.Invoke();
        public void OpenOptions() => OpenOptionsRequested?.Invoke();
        public void CloseOptions() => CloseOptionsRequested?.Invoke();
        public void QuitGame() => QuitRequested?.Invoke();
        public void SkipCredits() => SkipRequested?.Invoke();
        public void AlignControlOption() => OptionSelected?.Invoke(0);
        public void AlignCameraOption() => OptionSelected?.Invoke(1);
        public void AlignGameSettingOption() => OptionSelected?.Invoke(2);
        public void AlignGraphicOption() => OptionSelected?.Invoke(3);
        public void AlignAudioOption() => OptionSelected?.Invoke(4);
        public void AlignLanguageOption() => OptionSelected?.Invoke(5);
        public void AlignAccessibilityOption() => OptionSelected?.Invoke(6);

        private void Update()
        {
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
                SkipCredits();
        }
    }
}
