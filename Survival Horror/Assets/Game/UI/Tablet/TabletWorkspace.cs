using System.Collections.Generic;
using System.Text;
using HorrorEngine;
using HorrorGame.Chapter1;
using TMPro;
using UnityEngine;
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
            selectedMap = null;
            nextSearch = 0;
            previousObjective = null;
            completionStarted = float.NegativeInfinity;
            if (completionPopup) completionPopup.gameObject.SetActive(false);
            SetPage(0);
        }
        void OnDisable(){Unbind();if(Map)Map.AllowMapNavigation=true;}
        void LateUpdate()
        {
            if (Map.IsTabletLoading) return;
            RefreshObjectiveBoard();
            if(selectedMap!=Map.CurrentMap || (!Network && Time.unscaledTime>=nextSearch))
            {
                selectedMap=Map.CurrentMap;nextSearch=Time.unscaledTime+1;BindNetwork();
            }
            if(Directory && Directory.activeSelf)Legend.SetActive(false);
            Map.AllowMapNavigation=Page==0;
            if(Page==2)RefreshLog();
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
            if (empty && !string.IsNullOrWhiteSpace(previousObjective))
            {
                completionStarted = Time.unscaledTime;
                completedObjectiveText.text = previousObjective;
                completionPopup.gameObject.SetActive(true);
            }
            previousObjective = objective;
            objectiveText.text = empty ? "NO OBJECTIVE" : objective;
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

        internal static void AnimatePopup(CanvasGroup group, float elapsed, float remaining)
        {
            var rect = (RectTransform)group.transform;
            float enter = Mathf.Clamp01(elapsed / .28f);
            float leave = 1f - Mathf.Clamp01(remaining / .35f);
            float travel = Mathf.Max(40f, rect.rect.height);
            rect.anchoredPosition = Vector2.up * (travel * Mathf.Max(Mathf.Pow(1f - enter, 3f), leave * leave * leave));
            group.alpha = Mathf.SmoothStep(0f, 1f, enter) * (1f - Mathf.SmoothStep(0f, 1f, leave));
        }

        internal static CanvasGroup CreatePopup(Transform surface, Vector2 min, Vector2 max,
            TMP_FontAsset font, string title, bool signal, out TMP_Text body, string subtitle = null,
            bool exclamation = false)
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
            var heading = CreateObjectiveLabel(popup, "Heading", .62f, .96f, 12f, font, fitToRow: true);
            heading.text = title;
            heading.color = ObjectiveGold;
            body = CreateObjectiveLabel(popup, "Message", subtitle == null ? .07f : .30f, .62f, 16f, font, fitToRow: true);
            body.color = Color.white;
            if (subtitle != null)
            {
                var hint = CreateObjectiveLabel(popup, "Subtitle", .04f, .30f, 12f, font, fitToRow: true);
                hint.text = subtitle;
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
            Page=Mathf.Clamp(page,0,2);if(Footer)Footer.text=Page==0?"PAN WASD / ZOOM WHEEL / FLOOR UP-DOWN / CLOSE ESC":Page==1?"SELECT A CIRCUIT / REVIEW LOAD BEFORE ENABLING / CLOSE ESC":"SCROLL TO READ / CLOSE ESC";Map.CloseDirectory();Map.AllowMapNavigation=Page==0;
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
            foreach(var candidate in Object.FindObjectsByType<PowerMapCircuitPanel>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))
                if(candidate.MapData==Map.CurrentMap && candidate.SourceGrid){Network=candidate;break;}
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
                if(!Network.CanAccessCircuit(circuit.Id))continue;
                var row=Instantiate(PowerRowTemplate,PowerRows);row.gameObject.SetActive(true);string id=circuit.Id;
                row.onClick.AddListener(()=>ToggleCircuit(id));rows.Add(row);circuits.Add(circuit);
            }
        }
        public void ToggleCircuit(string id)
        {
            if(!Grid || !Network || !Network.CanAccessCircuit(id) || Grid.IsShutdown)return;
            var opening = Chapter1Opening.Instance;
            if (opening && opening.IsDepotCircuit(Grid, id))
            {
                bool unlocked = Network.TryToggleCircuit(id);
                RefreshPower();
                PowerHelp.text = unlocked ? "Depot unlocked." : "Move closer to the depot gate.";
                return;
            }
            bool changed=Network.TryToggleCircuit(id);RefreshPower();
            if (Network.SequencePuzzle && Network.SequencePuzzle.ShowingWrongAnswer)
            {
                PowerHelp.text = "Shutter pattern rejected. Check the sequence note.";
                return;
            }
            if(!changed)PowerHelp.text="Move closer to the circuit control before changing its power.";
        }
        public void RefreshPower()
        {
            if(!Network || !Grid){NetworkStatus.text="NO POWER NETWORK FOR THIS MAP";PowerHelp.text="Connect to a district service network to view its circuits.";return;}
            NetworkStatus.text=Grid.IsShutdown?"SUPPLY TRIPPED / RESET AT FUSE BOX":"LOAD  "+Grid.CurrentConsumption+" / "+Grid.MaximumCapacity+"     AVAILABLE  "+Grid.RemainingCapacity;
            NetworkStatus.color=Grid.IsShutdown?new Color(.85f,.49f,.36f):new Color(.65f,.84f,.88f);
            PowerHelp.text=rows.Count==0?"Find a circuit diagram to identify and control this network.":Grid.IsShutdown?"All circuits are off. Restore supply at the physical fuse box.":"Enabling a circuit above capacity immediately trips the supply.";
            for(int i=0;i<rows.Count;i++)
            {
                var circuit=circuits[i];bool on=Grid.IsCircuitPowered(circuit.Id);bool trips=!on && Grid.CurrentConsumption+circuit.Consumption>Grid.MaximumCapacity;
                rows[i].GetComponentInChildren<TMP_Text>().text=circuit.DisplayName+"   /   "+circuit.Consumption+" UNITS   /   "+(on?"[ON]":"[OFF]")+(trips?"\nWARNING: ENABLING WILL TRIP SUPPLY":"");
                rows[i].interactable=!Grid.IsShutdown;rows[i].GetComponent<Image>().color=trips?new Color(.28f,.13f,.10f):on?selected:normal;
                var opening = Chapter1Opening.Instance;
                if (opening && opening.IsDepotCircuit(Grid, circuit.Id))
                {
                    rows[i].GetComponentInChildren<TMP_Text>().text = opening.DepotUnlocked ? "Depot unlocked" : "Unlock Depot";
                    rows[i].interactable = !Grid.IsShutdown && !opening.DepotUnlocked;
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
