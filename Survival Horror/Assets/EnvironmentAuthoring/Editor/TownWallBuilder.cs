using System.Collections.Generic;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    internal static class TownWallBuilder
    {
        internal static void Build(TownWallRun wall, List<TownRebuild.Part> parts)
        {
            foreach (var span in wall.Path.Spans)
            {
                var model = span.Model != null ? span.Model : wall.Model; TownModule.CheckCompatibility(model, TownModule.ModuleKind.WallPanel);
                var a = wall.Path.Find(span.Start).Position; var b = wall.Path.Find(span.End).Position;
                BuildSpan(span.Id, model, a, b, wall.Height,
                    span.Material != null ? span.Material : wall.FrontMaterial,
                    span.BackMaterial != null ? span.BackMaterial : wall.BackMaterial,
                    wall.GenerateCollision, parts, Post);
            }
            foreach (var point in wall.Path.Points) Post(point.Id + "/post", point.Position);
            void Post(string key, Vector3 position)
            {
                if (!wall.Posts || wall.PostMesh == null) return;
                AddPost(key, position, wall.PostMesh, wall.PostWidth,
                    wall.PostHeight > 0 ? wall.PostHeight : wall.Height,
                    wall.PostMaterials, wall.PostMaterial != null ? wall.PostMaterial : wall.FrontMaterial,
                    wall.GenerateCollision, parts);
            }
        }

        internal static void BuildFence(string id, FenceFamily family, Vector3 a, Vector3 b,
            Material front, Material back, bool collision, List<TownRebuild.Part> parts)
        {
            var model = family.Panel;
            TownModule.CheckCompatibility(model, TownModule.ModuleKind.WallPanel);
            BuildSpan(id, model, a, b, model.Height, front, back, collision, parts, Post);
            Post(id + "/start-post", a); Post(id + "/end-post", b);
            void Post(string key, Vector3 position)
            {
                var post = family.DefaultPost;
                if (post == null) return;
                AddPost(key, position, post.Mesh, post.Mesh.bounds.size.x, post.Mesh.bounds.size.y,
                    post.Materials, null, collision, parts);
            }
        }

        private static void BuildSpan(string id, TownModule model, Vector3 a, Vector3 b, float height,
            Material front, Material back, bool collision, List<TownRebuild.Part> parts,
            System.Action<string, Vector3> post)
        {
                var direction = b - a; direction.y = 0; float length = direction.magnitude; direction /= length;
                var rotation = Quaternion.LookRotation(Vector3.Cross(direction, Vector3.up), Vector3.up);
                int count = Mathf.Max(1, Mathf.CeilToInt(length / model.Length - .00001f));
                if (count > 4096) throw new System.InvalidOperationException("Use a longer panel: at most 4096 repeats fit in one span.");
                for (int i = 0; i < count; i++)
                {
                    float offset = model.AllowStretch ? length * i / count : model.Length * i;
                    float pieceLength = model.AllowStretch ? length / count : Mathf.Min(model.Length, length - offset);
                    var mesh = !model.AllowStretch && pieceLength < model.Length - .00001f
                        ? WallLengthFit.Crop(model.Mesh, model.Length, pieceLength) : model.Mesh;
                    var position = Vector3.Lerp(a, b, offset / length);
                    // Each panel is horizontal at its midpoint elevation. Only
                    // its transform changes; the authored mesh stays shared.
                    position.y = Mathf.Lerp(a.y, b.y, (offset + pieceLength / 2) / length);
                    parts.Add(new TownRebuild.Part { Key = id + "/panel-" + i, Mesh = mesh,
                        Position = position, Rotation = rotation, Scale = new Vector3(model.AllowStretch ? pieceLength / model.Length : 1, model.AllowStretch ? height / model.Height : 1, 1),
                        Materials = model.ResolveMaterials(front, back), Collision = collision });
                    if (i > 0) post(id + "/post-" + i, Vector3.Lerp(a, b, offset / length));
                }
        }

        private static void AddPost(string key, Vector3 position, Mesh mesh, float width, float height,
            Material[] defaults, Material finish, bool collision, List<TownRebuild.Part> parts)
        {
                var bounds = mesh.bounds;
                var scale = new Vector3(width / Mathf.Max(.001f, bounds.size.x), height / Mathf.Max(.001f, bounds.size.y), width / Mathf.Max(.001f, bounds.size.x));
                var origin = Vector3.Scale(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z), scale);
                var materials = new Material[mesh.subMeshCount];
                defaults ??= System.Array.Empty<Material>();
                for (int slot = 0; slot < materials.Length; slot++)
                    materials[slot] = finish != null ? finish : slot < defaults.Length ? defaults[slot]
                        : defaults.Length > 0 ? defaults[0] : null;
                parts.Add(new TownRebuild.Part { Key = key, Mesh = mesh, Position = position - origin,
                    Scale = scale, Materials = materials, Collision = collision });
        }
    }
}
