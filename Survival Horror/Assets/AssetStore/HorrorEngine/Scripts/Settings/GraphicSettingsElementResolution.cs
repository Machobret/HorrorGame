using System;
using UnityEngine;

namespace HorrorEngine
{
    [CreateAssetMenu(fileName = "GraphicSettingsResolution", menuName = "Horror Engine/Settings/Resolution")]
    public class GraphicSettingsElementResolution : SettingsElementComboContent
    {
        public Vector2Int[] Options;

        public override void Apply()
        {
            if (SettingsManager.Instance.Get(this, out string outVal))
            {
                int index = GetItemIndex(outVal);
                if (index >= 0)
                {
                    Vector2Int res = Options[index];
                    Screen.SetResolution(res.x, res.y, Screen.fullScreen);
                }
            }
        }

        public override string GetDefaultValue()
        {
            int index = FindClosestOption(Screen.width, Screen.height);
            return index >= 0 ? GetItemName(index) : string.Empty;
        }

        public override int GetItemIndex(string name)
        {
            var parts = name?.Split('x');
            if (parts?.Length == 2 && int.TryParse(parts[0], out int width) &&
                int.TryParse(parts[1], out int height) && width > 0 && height > 0)
                return FindClosestOption(width, height);
            return FindClosestOption(Screen.width, Screen.height);
        }

        private int FindClosestOption(int width, int height)
        {
            int closest = -1;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < Options.Length; i++)
            {
                var size = Options[i];
                float candidate = new Vector2(size.x - width, size.y - height).sqrMagnitude;
                if (candidate >= distance) continue;
                distance = candidate;
                closest = i;
            }
            return closest;
        }

        public override int GetItemCount()
        {
            return Options.Length;
        }

        public override string GetItemName(int index)
        {
            Vector2Int item = Options[index];
            return item.x + "x" + item.y;
        }
    }
}
