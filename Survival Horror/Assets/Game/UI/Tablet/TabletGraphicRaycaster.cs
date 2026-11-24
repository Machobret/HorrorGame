using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HorrorGame.UI
{
    public sealed class TabletGraphicRaycaster : GraphicRaycaster
    {
        TabletDisplayFilter filter;

        protected override void Awake()
        {
            base.Awake();
            filter = GetComponentInParent<TabletDisplayFilter>();
        }

        public override void Raycast(PointerEventData eventData, List<RaycastResult> results)
        {
            Vector2 position = eventData.position;
            try
            {
                if (filter && filter.isActiveAndEnabled)
                    eventData.position = filter.GlassToScreen(position);
                base.Raycast(eventData, results);
            }
            finally
            {
                eventData.position = position;
            }
        }
    }
}
