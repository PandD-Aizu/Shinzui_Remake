if (!UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Play Mode is required.");
var scope = UnityEngine.Object.FindFirstObjectByType<Shinzui.DI.TitleLifetimeScope>();
if (scope == null || scope.Container == null) throw new System.InvalidOperationException("Title scope did not build.");
var view = (Shinzui.Src.Title.ButtonController)scope.Container.Resolve(typeof(Shinzui.Src.Title.ButtonController));
var options = (Shinzui.View.Settings.OptionWindowView)scope.Container.Resolve(typeof(Shinzui.View.Settings.OptionWindowView));
var presenter = (Shinzui.Presentation.Title.TitlePresenter)scope.Container.Resolve(typeof(Shinzui.Presentation.Title.TitlePresenter));
if (!view.TitlePanel.activeSelf || view.OptionPanel.activeSelf || view.CreditsPanel.activeSelf)
    throw new System.InvalidOperationException("Unexpected initial panel state.");
var buttons = new[] { "ControllButton", "CameraButton", "GameSettingButton", "GraphicsButton", "AudioButton", "LanguageButton", "AccessibilityButton" };
var panels = new[] { "Control", "Camera", "GameSetting", "Graphics", "Audio", "Language", "Accessibility" };
var selectedTabs = 0;
for (var cycle = 0; cycle < 2; cycle++)
{
    view.TitlePanel.transform.Find("OptionButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
    if (!view.OptionPanel.activeSelf || view.TitlePanel.activeSelf) throw new System.InvalidOperationException("Options did not open.");
    for (var i = 0; i < buttons.Length; i++)
    {
        options.CategoryTabParent.Find(buttons[i]).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        for (var j = 0; j < panels.Length; j++)
            if (options.CategoryPanelParent.Find(panels[j]).gameObject.activeSelf != (i == j))
                throw new System.InvalidOperationException("Wrong option panel after " + buttons[i]);
        selectedTabs++;
    }
    options.transform.Find("BackButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
    if (view.OptionPanel.activeSelf || !view.TitlePanel.activeSelf) throw new System.InvalidOperationException("Options did not close.");
    view.TitlePanel.transform.Find("CreditButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
    if (!view.CreditsPanel.activeSelf) throw new System.InvalidOperationException("Credits did not open.");
    presenter.Tick(0.25f);
    view.SkipCredits();
    presenter.Tick(2f);
    if (view.CreditsPanel.activeSelf || !view.TitlePanel.activeSelf) throw new System.InvalidOperationException("Credits did not close.");
}
return new { titleScopeBuilt = true, optionCycles = 2, selectedTabs, creditCycles = 2, titleVisible = view.TitlePanel.activeSelf, cameraRange = new[] { options.ControllerSpeedSlider.minValue, options.ControllerSpeedSlider.maxValue } };
