using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EnvironmentAuthoring
{
    [CreateAssetMenu(menuName = "Environment/Window or Door Family")]
    public sealed class OpeningFamily : ScriptableObject
    {
        public enum Condition { Clean, Broken }
        [Serializable] public sealed class ModelSet
        {
            public string Name;
            public Condition State;
            public WallOpeningInsert[] Models = Array.Empty<WallOpeningInsert>();
        }
        public FloorPlan.OpeningKind Kind;
        public ModelSet[] Sets = Array.Empty<ModelSet>();

        public IEnumerable<WallOpeningInsert> Models(Condition condition)
            => (Sets ?? Array.Empty<ModelSet>()).Where(set => set != null && set.State == condition)
                .SelectMany(set => set.Models ?? Array.Empty<WallOpeningInsert>()).Where(model => model != null).Distinct();

        public IEnumerable<WallOpeningInsert> Compatible(WallFamily wall, float width, Condition condition)
        {
            if (wall == null || !(wall.OpeningFamilies ?? Array.Empty<OpeningFamily>()).Contains(this)) yield break;
            foreach (var model in Models(condition))
                if (model.Fits(wall, Kind, wall.OpeningFor(Kind, width, model.Shape))) yield return model;
        }
    }
}
