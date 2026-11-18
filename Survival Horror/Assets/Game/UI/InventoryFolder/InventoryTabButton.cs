using UnityEngine;
using UnityEngine.UI;

namespace HorrorGame.UI
{
    public sealed class InventoryTabButton : Button
    {
        public Transform Content;

        public override Selectable FindSelectableOnDown()
        {
            if (!Content)
                return null;

            // Look up the current grid so rebuilt slots and newly opened pages are included.
            foreach (var selectable in Content.GetComponentsInChildren<Selectable>())
            {
                if (selectable.IsActive() && selectable.IsInteractable() && selectable.navigation.mode != Navigation.Mode.None)
                    return selectable;
            }

            return null;
        }
    }
}
