using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EnvironmentAuthoring
{
    [CreateAssetMenu(menuName = "Environment/Wall Family")]
    public sealed class WallFamily : ScriptableObject
    {
        public string Id = "01";
        public WallModuleDefinition Blank;
        public WallModuleDefinition[] BlankVariants = Array.Empty<WallModuleDefinition>();
        [Serializable] public sealed class Opening
        {
            public FloorPlan.OpeningKind Kind;
            public WallModuleDefinition Definition;
            public string Shape => Definition != null ? Definition.OpeningShape : null;
        }
        public Opening[] Openings = Array.Empty<Opening>();
        public OpeningFamily[] OpeningFamilies = Array.Empty<OpeningFamily>();
        public WallModuleDefinition ReferenceBlank => Blank;
        public IEnumerable<WallModuleDefinition> Blanks()
        {
            yield return Blank;
            foreach (var piece in BlankVariants ?? Array.Empty<WallModuleDefinition>())
                if (piece != null) yield return piece;
        }
        public IEnumerable<WallModuleDefinition> Definitions()
        {
            foreach (var piece in Blanks()) yield return piece;
            foreach (var opening in Openings ?? Array.Empty<Opening>())
                if (opening?.Definition != null) yield return opening.Definition;
        }
        public WallModuleDefinition OpeningFor(FloorPlan.OpeningKind kind, float width, string shape = null)
        {
            string selected = string.IsNullOrEmpty(shape) ? "01" : shape;
            return (Openings ?? Array.Empty<Opening>()).FirstOrDefault(o => o != null && o.Kind == kind
                && o.Definition != null && Mathf.Abs(o.Definition.Width - width) < .0001f && o.Shape == selected)?.Definition;
        }

        public bool IsValid(out string reason)
        {
            reason = "A complete family needs a solid primary and a family ID.";
            if (Blank == null || string.IsNullOrWhiteSpace(Id) || !Blank.AllowLengthStretch) return false;
            var widths = new HashSet<float>();
            foreach (var piece in Blanks())
            {
                reason = "Solid widths must be unique 0.5/1/2 m pieces with no aperture.";
                if (!piece.AllowLengthStretch || piece.OpeningOutline != default
                    || piece.Width != .5f && piece.Width != 1 && piece.Width != 2 || !widths.Add(piece.Width)) return false;
            }
            var keys = new HashSet<string>();
            foreach (var opening in Openings ?? Array.Empty<Opening>())
            {
                reason = "Openings need unique type/width/shape, rigid 1/2 m segments and a positive aperture.";
                var piece = opening?.Definition;
                if (piece == null || opening.Kind != FloorPlan.OpeningKind.Doorway && opening.Kind != FloorPlan.OpeningKind.Window
                    || piece.AllowLengthStretch || piece.Width != 1 && piece.Width != 2
                    || string.IsNullOrEmpty(piece.OpeningShape) || piece.OpeningOutline.width <= 0 || piece.OpeningOutline.height <= 0
                    || !keys.Add(opening.Kind + ":" + piece.Width + ":" + piece.OpeningShape)) return false;
            }
            foreach (var piece in Definitions())
            {
                reason = "Complete family modules must share their family, height and connection depths.";
                if (piece.FamilyId != Id
                    || Mathf.Abs(piece.Height - Blank.Height) > .0001f
                    || (piece.ConnectionDepths - Blank.ConnectionDepths).sqrMagnitude > .00000001f) return false;
                if (!piece.IsValid(out reason)) return false;
            }
            reason = ""; return true;
        }

    }
}
