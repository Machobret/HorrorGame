using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    internal static partial class HouseWallOutputBuilder
    {
        private static void UpdateCornerGroups(HouseWallOutput output,Dictionary<string,HouseWallOutput.Group> previous,
            List<HouseWallOutput.Group> next,HouseWallBuilder builder,HouseGenerationData data,HouseWallLayout.Result layout,
            HashSet<string> failedTrims)
        {
            var plan=builder.GetComponent<HousePlan>();
            foreach(var cover in layout.Corners)
            {
                float height=HouseHeightWalls.CornerTop(plan,cover,data) - cover.TopInset;
                Add(cover,"corner:",0,height,CornerMaterial(plan,cover));
                var first=plan.Data.Walls.First(w=>w.Id==cover.WallA);
                var second=plan.Data.Walls.First(w=>w.Id==cover.WallB);
                var firstTrim = HouseTrimLayout.Resolve(plan, first);
                var secondTrim = HouseTrimLayout.Resolve(plan, second);
                if(firstTrim?.Mesh==null || secondTrim?.Mesh==null
                    || !HouseTrimLayout.Eligible(plan.Data,first) || !HouseTrimLayout.Eligible(plan.Data,second))continue;
                if (failedTrims.Contains(first.Id) || failedTrims.Contains(second.Id))
                {
                    // A failed attached leg hides its one existing cover too. Reuse
                    // the leg interpretation; do not run a weaker profile-only check.
                    if (previous.TryGetValue("trim-corner:" + cover.Id, out var retained) && retained.Root != null)
                    {
                        retained.Root.SetActive(false);
                        next.Add(retained);
                    }
                    continue;
                }
                var a=Direction(first); var b=Direction(second);
                Vector2 Band(FloorPlan.Wall wall,Vector2 along)
                {
                    var band=HouseTrimLayout.DepthRange(plan.Data,wall,builder.Data,HouseTrimLayout.Resolve(plan,wall).Depth);
                    plan.Data.TryGetExteriorOutward(wall,out var outward);
                    return Vector2.Dot(new Vector2(-along.y,along.x),outward)>0 ? band : HouseWallLayout.ReverseDepths(band);
                }
                var trimCover=HouseWallLayout.FitCornerCover(cover.Id,cover.Node,cover.WallA,cover.WallB,a,b,Band(first,a),Band(second,b));
                float baseA=firstTrim.Offset;
                float baseB=secondTrim.Offset;
                float bottom=Mathf.Max(baseA,baseB);
                float top=Mathf.Min(baseA+firstTrim.Height,baseB+secondTrim.Height);
                Add(trimCover,"trim-corner:",bottom,top,HouseFinishResolution.Trim(firstTrim,plan));

                Vector2 Direction(FloorPlan.Wall wall)=>(plan.Data.GetPosition(wall.StartId==cover.Id?wall.EndId:wall.StartId)-cover.Node).normalized;
            }

            void Add(HouseWallLayout.CornerCover cover,string prefix,float bottom,float top,Material material)
            {
                if(cover.Size.x<=.0001f || cover.Size.y<=.0001f || top-bottom<=.0001f)return;
                // Position is placement, not shape. Moving an unchanged cover must not
                // create another native hierarchy (and another pair of Undo lifetimes).
                string inputs="local-corner-cover-v1:"+JsonUtility.ToJson(new CoverInputs
                {Along=cover.Along,Size=cover.Size,Profile=cover.Profile,Bottom=bottom,Top=top});
                // Preserve the existing replacement guard for fixed Boolean cutters.
                if (data.BooleanInputs.Length > 0) inputs += ":center:" + cover.Center.ToString("R");
                // Normalize only an exact old binding; its mesh may contain paint.
                if (previous.TryGetValue(prefix + cover.Id, out var prior) && GroupIsHealthy(prior)
                    && prior.Root.transform.parent == output.transform && prior.Signature == LegacySignature(cover,bottom,top))
                {
                    var center = new Vector3(cover.Center.x,0,cover.Center.y);
                    foreach (Transform child in prior.Root.transform) child.localPosition -= center;
                    prior.Root.transform.localPosition = center;
                    prior.Signature = GroupSignature(prefix + cover.Id,inputs);
                }
                if (RetainEditedGroup(output, previous, next, prefix + cover.Id, inputs)) return;
                var group=RetainOrCreate(output,previous,prefix+cover.Id,inputs,out bool changed);
                group.Root.SetActive(true);
                if(changed)
                {
                    var filter=WallModuleExamples.CreatePart(group.Root,"Corner cover"); filter.sharedMesh=HouseCornerCoverMesh.For(cover);
                    filter.transform.localRotation=RotationAlong(cover.Along);
                    var origin=-(cover.Along*cover.Size.x+new Vector2(-cover.Along.y,cover.Along.x)*cover.Size.y)/2;
                    filter.transform.localPosition=new Vector3(origin.x,bottom,origin.y);
                    filter.transform.localScale=new Vector3(cover.Size.x,top-bottom,cover.Size.y);
                }
                group.Root.transform.localPosition = new Vector3(cover.Center.x, 0, cover.Center.y);
                HouseCornerCoverMesh.PrepareRetainedMapping(group.Root.GetComponentInChildren<MeshFilter>(true));
                group.Root.GetComponentInChildren<MeshRenderer>(true).sharedMaterial=HouseFinishResolution.RenderMaterial(material);
                next.Add(group);
            }
        }

        [System.Serializable] private struct CoverInputs
        { public Vector2 Along,Size; public Vector2[] Profile; public float Bottom,Top; }
        private static string LegacySignature(HouseWallLayout.CornerCover cover,float bottom,float top)
        {
            string inputs="corner-cover-v3:"+JsonUtility.ToJson(new LegacyCoverInputs
            {Center=cover.Center,Along=cover.Along,Size=cover.Size,Profile=cover.Profile,Bottom=bottom,Top=top});
            if (cover.Profile == null)
                inputs="corner-cover-v2:"+JsonUtility.ToJson(new RectangleCoverInputs
                {Center=cover.Center,Along=cover.Along,Size=cover.Size,Bottom=bottom,Top=top});
            if (cover.Profile == null && cover.Size.x == cover.Size.y)
                inputs="square-cover-v1:"+JsonUtility.ToJson(new SquareCoverInputs
                {Center=cover.Center,Along=cover.Along,Size=cover.Size.x,Bottom=bottom,Top=top});
            return Hash128.Compute(inputs).ToString();
        }
        [System.Serializable] private struct LegacyCoverInputs
        { public Vector2 Center,Along,Size; public Vector2[] Profile; public float Bottom,Top; }
        [System.Serializable] private struct RectangleCoverInputs
        { public Vector2 Center,Along,Size; public float Bottom,Top; }
        [System.Serializable] private struct SquareCoverInputs
        { public Vector2 Center,Along; public float Size,Bottom,Top; }

        private static Material CornerMaterial(HousePlan plan,HouseWallLayout.CornerCover cover)
        {
            // A single finish belongs to the missing corner sector. Stable wall ID
            // order resolves competing overrides without triangle-by-triangle policies.
            var wall=plan.Data.Walls.First(w=>w.Id==cover.WallA);
            var stored=plan.Data.GetPosition(wall.EndId)-plan.Data.GetPosition(wall.StartId);
            bool left=Vector2.Dot(new Vector2(-stored.y,stored.x),cover.Center-cover.Node)>0;
            return HouseFinishResolution.Wall(plan, wall, left);
        }
    }
}
