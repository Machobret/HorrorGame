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
                    parts.Add(new TownRebuild.Part { Key = span.Id + "/panel-" + i, Mesh = mesh,
                        Position = position, Rotation = rotation, Scale = new Vector3(model.AllowStretch ? pieceLength / model.Length : 1, model.AllowStretch ? wall.Height / model.Height : 1, 1),
                        Materials = model.ResolveMaterials(span.Material != null ? span.Material : wall.FrontMaterial,
                            span.BackMaterial != null ? span.BackMaterial : wall.BackMaterial), Collision = wall.GenerateCollision });
                    if (i > 0) Post(span.Id + "/post-" + i, Vector3.Lerp(a, b, offset / length));
                }
            }
            foreach (var point in wall.Path.Points) Post(point.Id + "/post", point.Position);
            void Post(string key, Vector3 position)
            {
                if (!wall.Posts || wall.PostMesh == null) return;
                var bounds = wall.PostMesh.bounds;
                var scale = new Vector3(wall.PostWidth / Mathf.Max(.001f, bounds.size.x), (wall.PostHeight > 0 ? wall.PostHeight : wall.Height) / Mathf.Max(.001f, bounds.size.y), wall.PostWidth / Mathf.Max(.001f, bounds.size.x));
                var origin = Vector3.Scale(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z), scale);
                var materials = new Material[wall.PostMesh.subMeshCount];
                var defaults = wall.PostMaterials ?? System.Array.Empty<Material>();
                var finish = wall.PostMaterial != null ? wall.PostMaterial : wall.FrontMaterial;
                for (int slot = 0; slot < materials.Length; slot++)
                    materials[slot] = finish != null ? finish : slot < defaults.Length ? defaults[slot]
                        : defaults.Length > 0 ? defaults[0] : null;
                parts.Add(new TownRebuild.Part { Key = key, Mesh = wall.PostMesh, Position = position - origin,
                    Scale = scale, Materials = materials, Collision = wall.GenerateCollision });
            }
        }
    }
}
