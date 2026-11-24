using System;
using TMPro;
using UnityEngine;

namespace HorrorGame.UI
{
    public sealed class ReporterMenuArt : ScriptableObject
    {
        internal static readonly Color Oxblood = new Color32(101, 49, 44, 255);
        internal static readonly Color Paper = new Color32(225, 213, 191, 255);

        [Serializable]
        struct SpriteVariant
        {
            public Sprite Original;
            public Sprite Preview;
        }

        [Serializable]
        struct ConditionPortraits
        {
            public Sprite Original;
            public Sprite Healthy;
            public Sprite Critical;
        }

        public Sprite PressCard;
        public Sprite Satchel;
        public Sprite LedgerPage;
        public Sprite Category;
        public Sprite Selection;
        public Sprite ActionPaper;
        public Sprite ConditionBadge;
        public Sprite EquippedShadow;
        public Material ItemOutline;
        public Sprite Pin;
        public TMP_FontAsset InterfaceFont;
        [SerializeField] SpriteVariant[] previewSprites = Array.Empty<SpriteVariant>();
        [SerializeField] ConditionPortraits[] conditionPortraits = Array.Empty<ConditionPortraits>();

        internal Sprite Resolve(Sprite original)
        {
            if (!original) return null;
            foreach (var variant in previewSprites)
                if (variant.Original == original && variant.Preview) return variant.Preview;
            return original;
        }

        internal Sprite ResolvePortrait(Sprite original, ReporterCondition condition)
        {
            if (!original) return null;
            foreach (var portraits in conditionPortraits)
            {
                if (portraits.Original != original) continue;
                if (condition == ReporterCondition.Healthy && portraits.Healthy) return Resolve(portraits.Healthy);
                if (condition == ReporterCondition.Critical && portraits.Critical) return Resolve(portraits.Critical);
                break;
            }
            return Resolve(original);
        }
    }
}
