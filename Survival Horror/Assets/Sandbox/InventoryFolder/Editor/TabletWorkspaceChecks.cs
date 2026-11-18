using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HorrorEngine;
using HorrorGame.Chapter1;
using HorrorGame.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object=UnityEngine.Object;

namespace HorrorGame.Tests
{
    public static class TabletWorkspaceChecks
    {
        static TabletWorkspace workspace;
        static DistrictPowerGrid grid;
        static int stage;
        const string TextPreference = "Settings.Game.InventoryTextSize";
        static string oldText;
        static bool capturedPreferences;
        static void Check(bool value,string text){if(!value)throw new Exception(text);Debug.Log("TABLET PASS: "+text);}
        public static void Begin()
        {
            var map=AssetDatabase.LoadAssetAtPath<MapData>("Assets/Game/Levels/StorageFacility/Assets/StorageFacility_Map.asset");
            Check(map,"test map exists");GameManager.Instance.Inventory.Maps.Add(map);
            var owner=new GameObject("Tablet validation power source");owner.SetActive(false);grid=owner.AddComponent<DistrictPowerGrid>();grid.MaximumCapacity=100;
            grid.Circuits=new List<PowerCircuit>{new PowerCircuit{Id="wash",DisplayName="Washing machines",Consumption=60},new PowerCircuit{Id="fridge",DisplayName="Pharmacy fridge",Consumption=50},new PowerCircuit{Id="secret",DisplayName="Undiscovered circuit",Consumption=10}};
            var controller=MapController.GetCurrent();
            Check(controller && controller.Data==map,"test player occupies the mapped facility");
            var room=controller.GetCurrentRoom();
            Check(room && room.TryGetComponent<ObjectUniqueId>(out _),"test room has a persistent map identity");
            string roomId=room.GetComponent<ObjectUniqueId>().Id;
            var layout=ScriptableObject.CreateInstance<PowerPanelLayout>();layout.Circuits=new List<PowerPanelCircuitShape>{new PowerPanelCircuitShape{CircuitId="wash",RoomUniqueId=roomId,WorldPosition=GameManager.Instance.Player.transform.position,ActivationRange=8,MapPosition=new Vector2(100,100)},new PowerPanelCircuitShape{CircuitId="fridge",RoomUniqueId=roomId,WorldPosition=GameManager.Instance.Player.transform.position,ActivationRange=8,MapPosition=new Vector2(200,100)},new PowerPanelCircuitShape{CircuitId="secret",RoomUniqueId="unknown",MapPosition=new Vector2(300,100)}};
            var diagram=ScriptableObject.CreateInstance<CircuitDiagramData>();diagram.Map=map;diagram.RoomUniqueId=roomId;GameManager.Instance.Inventory.Documents.Add(diagram);
            var network=owner.AddComponent<PowerMapCircuitPanel>();network.Grid=grid;network.MapData=map;network.Layout=layout;owner.SetActive(true);
            var ui=UIManager.Get<UIMap>();ui.Show(map);workspace=ui.GetComponent<TabletWorkspace>();Check(workspace,"tablet workspace registered on native map");oldText=PlayerPrefs.HasKey(TextPreference)?PlayerPrefs.GetString(TextPreference):null;capturedPreferences=true;
        }
        public static bool Tick()
        {
            if(stage==0)
            {
                Check(workspace.Page==0 && workspace.Map.AllowMapNavigation,"map starts with native navigation");
                workspace.Map.ToggleMapDirectory();var list=workspace.Map.GetComponentInChildren<UIMapList>(true);Check(list.gameObject.activeSelf,"directory opens as overlay");
                Check(Mathf.Abs(workspace.MapImage.rectTransform.anchorMin.x-.075f)<.001f,"directory does not move map viewport");workspace.Map.CloseDirectory();
                Check(!workspace.GetComponentsInChildren<TMP_InputField>(true).Any(),"map note input removed");Check(!workspace.GetComponentsInChildren<Button>(true).Any(b=>b.name=="Place note" || b.name=="Clear notes"),"note actions removed");
                workspace.ToggleLegend();stage++;return false;
            }
            if(stage==1)
            {
                Check(workspace.Legend.GetComponentsInChildren<TabletMapSymbol>().Length==5,"legend contains five actual marker symbols");Canvas.ForceUpdateCanvases();foreach(var symbol in workspace.Legend.GetComponentsInChildren<TabletMapSymbol>()){var mesh=symbol.canvasRenderer.GetMesh();Check(mesh && mesh.vertexCount>0,"legend glyph renders geometry: "+symbol.Kind);}Check(workspace.Legend.GetComponentsInChildren<TabletMapSymbol>().Select(s=>s.Kind).Distinct().Count()==5,"legend symbols have distinct meanings");Check(!workspace.Annotations.raycastTarget,"map overlay does not intercept navigation clicks");Capture("Tablet_Map.png");workspace.ShowPower();stage++;return false;
            }
            if(stage==2)
            {
                Check(workspace.Grid==grid,"power page uses existing scene grid");Check(!workspace.Map.AllowMapNavigation,"power page blocks map panning and marker interaction");
                Check(workspace.PowerRows.GetComponentsInChildren<Button>().Length==2,"only discovered circuits are listed");workspace.ToggleCircuit("secret");Check(grid.CurrentConsumption==0,"unknown circuit cannot be controlled");
                workspace.ToggleCircuit("wash");Check(grid.CurrentConsumption==60,"power row changes real grid load");Check(workspace.PowerRows.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("WARNING")),"overload warning shown before enabling circuit");Capture("Tablet_Power.png");workspace.ToggleCircuit("fridge");stage++;return false;
            }
            if(stage==3)
            {
                Check(grid.IsShutdown && grid.CurrentConsumption==0,"overload preserves fuse-box shutdown behavior");Check(workspace.NetworkStatus.text.Contains("RESET AT FUSE BOX"),"tripped supply directs player to physical reset");Capture("Tablet_Power_Tripped.png");workspace.ShowLog();stage++;return false;
            }
            if(stage==4)
            {
                Check(!workspace.LogText.text.Contains("MAP NOTES") && workspace.LogText.text.Contains("TRIPPED"),"log retains power history without note section");Capture("Tablet_Log.png");
                var readability = workspace.GetComponent<MenuReadability>();
                var textSize = Resources.Load<SettingsSet>("GameSettingsSet").Elements.OfType<MenuTextSizeSetting>().Single();
                Check(textSize.GetDefaultValue() == "Large", "large text is the Settings default");
                Check(!workspace.GetComponentsInChildren<Button>(true).Any(b => b.name == "Text size toggle"), "tablet has no local text-size control");
                SettingsManager.Instance.Set(textSize, "Large");
                textSize.SaveInPlayerPrefs();
                readability.Refresh();
                Check(MenuReadability.LargeText, "the tablet reads the saved text size from Settings");
                var header=workspace.GetComponentsInChildren<TMP_Text>(true).First(t=>t.name=="Device identification");Check(header.fontSize>header.GetComponent<MenuTextScale>().BaseSize,"larger-text option scales labels");
                RestorePreferences();readability.Refresh();
                var snapshot=GameManager.Instance.Inventory.GetSavableData();snapshot.Items=new List<InventoryEntrySaveData>();snapshot.EquippedItems=new List<InventoryEquippedEntrySaveData>();snapshot.Documents=new List<string>();snapshot.ReadDocuments=new List<string>();snapshot.Maps=new List<string>();var restored=new Inventory();restored.SetFromSavedData(JsonUtility.FromJson<InventorySaveData>(JsonUtility.ToJson(snapshot)));Check(restored.TabletLog.Any(s=>s.Contains("TRIPPED")),"service history survives save/load");
                var shotgun=AssetDatabase.LoadAssetAtPath<ItemData>("Assets/AssetStore/HorrorEngine/ExampleContent/Weapons/Shotgun/Shotgun.asset");var pistol=AssetDatabase.LoadAssetAtPath<ItemData>("Assets/AssetStore/HorrorEngine/ExampleContent/Weapons/Gun/Gun.asset");snapshot.MaxItems=8;snapshot.Items=new List<InventoryEntrySaveData>();for(int i=0;i<8;i++)snapshot.Items.Add(new InventoryEntrySaveData{ItemId=(i<3?shotgun:pistol).UniqueId,Count=1});restored.SetFromSavedData(snapshot);Check(restored.UsedSlots==11 && restored.Capacity==11 && restored.Items.Count(e=>e.Item)==8,"legacy saves preserve all items when footprints grow");
                snapshot.MapNotes=new List<TabletMapNote>{new TabletMapNote{MapId=workspace.Map.CurrentMap.UniqueId,Text="Legacy note"}};restored.SetFromSavedData(snapshot);Check(restored.GetSavableData().MapNotes==null,"legacy note data loads safely and is no longer saved");
                typeof(UIMap).GetMethod("Hide",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(workspace.Map,null);Check(!PauseController.Instance.IsPaused,"closing tablet restores gameplay");return true;
            }
            return false;
        }
        public static void RestorePreferences()
        {
            if (!capturedPreferences)
                return;

            if (oldText == null)
                PlayerPrefs.DeleteKey(TextPreference);
            else
                PlayerPrefs.SetString(TextPreference, oldText);
            PlayerPrefs.Save();
            SettingsManager.Instance?.Discard();
            capturedPreferences = false;
        }
        static void Capture(string filename)
        {
            var filter=workspace.GetComponent<TabletDisplayFilter>();var camera=filter.DisplayCamera;var old=camera.targetTexture;var target=new RenderTexture(1600,900,24);var filtered=new RenderTexture(1600,900,0);var previous=RenderTexture.active;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
            try{camera.targetTexture=target;Canvas.ForceUpdateCanvases();MapRenderer.Instance.RenderCamera.Render();camera.Render();Graphics.Blit(target,filtered,filter.Output.material);RenderTexture.active=filtered;image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();Directory.CreateDirectory("Exports/Reviews/TabletWorkspace");File.WriteAllBytes("Exports/Reviews/TabletWorkspace/"+filename,image.EncodeToPNG());Check(image.GetPixels32().Count(p=>p.r+p.g+p.b>70)>15000,"tablet page renders visible content: "+filename);}
            finally{camera.targetTexture=old;RenderTexture.active=previous;target.Release();filtered.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(filtered);Object.DestroyImmediate(image);Canvas.ForceUpdateCanvases();}
        }
    }
}
