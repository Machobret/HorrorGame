using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    internal static class GarageDoorFields
    {
        static readonly System.Random random=new System.Random();
        internal static Vector2 BrushSize;
        internal static int BrushTile=random.Next(16);
        static bool randomize=true;
        static Material preview;
        internal static int RandomTile()=>random.Next(16);
        internal static void Advance(){if(randomize)BrushTile=random.Next(16);}

        internal static void Brush(GarageDoorInsert garage)
        {
            BrushSize=Dimensions(garage.ClearSize(BrushSize));
            randomize=EditorGUILayout.Toggle("Random finish",randomize);
            int chosen=Swatches(garage,randomize?-1:BrushTile);
            if(chosen>=0){BrushTile=chosen;randomize=false;}
        }

        internal static void Selection(GarageDoorInsert garage,ref Vector2 size,ref int tile)
        {
            size=Dimensions(garage.ClearSize(size));
            int chosen=Swatches(garage,tile);
            if(chosen>=0){tile=chosen;GUI.changed=true;}
            if(GUILayout.Button("Randomize finish")){tile=random.Next(16);GUI.changed=true;}
        }

        internal static Vector2 Dimensions(Vector2 size)
        {
            size.x=EditorGUILayout.DelayedFloatField("Clear width (m)",size.x);
            size.y=EditorGUILayout.DelayedFloatField("Clear height (m)",size.y);
            return size;
        }

        internal static int Swatches(GarageDoorInsert garage,int selected)
        {
            var rect=EditorGUILayout.GetControlRect(false,100);
            float side=Mathf.Min(25,rect.width/4);
            for(int i=0;i<16;i++)
            {
                var cell=new Rect(rect.x+(i%4)*side,rect.y+(i/4)*side,side,side);
                if(GUI.Button(cell,new GUIContent("", "Finish "+(i+1)),GUIStyle.none))return i;
                if(Event.current.type!=EventType.Repaint)continue;
                EditorGUI.DrawRect(cell,i==selected?Color.cyan:Color.black);
                var inset=new Rect(cell.x+2,cell.y+2,side-4,side-4);
                if(garage.atlas)GUI.DrawTextureWithTexCoords(inset,garage.atlas,new Rect(i%4*.25f,(3-i/4)*.25f,.25f,.25f));
            }
            return -1;
        }

        internal static void Preview(HouseWall wall,PlacedOpening placement,GarageDoorInsert garage)
        {
            if(!garage.atlas || !garage.ValidSize(placement.clearSize))return;
            var door=garage.prefab.GetComponent<GarageDoor>();
            if(!door || !door.leaf)return;
            if(!preview)
            {
                var shader=Shader.Find("Unlit/Texture");
                if(!shader)return;
                preview=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
                AssemblyReloadEvents.beforeAssemblyReload+=()=>Object.DestroyImmediate(preview);
            }
            var clear=garage.ClearSize(placement.clearSize);
            preview.mainTexture=garage.atlas;
            preview.mainTextureScale=Vector2.one*.25f;
            preview.mainTextureOffset=new Vector2(placement.finishTile%4,3-placement.finishTile/4)*.25f;
            var camera=SceneView.currentDrawingSceneView.camera;
            float side=Mathf.Sign(wall.transform.InverseTransformPoint(camera.transform.position).z);
            var matrix=wall.transform.localToWorldMatrix*Matrix4x4.TRS(new Vector3(placement.distance,placement.sill,side*(wall.Thickness*.5f+.03f)),
                placement.reversed?Quaternion.Euler(0,180,0):Quaternion.identity,new Vector3(clear.x,clear.y,.05f));
            using(new Handles.DrawingScope(Color.green,wall.transform.localToWorldMatrix))
            {
                float w=clear.x,h=clear.y,j=GarageDoorInsert.Jamb,top=GarageDoorInsert.Header;
                for(int sign=-1;sign<=1;sign+=2)
                    Handles.DrawWireCube(new Vector3(placement.distance+sign*(w+j)*.5f,h*.5f,0),new Vector3(j,h,.13f));
                Handles.DrawWireCube(new Vector3(placement.distance,h+top*.5f,0),new Vector3(w+2*j,top,.18f));
            }
            if(preview.SetPass(0))Graphics.DrawMeshNow(door.leaf.GetComponent<MeshFilter>().sharedMesh,matrix);
        }
    }
}
