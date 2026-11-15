using System.Collections.Generic;
using UnityEngine;

namespace HouseToolkit
{
    [DisallowMultipleComponent, AddComponentMenu("")]
    public sealed class RoomContents : MonoBehaviour
    {
        readonly List<Renderer> hiddenByRoom = new List<Renderer>();

        // The visibility controller owns the decision about which rooms are visible.
        public void SetCulled(bool culled)
        {
            if (culled)
            {
                foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.forceRenderingOff) continue;
                    renderer.forceRenderingOff = true;
                    hiddenByRoom.Add(renderer);
                }
            }
            else
            {
                foreach (var renderer in hiddenByRoom)
                    if (renderer) renderer.forceRenderingOff = false;
                hiddenByRoom.Clear();
            }
        }

        void OnDisable() => SetCulled(false);
    }
}
