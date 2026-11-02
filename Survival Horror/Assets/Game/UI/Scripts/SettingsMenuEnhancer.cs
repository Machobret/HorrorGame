using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HorrorEngine
{
    public static class InputBindingSettings
    {
        private const string Key = "HorrorGame.PlayerBindingOverrides";
        public static bool IsRebinding { get; private set; }
        private static int cancelRebindFrame = -1;

        public static bool ShouldBlockMenuCancel => IsRebinding || Time.frameCount == cancelRebindFrame;

        public static void BeginRebind() => IsRebinding = true;
        public static void EndRebind() => IsRebinding = false;
        public static void MarkRebindCanceled() => cancelRebindFrame = Time.frameCount;
#if ENABLE_INPUT_SYSTEM
        private static InputActionAsset activeActions;
        public static void Apply(InputActionAsset actions)
        {
            activeActions = actions;
            string saved = PlayerPrefs.GetString(Key, string.Empty);
            if (!string.IsNullOrEmpty(saved)) actions.LoadBindingOverridesFromJson(saved);
        }
        public static void SaveActive()
        {
            if (activeActions == null) return;
            PlayerPrefs.SetString(Key, activeActions.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }
        public static void DiscardActive()
        {
            if (activeActions == null) return;
            activeActions.RemoveAllBindingOverrides();
            string saved = PlayerPrefs.GetString(Key, string.Empty);
            if (!string.IsNullOrEmpty(saved)) activeActions.LoadBindingOverridesFromJson(saved);
        }
        public static void ResetActive()
        {
            if (activeActions != null) activeActions.RemoveAllBindingOverrides();
        }
#else
        public static void SaveActive() { }
        public static void DiscardActive() { }
        public static void ResetActive() { }
#endif
    }

    /// <summary>Gives the stock settings lists a scroll viewport and game key bindings.</summary>
    public sealed class SettingsMenuEnhancer : MonoBehaviour
    {
        [SerializeField] private Font gameFont;
        private TMP_FontAsset font;
        private ScrollRect settingsScroll;
#if ENABLE_INPUT_SYSTEM
        private readonly List<BindingEntry> bindingEntries = new List<BindingEntry>();

        private sealed class BindingEntry
        {
            public InputAction Action;
            public int Index;
            public TextMeshProUGUI Label;
        }
#endif

        private IEnumerator Start()
        {
            if (gameFont != null) font = TMP_FontAsset.CreateFontAsset(gameFont);
            yield return null; // Stock UISettingsList creates its rows in Start.
            // The stock menu already arranges Graphics, Audio, and Game as one
            // vertical list. Scroll that list as a whole; altering each category
            // independently breaks the original Content layout and tab controls.
            MakeScrollable(Find("Content"));
            OrderLegacySections();
            AddControls();
            AddResetButton();
            yield return null;
            WireKeyboardNavigation();
        }

        private void Update()
        {
            if (settingsScroll != null && Mathf.Abs(Input.mouseScrollDelta.y) > .01f)
            {
                float wheelMaxY = Mathf.Max(0, settingsScroll.content.rect.height - settingsScroll.viewport.rect.height);
                float nextY = settingsScroll.content.anchoredPosition.y - Input.mouseScrollDelta.y * settingsScroll.scrollSensitivity;
                settingsScroll.content.anchoredPosition = new Vector2(0, Mathf.Clamp(nextY, 0, wheelMaxY));
            }
        }

        private void ScrollSelectionIntoView(GameObject selectedObject)
        {
            if (selectedObject == null) return;
            var selected = selectedObject.transform;
            var scroll = selected.GetComponentInParent<ScrollRect>();
            if (scroll == null || scroll.content == null || scroll.viewport == null || !selected.IsChildOf(scroll.content)) return;
            var selectedRect = selected as RectTransform;
            if (selectedRect == null) return;
            // Use the selected control's real bounds, rather than its top-level
            // section. This lets a long keyboard-bindings section keep scrolling.
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.content, selectedRect);
            float rowTop = -bounds.max.y;
            float rowBottom = -bounds.min.y;
            float currentTop = scroll.content.anchoredPosition.y;
            float currentBottom = currentTop + scroll.viewport.rect.height;
            float targetY = currentTop;

            // Keep the current selection visible without constantly centering it.
            // This produces one small movement only after a row crosses an edge.
            if (rowTop < currentTop) targetY = rowTop;
            else if (rowBottom > currentBottom) targetY = rowBottom - scroll.viewport.rect.height;
            else return;

            float maxY = Mathf.Max(0, scroll.content.rect.height - scroll.viewport.rect.height);
            scroll.content.anchoredPosition = new Vector2(0, Mathf.Clamp(targetY, 0, maxY));
        }

        private void MakeScrollable(RectTransform tab)
        {
            if (tab == null || tab.Find("ScrollContent") != null) return;
            var children = new Transform[tab.childCount];
            for (int i = 0; i < children.Length; i++) children[i] = tab.GetChild(i);
            var previousLayout = tab.GetComponent<VerticalLayoutGroup>();
            if (previousLayout != null) previousLayout.enabled = false;
            var previousFitter = tab.GetComponent<ContentSizeFitter>();
            if (previousFitter != null) previousFitter.enabled = false;
            tab.gameObject.AddComponent<RectMask2D>();
            var scroll = tab.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true; scroll.viewport = tab; scroll.scrollSensitivity = 30;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            var content = Rect("ScrollContent", tab);
            // Stretch horizontally. Anchoring both sides at .5 collapses the legacy
            // rows to zero width, which makes their labels render one character wide.
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(.5f, 1); content.sizeDelta = new Vector2(0, 1);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter; layout.spacing = 8; layout.padding = new RectOffset(8, 8, 8, 12);
            // Graphics, Audio, and Game report their own preferred height via the
            // stock ContentSizeFitter components. Let the parent layout consume
            // that height so sections are stacked rather than overlaid.
            layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            foreach (var child in children) child.SetParent(content, false);
            scroll.content = content;
            settingsScroll = scroll;
        }

        private void AddControls()
        {
#if ENABLE_INPUT_SYSTEM
            var tab = Find("Game");
            var listener = FindFirstObjectByType<PlayerInputListener>();
            if (tab == null || listener == null || listener.Actions == null) return;
            var list = Find("Content")?.Find("ScrollContent") as RectTransform;
            if (list == null || list.Find("KeyboardBindings") != null) return;
            // This is a sibling of the stock sections, not a child of Game. That
            // keeps every configuration in one continuous, correctly-sized list.
            var section = Rect("KeyboardBindings", list);
            section.SetAsLastSibling();
            var image = section.gameObject.AddComponent<Image>(); image.color = new Color(.07f, .07f, .07f, .9f);
            var layout = section.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10); layout.spacing = 6; layout.childControlWidth = true; layout.childControlHeight = false; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            section.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Label(section, "KEYBOARD CONTROLS — Pause is fixed to P", 19);
            AddRow(section, listener.Actions.asset, "PrimaryAxis", "Move Forward", "up");
            AddRow(section, listener.Actions.asset, "PrimaryAxis", "Move Back", "down");
            AddRow(section, listener.Actions.asset, "PrimaryAxis", "Move Left", "left");
            AddRow(section, listener.Actions.asset, "PrimaryAxis", "Move Right", "right");
            AddRow(section, listener.Actions.asset, "Aiming", "Aim", null);
            AddRow(section, listener.Actions.asset, "Attack", "Attack", null);
            AddRow(section, listener.Actions.asset, "Interact", "Interact", null);
            AddRow(section, listener.Actions.asset, "Run", "Run", null);
            AddRow(section, listener.Actions.asset, "Reload", "Reload", null);
            AddRow(section, listener.Actions.asset, "Turn180", "Turn 180", null);
            AddRow(section, listener.Actions.asset, "ChangeAimTarget", "Change Aim Target", null);
#endif
        }

        private void OrderLegacySections()
        {
            var list = Find("Content")?.Find("ScrollContent");
            if (list == null) return;
            var graphics = Find("Graphics");
            var audio = Find("Audio");
            var game = Find("Game");
            if (graphics != null) graphics.SetSiblingIndex(0);
            if (audio != null) audio.SetSiblingIndex(1);
            if (game != null) game.SetSiblingIndex(2);
        }

        private void AddResetButton()
        {
            var apply = Find("ApplyButton")?.GetComponent<Button>();
            if (apply == null || Find("ResetDefaultsButton") != null) return;

            var resetObject = Instantiate(apply.gameObject, apply.transform.parent);
            resetObject.name = "ResetDefaultsButton";
            var resetRect = resetObject.GetComponent<RectTransform>();
            resetRect.anchoredPosition = new Vector2(-260, resetRect.anchoredPosition.y);
            var label = resetObject.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null) label.text = "Defaults";
            var reset = resetObject.GetComponent<Button>();
            reset.onClick.RemoveAllListeners();
            reset.onClick.AddListener(() => GetComponent<UISettings>()?.ResetDefaults());
        }

        private void WireKeyboardNavigation()
        {
            var list = Find("Content")?.Find("ScrollContent") as RectTransform;
            var apply = Find("ApplyButton")?.GetComponent<Button>();
            var cancel = Find("CancelButton")?.GetComponent<Button>();
            var reset = Find("ResetDefaultsButton")?.GetComponent<Button>();
            if (list == null || apply == null || cancel == null || reset == null) return;

            Canvas.ForceUpdateCanvases();
            var rows = new List<List<Selectable>>();
            foreach (var selectable in list.GetComponentsInChildren<Selectable>(true))
            {
                if (!selectable.isActiveAndEnabled) continue;
                var callbacks = selectable.GetComponent<UISelectableCallbacks>();
                if (callbacks == null) callbacks = selectable.gameObject.AddComponent<UISelectableCallbacks>();
                callbacks.OnSelected.AddListener(ScrollSelectionIntoView);
                int rowIndex = -1;
                for (int i = 0; i < rows.Count; i++)
                {
                    if (Mathf.Abs(rows[i][0].transform.position.y - selectable.transform.position.y) < 3f)
                    {
                        rowIndex = i;
                        break;
                    }
                }

                if (rowIndex < 0)
                {
                    rows.Add(new List<Selectable> { selectable });
                }
                else
                {
                    rows[rowIndex].Add(selectable);
                }
            }

            rows.Sort((a, b) => b[0].transform.position.y.CompareTo(a[0].transform.position.y));
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
            }

            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                for (int column = 0; column < row.Count; column++)
                {
                    var current = row[column];
                    var navigation = new Navigation { mode = Navigation.Mode.Explicit };
                    navigation.selectOnUp = rowIndex == 0 ? null : FindClosestX(rows[rowIndex - 1], current);
                    navigation.selectOnDown = rowIndex == rows.Count - 1 ? reset : FindClosestX(rows[rowIndex + 1], current);
                    navigation.selectOnLeft = column > 0 ? row[column - 1] : current;
                    navigation.selectOnRight = column < row.Count - 1 ? row[column + 1] : current;
                    current.navigation = navigation;
                }
            }

            var footerNavigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = rows.Count == 0 ? null : FindClosestX(rows[rows.Count - 1], reset),
                selectOnDown = apply,
                selectOnLeft = cancel,
                selectOnRight = apply
            };
            reset.navigation = footerNavigation;
            footerNavigation.selectOnUp = reset;
            footerNavigation.selectOnDown = cancel;
            footerNavigation.selectOnLeft = reset;
            footerNavigation.selectOnRight = cancel;
            apply.navigation = footerNavigation;
            footerNavigation.selectOnUp = apply;
            footerNavigation.selectOnDown = reset;
            footerNavigation.selectOnLeft = apply;
            footerNavigation.selectOnRight = reset;
            cancel.navigation = footerNavigation;
        }

        private static Selectable FindClosestX(List<Selectable> candidates, Selectable target)
        {
            Selectable closest = candidates[0];
            float bestDistance = Mathf.Abs(closest.transform.position.x - target.transform.position.x);
            for (int i = 1; i < candidates.Count; i++)
            {
                float distance = Mathf.Abs(candidates[i].transform.position.x - target.transform.position.x);
                if (distance < bestDistance)
                {
                    closest = candidates[i];
                    bestDistance = distance;
                }
            }
            return closest;
        }

#if ENABLE_INPUT_SYSTEM
        private void AddRow(RectTransform parent, InputActionAsset actions, string actionName, string title, string part)
        {
            var action = actions.FindAction("Gameplay/" + actionName);
            if (action == null) return;
            int index = -1;
            for (int i = 0; i < action.bindings.Count; i++)
                if (action.bindings[i].groups != null && action.bindings[i].groups.Contains("KeyboardAndMouse") && (part == null || action.bindings[i].name == part)) { index = i; break; }
            if (index < 0) return;
            var row = Rect(title, parent); row.sizeDelta = new Vector2(0, 34);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>(); layout.spacing = 10; layout.childControlWidth = false; layout.childControlHeight = true; layout.childForceExpandWidth = false;
            var text = Label(row, title, 17); text.gameObject.AddComponent<LayoutElement>().preferredWidth = 225;
            var buttonRect = Rect("Rebind", row); var buttonImage = buttonRect.gameObject.AddComponent<Image>(); buttonImage.color = new Color(.16f, .16f, .15f, 1);
            var button = buttonRect.gameObject.AddComponent<Button>(); button.targetGraphic = buttonImage; buttonRect.gameObject.AddComponent<LayoutElement>().preferredWidth = 150;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, .9f, .45f, 1f);
            colors.selectedColor = new Color(1f, .9f, .45f, 1f);
            colors.pressedColor = new Color(.75f, .65f, .3f, 1f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
            var callbacks = buttonRect.gameObject.AddComponent<UISelectableCallbacks>();
            callbacks.OnSelected.AddListener(_ => buttonImage.color = new Color(.48f, .4f, .12f, 1f));
            callbacks.OnDeselected.AddListener(_ => buttonImage.color = new Color(.16f, .16f, .15f, 1f));
            var value = Label(buttonRect, action.GetBindingDisplayString(index), 17); value.alignment = TextAlignmentOptions.Center;
            bindingEntries.Add(new BindingEntry { Action = action, Index = index, Label = value });
            button.onClick.AddListener(() => Rebind(action, index, button, value));
        }

        public void RefreshBindingLabels()
        {
#if ENABLE_INPUT_SYSTEM
            foreach (var entry in bindingEntries)
            {
                if (entry.Label != null) entry.Label.text = entry.Action.GetBindingDisplayString(entry.Index);
            }
#endif
        }

        private void Rebind(InputAction action, int index, Button button, TextMeshProUGUI label)
        {
            // Keep the selected button interactable. Disabling it makes the
            // EventSystem fall back to the first legacy setting and scroll there.
            if (InputBindingSettings.IsRebinding) return;
            label.text = "Press a key..."; action.Disable();
            InputBindingSettings.BeginRebind();
            action.PerformInteractiveRebinding(index).WithControlsHavingToMatchPath("<Keyboard>").WithCancelingThrough("<Keyboard>/escape")
                .OnCancel(o => Finish(o, action, index, button, label, true)).OnComplete(o => Finish(o, action, index, button, label, false)).Start();
        }
        private void Finish(InputActionRebindingExtensions.RebindingOperation operation, InputAction action, int index, Button button, TextMeshProUGUI label, bool canceled)
        {
            operation.Dispose(); action.Enable(); label.text = action.GetBindingDisplayString(index);
            InputBindingSettings.EndRebind();
            if (canceled) InputBindingSettings.MarkRebindCanceled();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(button.gameObject);
        }
#endif

        private TextMeshProUGUI Label(RectTransform parent, string value, float size)
        {
            var rect = Rect("Text", parent); var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font; label.text = value; label.fontSize = size; label.color = new Color(.94f, .92f, .84f, 1); label.textWrappingMode = TextWrappingModes.NoWrap; label.alignment = TextAlignmentOptions.Left;
            return label;
        }
        private RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = gameObject.layer; var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false); return rect;
        }
        private RectTransform Find(string name)
        {
            foreach (var rect in GetComponentsInChildren<RectTransform>(true)) if (rect.name == name) return rect;
            return null;
        }
    }
}
