using System.Collections.Generic;
using System.Text;
using HorrorEngine;
using HorrorGame.Chapter1;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HorrorGame.UI
{
    /// <summary>Presentation of the native map, collected information and the authored district power grid.</summary>
    public sealed class TabletWorkspace : MonoBehaviour
    {
        public UIMap Map;
        public RawImage MapImage;
        public GameObject MapFrame,Floors,MapTools,Legend,PowerPage,LogPage,Directory;
        public TMP_Text NetworkStatus,PowerHelp,LogText,Footer;
        public Transform PowerRows;
        public Button PowerRowTemplate;
        public Button BackToMap;
        public TabletMapAnnotations Annotations;
        public int Page { get; private set; }
        public PowerMapCircuitPanel Network { get; private set; }
        public DistrictPowerGrid Grid => Network ? Network.SourceGrid : null;
        MapData selectedMap;
        DistrictPowerGrid subscribedGrid;
        readonly List<Button> rows=new List<Button>();
        readonly List<PowerCircuit> circuits=new List<PowerCircuit>();
        float nextSearch;
        private GameObject controlsGuide;
        private TMP_Text guideHeader, guideBody, guideButtonLabel;
        private Image guideStripe;
        private int guidePage;
        private int guideInputFrame = -1;
        public bool IsControlsGuideOpen => controlsGuide && controlsGuide.activeInHierarchy;

        private static readonly (string Title, string Copy)[] GuidePages =
        {
            ("OPEN AND CLOSE", "Press M to open your tablet. Press M or ESC to close it and return to the game."),
            ("READ THE MAP", "Use WASD or click and drag to pan. Scroll to zoom. Press I to reopen your inventory."),
            ("POWER A CIRCUIT", "Aim at a lightning bolt and press E, or click it. Restore power before using a circuit marked NO POWER. CONNECTION LOST means the circuit is out of reach.")
        };

        private TMP_Text objectiveText;
        private Image objectiveBackground;
        private CanvasGroup completionPopup;
        private TMP_Text completedObjectiveText;
        private string previousObjective;
        private float completionStarted = float.NegativeInfinity;
        private static readonly Color ObjectiveGold = new Color(1f, .73f, .37f);
        private static readonly Color ObjectiveBackground = new Color(.15f, .17f, .18f, .96f);
        Color normal=new Color(.045f,.10f,.13f),selected=new Color(.15f,.28f,.32f);

        void OnEnable()
        {
            Map.HasTabletWorkspace = true;
            ConfigureMapTools();
            selectedMap = null;
            nextSearch = 0;
            previousObjective = null;
            completionStarted = float.NegativeInfinity;
            if (completionPopup) completionPopup.gameObject.SetActive(false);
            SetPage(0);
            if (Chapter1MapTutorial.Instance is { MapUnlocked: true, ControlsTutorialSeen: false })
                ShowControlsGuide();
        }
        void OnDisable()
        {
            Unbind();
            if (controlsGuide) controlsGuide.SetActive(false);
            if (Map) Map.AllowMapNavigation = true;
        }
        void LateUpdate()
        {
            if (Map.IsTabletLoading) return;
            if (Page == 0 && !IsControlsGuideOpen && Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
                ToggleLegend();
            RefreshObjectiveBoard();
            if(selectedMap!=Map.CurrentMap || (!Network && Time.unscaledTime>=nextSearch))
            {
                selectedMap=Map.CurrentMap;nextSearch=Time.unscaledTime+1;BindNetwork();
            }
            if(Directory && Directory.activeSelf)Legend.SetActive(false);
            Map.AllowMapNavigation=Page==0 && !IsControlsGuideOpen && guideInputFrame != Time.frameCount;
            if(Page==2)RefreshLog();
        }

        private void ConfigureMapTools()
        {
            if (!MapTools) return;
            SetMapToolVisible("Open power controls", false);
            SetMapToolVisible("Open service log", false);
            ConfigureMapTool("Map directory", "INVENTORY [I]",
                new Vector2(.005f, .835f), new Vector2(.20f, .88f));
            ConfigureMapTool("Legend toggle", "LEGEND [K]",
                new Vector2(.73f, .835f), new Vector2(.93f, .88f));
        }

        private void SetMapToolVisible(string name, bool visible)
        {
            var tool = MapTools.transform.Find(name);
            if (tool) tool.gameObject.SetActive(visible);
        }

        private void ConfigureMapTool(string name, string label, Vector2 min, Vector2 max)
        {
            var tool = MapTools.transform.Find(name) as RectTransform;
            if (!tool) return;
            tool.anchorMin = min;
            tool.anchorMax = max;
            tool.offsetMin = tool.offsetMax = Vector2.zero;
            var text = tool.GetComponentInChildren<TMP_Text>(true);
            if (text) text.text = label;
            if (name == "Map directory")
            {
                var button = tool.GetComponent<Button>();
                if (button)
                {
                    button.onClick = new Button.ButtonClickedEvent();
                    button.onClick.AddListener(Map.OpenInventory);
                }
            }
        }
        private void ShowControlsGuide()
        {
            if (!controlsGuide)
            {
                var startup = GetComponent<TabletStartupScreen>();
                var cover = startup && startup.Screen ? startup.Screen.transform : null;
                var parent = cover ? cover.parent : Footer.transform.parent;
                var overlay = CreateObjectiveRect("Tablet controls tutorial", parent,
                    new Vector2(.055f, .12f), new Vector2(.945f, .89f));
                controlsGuide = overlay.gameObject;
                if (cover) overlay.SetSiblingIndex(cover.GetSiblingIndex());
                var shade = overlay.gameObject.AddComponent<Image>();
                shade.color = new Color(.015f, .022f, .028f, .52f);

                var card = CreateObjectiveRect("Instructions", overlay, new Vector2(.12f, .18f), new Vector2(.88f, .82f));
                card.gameObject.AddComponent<Image>().color = new Color(.06f, .07f, .08f, .99f);
                guideStripe = CreateObjectiveRect("Status stripe", card, new Vector2(0f, .96f), Vector2.one)
                    .gameObject.AddComponent<Image>();
                guideStripe.raycastTarget = false;

                guideHeader = GuideLabel(card, "Heading", new Vector2(.065f, .72f), new Vector2(.945f, .93f), 34f);
                guideBody = GuideLabel(card, "Instructions", new Vector2(.065f, .28f), new Vector2(.935f, .70f), 30f);
                var buttonRect = CreateObjectiveRect("Next", card, new Vector2(.63f, .07f), new Vector2(.935f, .25f));
                var buttonImage = buttonRect.gameObject.AddComponent<Image>();
                buttonImage.color = new Color(.28f, .13f, .09f);
                var button = buttonRect.gameObject.AddComponent<Button>();
                button.targetGraphic = buttonImage;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.onClick.AddListener(AdvanceControlsGuide);
                guideButtonLabel = GuideLabel(buttonRect, "Key hint", new Vector2(.04f, .08f), new Vector2(.96f, .92f), 26f);
                guideButtonLabel.alignment = TextAlignmentOptions.Center;
                guideButtonLabel.color = ObjectiveGold;
            }
            controlsGuide.SetActive(true);
            Map.AllowMapNavigation = false;
            RedrawControlsGuide();
        }

        private TMP_Text GuideLabel(Transform parent, string labelName, Vector2 min, Vector2 max, float size)
        {
            var label = CreateObjectiveRect(labelName, parent, min, max).gameObject.AddComponent<TextMeshProUGUI>();
            if (Footer && Footer.font) label.font = Footer.font;
            label.enableAutoSizing = true;
            label.fontSizeMin = 11f;
            label.fontSizeMax = size;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.color = new Color(.94f, .87f, .78f);
            label.raycastTarget = false;
            return label;
        }

        private void RedrawControlsGuide()
        {
            var page = GuidePages[guidePage];
            if (guidePage == GuidePages.Length - 1 && Chapter1MapTutorial.Instance is { WaitingForCircuitPower: true } tutorial)
                page = ("RESTORE POWER", tutorial.Grid.Supply && tutorial.Grid.Supply.RequiresTabletAuthorization
                    ? "Enable the electricity box on the map, then close the tablet and pull the lever to unlock the gate."
                    : "Close the tablet and turn on the alley breaker.");
            Color accent = guidePage == GuidePages.Length - 1 ? new Color(1f, .34f, .27f) : ObjectiveGold;
            guideStripe.color = guideHeader.color = accent;
            GameText.SetLabel(guideHeader, (guidePage + 1) + " / " + GuidePages.Length + "   " + page.Title);
            GameText.SetLabel(guideBody, page.Copy);
            GameText.SetLabel(guideButtonLabel, guidePage == GuidePages.Length - 1 ? "E: DONE" : "E: NEXT");
        }

        public void AdvanceControlsGuide()
        {
            if (!IsControlsGuideOpen || Map.IsTabletLoading || guideInputFrame == Time.frameCount) return;
            guideInputFrame = Time.frameCount;
            if (guidePage + 1 < GuidePages.Length)
            {
                guidePage++;
                RedrawControlsGuide();
                return;
            }
            controlsGuide.SetActive(false);
            Chapter1MapTutorial.Instance?.CompleteControlsTutorial();
        }

        private void RefreshObjectiveBoard()
        {
            if (!objectiveText)
            {
                if (!MapImage || !Footer) return;
                var board = CreateObjectiveRect("Objective board", MapImage.transform.parent,
                    new Vector2(.66f, .89f), new Vector2(.925f, .965f));
                objectiveBackground = board.gameObject.AddComponent<Image>();
                objectiveBackground.color = ObjectiveBackground;
                objectiveBackground.raycastTarget = false;

                var star = CreateObjectiveRect("Objective star", board, new Vector2(.025f, .15f),
                    new Vector2(.13f, .85f)).gameObject.AddComponent<TabletHeroBadge>();
                star.StarOnly = true;
                star.color = ObjectiveGold;
                star.raycastTarget = false;

                var heading = CreateObjectiveLabel(board, "Objective heading", .58f, .95f, 12f);
                heading.text = "OBJECTIVE";
                heading.color = ObjectiveGold;
                objectiveText = CreateObjectiveLabel(board, "Current objective", .07f, .61f, 18f);
                // Scale from the text's left edge to keep the star's column clear.
                var textRect = objectiveText.rectTransform;
                textRect.pivot = new Vector2(0f, .5f);
                textRect.offsetMin = textRect.offsetMax = Vector2.zero;
                CreateCompletionPopup(board.parent);
            }

            var tutorial = Chapter1MapTutorial.Instance;
            string objective = tutorial ? tutorial.CurrentQuest : null;
            bool empty = string.IsNullOrWhiteSpace(objective);
            // A completed objective normally advances straight to the next one.  The
            // old empty-only test meant that path never played the tablet completion
            // pop; treat any actual objective transition as completing the previous
            // objective instead.
            if (!string.IsNullOrWhiteSpace(previousObjective) && previousObjective != objective)
            {
                completionStarted = Time.unscaledTime;
                GameText.SetLabel(completedObjectiveText, previousObjective);
                completionPopup.gameObject.SetActive(true);
            }
            previousObjective = objective;
            GameText.SetLabel(objectiveText, empty ? "NO OBJECTIVE" : objective);
            float elapsed = Time.unscaledTime - completionStarted;
            float progress = Mathf.Clamp01(elapsed / .48f);
            float glow = 1f - Mathf.SmoothStep(0f, 1f, progress);
            float pop = Mathf.Sin(progress * Mathf.PI) * (1f - progress) * .16f;
            objectiveText.rectTransform.localScale = new Vector3(1f + pop * .5f, 1f + pop, 1f);
            objectiveText.color = empty ? Color.Lerp(Color.white, ObjectiveGold, glow) : ObjectiveGold;
            objectiveBackground.color = Color.Lerp(ObjectiveBackground, new Color(.37f, .29f, .15f, .98f), glow);

            if (completionPopup.gameObject.activeSelf)
            {
                const float duration = 3f;
                if (elapsed >= duration)
                    completionPopup.gameObject.SetActive(false);
                else AnimatePopup(completionPopup, elapsed, duration - elapsed);
            }
        }

        private void CreateCompletionPopup(Transform surface)
        {
            completionPopup = CreatePopup(surface, new Vector2(.66f, .807f), new Vector2(.925f, .882f),
                Footer.font, "OBJECTIVE COMPLETE", false, out completedObjectiveText);
        }

        internal static void AnimatePopup(CanvasGroup group, float elapsed, float remaining, bool fromBottom = false)
        {
            var rect = (RectTransform)group.transform;
            float enter = Mathf.Clamp01(elapsed / .28f);
            float leave = 1f - Mathf.Clamp01(remaining / .35f);
            float travel = Mathf.Max(40f, rect.rect.height);
            Vector2 direction = fromBottom ? Vector2.down : Vector2.up;
            rect.anchoredPosition = direction * (travel * Mathf.Max(Mathf.Pow(1f - enter, 3f), leave * leave * leave));
            group.alpha = Mathf.SmoothStep(0f, 1f, enter) * (1f - Mathf.SmoothStep(0f, 1f, leave));
        }

        internal static CanvasGroup CreatePopup(Transform surface, Vector2 min, Vector2 max,
            TMP_FontAsset font, string title, bool signal, out TMP_Text body, string subtitle = null,
            bool exclamation = false, bool large = false)
        {
            var popup = CreateObjectiveRect(title, surface, min, max);
            var background = popup.gameObject.AddComponent<Image>();
            background.color = new Color(.19f, .19f, .16f, .98f);
            background.raycastTarget = false;
            var group = popup.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            var star = CreateObjectiveRect("Popup icon", popup, new Vector2(.025f, .15f),
                new Vector2(.13f, .85f)).gameObject.AddComponent<TabletHeroBadge>();
            star.StarOnly = true;
            star.SignalBars = signal;
            star.Exclamation = exclamation;
            star.color = ObjectiveGold;
            star.raycastTarget = false;
            var heading = CreateObjectiveLabel(popup, "Heading", .62f, .96f, large ? 18f : 12f, font, fitToRow: true);
            heading.text = GameText.Translate(title);
            heading.color = ObjectiveGold;
            body = CreateObjectiveLabel(popup, "Message", subtitle == null ? .07f : .30f, .62f, large ? 23f : 16f, font, fitToRow: true);
            body.color = Color.white;
            if (subtitle != null)
            {
                var hint = CreateObjectiveLabel(popup, "Subtitle", .04f, .30f, large ? 16f : 12f, font, fitToRow: true);
                hint.text = GameText.Translate(subtitle);
                hint.color = ObjectiveGold;
            }
            popup.gameObject.SetActive(false);
            return group;
        }

        private TMP_Text CreateObjectiveLabel(Transform parent, string name, float bottom, float top, float size) =>
            CreateObjectiveLabel(parent, name, bottom, top, size, Footer.font);

        private static TMP_Text CreateObjectiveLabel(Transform parent, string name, float bottom, float top,
            float size, TMP_FontAsset font, bool fitToRow = false)
        {
            var label = CreateObjectiveRect(name, parent, new Vector2(.15f, bottom),
                new Vector2(.96f, top)).gameObject.AddComponent<TextMeshProUGUI>();
            if (font) label.font = font;
            label.fontSize = size;
            // Popup rows scale with screen height and need room for a full font line.
            label.enableAutoSizing = fitToRow;
            if (fitToRow)
            {
                label.fontSizeMin = 7f;
                label.fontSizeMax = size;
            }
            label.enableWordWrapping = false;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            return label;
        }

        private static RectTransform CreateObjectiveRect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        public void ShowMap()=>SetPage(0);
        public void ShowPower()=>SetPage(1);
        public void ShowLog()=>SetPage(2);
        public void ToggleLegend(){bool open=!Legend.activeSelf;if(open)Map.CloseDirectory();Legend.SetActive(open);}
        public void SetPage(int page)
        {
            if (IsControlsGuideOpen) return;
            Page=Mathf.Clamp(page,0,2);if(Footer)Footer.text=Page==0?"WASD: PAN  |  CLICK + DRAG: PAN  |  WHEEL: ZOOM  |  UP or DOWN: FLOOR  |  ESC: CLOSE":Page==1?"SELECT A CIRCUIT  |  CHECK LOAD  |  ESC: CLOSE":"WHEEL: SCROLL  |  ESC: CLOSE";Map.CloseDirectory();Map.AllowMapNavigation=Page==0;
            bool hasMap=Map.CurrentMap && GameManager.Exists && GameManager.Instance.Inventory.Maps.Contains(Map.CurrentMap);
            MapImage.gameObject.SetActive(Page==0 && hasMap);MapFrame.SetActive(Page==0);Floors.SetActive(Page==0);MapTools.SetActive(Page==0);
            Annotations.gameObject.SetActive(Page==0 && hasMap);PowerPage.SetActive(Page==1);LogPage.SetActive(Page==2);Legend.SetActive(false);
            if(BackToMap)BackToMap.gameObject.SetActive(Page!=0);
            if(Page==1)BindNetwork();
            if(Page==2)RefreshLog();
        }
        void Unbind()
        {
            if(subscribedGrid){subscribedGrid.OnGridChanged.RemoveListener(RefreshPower);subscribedGrid.OnGridOverloaded.RemoveListener(RecordOverload);subscribedGrid.CircuitChanged-=RecordCircuit;}
            subscribedGrid=null;
        }
        void BindNetwork()
        {
            Unbind();Network=null;
            var currentController = MapController.GetCurrent();
            MapData currentRoomMap = currentController && currentController.GetCurrentRoom() ? currentController.Data : null;
            foreach(var candidate in Object.FindObjectsByType<PowerMapCircuitPanel>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))
                if(currentRoomMap && candidate.MapData==currentRoomMap && candidate.SourceGrid){Network=candidate;break;}
            subscribedGrid=Grid;
            if(subscribedGrid){subscribedGrid.OnGridChanged.AddListener(RefreshPower);subscribedGrid.OnGridOverloaded.AddListener(RecordOverload);subscribedGrid.CircuitChanged+=RecordCircuit;}
            BuildRows();RefreshPower();
        }
        void RecordCircuit(string id,bool enabled)
        {
            if(!GameManager.Exists || !Network || !Network.CanAccessCircuit(id))return;
            var circuit=Grid.Circuits.Find(c=>c.Id==id);GameManager.Instance.Inventory.AddTabletLog((circuit!=null?circuit.DisplayName:id)+(enabled?" / ON":" / OFF"));
        }
        void RecordOverload(){if(GameManager.Exists)GameManager.Instance.Inventory.AddTabletLog("SUPPLY TRIPPED / RESET AT FUSE BOX");}
        void BuildRows()
        {
            foreach(var row in rows)if(row){row.gameObject.SetActive(false);Destroy(row.gameObject);}rows.Clear();circuits.Clear();
            if(!Grid || !Network)return;
            foreach(var circuit in Grid.Circuits)
            {
                if(!Network.HasCircuitService(circuit.Id))continue;
                var row=Instantiate(PowerRowTemplate,PowerRows);row.gameObject.SetActive(true);string id=circuit.Id;
                row.onClick.AddListener(()=>ToggleCircuit(id));rows.Add(row);circuits.Add(circuit);
            }
        }
        public void ToggleCircuit(string id)
        {
            if(!Grid || !Network || !Network.CanAccessCircuit(id) || Grid.IsShutdown)return;
            if (!Network.HasCircuitService(id)) { PowerHelp.text = PowerMapCircuitPanel.ConnectionLostLabel; return; }
            if (Network.IsCircuitOutOfService(id)) { PowerHelp.text = PowerMapCircuitPanel.OutOfServiceLabel; return; }
            if (!Network.HasControlPower(id)) { PowerHelp.text = GameText.Translate("No power"); return; }
            bool changed=Network.TryToggleCircuit(id);RefreshPower();
            if (Network.SequencePuzzle && Network.SequencePuzzle.ShowingWrongAnswer)
            {
                PowerHelp.text = "Shutter pattern rejected. Check the sequence note.";
                return;
            }
            if(!changed)PowerHelp.text="Circuit state did not change.";
        }
        public void RefreshPower()
        {
            if(!Network || !Grid){NetworkStatus.text="NO POWER NETWORK FOR THIS MAP";PowerHelp.text="No circuit network in your current room.";return;}
            NetworkStatus.text=Grid.IsShutdown?"SUPPLY TRIPPED / RESET AT FUSE BOX":!Grid.EnforceCapacityLimit?"ROOM CIRCUITS":"LOAD  "+Grid.CurrentConsumption+" / "+Grid.MaximumCapacity+"     AVAILABLE  "+Grid.RemainingCapacity;
            NetworkStatus.color=Grid.IsShutdown?new Color(.85f,.49f,.36f):new Color(.65f,.84f,.88f);
            PowerHelp.text=rows.Count==0?"No accessible circuits in your current room.":Grid.IsShutdown?"All circuits are off. Restore supply at the physical fuse box.":Grid.EnforceCapacityLimit?"Enabling a circuit above capacity immediately trips the supply.":"";
            for(int i=0;i<rows.Count;i++)
            {
                var circuit=circuits[i];
                if (Network.IsCircuitOutOfService(circuit.Id))
                {
                    rows[i].interactable = false;
                    rows[i].GetComponent<Image>().color = normal;
                    rows[i].GetComponentInChildren<TMP_Text>().text = circuit.DisplayName + " [" +
                        PowerMapCircuitPanel.OutOfServiceLabel + "]";
                    continue;
                }
                if (!Network.HasCircuitService(circuit.Id))
                {
                    rows[i].interactable = false;
                    rows[i].GetComponent<Image>().color = normal;
                    rows[i].GetComponentInChildren<TMP_Text>().text = circuit.DisplayName + " [" +
                        PowerMapCircuitPanel.ConnectionLostLabel + "]";
                    continue;
                }
                bool on=Network.IsCircuitShownPowered(circuit.Id);bool trips=Grid.EnforceCapacityLimit && !on && Grid.CurrentConsumption+circuit.Consumption>Grid.MaximumCapacity;
                rows[i].GetComponentInChildren<TMP_Text>().text=circuit.DisplayName+(Grid.EnforceCapacityLimit?"   /   "+circuit.Consumption+" UNITS":"")+"   /   "+(on?"[ON]":"[OFF]")+(trips?"\nWARNING: ENABLING WILL TRIP SUPPLY":"");
                rows[i].interactable=!Grid.IsShutdown;rows[i].GetComponent<Image>().color=trips?new Color(.28f,.13f,.10f):on?selected:normal;
                if (Network.TryGetQueuedCircuit(circuit.Id, out bool queuedOn))
                {
                    rows[i].GetComponentInChildren<TMP_Text>().text = circuit.DisplayName + " [" +
                        (queuedOn ? "QUEUED ON" : "QUEUED OFF") + "]";
                    PowerHelp.text = GameText.Translate("Shutter changes apply when you close the tablet.");
                }
                var opening = Chapter1Opening.Instance;
                if (opening && opening.IsDepotCircuit(Grid, circuit.Id))
                {
                    if (opening.DepotMotor)
                    {
                        string state = !opening.DepotControlHasPower ? "NO POWER" : opening.DepotHasPower ? "ON" : "OFF";
                        rows[i].GetComponentInChildren<TMP_Text>().text = "Main shutter power [" + state + "]";
                    }
                    else
                    {
                        rows[i].GetComponentInChildren<TMP_Text>().text = opening.DepotUnlocked ? "Depot unlocked" : opening.DepotHasPower ? "Entrance shutter power [ON]" : "Entrance shutter power [OFF]";
                        rows[i].interactable = !Grid.IsShutdown && !opening.DepotUnlocked;
                    }
                }
            }
        }
        void RefreshLog()
        {
            if(!GameManager.Exists)return;
            var inventory=GameManager.Instance.Inventory;var text=new StringBuilder("FIELD RECORD\n\n");
            text.Append("FILES COLLECTED: ").Append(inventory.Documents.Count).Append("    READ: ").Append(inventory.ReadDocuments.Count).Append("\n");
            text.Append("\nSERVICE HISTORY\n");if(inventory.TabletLog.Count==0)text.Append("No recorded service changes.");
            foreach(var entry in inventory.TabletLog)text.Append(entry).Append('\n');LogText.text=text.ToString();
        }
    }
}
