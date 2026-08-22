using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemIntelligence
{
    /// <summary>
    /// Save-affecting Modder Mode actions live in a visually separate side drawer.
    /// The drawer is never created for ordinary players and is destroyed when MCM
    /// disables Modder Mode, so the normal browser keeps its read-only surface.
    /// </summary>
    public static partial class ModMain
    {
        private static GameObject _modderSpawnPanelRoot;
        private static Button _modderSpawnButton;
        private static Image _modderSpawnButtonBackground;
        private static Image _modderSpawnTargetIcon;
        private static TMP_Text _modderSpawnTitleText;
        private static TMP_Text _modderSpawnWarningText;
        private static TMP_Text _modderSpawnButtonText;
        private static TMP_Text _modderSpawnStatusText;
        private static string _modderSpawnStatusItemId = string.Empty;
        private static string _modderSpawnStatusKey = string.Empty;
        private static bool _modderSpawnStatusSuccess;

        private static void RefreshModderSpawnPanel()
        {
            if (!ModderMode || _inspectorRoot == null)
            {
                DestroyModderSpawnPanel();
                return;
            }

            EnsureModderSpawnPanel();
            if (_modderSpawnPanelRoot == null) return;
            _modderSpawnPanelRoot.SetActive(true);
            UpdateModderSpawnPanelPosition();

            bool mission = IsModderSpawnMissionContext();
            bool available = IsModderSpawnTargetAvailable() &&
                !string.IsNullOrEmpty(_inspectorItemId) && IsKnownItemId(_inspectorItemId);
            SetBrowserTextIfChanged(_modderSpawnTitleText, NormalizeModUiText(Ui("ui.modder_spawn_title")));
            SetBrowserTextIfChanged(_modderSpawnWarningText, NormalizeModUiText(Ui("ui.modder_spawn_save_warning")));
            SetBrowserTextIfChanged(_modderSpawnButtonText, NormalizeModUiText(Ui(mission
                ? "ui.modder_spawn_clone"
                : "ui.modder_spawn_cargo")));
            SetBrowserInteractableIfChanged(_modderSpawnButton, available);

            if (_modderSpawnButtonBackground != null)
                _modderSpawnButtonBackground.color = available
                    ? new Color(0.035f, 0.125f, 0.084f, 0.98f)
                    : new Color(0.038f, 0.052f, 0.046f, 0.82f);

            UpdateModderSpawnTargetIcon(mission, available);
            if (!string.Equals(_modderSpawnStatusItemId, _inspectorItemId, System.StringComparison.OrdinalIgnoreCase))
                ResetModderSpawnPanelStatus();
            UpdateModderSpawnStatusText();
        }

        private static void EnsureModderSpawnPanel()
        {
            if (_modderSpawnPanelRoot != null || _inspectorRoot == null || !ModderMode) return;

            _modderSpawnPanelRoot = new GameObject("QII_ModderSpawnDrawer");
            _modderSpawnPanelRoot.transform.SetParent(_inspectorRoot.transform, false);
            RectTransform panelRt = _modderSpawnPanelRoot.AddComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0f, 1f);
            panelRt.anchorMax = new Vector2(0f, 1f);
            panelRt.pivot = new Vector2(1f, 1f);
            panelRt.anchoredPosition = new Vector2(0f, -86f);
            panelRt.sizeDelta = new Vector2(232f, 188f);

            Image panelBackground = _modderSpawnPanelRoot.AddComponent<Image>();
            panelBackground.color = new Color(0.012f, 0.036f, 0.030f, 0.985f);
            panelBackground.raycastTarget = true;
            Outline panelOutline = _modderSpawnPanelRoot.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.80f, 0.67f, 0.30f, 0.88f);
            panelOutline.effectDistance = new Vector2(1f, -1f);

            GameObject title = CreateBrowserText("Title", _modderSpawnPanelRoot.transform,
                new Vector2(12f, -5f), new Vector2(208f, 24f), 14f,
                new Color(0.94f, 0.86f, 0.52f, 1f), FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);
            _modderSpawnTitleText = title.GetComponent<TMP_Text>();
            _modderSpawnTitleText.enableAutoSizing = true;
            _modderSpawnTitleText.fontSizeMin = 12f;
            _modderSpawnTitleText.fontSizeMax = 14f;

            GameObject warning = CreateBrowserText("SaveWarning", _modderSpawnPanelRoot.transform,
                new Vector2(12f, -29f), new Vector2(208f, 40f), 11.5f,
                new Color(0.70f, 0.66f, 0.49f, 1f), FontStyles.Italic,
                TextAlignmentOptions.TopLeft);
            _modderSpawnWarningText = warning.GetComponent<TMP_Text>();
            _modderSpawnWarningText.enableAutoSizing = true;
            _modderSpawnWarningText.fontSizeMin = 10.5f;
            _modderSpawnWarningText.fontSizeMax = 11.5f;
            _modderSpawnWarningText.enableWordWrapping = true;
            _modderSpawnWarningText.overflowMode = TextOverflowModes.Ellipsis;

            GameObject buttonRoot = new GameObject("SpawnOneButton");
            buttonRoot.transform.SetParent(_modderSpawnPanelRoot.transform, false);
            RectTransform buttonRt = buttonRoot.AddComponent<RectTransform>();
            buttonRt.anchorMin = new Vector2(0f, 1f);
            buttonRt.anchorMax = new Vector2(0f, 1f);
            buttonRt.pivot = new Vector2(0f, 1f);
            buttonRt.anchoredPosition = new Vector2(8f, -76f);
            buttonRt.sizeDelta = new Vector2(216f, 56f);

            _modderSpawnButtonBackground = buttonRoot.AddComponent<Image>();
            _modderSpawnButtonBackground.raycastTarget = true;
            Outline buttonOutline = buttonRoot.AddComponent<Outline>();
            buttonOutline.effectColor = new Color(0.45f, 0.84f, 0.60f, 0.92f);
            buttonOutline.effectDistance = new Vector2(1f, -1f);
            _modderSpawnButton = buttonRoot.AddComponent<Button>();
            _modderSpawnButton.targetGraphic = _modderSpawnButtonBackground;
            _modderSpawnButton.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = _modderSpawnButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.76f, 1.00f, 0.80f, 1f);
            colors.pressedColor = new Color(1.00f, 0.88f, 0.54f, 1f);
            colors.disabledColor = new Color(0.50f, 0.55f, 0.51f, 0.72f);
            colors.colorMultiplier = 1f;
            _modderSpawnButton.colors = colors;
            _modderSpawnButton.onClick.AddListener(HandleModderSpawnButton);

            GameObject iconRoot = new GameObject("TargetIcon");
            iconRoot.transform.SetParent(buttonRoot.transform, false);
            RectTransform iconRt = iconRoot.AddComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0f, 0.5f);
            iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.pivot = new Vector2(0f, 0.5f);
            iconRt.anchoredPosition = new Vector2(12f, 0f);
            iconRt.sizeDelta = new Vector2(24f, 24f);
            _modderSpawnTargetIcon = iconRoot.AddComponent<Image>();
            _modderSpawnTargetIcon.preserveAspect = true;
            _modderSpawnTargetIcon.raycastTarget = false;

            GameObject label = CreateBrowserText("Label", buttonRoot.transform,
                new Vector2(44f, 0f), new Vector2(162f, 56f), 13.5f,
                new Color(0.82f, 0.94f, 0.72f, 1f), FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);
            _modderSpawnButtonText = label.GetComponent<TMP_Text>();
            _modderSpawnButtonText.enableAutoSizing = true;
            _modderSpawnButtonText.fontSizeMin = 10.5f;
            _modderSpawnButtonText.fontSizeMax = 13.5f;
            _modderSpawnButtonText.enableWordWrapping = false;

            GameObject status = CreateBrowserText("Status", _modderSpawnPanelRoot.transform,
                new Vector2(12f, -144f), new Vector2(208f, 34f), 11.5f,
                new Color(0.48f, 0.70f, 0.59f, 1f), FontStyles.Normal,
                TextAlignmentOptions.TopLeft);
            _modderSpawnStatusText = status.GetComponent<TMP_Text>();
            _modderSpawnStatusText.enableAutoSizing = true;
            _modderSpawnStatusText.fontSizeMin = 10.5f;
            _modderSpawnStatusText.fontSizeMax = 11.5f;
            _modderSpawnStatusText.enableWordWrapping = true;
            _modderSpawnStatusText.overflowMode = TextOverflowModes.Ellipsis;
        }

        private static void HandleModderSpawnButton()
        {
            string statusKey;
            bool success = TrySpawnCurrentModderItem(out statusKey);
            _modderSpawnStatusItemId = _inspectorItemId ?? string.Empty;
            _modderSpawnStatusKey = statusKey ?? "ui.modder_spawn_failed";
            _modderSpawnStatusSuccess = success;
            UpdateModderSpawnStatusText();
        }

        private static void UpdateModderSpawnPanelPosition()
        {
            if (_modderSpawnPanelRoot == null) return;
            RectTransform panelRt = _modderSpawnPanelRoot.GetComponent<RectTransform>();
            if (panelRt == null) return;
            // The normal right-side inspector opens the drawer to its left. If the
            // inspector moves to the left edge, mirror the drawer so it stays on-screen.
            bool openToLeft = _inspectorRect == null || _inspectorRect.pivot.x >= 0.5f;
            Vector2 edge = openToLeft ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
            panelRt.anchorMin = edge;
            panelRt.anchorMax = edge;
            panelRt.pivot = openToLeft ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            panelRt.anchoredPosition = new Vector2(0f, -86f);
        }

        private static void UpdateModderSpawnTargetIcon(bool mission, bool available)
        {
            if (_modderSpawnTargetIcon == null || _modderSpawnButtonText == null) return;
            bool show = BrowserInterfaceIconLayoutEnabled;
            _modderSpawnTargetIcon.sprite = show
                ? GetBrowserInterfaceIconSprite(mission
                    ? BrowserInterfaceIconKind.Clone
                    : BrowserInterfaceIconKind.Cargo)
                : null;
            _modderSpawnTargetIcon.enabled = _modderSpawnTargetIcon.sprite != null;
            _modderSpawnTargetIcon.color = available
                ? new Color(0.88f, 0.91f, 0.62f, 1f)
                : new Color(0.38f, 0.47f, 0.41f, 0.82f);
            RectTransform labelRt = _modderSpawnButtonText.rectTransform;
            labelRt.anchoredPosition = show ? new Vector2(44f, 0f) : new Vector2(12f, 0f);
            labelRt.sizeDelta = show ? new Vector2(162f, 56f) : new Vector2(192f, 56f);
        }

        private static void UpdateModderSpawnStatusText()
        {
            if (_modderSpawnStatusText == null) return;
            string value = string.IsNullOrEmpty(_modderSpawnStatusKey)
                ? string.Empty
                : NormalizeModUiText(Ui(_modderSpawnStatusKey));
            SetBrowserTextIfChanged(_modderSpawnStatusText, value);
            _modderSpawnStatusText.color = _modderSpawnStatusSuccess
                ? new Color(0.48f, 0.90f, 0.55f, 1f)
                : new Color(0.92f, 0.57f, 0.40f, 1f);
        }

        private static void ResetModderSpawnPanelStatus()
        {
            _modderSpawnStatusItemId = _inspectorItemId ?? string.Empty;
            _modderSpawnStatusKey = string.Empty;
            _modderSpawnStatusSuccess = false;
            UpdateModderSpawnStatusText();
        }

        private static void DestroyModderSpawnPanel()
        {
            if (_modderSpawnPanelRoot != null)
                Object.Destroy(_modderSpawnPanelRoot);
            _modderSpawnPanelRoot = null;
            _modderSpawnButton = null;
            _modderSpawnButtonBackground = null;
            _modderSpawnTargetIcon = null;
            _modderSpawnTitleText = null;
            _modderSpawnWarningText = null;
            _modderSpawnButtonText = null;
            _modderSpawnStatusText = null;
            _modderSpawnStatusItemId = string.Empty;
            _modderSpawnStatusKey = string.Empty;
            _modderSpawnStatusSuccess = false;
        }
    }
}
