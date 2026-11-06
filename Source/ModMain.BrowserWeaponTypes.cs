using System;
using System.Collections.Generic;
using MGSC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static Button _browserWeaponTypeButton;
        private static TMP_Text _browserWeaponTypeText;
        private static GameObject _browserWeaponTypeChoices;
        private static readonly List<Button> BrowserWeaponTypeButtons = new List<Button>();
        private static readonly List<TMP_Text> BrowserWeaponTypeLabels = new List<TMP_Text>();
        private static readonly List<string> BrowserWeaponTypeIds = new List<string>();

        private static string BrowserWeaponTypeLabel(string id)
        {
            return string.IsNullOrEmpty(id) ? GetBrowserCatalogCategoryLabel(0, true) : LocalizeWeaponClass(id);
        }

        private static void CreateBrowserWeaponTypeUi()
        {
            _browserWeaponTypeButton = CreateBrowserCatalogControlButton(
                "WeaponType", new Vector2(5f, -145f), new Vector2(270f, 28f), 11f,
                ToggleBrowserWeaponTypeChoices, out _browserWeaponTypeText);
            _browserWeaponTypeButton.gameObject.SetActive(false);
            // Choices are created on first use, above the pooled item rows.
            _browserWeaponTypeChoices = null;
            BrowserWeaponTypeButtons.Clear();
            BrowserWeaponTypeLabels.Clear();
            BrowserWeaponTypeIds.Clear();
        }

        private static void CloseBrowserWeaponTypeChoices()
        {
            if (_browserWeaponTypeChoices != null) _browserWeaponTypeChoices.SetActive(false);
        }

        private static void SelectBrowserWeaponType(string classId)
        {
            _browserCatalogWeaponClass = classId;
            _browserCatalogScrollOffset = 0;
            CloseBrowserWeaponTypeChoices();
            RefreshBrowserCatalog();
        }

        private static void ToggleBrowserWeaponTypeChoices()
        {
            if (_browserWeaponTypeChoices != null && _browserWeaponTypeChoices.activeSelf)
            {
                CloseBrowserWeaponTypeChoices();
                return;
            }
            if (_browserWeaponTypeChoices == null)
            {
                _browserWeaponTypeChoices = new GameObject("WeaponTypeChoices", typeof(RectTransform), typeof(Image));
                _browserWeaponTypeChoices.transform.SetParent(_browserCatalogPanel.transform, false);
                Image background = _browserWeaponTypeChoices.GetComponent<Image>();
                background.color = new Color(0.012f, 0.055f, 0.042f, 1f);
                background.raycastTarget = true;
                BrowserWeaponTypeIds.Add(string.Empty);
                foreach (WeaponClass value in Enum.GetValues(typeof(WeaponClass)))
                    if (value != WeaponClass.None) BrowserWeaponTypeIds.Add(value.ToString());
                BrowserWeaponTypeIds.Add("ThrowingWeapon");
                for (int i = 0; i < BrowserWeaponTypeIds.Count; i++)
                {
                    string capturedId = BrowserWeaponTypeIds[i];
                    TMP_Text text;
                    Button button = CreateBrowserCatalogControlButton("WeaponType_" + i,
                        new Vector2(5f + (i % 3) * 228f, -(5f + (i / 3) * 30f)),
                        new Vector2(224f, 28f), 10f,
                        delegate { SelectBrowserWeaponType(capturedId); }, out text);
                    button.transform.SetParent(_browserWeaponTypeChoices.transform, false);
                    BrowserWeaponTypeButtons.Add(button);
                    BrowserWeaponTypeLabels.Add(text);
                }
            }
            RectTransform panel = _browserCatalogPanel.GetComponent<RectTransform>();
            RectTransform choices = _browserWeaponTypeChoices.GetComponent<RectTransform>();
            choices.anchorMin = choices.anchorMax = choices.pivot = new Vector2(0f, 1f);
            float height = 10f + (float)Math.Ceiling(BrowserWeaponTypeIds.Count / 3d) * 30f;
            choices.anchoredPosition = new Vector2(5f, -Math.Min(175f, Math.Max(5f, panel.sizeDelta.y - height - 5f)));
            choices.sizeDelta = new Vector2(690f, height);
            for (int i = 0; i < BrowserWeaponTypeIds.Count; i++)
                SetBrowserCatalogToggleStyle(BrowserWeaponTypeButtons[i], BrowserWeaponTypeLabels[i],
                    BrowserWeaponTypeIds[i] == _browserCatalogWeaponClass, BrowserWeaponTypeLabel(BrowserWeaponTypeIds[i]));
            _browserWeaponTypeChoices.transform.SetAsLastSibling();
            _browserWeaponTypeChoices.SetActive(true);
        }

        private static void UpdateBrowserWeaponTypeControl()
        {
            bool visible = _browserCatalogCategory == 1;
            if (_browserWeaponTypeButton != null) SetBrowserActiveIfChanged(_browserWeaponTypeButton.gameObject, visible);
            if (!visible) CloseBrowserWeaponTypeChoices();
            if (_browserWeaponTypeText != null)
                SetBrowserTextIfChanged(_browserWeaponTypeText, NormalizeModUiText(Ui("catalog.weapon_type") + ": " +
                    BrowserWeaponTypeLabel(_browserCatalogWeaponClass) + "  ›"));
            if (_browserCatalogHeaderText != null)
            {
                SetBrowserRectPositionIfChanged(_browserCatalogHeaderText.rectTransform, visible ? 284f : 8f, -145f);
                SetBrowserRectSizeIfChanged(_browserCatalogHeaderText.rectTransform, visible ? 400f : 676f, 28f);
            }
        }
    }
}
