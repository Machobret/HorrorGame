using System;
using System.Collections.Generic;
using UnityEngine;

namespace HouseToolkit
{
    [AddComponentMenu("Procedural Environment/Filler Building")]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class FillerBuilding : ProceduralSurface
    {
        public enum GroundStyle { Inherit, Normal, Solid, Material }

        [Serializable]
        public sealed class Finish
        {
            public int wall, floor;
            public Material material;
            public GroundStyle groundStyle;
        }

        [Serializable]
        public sealed class Entrance
        {
            public int wall, bay;
        }

        [HideInInspector] public GroundPlan footprint=new GroundPlan();
        public FillerBuildingType buildingType;
        public Material facade, roof;
        public const int MaxStoreys=64;
        [Range(1,MaxStoreys)] public int floors=3;
        [Min(.1f)] public float clearHeight=HouseFloor.DefaultHeight;
        [Min(.1f)] public float bayWidth=3;
        [HideInInspector] public int seed;
        [HideInInspector] public List<Finish> finishes=new List<Finish>();
        [HideInInspector] public List<Entrance> entrances=new List<Entrance>();
        public Material DefaultFacade=>facade ? facade : buildingType ? buildingType.facade : null;
        public float FloorPitch=>clearHeight+House.DefaultSlabThickness;
        public float Elevation(int storeys)
        {
            float ground=buildingType && buildingType.groundFloorHeight>0 ? buildingType.groundFloorHeight : clearHeight;
            return storeys<=0 ? 0 : ground+House.DefaultSlabThickness+(storeys-1)*FloorPitch;
        }
        public void SetType(FillerBuildingType value)
        {
            var previous=DefaultFacade;
            buildingType=value;
            facade=value ? null : previous;
            if(!value)return;
            bayWidth=value.preferredBayWidth;
            PlaceInitialEntrance();
        }

        internal void PlaceInitialEntrance()
        {
            if(!buildingType || !buildingType.placeEntrance || footprint.faces.Count!=1 || entrances.Count!=0)return;
            GroundEdge longest=null;
            float minimum=bayWidth*bayWidth;
            foreach(var edge in footprint.edges)
            {
                float length=(edge.b-edge.a).sqrMagnitude;
                if(length<minimum || (longest!=null && length==minimum))continue;
                longest=edge;minimum=length;
            }
            if(longest!=null)SetEntrance(longest.id,Bays(longest)/2);
        }

        internal override GroundPlan Areas { get=>footprint; set=>footprint=value; }
        public override Material Pavement=>roof;

        // Wall 0 means the whole floor. Floor -1 means every storey of a wall.
        public Finish FinishAt(int wall,int floor)=>finishes.Find(f=>f.wall==wall && f.floor==floor);
        public Material Override(int wall,int floor)=>FinishAt(wall,floor)?.material;
        public Material MaterialFor(int wall,int floor)
        {
            return MaterialFor(wall,floor,FinishAt(wall,floor));
        }
        Material MaterialFor(int wall,int floor,Finish finish)
        {
            if(floor==0 && finish?.groundStyle==GroundStyle.Solid && buildingType && buildingType.solidFacade)
                return buildingType.solidFacade;
            if(floor==0 && (finish?.groundStyle==GroundStyle.Normal || finish?.groundStyle==GroundStyle.Solid))
                return DefaultFacade;
            var value=finish?.material;
            if(!value)value=Override(0,floor);
            if(!value)value=Override(wall,-1);
            if (value) return value;
            return floor > 0 && !facade && buildingType && buildingType.upperFacade ? buildingType.upperFacade : DefaultFacade;
        }
        public void SetFinish(int wall,int floor,Material value)
        {
            finishes.RemoveAll(f=>f.wall==wall && f.floor==floor);
            if(value)finishes.Add(new Finish{wall=wall,floor=floor,material=value});
        }
        public void SetGroundStyle(int wall,GroundStyle style,Material material=null)
        {
            finishes.RemoveAll(f=>f.wall==wall && f.floor==0);
            if(style!=GroundStyle.Inherit)
                finishes.Add(new Finish{wall=wall,floor=0,groundStyle=style,material=style==GroundStyle.Material ? material : null});
        }
        public int Bays(GroundEdge edge)=>Mathf.Clamp(Mathf.RoundToInt(Vector2.Distance(edge.a,edge.b)/Mathf.Max(.1f,bayWidth)),1,512);
        public int EntranceBay(int wall)=>entrances.Find(e=>e.wall==wall)?.bay ?? -1;
        public void SetEntrance(int wall,int bay)
        {
            entrances.RemoveAll(e=>e.wall==wall);
            if(bay>=0)entrances.Add(new Entrance{wall=wall,bay=bay});
        }

        static readonly Vector2[] cellUV={Vector2.zero,Vector2.right,Vector2.one,Vector2.up};
        static readonly int roofTrimHeight=Shader.PropertyToID("_RoofTrimHeight");
        static Color32? AtlasStyle(Material source,int? row,GroundStyle groundStyle)
        {
            bool explicitBay=groundStyle==GroundStyle.Normal || groundStyle==GroundStyle.Solid;
            if((!row.HasValue && !explicitBay) || !source || !source.HasProperty("_BayType"))return null;
            int atlasRow=row ?? Mathf.RoundToInt(source.GetFloat("_FamilyRow"));
            int bay=explicitBay ? (groundStyle==GroundStyle.Solid ? 1 : 0) : Mathf.RoundToInt(source.GetFloat("_BayType"));
            return new Color32((byte)(1+Mathf.Clamp(atlasRow,0,4)),(byte)bay,
                (byte)Mathf.RoundToInt(source.GetFloat("_PlainMetalCoping")),255);
        }

        // Stock authoring presets differ in atlas addressing. Explicit custom materials keep their own draw state.
        static bool CanShare(Material source,Material shared)
        {
            return source && shared && source.GetTag("TownFacadePreset",false)=="1" && source.shader==shared.shader &&
                source.mainTexture==shared.mainTexture && source.color==shared.color &&
                source.GetFloat("_Smoothness")==shared.GetFloat("_Smoothness") &&
                source.GetFloat("_Metallic")==shared.GetFloat("_Metallic") && source.renderQueue==shared.renderQueue &&
                (source.GetFloat("_BayType")<1.5f || source.GetTexture("_DisplayMap")==shared.GetTexture("_DisplayMap"));
        }

        static Vector3 At(Vector2 p,float y)=>new Vector3(p.x,y,p.y);
        internal override RoadMesh CreateGeometry()
        {
            var mesh=new RoadMesh();
            if(footprint.faces.Count!=1)return mesh;
            var face=footprint.faces[0];
            var polygon=face.polygon;
            int levels=Mathf.Clamp(floors,1,MaxStoreys);
            float top=Elevation(levels);
            var roofHit=new RoadHit(RoadPart.Ground,face.id);
            for(int i=0;i<face.triangles.Length;i+=3)
            {
                var tri=new Vector3[3];
                for(int j=0;j<3;j++)tri[j]=At(polygon[face.triangles[i+j]],top);
                mesh.Face(roof,roofHit,Vector3.up,tri);
            }
            var walls=new Dictionary<(Vector2,Vector2),GroundEdge>();
            foreach(var edge in footprint.edges){walls[(edge.a,edge.b)]=edge;walls[(edge.b,edge.a)]=edge;}
            for(int side=0;side<polygon.Length;side++)
            {
                var a=polygon[side];var b=polygon[(side+1)%polygon.Length];
                var edge=walls[(a,b)];
                var normal=Vector3.Cross(Vector3.up,At(b-a,0)).normalized;
                int count=Bays(edge);
                int door=EntranceBay(edge.id);
                var hit=new RoadHit(RoadPart.Boundary,edge.id);
                for(int floor=0;floor<levels;floor++)
                {
                    float low=Elevation(floor),high=Elevation(floor+1);
                    var finish=FinishAt(edge.id,floor);
                    var groundStyle=floor==0 && finish!=null ? finish.groundStyle : GroundStyle.Inherit;
                    var material=MaterialFor(edge.id,floor,finish);
                    float trim=floor==levels-1 && material && material.HasProperty(roofTrimHeight)
                        ? Mathf.Clamp(material.GetFloat(roofTrimHeight),0,(high-low)*.25f) : 0;
                    float wallTop=high-trim;
                    float vTop=1-trim/(high-low);
                    var renderMaterial=material;
                    int? row=null;
                    if(buildingType && material && (material==buildingType.facade || material==buildingType.upperFacade))
                        row=buildingType.atlasRow;
                    else if(buildingType && CanShare(material,buildingType.facade))
                    {
                        renderMaterial=buildingType.facade;
                        row=Mathf.RoundToInt(material.GetFloat("_FamilyRow"));
                    }
                    var style=AtlasStyle(material,row,groundStyle);
                    var wallUV=trim>0 ? new[]{Vector2.zero,Vector2.right,new Vector2(1,vTop),new Vector2(0,vTop)} : cellUV;
                    for(int bay=0;bay<count;bay++)
                    {
                        Vector2 start=Vector2.Lerp(a,b,(float)bay/count),end=Vector2.Lerp(a,b,(bay+1f)/count);
                        var quad=new[]{At(start,low),At(end,low),At(end,wallTop),At(start,wallTop)};
                        bool entrance=floor==0 && groundStyle!=GroundStyle.Solid && bay==Mathf.Min(door,count-1);
                        mesh.Face(renderMaterial,hit,normal,quad,wallUV,facade:new Vector4(edge.id,floor,entrance ? -1-bay : bay,seed),facadeStyle:style);
                        if(trim>0)
                        {
                            // Negative wall IDs identify the coping in the atlas. Picking keeps the real wall ID.
                            var band=new[]{At(start,wallTop),At(end,wallTop),At(end,high),At(start,high)};
                            mesh.Face(renderMaterial,hit,normal,band,cellUV,facade:new Vector4(-edge.id,floor,bay,seed),facadeStyle:style);
                        }
                    }
                }
            }
            return mesh;
        }
    }
}
