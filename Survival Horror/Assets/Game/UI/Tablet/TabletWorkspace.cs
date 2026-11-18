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
        Color normal=new Color(.045f,.10f,.13f),selected=new Color(.15f,.28f,.32f);

        void OnEnable(){Map.HasTabletWorkspace=true;selectedMap=null;nextSearch=0;SetPage(0);}
        void OnDisable(){Unbind();if(Map)Map.AllowMapNavigation=true;}
        void LateUpdate()
        {
            if(selectedMap!=Map.CurrentMap || (!Network && Time.unscaledTime>=nextSearch))
            {
                selectedMap=Map.CurrentMap;nextSearch=Time.unscaledTime+1;BindNetwork();
            }
            if(Directory && Directory.activeSelf)Legend.SetActive(false);
            Map.AllowMapNavigation=Page==0;
            if(Page==2)RefreshLog();
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
            bool changed=Network.TryToggleCircuit(id);RefreshPower();
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
