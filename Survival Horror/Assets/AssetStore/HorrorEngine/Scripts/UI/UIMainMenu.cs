using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Events;
using TMPro;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HorrorEngine
{
    [DefaultExecutionOrder(-100)]
    public class UIMainMenu : MonoBehaviour
    {
        [SerializeField] private GameObject m_MainScreen;
        [SerializeField] private GameObject m_LoadGamePrefab;
        [SerializeField] private Button m_DefaultButton;
        [SerializeField] private Button m_LoadButton;
        [SerializeField] private SceneTransition m_StartTransition;
        [SerializeField] private Sprite m_SelectorArrowIcon;
        
        private UILoadGame m_LoadGameScreen;
        private GameObject m_NewGameSetup;
        private Coroutine m_SetupReveal;
        private bool m_StartingNewGame;
        private bool m_UsingMouse;
        private Selectable m_SetupDefaultButton;
        private TMP_Text m_SetupDescription;
        private CanvasGroup m_DescriptionGroup;
        private bool m_DescriptionVisible;
        private static readonly string[] k_SetupDescriptions =
        {
            "Choose your difficulty for this playthrough.",
            "Choose the language used for game text.",
            "Choose between Classic and Modern controls."
        };
        private TMP_Text m_DifficultyValue;
        private TMP_Text m_LanguageValue;
        private TMP_Text m_ControlTypeValue;
        private HorrorGame.General.GameDifficulty m_SelectedDifficulty;
        private int m_SelectedLanguageIndex;
        private int m_SelectedControlTypeIndex;
        private readonly List<(Selectable Selector, Button Previous, Button Next)> m_SetupSelectors = new();

        private static readonly string[] k_ControlTypes = { "Classic", "Modern" };

        public UnityEvent OnNewGame;

        // --------------------------------------------------------------------

        private void Start()
        {
            foreach (var button in m_MainScreen.GetComponentsInChildren<Button>(true))
            {
                var backing = button.targetGraphic as Image;
                ApplyMenuSelectionStyle(button);
                if (backing && backing != button.targetGraphic)
                    backing.color = Color.clear;
                if (button.targetGraphic is TMP_Text label)
                {
                    label.enableAutoSizing = false;
                    label.fontSize = 18f;
                    label.alignment = TextAlignmentOptions.Center;
                }
                foreach (var image in button.GetComponentsInChildren<Image>(true))
                    if (image.name.StartsWith("Selected", System.StringComparison.Ordinal))
                        image.enabled = false;
            }
            int lastSavedSlot = GameSaveUtils.GetLastSavedSlot();
            m_LoadButton.gameObject.SetActive(lastSavedSlot >= 0);

            Debug.Assert(m_LoadGamePrefab, "LoadGamePrefab hasn't been assigned", this);
            if (m_LoadGamePrefab)
            {
                var loadScreenObj = Instantiate(m_LoadGamePrefab);

                m_LoadGameScreen = loadScreenObj.GetComponent<UILoadGame>();

                m_LoadGameScreen.OnShow.AddListener(() => { m_MainScreen.gameObject.SetActive(false); });
                m_LoadGameScreen.OnHide.AddListener(() => { m_MainScreen.gameObject.SetActive(true); });
            }
        }

        // --------------------------------------------------------------------

        private void OnEnable()
        {
            CursorController.Instance.SetInUI(true);   
        }

        // --------------------------------------------------------------------

        private void Update()
        {
            if (m_StartingNewGame) return;
            UpdateMenuInputMode();
            if (m_LoadGameScreen && m_LoadGameScreen.gameObject.activeSelf)
            {
                return;
            }

            if (m_NewGameSetup && m_NewGameSetup.activeSelf)
            {
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                    HideNewGameSetup();

                if (!m_UsingMouse && EventSystem.current && !EventSystem.current.currentSelectedGameObject && m_SetupDefaultButton
                    && m_NewGameSetup.GetComponent<CanvasGroup>().interactable)
                    EventSystem.current.SetSelectedGameObject(m_SetupDefaultButton.gameObject);
                HandleSetupAdjustment();
                return;
            }

            if (!m_UsingMouse && m_MainScreen.activeInHierarchy)
                EventSystemUtils.SelectDefaultOnLostFocus(m_DefaultButton.gameObject);
        }


        private void UpdateMenuInputMode()
        {
            var events = EventSystem.current;
            if (!events || InputBindingSettings.IsRebinding) return;
            var keyboard = Keyboard.current;
            bool keyboardUsed = keyboard != null && keyboard.anyKey.wasPressedThisFrame;
            var mouse = Mouse.current;
            bool mouseUsed = mouse != null && (mouse.delta.ReadValue().sqrMagnitude > .01f ||
                mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame ||
                mouse.scroll.ReadValue().sqrMagnitude > 0f);

            if (mouseUsed && !keyboardUsed && !m_UsingMouse)
            {
                m_UsingMouse = true;
                events.SetSelectedGameObject(null);
            }
            if (!keyboardUsed) return;

            m_UsingMouse = false;
            if (!events.currentSelectedGameObject)
            {
                if (m_NewGameSetup && m_NewGameSetup.activeSelf && m_NewGameSetup.GetComponent<CanvasGroup>().interactable)
                    events.SetSelectedGameObject(m_SetupDefaultButton.gameObject);
                else if (m_MainScreen.activeInHierarchy)
                    events.SetSelectedGameObject(m_DefaultButton.gameObject);
            }

            if (events.currentSelectedGameObject &&
                (NeedsManualSubmit(keyboard.enterKey) || NeedsManualSubmit(keyboard.numpadEnterKey) || NeedsManualSubmit(keyboard.spaceKey)))
                ExecuteEvents.Execute(events.currentSelectedGameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
        }

        private bool NeedsManualSubmit(UnityEngine.InputSystem.Controls.KeyControl key)
        {
            if (!key.wasPressedThisFrame) return false;
            if (EventSystem.current.currentInputModule is InputSystemUIInputModule module && module.submit?.action is InputAction submit && submit.enabled)
                foreach (var control in submit.controls)
                    if (control == key) return false;
            return true;
        }

        
        // --------------------------------------------------------------------

        public void NewGame()
        {
            ShowNewGameSetup();
        }

        /// <summary>Confirms the selected new-game setup and begins the scene transition.</summary>
        public void StartNewGame()
        {
            if (m_StartingNewGame) return;
            m_StartingNewGame = true;
            if (m_NewGameSetup && m_NewGameSetup.activeSelf)
            {
                var group = m_NewGameSetup.GetComponent<CanvasGroup>();
                group.interactable = group.blocksRaycasts = false;
                StartCoroutine(FadeOutSetup(group));
            }
            HorrorGame.General.DifficultySettings.Save(m_SelectedDifficulty);
            var settings = SettingsManager.Instance;
            var movement = settings.FindSetting<GameSettingsElementMovementType>();
            if (movement)
                settings.Set(movement, (m_SelectedControlTypeIndex == 0 ? PlayerMovementType.Tank : PlayerMovementType.Alternate).ToString());
            var language = settings.FindSetting<GameSettingsElementLanguage>();
            var locales = GetSetupLocales();
            if (language && locales != null && m_SelectedLanguageIndex >= 0 && m_SelectedLanguageIndex < locales.Count)
                settings.Set(language, locales[m_SelectedLanguageIndex].LocaleName);
            settings.SaveAndApply();
            PlayerPrefs.Save();
            CursorController.Instance.SetInUI(false);

            GetComponent<HorrorGame.General.MainMenuBackgroundCarousel>()?.PlayExitFade();

            ObjectStateManager.Instance.ClearStates();

            if (m_StartTransition) m_StartTransition.RunNewGameTutorial = true;
            OnNewGame?.Invoke();
        }

        private System.Collections.IEnumerator FadeOutSetup(CanvasGroup group)
        {
            const float duration = .25f;
            float initialAlpha = group.alpha;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.SmoothStep(initialAlpha, 0f, elapsed / duration);
                yield return null;
            }
            group.alpha = 0f;
            group.gameObject.SetActive(false);
        }

        private void ShowNewGameSetup()
        {
            if (!m_NewGameSetup)
                CreateNewGameSetup();

            m_SelectedDifficulty = HorrorGame.General.DifficultySettings.Current;
            var settings = SettingsManager.Instance;
            var movement = settings.FindSetting<GameSettingsElementMovementType>();
            var currentMode = movement ? movement.GetAsEnum<PlayerMovementType>() : settings.Defaults.MovementType;
            m_SelectedControlTypeIndex = currentMode == PlayerMovementType.Tank ? 0 : 1;
            SelectCurrentLanguage();
            RefreshNewGameSetupValues();

            m_MainScreen.SetActive(false);
            ShowSetupDescription(0);
            m_DescriptionGroup.alpha = 1f;
            m_NewGameSetup.SetActive(true);
            var group = m_NewGameSetup.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = group.blocksRaycasts = false;
            var branding = GetComponent<HorrorGame.General.MainMenuBackgroundCarousel>();
            if (branding)
                branding.FadeLogoForNewGameSetup(true, RevealNewGameSetup);
            else
                RevealNewGameSetup();
        }

        private void RevealNewGameSetup()
        {
            if (!m_NewGameSetup.activeSelf) return;
            m_SetupReveal = StartCoroutine(FadeInSetup());
        }

        private System.Collections.IEnumerator FadeInSetup()
        {
            var group = m_NewGameSetup.GetComponent<CanvasGroup>();
            const float duration = .16f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                if (!m_NewGameSetup.activeSelf) yield break;
                group.alpha = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                yield return null;
            }
            if (!m_NewGameSetup.activeSelf) yield break;
            group.alpha = 1f;
            group.interactable = group.blocksRaycasts = true;
            m_SetupReveal = null;
            if (!m_UsingMouse && EventSystem.current && m_SetupDefaultButton)
                EventSystem.current.SetSelectedGameObject(m_SetupDefaultButton.gameObject);
        }

        private void HideNewGameSetup()
        {
            if (!m_NewGameSetup) return;
            if (m_SetupReveal != null)
            {
                StopCoroutine(m_SetupReveal);
                m_SetupReveal = null;
            }
            m_NewGameSetup.SetActive(false);
            m_MainScreen.SetActive(true);
            GetComponent<HorrorGame.General.MainMenuBackgroundCarousel>()?.FadeLogoForNewGameSetup(false);
            if (EventSystem.current && m_DefaultButton)
                EventSystem.current.SetSelectedGameObject(m_DefaultButton.gameObject);
        }

        private void CreateNewGameSetup()
        {
            Transform parent = m_MainScreen.transform.parent;
            m_NewGameSetup = new GameObject("New Game Setup", typeof(RectTransform), typeof(CanvasGroup));
            m_NewGameSetup.transform.SetParent(parent, false);
            RectTransform setupRect = m_NewGameSetup.GetComponent<RectTransform>();
            setupRect.anchorMin = new Vector2(.5f, .305f);
            setupRect.anchorMax = setupRect.anchorMin;
            setupRect.pivot = new Vector2(.5f, .5f);
            setupRect.anchoredPosition = Vector2.zero;
            setupRect.sizeDelta = new Vector2(400f, 200f);

            var layout = m_NewGameSetup.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 9f;
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            CreateSetupText(m_NewGameSetup.transform, "NEW GAME", 24, 0f, 30f, TextAlignmentOptions.Center, true);
            CreateSetupRow("Difficulty", () => ChangeDifficulty(-1), () => ChangeDifficulty(1), out m_DifficultyValue);
            CreateSetupRow("Language", () => ChangeLanguage(-1), () => ChangeLanguage(1), out m_LanguageValue);
            CreateSetupRow("Control Type", () => ChangeControlType(-1), () => ChangeControlType(1), out m_ControlTypeValue);

            var description = new GameObject("Setting Description", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(CanvasGroup));
            description.transform.SetParent(m_NewGameSetup.transform, false);
            m_DescriptionGroup = description.GetComponent<CanvasGroup>();
            m_DescriptionGroup.blocksRaycasts = false;
            description.GetComponent<LayoutElement>().preferredHeight = 41f;
            var backing = description.GetComponent<Image>();
            backing.color = new Color(.035f, .03f, .025f, .88f);
            backing.raycastTarget = false;

            m_SetupDescription = CreateSetupText(description.transform, "", 14, 0f, 0f, TextAlignmentOptions.Center, false);
            m_SetupDescription.raycastTarget = false;
            m_SetupDescription.enableAutoSizing = true;
            m_SetupDescription.fontSizeMin = 9f;
            m_SetupDescription.fontSizeMax = 14f;
            m_SetupDescription.overflowMode = TextOverflowModes.Ellipsis;
            var descriptionRect = m_SetupDescription.rectTransform;
            descriptionRect.anchorMin = Vector2.zero;
            descriptionRect.anchorMax = Vector2.one;
            descriptionRect.offsetMin = new Vector2(12f, 4f);
            descriptionRect.offsetMax = new Vector2(-12f, -4f);

            Button startButton = CreateSetupButton(m_NewGameSetup.transform, "START GAME", StartNewGame, 160f, 30f);
            if (!m_SetupDefaultButton) m_SetupDefaultButton = startButton;
            Button backButton = CreateSetupButton(m_NewGameSetup.transform, "BACK", HideNewGameSetup, 160f, 30f);
            ConfigureSetupNavigation(startButton, backButton);
            m_NewGameSetup.SetActive(false);
        }

        private void LateUpdate()
        {
            if (!m_NewGameSetup || !m_NewGameSetup.activeSelf) return;
            m_DescriptionGroup.alpha = Mathf.MoveTowards(m_DescriptionGroup.alpha,
                m_DescriptionVisible ? 1f : 0f, Time.unscaledDeltaTime / .18f);
            var panel = (RectTransform)m_NewGameSetup.transform;
            Rect available = ((RectTransform)panel.parent).rect;
            float scale = Mathf.Min(1f, available.width * .9f / panel.sizeDelta.x, available.height * .55f / panel.sizeDelta.y);
            panel.localScale = Vector3.one * scale;
        }

        private void CreateSetupRow(string label, UnityAction previous, UnityAction next, out TMP_Text valueText)
        {
            var row = new GameObject(label + " Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(m_NewGameSetup.transform, false);
            row.GetComponent<LayoutElement>().preferredHeight = 30f;
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            CreateSetupText(row.transform, label.ToUpperInvariant(), 16, 144f, 30f, TextAlignmentOptions.Left, true);
            var selector = new GameObject(label + " Selector", typeof(RectTransform), typeof(Image), typeof(Button), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            selector.transform.SetParent(row.transform, false);
            selector.GetComponent<Image>().color = Color.clear;
            selector.GetComponent<LayoutElement>().preferredWidth = 250f;
            selector.GetComponent<LayoutElement>().preferredHeight = 34f;
            var selectorLayout = selector.GetComponent<HorizontalLayoutGroup>();
            selectorLayout.padding = new RectOffset(0, 0, 0, 0);
            selectorLayout.spacing = 0f;
            selectorLayout.childAlignment = TextAnchor.MiddleCenter;
            selectorLayout.childControlWidth = true;
            selectorLayout.childControlHeight = true;
            selectorLayout.childForceExpandWidth = false;
            selectorLayout.childForceExpandHeight = false;

            Button previousArrow = null;
            var focus = selector.GetComponent<Button>();
            focus.transition = Selectable.Transition.None;
            previousArrow = CreateSelectorArrow(selector.transform, "‹", () =>
            {
                if (EventSystem.current) EventSystem.current.SetSelectedGameObject(selector);
                previous();
                PopSelectorArrow(previousArrow);
            });
            if (!m_SetupDefaultButton) m_SetupDefaultButton = focus;
            valueText = CreateSelectorValue(selector.transform);
            Button nextArrow = null;
            nextArrow = CreateSelectorArrow(selector.transform, "›", () =>
            {
                if (EventSystem.current) EventSystem.current.SetSelectedGameObject(selector);
                next();
                PopSelectorArrow(nextArrow);
            });
            focus.onClick.AddListener(() => nextArrow.onClick.Invoke());
            previousArrow.navigation = new Navigation { mode = Navigation.Mode.None };
            nextArrow.navigation = previousArrow.navigation;
            previousArrow.transition = nextArrow.transition = Selectable.Transition.None;
            var value = valueText;
            var callbacks = selector.AddComponent<UISelectableCallbacks>();
            int rowIndex = m_SetupSelectors.Count;
            Color normalValue = value.color;
            Color normalArrow = previousArrow.targetGraphic.color;
            callbacks.OnSelected.AddListener(_ =>
            {
                value.color = previousArrow.targetGraphic.color = nextArrow.targetGraphic.color = MenuHighlight;
                ShowSetupDescription(rowIndex);
            });
            callbacks.OnDeselected.AddListener(_ =>
            {
                value.color = normalValue;
                previousArrow.targetGraphic.color = nextArrow.targetGraphic.color = normalArrow;
            });
            m_SetupSelectors.Add((focus, previousArrow, nextArrow));
        }

        private void ConfigureSetupNavigation(Button startButton, Button backButton)
        {
            startButton.gameObject.AddComponent<UISelectableCallbacks>().OnSelected.AddListener(_ => m_DescriptionVisible = false);
            backButton.gameObject.AddComponent<UISelectableCallbacks>().OnSelected.AddListener(_ => m_DescriptionVisible = false);
            for (int i = 0; i < m_SetupSelectors.Count; i++)
            {
                Selectable up = i == 0 ? backButton : m_SetupSelectors[i - 1].Selector;
                Selectable down = i == m_SetupSelectors.Count - 1 ? startButton : m_SetupSelectors[i + 1].Selector;
                m_SetupSelectors[i].Selector.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = up, selectOnDown = down };
            }
            startButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = m_SetupSelectors[m_SetupSelectors.Count - 1].Selector, selectOnDown = backButton };
            backButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = startButton, selectOnDown = m_SetupDefaultButton };
        }

        private void HandleSetupAdjustment()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !EventSystem.current || !m_NewGameSetup.GetComponent<CanvasGroup>().interactable) return;
            bool previous = keyboard.aKey.wasPressedThisFrame || keyboard.qKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame;
            bool next = keyboard.dKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame;
            if (previous == next) return;
            foreach (var row in m_SetupSelectors)
                if (row.Selector.gameObject == EventSystem.current.currentSelectedGameObject)
                    (previous ? row.Previous : row.Next).onClick.Invoke();
        }

        internal static readonly Color MenuHighlight = new Color(1f, .94f, .21f);

        internal static void ApplyMenuSelectionStyle(Selectable control)
        {
            if (control is Button && control.GetComponentInChildren<TMP_Text>(true) is TMP_Text label)
                control.targetGraphic = label;
            else if (control is Button && control.GetComponentInChildren<Text>(true) is Text legacyLabel)
                control.targetGraphic = legacyLabel;
            var colors = control.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = colors.selectedColor = MenuHighlight;
            colors.pressedColor = new Color(.86f, .75f, .15f);
            control.colors = colors;
            control.transition = Selectable.Transition.ColorTint;
        }

        private void ShowSetupDescription(int rowIndex)
        {
            m_DescriptionVisible = true;
            m_SetupDescription.text = GameText.Translate(k_SetupDescriptions[rowIndex]);
        }

        private TMP_Text CreateSelectorValue(Transform parent)
        {
            var valueObject = new GameObject("Value", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
            valueObject.transform.SetParent(parent, false);
            var value = valueObject.GetComponent<TextMeshProUGUI>();
            TMP_Text templateText = m_DefaultButton.GetComponentInChildren<TMP_Text>(true);
            value.font = templateText.font;
            value.fontSharedMaterial = templateText.fontSharedMaterial;
            value.color = new Color(.9f, .89f, .80f);
            value.fontSize = 18f;
            value.textWrappingMode = TextWrappingModes.NoWrap;
            value.enableAutoSizing = true;
            value.fontSizeMin = 10f;
            value.fontSizeMax = 18f;
            value.overflowMode = TextOverflowModes.Ellipsis;
            value.fontStyle = FontStyles.Bold;
            value.alignment = TextAlignmentOptions.Center;
            var layout = valueObject.GetComponent<LayoutElement>();
            layout.preferredWidth = 160f;
            layout.preferredHeight = 34f;
            return value;
        }

        private Button CreateSelectorArrow(Transform parent, string glyph, UnityAction action)
        {
            bool previous = glyph == "‹";
            var buttonObject = new GameObject(previous ? "Previous Arrow" : "Next Arrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);
            var hitTarget = buttonObject.GetComponent<Image>();
            hitTarget.color = new Color(1f, 1f, 1f, .001f);
            Button button = buttonObject.GetComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            button.colors = m_DefaultButton.colors;
            button.navigation = m_DefaultButton.navigation;
            button.onClick.AddListener(action);
            var layout = buttonObject.GetComponent<LayoutElement>();
            layout.preferredWidth = 45f;
            layout.preferredHeight = 34f;

            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.transform.SetParent(buttonObject.transform, false);
            var icon = iconObject.GetComponent<Image>();
            icon.sprite = m_SelectorArrowIcon;
            icon.color = m_DefaultButton.GetComponentInChildren<TMP_Text>(true).color;
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            var iconRect = icon.rectTransform;
            iconRect.anchorMin = new Vector2(.5f, .5f);
            iconRect.anchorMax = new Vector2(.5f, .5f);
            iconRect.pivot = new Vector2(.5f, .5f);
            iconRect.sizeDelta = new Vector2(25f, 25f);
            iconRect.localEulerAngles = new Vector3(0f, 0f, previous ? 90f : -90f);
            button.targetGraphic = icon;
            return button;
        }

        private void PopSelectorArrow(Button arrow)
        {
            if (!arrow) return;
            StartCoroutine(PopSelectorArrowRoutine(arrow.transform));
        }

        private System.Collections.IEnumerator PopSelectorArrowRoutine(Transform arrow)
        {
            const float duration = .14f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float wave = Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI);
                arrow.localScale = Vector3.one * Mathf.Lerp(1f, 1.22f, wave);
                yield return null;
            }
            if (arrow) arrow.localScale = Vector3.one;
        }

        private TMP_Text CreateSetupText(Transform parent, string value, int fontSize, float width, float height, TextAlignmentOptions alignment, bool bold)
        {
            var textObject = new GameObject(value + " Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<TextMeshProUGUI>();
            TMP_Text templateText = m_DefaultButton.GetComponentInChildren<TMP_Text>(true);
            text.font = templateText.font;
            text.fontSharedMaterial = templateText.fontSharedMaterial;
            text.color = templateText.color;
            text.fontSize = fontSize;
            text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.alignment = alignment;
            text.text = value;
            var layout = textObject.GetComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = height;
            return text;
        }

        private Button CreateSetupButton(Transform parent, string label, UnityAction action, float width, float height)
        {
            return CreateMenuTextButton(parent, label + " Button", label, action, width, height);
        }

        private Button CreateMenuTextButton(Transform parent, string objectName, string label, UnityAction action, float width, float height)
        {
            var buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);
            var hitTarget = buttonObject.GetComponent<Image>();
            hitTarget.color = new Color(1f, 1f, 1f, .001f);
            var button = buttonObject.GetComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            button.colors = m_DefaultButton.colors;
            button.navigation = m_DefaultButton.navigation;
            button.onClick.AddListener(action);
            var layout = buttonObject.GetComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = height;

            var textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(buttonObject.transform, false);
            var text = textObject.GetComponent<TextMeshProUGUI>();
            TMP_Text templateText = m_DefaultButton.GetComponentInChildren<TMP_Text>(true);
            text.font = templateText.font;
            text.fontSharedMaterial = templateText.fontSharedMaterial;
            text.color = templateText.color;
            text.fontSize = 18f;
            text.alignment = TextAlignmentOptions.Center;
            text.text = label;
            text.raycastTarget = false;
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            button.targetGraphic = text;
            return button;
        }

        private void ChangeDifficulty(int direction)
        {
            int value = ((int)m_SelectedDifficulty + direction + 3) % 3;
            m_SelectedDifficulty = (HorrorGame.General.GameDifficulty)value;
            RefreshNewGameSetupValues();
        }

        private static IReadOnlyList<Locale> GetSetupLocales()
        {
            var language = SettingsManager.Instance.FindSetting<GameSettingsElementLanguage>();
            if (language && language.Languages is { Length: > 0 }) return language.Languages;
            return LocalizationSettings.AvailableLocales?.Locales;
        }

        private void SelectCurrentLanguage()
        {
            var locales = GetSetupLocales();
            if (locales == null || locales.Count == 0)
            {
                m_SelectedLanguageIndex = 0;
                return;
            }

            Locale selected = LocalizationSettings.SelectedLocale;
            m_SelectedLanguageIndex = 0;
            for (int i = 0; i < locales.Count; i++)
            {
                if (locales[i] == selected)
                {
                    m_SelectedLanguageIndex = i;
                    break;
                }
            }
        }

        private void ChangeLanguage(int direction)
        {
            var locales = GetSetupLocales();
            if (locales == null || locales.Count == 0) return;
            m_SelectedLanguageIndex = (m_SelectedLanguageIndex + direction + locales.Count) % locales.Count;
            RefreshNewGameSetupValues();
        }

        private void ChangeControlType(int direction)
        {
            m_SelectedControlTypeIndex = (m_SelectedControlTypeIndex + direction + k_ControlTypes.Length) % k_ControlTypes.Length;
            RefreshNewGameSetupValues();
        }

        private void RefreshNewGameSetupValues()
        {
            if (m_DifficultyValue) m_DifficultyValue.text = m_SelectedDifficulty.ToString().ToUpperInvariant();
            if (m_ControlTypeValue) m_ControlTypeValue.text = k_ControlTypes[m_SelectedControlTypeIndex].ToUpperInvariant();
            if (!m_LanguageValue) return;

            var locales = GetSetupLocales();
            m_LanguageValue.text = locales != null && locales.Count > 0
                ? locales[m_SelectedLanguageIndex].LocaleName.ToUpperInvariant()
                : "DEFAULT";
        }

        // --------------------------------------------------------------------

        public void LoadGame()
        {
            m_LoadGameScreen.Show(GetComponent<IUIInput>());
        }

        // --------------------------------------------------------------------

        public void QuitGame()
        {
#if UNITY_EDITOR
            EditorApplication.ExecuteMenuItem("Edit/Play");
#else
            Application.Quit();
#endif
        }
    }

}
