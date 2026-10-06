using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Shinzui.View.Settings
{
    /// <summary>Scrollable detail rows and explicit transaction actions for the authored options view</summary>
    public sealed class GraphicsOptionsView : MonoBehaviour
    {
        private RectTransform _content;
        private TMP_FontAsset _font;
        private readonly Dictionary<string, TMP_Text> _values = new();
        private readonly List<UnityEngine.UI.Button> _editButtons = new();
        private GameObject _confirmation;
        private TMP_Text _countdown;
        private TMP_Text _capability;
        private UnityEngine.UI.Button _apply;
        private UnityEngine.UI.Button _keep;
        private RectTransform _rootPanel;
        public event Action ApplyRequested;
        public event Action CancelRequested;
        public event Action DefaultsRequested;
        public event Action ConfirmRequested;
        public event Action RevertRequested;
        public event Action<float, bool> TickRequested;

        /// <summary>Describe the actual graphics device and applied HDRP configuration</summary>
        /// <returns>Runtime capability report</returns>
        public string GetCapabilityReport() => Infrastructure.Services.HdrpGraphicsRuntime.CapabilityReport();

        /// <summary>Extend the graphics panel while retaining authored navigation and controls</summary>
        /// <param name="panel">Existing graphics panel</param>
        /// <param name="font">Existing title font</param>
        public void Build(RectTransform panel, TMP_FontAsset font)
        {
            _font = font;
            var title = FindAnyObjectByType<Shinzui.Src.Title.ButtonController>(FindObjectsInactive.Include);
            _rootPanel = title != null ? (RectTransform)title.OptionPanel.transform : (RectTransform)panel.GetComponentInParent<Canvas>().transform;
            panel.SetParent(_rootPanel, false);
            var oldLayout = panel.GetComponent<UnityEngine.UI.LayoutGroup>();
            if (oldLayout != null) oldLayout.enabled = false;
            var oldFit = panel.GetComponent<UnityEngine.UI.ContentSizeFitter>();
            if (oldFit != null) oldFit.enabled = false;
            panel.anchorMin = new Vector2(.25f, .14f);
            panel.anchorMax = new Vector2(.94f, .82f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            foreach (Transform child in panel) child.gameObject.SetActive(false);
            var scroll = Rect("GraphicsDetails", panel);
            var element = scroll.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            element.ignoreLayout = true;
            Stretch(scroll, Vector2.zero, Vector2.zero);
            var viewport = Rect("Viewport", scroll);
            Stretch(viewport, Vector2.zero, Vector2.zero);
            viewport.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0.03f, 0.035f, 0.04f, 0.97f);
            viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = true;
            _content = Rect("Content", viewport);
            _content.anchorMin = new Vector2(0, 1);
            _content.anchorMax = Vector2.one;
            _content.pivot = new Vector2(0.5f, 1);
            _content.sizeDelta = Vector2.zero;
            var layout = _content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 14, 14);
            layout.spacing = 7;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            _content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            var scroller = scroll.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroller.viewport = viewport;
            scroller.content = _content;
            scroller.horizontal = false;
            scroller.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroller.scrollSensitivity = 45;
            _capability = Text(Rect("Capability", _content), "", 21);
            _capability.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 200;
            _capability.textWrappingMode = TextWrappingModes.Normal;

            // Global actions stay reachable from all authored tabs
            var actions = Rect("SettingsActions", _rootPanel);
            actions.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
            actions.anchorMin = new Vector2(0.25f, .055f);
            actions.anchorMax = new Vector2(0.94f, .055f);
            actions.pivot = new Vector2(0.5f, 0);
            actions.anchoredPosition = new Vector2(0, 12);
            actions.sizeDelta = new Vector2(0, 58);
            var horizontal = actions.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            horizontal.spacing = 14;
            horizontal.childForceExpandWidth = true;
            horizontal.childControlWidth = true;
            horizontal.childControlHeight = true;
            _apply = Button(actions, "Apply", () => ApplyRequested?.Invoke());
            _editButtons.Add(Button(actions, "Cancel", () => CancelRequested?.Invoke()));
            _editButtons.Add(Button(actions, "Defaults", () => DefaultsRequested?.Invoke()));
            BuildConfirmation();
        }

        /// <summary>Create one keyboard-accessible choice row</summary>
        /// <param name="key">Stable row identifier</param>
        /// <param name="label">Visible setting name</param>
        /// <param name="move">Direction callback</param>
        public void AddChoice(string key, string label, Action<int> move)
        {
            var row = Rect(key, _content);
            row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 50;
            row.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0.10f, 0.11f, 0.12f, 0.9f);
            var labelRect = Rect("Label", row);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = new Vector2(0.45f, 1);
            labelRect.offsetMin = new Vector2(12, 0);
            labelRect.offsetMax = Vector2.zero;
            Text(labelRect, label, 22);
            var choices = Rect("Choice", row);
            choices.anchorMin = new Vector2(0.45f, 0);
            choices.anchorMax = Vector2.one;
            choices.offsetMin = choices.offsetMax = Vector2.zero;
            var layout = choices.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            var previous = Button(choices, "<", () => move(-1));
            previous.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 42;
            _editButtons.Add(previous);
            var valueRect = Rect("Value", choices);
            var value = Text(valueRect, "", 22);
            value.alignment = TextAlignmentOptions.Center;
            value.enableAutoSizing = true;
            value.fontSizeMin = 14;
            value.fontSizeMax = 22;
            value.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1;
            _values.Add(key, value);
            var next = Button(choices, ">", () => move(1));
            next.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 42;
            _editButtons.Add(next);
        }

        /// <summary>Update row text without dispatching input events</summary>
        /// <param name="key">Row identifier</param>
        /// <param name="value">Effective draft label</param>
        public void SetChoice(string key, string value) { if (_values.TryGetValue(key, out var text)) text.text = value; }

        /// <summary>Show requested and effective rendering capabilities separately</summary>
        /// <param name="text">Runtime capability report</param>
        public void SetCapability(string text) { if (_capability != null) _capability.text = text; }

        /// <summary>Block editing while a candidate display mode awaits confirmation</summary>
        /// <param name="pending">Whether confirmation is pending</param>
        /// <param name="seconds">Remaining unscaled seconds</param>
        public void SetConfirmation(bool pending, float seconds)
        {
            if (_confirmation == null) return;
            bool changed = _confirmation.activeSelf != pending;
            _confirmation.SetActive(pending);
            _countdown.text = $"Keep these display settings?\nReverting in {Mathf.CeilToInt(seconds)} seconds";
            _apply.interactable = !pending;
            foreach (var button in _editButtons) button.interactable = !pending;
            if (changed && UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(pending ? _keep.gameObject : _apply.gameObject);
        }

        /// <summary>Create a blocking confirmation panel above the options hierarchy</summary>
        private void BuildConfirmation()
        {
            var root = Rect("DisplayConfirmation", _rootPanel);
            root.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
            Stretch(root, Vector2.zero, Vector2.zero);
            root.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0.015f, 0.02f, 0.025f, 0.98f);
            var box = Rect("Message", root);
            box.anchorMin = new Vector2(0.15f, 0.48f);
            box.anchorMax = new Vector2(0.85f, 0.70f);
            box.offsetMin = box.offsetMax = Vector2.zero;
            _countdown = Text(box, "", 30);
            _countdown.alignment = TextAlignmentOptions.Center;
            var actions = Rect("Actions", root);
            actions.anchorMin = new Vector2(0.25f, 0.32f);
            actions.anchorMax = new Vector2(0.75f, 0.42f);
            actions.offsetMin = actions.offsetMax = Vector2.zero;
            var layout = actions.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 20;
            layout.childControlWidth = layout.childControlHeight = true;
            _keep = Button(actions, "Keep changes", () => ConfirmRequested?.Invoke());
            Button(actions, "Revert", () => RevertRequested?.Invoke());
            _confirmation = root.gameObject;
            _confirmation.SetActive(false);
        }

        /// <summary>Drive the rollback deadline even while gameplay is paused</summary>
        private void Update() => TickRequested?.Invoke(Time.unscaledDeltaTime, UnityEngine.Application.isFocused);

        /// <summary>Rollback immediately if focus is lost</summary>
        /// <param name="focus">Current application focus</param>
        private void OnApplicationFocus(bool focus) { if (!focus) TickRequested?.Invoke(0, false); }

        /// <summary>Cancel previews when the containing options screen closes</summary>
        private void OnDisable() => CancelRequested?.Invoke();

        /// <summary>Create a child rectangle</summary>
        /// <param name="name">Object name</param>
        /// <param name="parent">Owning rectangle</param>
        /// <returns>The new rectangle</returns>
        private static RectTransform Rect(string name, RectTransform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>Fill a parent with explicit inset offsets</summary>
        /// <param name="rect">Target rectangle</param>
        /// <param name="min">Lower inset</param>
        /// <param name="max">Upper inset</param>
        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = min; rect.offsetMax = max; }

        /// <summary>Create readable title-styled text</summary>
        /// <param name="rect">Text rectangle</param>
        /// <param name="value">Initial text</param>
        /// <param name="size">Font size</param>
        /// <returns>The text component</returns>
        private TMP_Text Text(RectTransform rect, string value, float size)
        {
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = _font;
            text.text = value;
            text.fontSize = size;
            text.color = new Color(0.88f, 0.9f, 0.91f);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>Create a selectable button with explicit visual states</summary>
        /// <param name="parent">Owning container</param>
        /// <param name="label">Button text</param>
        /// <param name="clicked">Requested action</param>
        /// <returns>The button</returns>
        private UnityEngine.UI.Button Button(RectTransform parent, string label, Action clicked)
        {
            var rect = Rect(label, parent);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.22f, 0.24f, 0.25f);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            rect.gameObject.AddComponent<SettingsScrollSelection>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.7f, 0.76f, 0.78f);
            colors.selectedColor = new Color(0.68f, 0.75f, 0.8f);
            button.colors = colors;
            button.onClick.AddListener(() => clicked());
            var textRect = Rect("Text", rect);
            Stretch(textRect, new Vector2(6, 0), new Vector2(-6, 0));
            var text = Text(textRect, label, 22);
            text.alignment = TextAlignmentOptions.Center;
            return button;
        }
    }

    /// <summary>Keep keyboard/controller-selected graphics choices visible in the scrolling panel</summary>
    public sealed class SettingsScrollSelection : MonoBehaviour, UnityEngine.EventSystems.ISelectHandler
    {
        /// <summary>Scroll the nearest options viewport to reveal this selected control</summary>
        /// <param name="eventData">Selection event</param>
        public void OnSelect(UnityEngine.EventSystems.BaseEventData eventData)
        {
            var scroll = GetComponentInParent<UnityEngine.UI.ScrollRect>();
            if (scroll == null) return;
            Canvas.ForceUpdateCanvases();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, (RectTransform)transform);
            var area = scroll.viewport.rect;
            float offset = bounds.min.y < area.yMin ? area.yMin - bounds.min.y : bounds.max.y > area.yMax ? area.yMax - bounds.max.y : 0;
            var position = scroll.content.anchoredPosition;
            position.y = Mathf.Clamp(position.y + offset, 0, Mathf.Max(0, scroll.content.rect.height - area.height));
            scroll.content.anchoredPosition = position;
        }
    }
}
