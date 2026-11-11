using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HorrorEngine;
using UnityEditor;
using UnityEngine;
using TMPro;
using Object=UnityEngine.Object;

namespace HorrorGame.Tests
{
    public static class NewspaperPlayCheck
    {
        static double next;
        static int stage;
        static DocumentData document;
        static string report;
        [MenuItem("Horror Game/Tests/Check Newspaper Pickup")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play mode first.");
            document=AssetDatabase.LoadAssetAtPath<DocumentData>("Assets/Game/Items/Newspaper/Newspaper_CityHerald.asset");
            var pickup=Object.FindObjectsByType<PickupDocument>(FindObjectsSortMode.None)
                .Single(p=>new SerializedObject(p).FindProperty("m_Data").objectReferenceValue==document);
            Check(!GameManager.Instance.Inventory.Documents.Contains(document),"Document should be unread in a fresh Play session");
            pickup.GetComponentInChildren<Interactive>().Interact(null);
            Check(!pickup.gameObject.activeSelf,"Physical newspaper hides after collection");
            Check(GameManager.Instance.Inventory.Documents.Contains(document),"Newspaper added to native document collection");
            Check(GameManager.Instance.Inventory.GetSavableData().Documents.Contains(document.UniqueId),"Saved inventory includes document ID");
            var database=AssetDatabase.LoadAssetAtPath<DocumentDatabase>("Assets/AssetStore/HorrorEngine/ExampleContent/Databases/DocumentDB.asset");
            Check(database.GetRegister(document.UniqueId)==document,"Document ID resolves for loading");
            report="Native Interactive -> PickupDocument.Take: PASS\nPhysical prop hidden and document collected: PASS\nInventory save ID and database lookup: PASS\n";
            stage=0;next=EditorApplication.timeSinceStartup+15;EditorApplication.update+=Tick;
        }
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        static void Tick()
        {
            if(EditorApplication.timeSinceStartup<next)return;
            try
            {
                var ui=UIManager.Get<UIDocument>();
                if(stage==0)
                {
                    Check(ui.gameObject.activeInHierarchy,"Document reader opens automatically");
                    Check(ui.GetComponentsInChildren<TMP_Text>(true).Any(t=>t.text.Contains("1,998 DEAD")),"Reader shows casualty code");
                    Check(Time.timeScale==0,"Reader pauses gameplay");
                    var text=(TMP_Text)typeof(UIDocument).GetField("m_Text",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui);
                    for(int page=0;page<document.Pages.Count;page++)
                    {
                        text.ForceMeshUpdate();Check(!text.isTextOverflowing,"Article page "+(page+1)+" overflows reader");
                        if(page<document.Pages.Count-1)typeof(UIDocument).GetMethod("ShowNextPage",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,null);
                    }
                    Check(text.text.Contains("Digits only"),"Last page shows the handwritten clue");
                    report+="Reader opens with casualty clue; all pages fit and margin note is readable: PASS\n";
                }
                else if(stage==1)
                {
                    typeof(UIDocument).GetMethod("Hide",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,null);
                    Check(Time.timeScale>0&&!ui.gameObject.activeSelf,"Closing reader restores gameplay");
                    ui.Show(document);report+="Close restores gameplay; reopen collected document: PASS\n";
                }
                else
                {
                    Check(ui.gameObject.activeInHierarchy,"Collected newspaper can be reread");
                    typeof(UIDocument).GetMethod("Hide",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,null);
                    Finish();return;
                }
                stage++;next=EditorApplication.timeSinceStartup+(stage==1?15:2);
            }
            catch(Exception e){report+="FAIL: "+e;Finish();}
        }
        static void Finish()
        {
            EditorApplication.update-=Tick;
            var folder=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Exports/Reviews/Newspaper");
            Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"PlayCheck.txt"),report);
            Debug.Log("NEWSPAPER_PLAY_CHECK\n"+report);
        }
    }
}
