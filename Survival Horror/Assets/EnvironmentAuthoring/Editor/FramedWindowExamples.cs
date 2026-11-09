using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace EnvironmentAuthoring.Editor
{
    // Small, editable teaching assets. Existing presets/materials are never overwritten.
    public static class FramedWindowExamples
    {
        public const string Folder = "Assets/EnvironmentAuthoring/Content/WallKits/FramedWindows";
        public const string FamilyPath = Folder + "/Framed Glass Windows.asset";

        [MenuItem("Assets/Environment/Create framed window examples")]
        public static void Create()
        {
            System.IO.Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var wall = AssetDatabase.LoadAssetAtPath<WallFamily>(WallFamilyExamples.CompleteFamilyPath);
            if (wall == null) throw new InvalidOperationException("Import the sample wall families first.");
            var frame = Finish("M_Frame_Painted_01", new Color(.16f, .19f, .2f, 1), false);
            var glass = Finish("M_Glass_Clear_01", new Color(.75f, .9f, 1, .25f), true);
            var models = new List<WallOpeningInsert>();
            foreach (string shape in new[] { "01", "02" })
            foreach (bool divided in new[] { false, true })
            {
                var module = wall.OpeningFor(FloorPlan.OpeningKind.Window, 1, shape);
                if (module == null) continue;
                string name = "Window " + shape + (divided ? " Divided" : " Clear");
                string path = Folder + "/" + name;
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path + " Mesh.asset");
                if (mesh == null) { mesh = BuildMesh(module.OpeningOutline, divided); mesh.name = name; AssetDatabase.CreateAsset(mesh, path + " Mesh.asset"); }
                var model = AssetDatabase.LoadAssetAtPath<WallOpeningInsert>(path + ".asset");
                if (model == null)
                {
                    model = ScriptableObject.CreateInstance<WallOpeningInsert>();
                    model.Kind = FloorPlan.OpeningKind.Window; model.Shape = shape;
                    model.SegmentWidth = 1; model.Opening = module.OpeningOutline;
                    model.Mesh = mesh; model.Material = frame; model.GlassMaterial = glass;
                    AssetDatabase.CreateAsset(model, path + ".asset");
                }
                models.Add(model);
            }
            var family = AssetDatabase.LoadAssetAtPath<OpeningFamily>(FamilyPath);
            if (family == null)
            {
                family = ScriptableObject.CreateInstance<OpeningFamily>(); family.Kind = FloorPlan.OpeningKind.Window;
                family.Sets = new[] { new OpeningFamily.ModelSet { Name = "Framed glass", State = OpeningFamily.Condition.Clean, Models = models.ToArray() } };
                AssetDatabase.CreateAsset(family, FamilyPath);
            }
            foreach (string path in new[] { WallFamilyExamples.CompleteFamilyPath, WallFamilyExamples.ExteriorFamilyPath })
            {
                var host = AssetDatabase.LoadAssetAtPath<WallFamily>(path);
                if (host == null || (host.OpeningFamilies ?? Array.Empty<OpeningFamily>()).Contains(family)) continue;
                Undo.RecordObject(host, "Support framed glass windows");
                host.OpeningFamilies = (host.OpeningFamilies ?? Array.Empty<OpeningFamily>()).Append(family).ToArray();
                EditorUtility.SetDirty(host); AssetDatabase.SaveAssetIfDirty(host);
            }
        }

        private static Material Finish(string name, Color tint, bool transparent)
        {
            string path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, enableInstancing = true };
            material.SetColor("_BaseColor", tint); material.SetFloat("_Smoothness", transparent ? .75f : .25f);
            if (transparent)
            {
                // URP Lit alpha blending preserves glass reflections independently of opacity.
                material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0);
                material.SetFloat("_BlendModePreserveSpecular", 1);
                material.SetFloat("_SrcBlend", (float)BlendMode.One);
                material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Transparent"); material.renderQueue = (int)RenderQueue.Transparent;
                material.SetShaderPassEnabled("ShadowCaster", false);
            }
            AssetDatabase.CreateAsset(material, path); return material;
        }

        private static Mesh BuildMesh(Rect opening, bool divided)
        {
            var cube = WallModuleExamples.BuildCornerBlock();
            var frame = new Mesh(); var parts = new List<CombineInstance>();
            const float rail = .05f;
            void Box(float x, float y, float w, float h) => parts.Add(new CombineInstance { mesh = cube,
                transform = Matrix4x4.TRS(new Vector3(x, y, -.04f), Quaternion.identity, new Vector3(w, h, .08f)) });
            try
            {
                Box(opening.xMin, opening.yMin, rail, opening.height);
                Box(opening.xMax - rail, opening.yMin, rail, opening.height);
                Box(opening.xMin + rail, opening.yMin, opening.width - 2 * rail, rail);
                Box(opening.xMin + rail, opening.yMax - rail, opening.width - 2 * rail, rail);
                if (divided) Box(opening.center.x - rail / 2, opening.yMin + rail, rail, opening.height - 2 * rail);
                frame.CombineMeshes(parts.ToArray(), true, true);
                var mesh = new Mesh();
                mesh.CombineMeshes(new[] { new CombineInstance { mesh = frame, transform = Matrix4x4.identity },
                    new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(
                        new Vector3(opening.xMin + rail, opening.yMin + rail, -.002f), Quaternion.identity,
                        new Vector3(opening.width - 2 * rail, opening.height - 2 * rail, .004f)) } }, false, true);
                mesh.RecalculateBounds(); return mesh;
            }
            finally { UnityEngine.Object.DestroyImmediate(cube); UnityEngine.Object.DestroyImmediate(frame); }
        }
    }
}
