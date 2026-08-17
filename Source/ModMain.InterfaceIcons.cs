using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemIntelligence
{
    /// <summary>
    /// Owns the optional browser-chrome icon system. Icons are tiny procedural sprites:
    /// no Resources scan, AssetBundle, disk IO or per-frame rebuild is involved. The
    /// bindings also own the reversible text layout used when MCM disables the icons.
    /// Item sprites and research/status markers deliberately remain separate owners.
    /// </summary>
    public static partial class ModMain
    {
        private enum BrowserInterfaceIconKind
        {
            Search,
            Catalog,
            Back,
            Close,
            Favorite,
            History,
            Overview,
            Magnum,
            Recipes,
            Trade,
            Ammo,
            Factions,
            Loot,
            Filter,
            Sort,
            SortAscending,
            SortDescending,
            Reset,
            Weapon,
            Armor,
            Implant,
            Consumable,
            Chip,
            Other
        }

        private sealed class BrowserInterfaceIconBinding
        {
            public GameObject IconRoot;
            public Image Icon;
            public RectTransform ControlRect;
            public bool HasControlLayout;
            public Vector2 PlainControlPosition;
            public Vector2 PlainControlSize;
            public Vector2 IconControlPosition;
            public Vector2 IconControlSize;
            public RectTransform AuxiliaryRect;
            public Vector2 PlainAuxiliaryPosition;
            public Vector2 PlainAuxiliarySize;
            public Vector2 IconAuxiliaryPosition;
            public Vector2 IconAuxiliarySize;
            public TMP_Text Label;
            public RectTransform LabelRect;
            public bool HideLabelWithIcon;
            public Vector2 PlainLabelPosition;
            public Vector2 PlainLabelSize;
            public TextAlignmentOptions PlainLabelAlignment;
            public Vector2 IconLabelPosition;
            public Vector2 IconLabelSize;
            public TextAlignmentOptions IconLabelAlignment;
        }

        private const int BrowserInterfaceIconBindingLimit = 64;
        private static readonly Dictionary<BrowserInterfaceIconKind, Sprite> BrowserInterfaceIconSprites =
            new Dictionary<BrowserInterfaceIconKind, Sprite>();
        private static readonly List<BrowserInterfaceIconBinding> BrowserInterfaceIconBindings =
            new List<BrowserInterfaceIconBinding>(BrowserInterfaceIconBindingLimit);

        private static readonly BrowserInterfaceIconKind[] BrowserTabInterfaceIconKinds =
        {
            BrowserInterfaceIconKind.Overview,
            BrowserInterfaceIconKind.Magnum,
            BrowserInterfaceIconKind.Recipes,
            BrowserInterfaceIconKind.Trade,
            BrowserInterfaceIconKind.Ammo,
            BrowserInterfaceIconKind.Factions,
            BrowserInterfaceIconKind.Loot
        };

        private static readonly BrowserInterfaceIconKind[] BrowserCatalogCategoryIconKinds =
        {
            BrowserInterfaceIconKind.Catalog,
            BrowserInterfaceIconKind.Weapon,
            BrowserInterfaceIconKind.Armor,
            BrowserInterfaceIconKind.Ammo,
            BrowserInterfaceIconKind.Implant,
            BrowserInterfaceIconKind.Consumable,
            BrowserInterfaceIconKind.Chip,
            BrowserInterfaceIconKind.Loot,
            BrowserInterfaceIconKind.Other
        };

        private static Image _browserInterfaceSearchIcon;
        private static RectTransform _browserInterfaceSearchViewport;
        private static Image _browserInterfaceCloseIcon;
        private static Image _browserInterfaceFavoriteIcon;
        private static Image _browserInterfaceBackIcon;
        private static Image _browserInterfaceCatalogIcon;
        private static Image _browserInterfaceCatalogFilterIcon;
        private static Image _browserInterfaceCatalogSortIcon;
        private static Image _browserInterfaceCatalogDirectionIcon;
        private static Image _browserInterfaceCatalogResetIcon;
        private static readonly Image[] BrowserInterfaceTabIcons = new Image[BrowserTabCount];
        private static readonly Image[] BrowserInterfaceCatalogScopeIcons = new Image[BrowserCatalogScopeCount];
        private static readonly Image[] BrowserInterfaceCatalogCategoryIcons = new Image[BrowserCatalogCategoryCount];
        private static readonly Image[] BrowserInterfaceCatalogRowFavoriteIcons = new Image[BrowserCatalogVisibleRows];

        private static bool _browserInterfaceIconGenerationFailed;
        private static bool _browserInterfaceIconFailureLogged;
        private static bool _browserInterfaceIconBindingOverflowLogged;
        private static bool _browserInterfaceIconLayoutKnown;
        private static bool _browserInterfaceIconLayoutValue;
        private static int _browserInterfaceIconLayoutBindingCount = -1;

        private static bool BrowserInterfaceIconLayoutEnabled
        {
            get { return ShowInterfaceIcons && !_browserInterfaceIconGenerationFailed; }
        }

        private static void ResetBrowserInterfaceIconPresentation()
        {
            BrowserInterfaceIconBindings.Clear();
            _browserInterfaceSearchIcon = null;
            _browserInterfaceSearchViewport = null;
            _browserInterfaceCloseIcon = null;
            _browserInterfaceFavoriteIcon = null;
            _browserInterfaceBackIcon = null;
            _browserInterfaceCatalogIcon = null;
            _browserInterfaceCatalogFilterIcon = null;
            _browserInterfaceCatalogSortIcon = null;
            _browserInterfaceCatalogDirectionIcon = null;
            _browserInterfaceCatalogResetIcon = null;
            Array.Clear(BrowserInterfaceTabIcons, 0, BrowserInterfaceTabIcons.Length);
            Array.Clear(BrowserInterfaceCatalogScopeIcons, 0, BrowserInterfaceCatalogScopeIcons.Length);
            Array.Clear(BrowserInterfaceCatalogCategoryIcons, 0, BrowserInterfaceCatalogCategoryIcons.Length);
            Array.Clear(BrowserInterfaceCatalogRowFavoriteIcons, 0, BrowserInterfaceCatalogRowFavoriteIcons.Length);
            _browserInterfaceIconLayoutKnown = false;
            _browserInterfaceIconLayoutBindingCount = -1;
        }

        private static void FinalizeBrowserInterfaceIconPresentation()
        {
            ApplyBrowserInterfaceIconVisibility(true);
            UpdateBrowserTabs();
            Debug.Log("[ItemIntelligence] Browser interface icons ready: proceduralSprites=" +
                BrowserInterfaceIconSprites.Count + ", bindings=" + BrowserInterfaceIconBindings.Count +
                ", enabled=" + BrowserInterfaceIconLayoutEnabled + ".");
        }

        private static void RefreshBrowserInterfaceIconSetting()
        {
            ApplyBrowserInterfaceIconVisibility(true);
            UpdateBrowserTabs();
            UpdateBrowserHeaderActions();
            UpdateBrowserCatalogButtonStyle();
            if (_browserCatalogPanel != null)
                UpdateBrowserCatalogControls();
        }

        private static void ApplyBrowserInterfaceIconVisibility(bool force)
        {
            bool enabled = BrowserInterfaceIconLayoutEnabled;
            if (!force && _browserInterfaceIconLayoutKnown &&
                _browserInterfaceIconLayoutValue == enabled &&
                _browserInterfaceIconLayoutBindingCount == BrowserInterfaceIconBindings.Count)
                return;

            for (int i = 0; i < BrowserInterfaceIconBindings.Count; i++)
                ApplyBrowserInterfaceIconBinding(BrowserInterfaceIconBindings[i], enabled);

            if (_browserInterfaceSearchViewport != null)
            {
                Vector2 offset = _browserInterfaceSearchViewport.offsetMin;
                offset.x = enabled && _browserInterfaceSearchIcon != null &&
                    _browserInterfaceSearchIcon.sprite != null ? 34f : 10f;
                _browserInterfaceSearchViewport.offsetMin = offset;
            }

            _browserInterfaceIconLayoutKnown = true;
            _browserInterfaceIconLayoutValue = enabled;
            _browserInterfaceIconLayoutBindingCount = BrowserInterfaceIconBindings.Count;
        }

        private static void ApplyBrowserInterfaceIconBinding(
            BrowserInterfaceIconBinding binding, bool enabled)
        {
            if (binding == null) return;
            bool iconVisible = enabled && binding.Icon != null && binding.Icon.sprite != null;
            if (binding.IconRoot != null)
                binding.IconRoot.SetActive(iconVisible);

            // A very small number of controls need extra horizontal room only while
            // their glyph is visible. Capture and restore their original geometry so
            // the MCM switch remains a genuinely reversible presentation preference.
            if (binding.HasControlLayout && binding.ControlRect != null)
            {
                binding.ControlRect.anchoredPosition = iconVisible
                    ? binding.IconControlPosition
                    : binding.PlainControlPosition;
                binding.ControlRect.sizeDelta = iconVisible
                    ? binding.IconControlSize
                    : binding.PlainControlSize;
            }
            if (binding.HasControlLayout && binding.AuxiliaryRect != null)
            {
                binding.AuxiliaryRect.anchoredPosition = iconVisible
                    ? binding.IconAuxiliaryPosition
                    : binding.PlainAuxiliaryPosition;
                binding.AuxiliaryRect.sizeDelta = iconVisible
                    ? binding.IconAuxiliarySize
                    : binding.PlainAuxiliarySize;
            }

            if (binding.Label == null || binding.LabelRect == null) return;
            if (binding.HideLabelWithIcon)
            {
                binding.Label.gameObject.SetActive(!iconVisible);
                return;
            }

            binding.Label.gameObject.SetActive(true);
            binding.LabelRect.anchoredPosition = iconVisible
                ? binding.IconLabelPosition
                : binding.PlainLabelPosition;
            binding.LabelRect.sizeDelta = iconVisible
                ? binding.IconLabelSize
                : binding.PlainLabelSize;
            binding.Label.alignment = iconVisible
                ? binding.IconLabelAlignment
                : binding.PlainLabelAlignment;
        }

        private static Image CreateBrowserInterfaceIcon(
            string name,
            Transform parent,
            BrowserInterfaceIconKind kind,
            Vector2 position,
            Vector2 size,
            Color color,
            TMP_Text label,
            bool hideLabelWithIcon,
            Vector2 iconLabelPosition,
            Vector2 iconLabelSize,
            TextAlignmentOptions iconLabelAlignment)
        {
            if (BrowserInterfaceIconBindings.Count >= BrowserInterfaceIconBindingLimit)
            {
                if (!_browserInterfaceIconBindingOverflowLogged)
                {
                    _browserInterfaceIconBindingOverflowLogged = true;
                    Debug.LogWarning("[ItemIntelligence] Interface-icon binding limit reached; " +
                        "remaining controls keep their text-only layout.");
                }
                return null;
            }

            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            RectTransform rt = root.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;

            Image image = root.AddComponent<Image>();
            image.sprite = GetBrowserInterfaceIconSprite(kind);
            image.color = color;
            image.preserveAspect = true;
            image.raycastTarget = false;

            BrowserInterfaceIconBinding binding = new BrowserInterfaceIconBinding();
            binding.IconRoot = root;
            binding.Icon = image;
            binding.Label = label;
            binding.HideLabelWithIcon = hideLabelWithIcon;
            binding.IconLabelPosition = iconLabelPosition;
            binding.IconLabelSize = iconLabelSize;
            binding.IconLabelAlignment = iconLabelAlignment;
            if (label != null)
            {
                binding.LabelRect = label.transform as RectTransform;
                if (binding.LabelRect != null)
                {
                    binding.PlainLabelPosition = binding.LabelRect.anchoredPosition;
                    binding.PlainLabelSize = binding.LabelRect.sizeDelta;
                    binding.PlainLabelAlignment = label.alignment;
                }
            }
            BrowserInterfaceIconBindings.Add(binding);
            ApplyBrowserInterfaceIconBinding(binding, BrowserInterfaceIconLayoutEnabled);
            _browserInterfaceIconLayoutKnown = false;
            return image;
        }

        private static void ConfigureBrowserInterfaceIconControlLayout(
            Image icon,
            RectTransform control,
            Vector2 iconPosition,
            Vector2 iconSize,
            RectTransform auxiliary,
            Vector2 iconAuxiliaryPosition,
            Vector2 iconAuxiliarySize)
        {
            if (icon == null || control == null) return;
            for (int i = 0; i < BrowserInterfaceIconBindings.Count; i++)
            {
                BrowserInterfaceIconBinding binding = BrowserInterfaceIconBindings[i];
                if (binding == null || binding.Icon != icon) continue;
                binding.ControlRect = control;
                binding.HasControlLayout = true;
                binding.PlainControlPosition = control.anchoredPosition;
                binding.PlainControlSize = control.sizeDelta;
                binding.IconControlPosition = iconPosition;
                binding.IconControlSize = iconSize;
                binding.AuxiliaryRect = auxiliary;
                if (auxiliary != null)
                {
                    binding.PlainAuxiliaryPosition = auxiliary.anchoredPosition;
                    binding.PlainAuxiliarySize = auxiliary.sizeDelta;
                    binding.IconAuxiliaryPosition = iconAuxiliaryPosition;
                    binding.IconAuxiliarySize = iconAuxiliarySize;
                }
                ApplyBrowserInterfaceIconBinding(binding, BrowserInterfaceIconLayoutEnabled);
                _browserInterfaceIconLayoutKnown = false;
                return;
            }
        }

        private static void CreateBrowserFavoriteInterfaceIcon()
        {
            if (_browserFavoriteButton == null) return;
            _browserInterfaceFavoriteIcon = CreateBrowserInterfaceIcon(
                "FavoriteGlyph", _browserFavoriteButton.transform,
                BrowserInterfaceIconKind.Favorite, new Vector2(18f, -8f), new Vector2(18f, 18f),
                new Color(0.48f, 0.74f, 0.62f, 1f), _browserFavoriteButtonText, true,
                Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
        }

        private static void CreateBrowserBackInterfaceIcon()
        {
            if (_browserBackButton == null) return;
            _browserInterfaceBackIcon = CreateBrowserInterfaceIcon(
                "BackGlyph", _browserBackButton.transform,
                BrowserInterfaceIconKind.Back, new Vector2(9f, -8f), new Vector2(18f, 18f),
                new Color(0.56f, 0.80f, 0.66f, 1f), _browserBackButtonText, false,
                new Vector2(30f, 0f), new Vector2(57f, 34f), TextAlignmentOptions.MidlineLeft);
        }

        private static void CreateBrowserCloseInterfaceIcon(Transform closeButton)
        {
            _browserInterfaceCloseIcon = CreateBrowserInterfaceIcon(
                "CloseGlyph", closeButton, BrowserInterfaceIconKind.Close,
                new Vector2(99f, -8f), new Vector2(18f, 18f),
                new Color(0.43f, 0.68f, 0.59f, 0.96f), _browserCloseText, false,
                new Vector2(10f, -1f), new Vector2(80f, 32f), TextAlignmentOptions.MidlineLeft);
        }

        private static void CreateBrowserSearchInterfaceIcon(RectTransform viewport)
        {
            _browserInterfaceSearchViewport = viewport;
            _browserInterfaceSearchIcon = CreateBrowserInterfaceIcon(
                "SearchGlyph", _browserSearchInput.transform,
                BrowserInterfaceIconKind.Search, new Vector2(9f, -9f), new Vector2(16f, 16f),
                new Color(0.35f, 0.62f, 0.53f, 0.94f), null, false,
                Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
        }

        private static void CreateBrowserCatalogLauncherInterfaceIcon()
        {
            if (_browserCatalogButtonBackground == null) return;
            _browserInterfaceCatalogIcon = CreateBrowserInterfaceIcon(
                "CatalogGlyph", _browserCatalogButtonBackground.transform,
                BrowserInterfaceIconKind.Catalog, new Vector2(7f, -9f), new Vector2(16f, 16f),
                new Color(0.92f, 0.94f, 0.78f, 1f), _browserCatalogButtonText, false,
                new Vector2(27f, 0f), new Vector2(70f, 34f), TextAlignmentOptions.MidlineLeft);
            ConfigureBrowserInterfaceIconControlLayout(
                _browserInterfaceCatalogIcon,
                _browserCatalogButtonBackground.transform as RectTransform,
                new Vector2(618f, -72f),
                new Vector2(100f, 34f),
                _browserSearchStatusText == null ? null : _browserSearchStatusText.rectTransform,
                new Vector2(562f, -72f),
                new Vector2(50f, 34f));
        }

        private static void CreateBrowserTabInterfaceIcon(Transform tab, int index)
        {
            if (index < 0 || index >= BrowserInterfaceTabIcons.Length) return;
            BrowserInterfaceTabIcons[index] = CreateBrowserInterfaceIcon(
                "TabGlyph", tab, BrowserTabInterfaceIconKinds[index],
                new Vector2(42f, -2f), new Vector2(12f, 12f),
                new Color(0.42f, 0.68f, 0.58f, 1f), BrowserTabTexts[index], false,
                new Vector2(2f, -14f), new Vector2(92f, 24f), TextAlignmentOptions.Center);
        }

        private static void CreateBrowserCatalogScopeInterfaceIcon(int index)
        {
            if (index < 0 || index >= BrowserInterfaceCatalogScopeIcons.Length ||
                BrowserCatalogScopeButtons[index] == null) return;
            BrowserInterfaceIconKind kind = index == (int)BrowserCatalogScope.Favorites
                ? BrowserInterfaceIconKind.Favorite
                : (index == (int)BrowserCatalogScope.Recent
                    ? BrowserInterfaceIconKind.History
                    : BrowserInterfaceIconKind.Catalog);
            BrowserInterfaceCatalogScopeIcons[index] = CreateBrowserInterfaceIcon(
                "ScopeGlyph", BrowserCatalogScopeButtons[index].transform, kind,
                new Vector2(10f, -7f), new Vector2(16f, 16f),
                new Color(0.48f, 0.74f, 0.62f, 1f), BrowserCatalogScopeTexts[index], false,
                new Vector2(34f, 0f), new Vector2(184f, 30f), TextAlignmentOptions.MidlineLeft);
        }

        private static void CreateBrowserCatalogCategoryInterfaceIcon(int index)
        {
            if (index < 0 || index >= BrowserInterfaceCatalogCategoryIcons.Length ||
                BrowserCatalogCategoryButtons[index] == null) return;
            RectTransform buttonRect = BrowserCatalogCategoryButtons[index].transform as RectTransform;
            float buttonWidth = buttonRect == null ? 70f : buttonRect.sizeDelta.x;
            BrowserInterfaceCatalogCategoryIcons[index] = CreateBrowserInterfaceIcon(
                "CategoryGlyph", BrowserCatalogCategoryButtons[index].transform,
                BrowserCatalogCategoryIconKinds[index], new Vector2(5f, -7f), new Vector2(16f, 16f),
                new Color(0.48f, 0.74f, 0.62f, 1f), BrowserCatalogCategoryTexts[index], false,
                new Vector2(24f, 0f), new Vector2(Math.Max(42f, buttonWidth - 28f), 30f),
                TextAlignmentOptions.MidlineLeft);
        }

        private static void CreateBrowserCatalogToolbarInterfaceIcons()
        {
            _browserInterfaceCatalogFilterIcon = CreateCatalogToolbarInterfaceIcon(
                _browserCatalogDataFilterButton, _browserCatalogDataFilterText,
                BrowserInterfaceIconKind.Filter, "FilterGlyph", 205f);
            _browserInterfaceCatalogSortIcon = CreateCatalogToolbarInterfaceIcon(
                _browserCatalogSortButton, _browserCatalogSortText,
                BrowserInterfaceIconKind.Sort, "SortGlyph", 170f);
            _browserInterfaceCatalogDirectionIcon = CreateCatalogToolbarInterfaceIcon(
                _browserCatalogDirectionButton, _browserCatalogDirectionText,
                BrowserInterfaceIconKind.SortAscending, "DirectionGlyph", 135f);
            _browserInterfaceCatalogResetIcon = CreateCatalogToolbarInterfaceIcon(
                _browserCatalogResetButton, _browserCatalogResetText,
                BrowserInterfaceIconKind.Reset, "ResetGlyph", 168f);
        }

        private static Image CreateCatalogToolbarInterfaceIcon(
            Button button, TMP_Text label, BrowserInterfaceIconKind kind, string name, float width)
        {
            if (button == null) return null;
            return CreateBrowserInterfaceIcon(
                name, button.transform, kind, new Vector2(7f, -8f), new Vector2(15f, 15f),
                new Color(0.48f, 0.74f, 0.62f, 1f), label, false,
                new Vector2(26f, 0f), new Vector2(width - 30f, 31f), TextAlignmentOptions.MidlineLeft);
        }

        private static void CreateBrowserCatalogRowFavoriteInterfaceIcon(int index)
        {
            if (index < 0 || index >= BrowserInterfaceCatalogRowFavoriteIcons.Length ||
                BrowserCatalogRowFavoriteButtons[index] == null) return;
            BrowserInterfaceCatalogRowFavoriteIcons[index] = CreateBrowserInterfaceIcon(
                "FavoriteGlyph", BrowserCatalogRowFavoriteButtons[index].transform,
                BrowserInterfaceIconKind.Favorite, new Vector2(14f, -5f), new Vector2(16f, 16f),
                new Color(0.42f, 0.66f, 0.56f, 1f), BrowserCatalogRowFavoriteTexts[index], true,
                Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
        }

        private static string GetBrowserInterfaceTabLabel(int index, string fullLabel)
        {
            if (!BrowserInterfaceIconLayoutEnabled) return fullLabel;
            switch (index)
            {
                case (int)BrowserTabId.Overview: return Ui("tab.overview.short");
                case (int)BrowserTabId.Magnum: return Ui("tab.magnum.short");
                case (int)BrowserTabId.Recipes: return Ui("tab.recipes.short");
                case (int)BrowserTabId.Trade: return Ui("tab.trade.short");
                case (int)BrowserTabId.Ammo: return Ui("tab.ammo.short");
                case (int)BrowserTabId.Factions: return Ui("tab.factions.short");
                case (int)BrowserTabId.Loot: return Ui("tab.loot.short");
                default: return fullLabel;
            }
        }

        private static float GetBrowserInterfaceTabFontSize(bool russian)
        {
            return BrowserInterfaceIconLayoutEnabled
                ? (russian ? 10.5f : 11f)
                : (russian ? 11.5f : 12.5f);
        }

        private static void UpdateBrowserHeaderInterfaceIconStyle(bool favorite, bool canBack)
        {
            SetBrowserInterfaceIconColor(_browserInterfaceFavoriteIcon, favorite
                ? new Color(0.96f, 0.91f, 0.55f, 1f)
                : new Color(0.48f, 0.74f, 0.62f, 1f));
            SetBrowserInterfaceIconColor(_browserInterfaceBackIcon, canBack
                ? new Color(0.56f, 0.80f, 0.66f, 1f)
                : new Color(0.31f, 0.45f, 0.39f, 0.72f));
        }

        private static void UpdateBrowserTabInterfaceIconStyle(int index, bool available, bool selected)
        {
            if (index < 0 || index >= BrowserInterfaceTabIcons.Length) return;
            SetBrowserInterfaceIconColor(BrowserInterfaceTabIcons[index], !available
                ? new Color(0.38f, 0.38f, 0.38f, 1f)
                : (selected
                    ? new Color(0.88f, 0.90f, 0.62f, 1f)
                    : new Color(0.42f, 0.68f, 0.58f, 1f)));
        }

        private static void UpdateBrowserCatalogScopeInterfaceIconStyle(int index, bool selected)
        {
            if (index < 0 || index >= BrowserInterfaceCatalogScopeIcons.Length) return;
            SetBrowserInterfaceIconColor(BrowserInterfaceCatalogScopeIcons[index], selected
                ? new Color(0.90f, 0.90f, 0.62f, 1f)
                : new Color(0.48f, 0.74f, 0.62f, 1f));
        }

        private static void UpdateBrowserCatalogCategoryInterfaceIconStyle(int index, bool selected)
        {
            if (index < 0 || index >= BrowserInterfaceCatalogCategoryIcons.Length) return;
            SetBrowserInterfaceIconColor(BrowserInterfaceCatalogCategoryIcons[index], selected
                ? new Color(0.90f, 0.90f, 0.62f, 1f)
                : new Color(0.48f, 0.74f, 0.62f, 1f));
        }

        private static void UpdateBrowserCatalogToolbarInterfaceIconStyle(bool sortable)
        {
            Color active = new Color(0.48f, 0.74f, 0.62f, 1f);
            Color inactive = new Color(0.31f, 0.45f, 0.39f, 0.72f);
            SetBrowserInterfaceIconColor(_browserInterfaceCatalogFilterIcon, active);
            SetBrowserInterfaceIconColor(_browserInterfaceCatalogSortIcon, sortable ? active : inactive);
            SetBrowserInterfaceIconColor(_browserInterfaceCatalogDirectionIcon, sortable ? active : inactive);
            SetBrowserInterfaceIconColor(_browserInterfaceCatalogResetIcon, active);
            SetBrowserInterfaceIconSprite(_browserInterfaceCatalogDirectionIcon,
                _browserCatalogSortDescending
                    ? BrowserInterfaceIconKind.SortDescending
                    : BrowserInterfaceIconKind.SortAscending);
        }

        private static void UpdateBrowserCatalogRowFavoriteInterfaceIconStyle(int index, bool favorite)
        {
            if (index < 0 || index >= BrowserInterfaceCatalogRowFavoriteIcons.Length) return;
            SetBrowserInterfaceIconColor(BrowserInterfaceCatalogRowFavoriteIcons[index], favorite
                ? new Color(0.96f, 0.91f, 0.55f, 1f)
                : new Color(0.42f, 0.66f, 0.56f, 1f));
        }

        private static void UpdateBrowserCatalogLauncherInterfaceIconStyle()
        {
            SetBrowserInterfaceIconColor(_browserInterfaceCatalogIcon, _browserCatalogOpen
                ? new Color(0.96f, 0.97f, 0.84f, 1f)
                : new Color(0.92f, 0.94f, 0.78f, 1f));
        }

        private static void SetBrowserInterfaceIconColor(Image image, Color color)
        {
            if (image != null) image.color = color;
        }

        private static void SetBrowserInterfaceIconSprite(Image image, BrowserInterfaceIconKind kind)
        {
            if (image == null) return;
            image.sprite = GetBrowserInterfaceIconSprite(kind);
            image.enabled = image.sprite != null;
        }

        private static Sprite GetBrowserInterfaceIconSprite(BrowserInterfaceIconKind kind)
        {
            Sprite sprite;
            if (BrowserInterfaceIconSprites.TryGetValue(kind, out sprite)) return sprite;

            try
            {
                const int size = 16;
                Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
                texture.name = "QII_UI_" + kind;
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.hideFlags = HideFlags.HideAndDontSave;
                Color[] pixels = new Color[size * size];
                Color clear = new Color(0f, 0f, 0f, 0f);
                for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
                texture.SetPixels(pixels);
                DrawBrowserInterfaceIcon(texture, kind, Color.white);
                texture.Apply(false, false);

                sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
                    new Vector2(0.5f, 0.5f), 16f);
                sprite.name = texture.name;
                sprite.hideFlags = HideFlags.HideAndDontSave;
                BrowserInterfaceIconSprites[kind] = sprite;
                return sprite;
            }
            catch (Exception ex)
            {
                _browserInterfaceIconGenerationFailed = true;
                if (!_browserInterfaceIconFailureLogged)
                {
                    _browserInterfaceIconFailureLogged = true;
                    Debug.LogWarning("[ItemIntelligence] Procedural interface icons unavailable; " +
                        "the browser will keep its text-only layout: " + ex.Message);
                }
                return null;
            }
        }

        private static void DrawBrowserInterfaceIcon(
            Texture2D texture, BrowserInterfaceIconKind kind, Color color)
        {
            switch (kind)
            {
                case BrowserInterfaceIconKind.Search:
                    DrawBrowserInterfaceOctagon(texture, 6, 9, 4, color);
                    DrawBrowserInterfaceLine(texture, 9, 6, 13, 2, color);
                    DrawBrowserInterfaceLine(texture, 10, 6, 13, 3, color);
                    break;
                case BrowserInterfaceIconKind.Catalog:
                    DrawBrowserInterfaceRect(texture, 2, 9, 6, 13, color);
                    DrawBrowserInterfaceRect(texture, 9, 9, 13, 13, color);
                    DrawBrowserInterfaceRect(texture, 2, 2, 6, 6, color);
                    DrawBrowserInterfaceRect(texture, 9, 2, 13, 6, color);
                    break;
                case BrowserInterfaceIconKind.Back:
                    DrawBrowserInterfaceLine(texture, 3, 8, 9, 13, color);
                    DrawBrowserInterfaceLine(texture, 3, 8, 9, 3, color);
                    DrawBrowserInterfaceLine(texture, 3, 8, 13, 8, color);
                    break;
                case BrowserInterfaceIconKind.Close:
                    DrawBrowserInterfaceLine(texture, 3, 3, 12, 12, color);
                    DrawBrowserInterfaceLine(texture, 3, 12, 12, 3, color);
                    DrawBrowserInterfaceLine(texture, 4, 3, 12, 11, color);
                    DrawBrowserInterfaceLine(texture, 4, 12, 12, 4, color);
                    break;
                case BrowserInterfaceIconKind.Favorite:
                    DrawBrowserInterfaceLine(texture, 4, 3, 4, 13, color);
                    DrawBrowserInterfaceLine(texture, 4, 13, 11, 13, color);
                    DrawBrowserInterfaceLine(texture, 11, 13, 11, 3, color);
                    DrawBrowserInterfaceLine(texture, 4, 3, 7, 6, color);
                    DrawBrowserInterfaceLine(texture, 7, 6, 11, 3, color);
                    break;
                case BrowserInterfaceIconKind.History:
                    DrawBrowserInterfaceOctagon(texture, 8, 8, 6, color);
                    DrawBrowserInterfaceLine(texture, 8, 8, 8, 12, color);
                    DrawBrowserInterfaceLine(texture, 8, 8, 11, 6, color);
                    FillBrowserInterfaceRect(texture, 7, 7, 8, 8, color);
                    break;
                case BrowserInterfaceIconKind.Overview:
                    DrawBrowserInterfaceRect(texture, 3, 2, 12, 13, color);
                    FillBrowserInterfaceRect(texture, 7, 10, 8, 11, color);
                    FillBrowserInterfaceRect(texture, 7, 4, 8, 8, color);
                    break;
                case BrowserInterfaceIconKind.Magnum:
                    DrawBrowserInterfaceLine(texture, 8, 14, 13, 8, color);
                    DrawBrowserInterfaceLine(texture, 13, 8, 8, 2, color);
                    DrawBrowserInterfaceLine(texture, 8, 2, 3, 8, color);
                    DrawBrowserInterfaceLine(texture, 3, 8, 8, 14, color);
                    DrawBrowserInterfaceLine(texture, 5, 8, 11, 8, color);
                    DrawBrowserInterfaceLine(texture, 8, 5, 8, 11, color);
                    break;
                case BrowserInterfaceIconKind.Recipes:
                    DrawBrowserInterfaceRect(texture, 3, 2, 12, 13, color);
                    DrawBrowserInterfaceLine(texture, 9, 13, 12, 10, color);
                    DrawBrowserInterfaceLine(texture, 9, 13, 9, 10, color);
                    DrawBrowserInterfaceLine(texture, 9, 10, 12, 10, color);
                    DrawBrowserInterfaceLine(texture, 5, 8, 10, 8, color);
                    DrawBrowserInterfaceLine(texture, 5, 5, 10, 5, color);
                    break;
                case BrowserInterfaceIconKind.Trade:
                    DrawBrowserInterfaceLine(texture, 2, 11, 12, 11, color);
                    DrawBrowserInterfaceLine(texture, 9, 14, 12, 11, color);
                    DrawBrowserInterfaceLine(texture, 9, 8, 12, 11, color);
                    DrawBrowserInterfaceLine(texture, 13, 5, 3, 5, color);
                    DrawBrowserInterfaceLine(texture, 6, 8, 3, 5, color);
                    DrawBrowserInterfaceLine(texture, 6, 2, 3, 5, color);
                    break;
                case BrowserInterfaceIconKind.Ammo:
                    DrawBrowserInterfaceLine(texture, 6, 2, 6, 11, color);
                    DrawBrowserInterfaceLine(texture, 10, 2, 10, 11, color);
                    DrawBrowserInterfaceLine(texture, 6, 11, 8, 14, color);
                    DrawBrowserInterfaceLine(texture, 8, 14, 10, 11, color);
                    DrawBrowserInterfaceLine(texture, 5, 2, 11, 2, color);
                    break;
                case BrowserInterfaceIconKind.Factions:
                    DrawBrowserInterfaceLine(texture, 4, 2, 4, 14, color);
                    DrawBrowserInterfaceLine(texture, 5, 13, 12, 13, color);
                    DrawBrowserInterfaceLine(texture, 12, 13, 10, 9, color);
                    DrawBrowserInterfaceLine(texture, 10, 9, 5, 9, color);
                    DrawBrowserInterfaceLine(texture, 2, 2, 8, 2, color);
                    break;
                case BrowserInterfaceIconKind.Loot:
                    DrawBrowserInterfaceRect(texture, 2, 3, 13, 11, color);
                    DrawBrowserInterfaceLine(texture, 2, 11, 5, 14, color);
                    DrawBrowserInterfaceLine(texture, 5, 14, 13, 11, color);
                    DrawBrowserInterfaceLine(texture, 8, 3, 8, 11, color);
                    DrawBrowserInterfaceLine(texture, 5, 7, 7, 7, color);
                    break;
                case BrowserInterfaceIconKind.Filter:
                    DrawBrowserInterfaceLine(texture, 2, 13, 13, 13, color);
                    DrawBrowserInterfaceLine(texture, 2, 13, 6, 8, color);
                    DrawBrowserInterfaceLine(texture, 13, 13, 9, 8, color);
                    DrawBrowserInterfaceLine(texture, 6, 8, 6, 3, color);
                    DrawBrowserInterfaceLine(texture, 9, 8, 9, 5, color);
                    DrawBrowserInterfaceLine(texture, 9, 5, 6, 3, color);
                    break;
                case BrowserInterfaceIconKind.Sort:
                    DrawBrowserInterfaceLine(texture, 3, 12, 13, 12, color);
                    DrawBrowserInterfaceLine(texture, 3, 8, 10, 8, color);
                    DrawBrowserInterfaceLine(texture, 3, 4, 7, 4, color);
                    break;
                case BrowserInterfaceIconKind.SortAscending:
                    DrawBrowserInterfaceLine(texture, 8, 13, 8, 3, color);
                    DrawBrowserInterfaceLine(texture, 8, 13, 4, 9, color);
                    DrawBrowserInterfaceLine(texture, 8, 13, 12, 9, color);
                    break;
                case BrowserInterfaceIconKind.SortDescending:
                    DrawBrowserInterfaceLine(texture, 8, 13, 8, 3, color);
                    DrawBrowserInterfaceLine(texture, 8, 3, 4, 7, color);
                    DrawBrowserInterfaceLine(texture, 8, 3, 12, 7, color);
                    break;
                case BrowserInterfaceIconKind.Reset:
                    DrawBrowserInterfaceOctagon(texture, 8, 8, 5, color);
                    FillBrowserInterfaceRect(texture, 2, 8, 5, 12, new Color(0f, 0f, 0f, 0f));
                    DrawBrowserInterfaceLine(texture, 2, 11, 6, 13, color);
                    DrawBrowserInterfaceLine(texture, 2, 11, 3, 7, color);
                    break;
                case BrowserInterfaceIconKind.Weapon:
                    DrawBrowserInterfaceLine(texture, 2, 10, 13, 10, color);
                    DrawBrowserInterfaceLine(texture, 4, 8, 10, 8, color);
                    DrawBrowserInterfaceLine(texture, 7, 8, 6, 4, color);
                    DrawBrowserInterfaceLine(texture, 6, 4, 9, 4, color);
                    DrawBrowserInterfaceLine(texture, 12, 10, 13, 12, color);
                    break;
                case BrowserInterfaceIconKind.Armor:
                    DrawBrowserInterfaceLine(texture, 8, 14, 13, 12, color);
                    DrawBrowserInterfaceLine(texture, 13, 12, 12, 6, color);
                    DrawBrowserInterfaceLine(texture, 12, 6, 8, 2, color);
                    DrawBrowserInterfaceLine(texture, 8, 2, 4, 6, color);
                    DrawBrowserInterfaceLine(texture, 4, 6, 3, 12, color);
                    DrawBrowserInterfaceLine(texture, 3, 12, 8, 14, color);
                    DrawBrowserInterfaceLine(texture, 8, 12, 8, 4, color);
                    break;
                case BrowserInterfaceIconKind.Implant:
                    DrawBrowserInterfaceOctagon(texture, 8, 8, 4, color);
                    DrawBrowserInterfaceLine(texture, 8, 12, 8, 15, color);
                    DrawBrowserInterfaceLine(texture, 8, 4, 8, 1, color);
                    DrawBrowserInterfaceLine(texture, 4, 8, 1, 8, color);
                    DrawBrowserInterfaceLine(texture, 12, 8, 15, 8, color);
                    DrawBrowserInterfaceLine(texture, 6, 8, 10, 8, color);
                    DrawBrowserInterfaceLine(texture, 8, 6, 8, 10, color);
                    break;
                case BrowserInterfaceIconKind.Consumable:
                    FillBrowserInterfaceRect(texture, 6, 2, 9, 13, color);
                    FillBrowserInterfaceRect(texture, 2, 6, 13, 9, color);
                    break;
                case BrowserInterfaceIconKind.Chip:
                    DrawBrowserInterfaceRect(texture, 4, 4, 11, 11, color);
                    DrawBrowserInterfaceRect(texture, 6, 6, 9, 9, color);
                    for (int p = 5; p <= 10; p += 5)
                    {
                        DrawBrowserInterfaceLine(texture, p, 2, p, 4, color);
                        DrawBrowserInterfaceLine(texture, p, 11, p, 13, color);
                        DrawBrowserInterfaceLine(texture, 2, p, 4, p, color);
                        DrawBrowserInterfaceLine(texture, 11, p, 13, p, color);
                    }
                    break;
                case BrowserInterfaceIconKind.Other:
                    FillBrowserInterfaceRect(texture, 2, 7, 4, 9, color);
                    FillBrowserInterfaceRect(texture, 7, 7, 9, 9, color);
                    FillBrowserInterfaceRect(texture, 12, 7, 14, 9, color);
                    break;
            }
        }

        private static void SetBrowserInterfacePixel(Texture2D texture, int x, int y, Color color)
        {
            if (texture == null || x < 0 || y < 0 || x >= texture.width || y >= texture.height) return;
            texture.SetPixel(x, y, color);
        }

        private static void DrawBrowserInterfaceLine(
            Texture2D texture, int x0, int y0, int x1, int y1, Color color)
        {
            int dx = Math.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            int error = dx + dy;
            while (true)
            {
                SetBrowserInterfacePixel(texture, x0, y0, color);
                if (x0 == x1 && y0 == y1) break;
                int twice = 2 * error;
                if (twice >= dy)
                {
                    error += dy;
                    x0 += sx;
                }
                if (twice <= dx)
                {
                    error += dx;
                    y0 += sy;
                }
            }
        }

        private static void DrawBrowserInterfaceRect(
            Texture2D texture, int left, int bottom, int right, int top, Color color)
        {
            DrawBrowserInterfaceLine(texture, left, bottom, right, bottom, color);
            DrawBrowserInterfaceLine(texture, right, bottom, right, top, color);
            DrawBrowserInterfaceLine(texture, right, top, left, top, color);
            DrawBrowserInterfaceLine(texture, left, top, left, bottom, color);
        }

        private static void FillBrowserInterfaceRect(
            Texture2D texture, int left, int bottom, int right, int top, Color color)
        {
            for (int y = bottom; y <= top; y++)
                for (int x = left; x <= right; x++)
                    SetBrowserInterfacePixel(texture, x, y, color);
        }

        private static void DrawBrowserInterfaceOctagon(
            Texture2D texture, int centerX, int centerY, int radius, Color color)
        {
            int cut = Math.Max(1, radius / 2);
            DrawBrowserInterfaceLine(texture, centerX - cut, centerY + radius,
                centerX + cut, centerY + radius, color);
            DrawBrowserInterfaceLine(texture, centerX + cut, centerY + radius,
                centerX + radius, centerY + cut, color);
            DrawBrowserInterfaceLine(texture, centerX + radius, centerY + cut,
                centerX + radius, centerY - cut, color);
            DrawBrowserInterfaceLine(texture, centerX + radius, centerY - cut,
                centerX + cut, centerY - radius, color);
            DrawBrowserInterfaceLine(texture, centerX + cut, centerY - radius,
                centerX - cut, centerY - radius, color);
            DrawBrowserInterfaceLine(texture, centerX - cut, centerY - radius,
                centerX - radius, centerY - cut, color);
            DrawBrowserInterfaceLine(texture, centerX - radius, centerY - cut,
                centerX - radius, centerY + cut, color);
            DrawBrowserInterfaceLine(texture, centerX - radius, centerY + cut,
                centerX - cut, centerY + radius, color);
        }
    }
}
