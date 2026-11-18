using HorrorEngine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HorrorGame.UI
{
    /// <summary>Composites the tablet's Canvas through its screen material, without filtering other UI.</summary>
    public sealed class TabletDisplayFilter : MonoBehaviour
    {
        public Camera DisplayCamera;
        public RawImage Output;
        public Material ScreenMaterial;
        RenderTexture target;
        Material material;
        private UIMap map;
        private RawImage pointer;
        private Texture2D pointerTexture;
        private bool showPointer;

        public Vector2 ProjectToGlass(Vector2 screenPoint)
        {
            if (!material) return screenPoint;
            var halfScreen = new Vector2(Screen.width, Screen.height) * .5f;
            var source = new Vector2(screenPoint.x / halfScreen.x, screenPoint.y / halfScreen.y) - Vector2.one;
            float curvature = material.GetFloat("_Curvature");
            Vector2 glass = source;
            for (int i = 0; i < 4; i++)
                glass = source / (1f + curvature * glass.sqrMagnitude);
            return Vector2.Scale(glass + Vector2.one, halfScreen);
        }

        public Vector2 GlassToScreen(Vector2 glassPoint)
        {
            if (!material) return glassPoint;
            var halfScreen = new Vector2(Screen.width, Screen.height) * .5f;
            var glass = new Vector2(glassPoint.x / halfScreen.x, glassPoint.y / halfScreen.y) - Vector2.one;
            var source = glass * (1f + material.GetFloat("_Curvature") * glass.sqrMagnitude);
            return Vector2.Scale(source + Vector2.one, halfScreen);
        }
        void OnEnable()
        {
            map = GetComponent<UIMap>();
            showPointer = false;
            if (CursorController.Exists) CursorController.Instance.SetSoftwareCursorActive(true);
            Resize();
        }

        void LateUpdate()
        {
            if(target==null||target.width!=Screen.width||target.height!=Screen.height)Resize();
            if (material)
            {
                material.SetFloat("_ScreenTime", Time.unscaledTime);
                Vector4 bounds = Vector4.zero;
                if (map && !map.IsTabletLoading &&
                    map.TryProjectMapViewport(Vector2.zero, false, out var bottomLeft) &&
                    map.TryProjectMapViewport(Vector2.one, false, out var topRight))
                {
                    bounds = new Vector4((bottomLeft.x + 2f) / Screen.width, (bottomLeft.y + 2f) / Screen.height,
                        (topRight.x - 2f) / Screen.width, (topRight.y - 2f) / Screen.height);
                }
                material.SetVector("_MapEffectBounds", bounds);
            }
            UpdatePointer();
        }

        private void UpdatePointer()
        {
            var mouse = Mouse.current;
            if (mouse == null || !Output || (map && map.IsTabletLoading))
                showPointer = false;
            else if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                showPointer = false;
            else if (mouse.delta.ReadValue().sqrMagnitude > .25f)
                showPointer = true;

            if (!showPointer)
            {
                if (pointer) pointer.enabled = false;
                return;
            }

            if (!pointer) CreatePointer();
            pointer.enabled = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                Output.rectTransform, mouse.position.ReadValue(), null, out var localPoint);
            pointer.rectTransform.localPosition = localPoint;
        }

        private void CreatePointer()
        {
            string[] pixels =
            {
                "#.........",
                "##........",
                "#+#.......",
                "#++#......",
                "#+++#.....",
                "#++++#....",
                "#+++++#...",
                "#+++####..",
                "#+#+#.....",
                "##.#+#....",
                "#..#+#....",
                "....##...."
            };
            int width = pixels[0].Length;
            int height = pixels.Length;
            var colors = new Color32[width * height];
            Color32 edge = new Color32(27, 62, 73, 255);
            Color32 fill = new Color32(182, 235, 244, 255);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    char pixel = pixels[y][x];
                    if (pixel != '.')
                        colors[(height - 1 - y) * width + x] = pixel == '+' ? fill : edge;
                }

            pointerTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Tablet pointer",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            pointerTexture.SetPixels32(colors);
            pointerTexture.Apply(false, true);

            pointer = new GameObject("Tablet pointer", typeof(RectTransform), typeof(RawImage))
                .GetComponent<RawImage>();
            pointer.rectTransform.SetParent(Output.rectTransform, false);
            pointer.rectTransform.pivot = new Vector2(0f, 1f);
            pointer.rectTransform.sizeDelta = new Vector2(width, height) * 2f;
            pointer.texture = pointerTexture;
            pointer.raycastTarget = false;
        }
        void Resize()
        {
            if(!DisplayCamera||!Output||!ScreenMaterial)return;
            Release();
            // Keep screen-sized input coordinates for native UI raycasts. The shader reduces the visible resolution.
            target=new RenderTexture(Mathf.Max(1,Screen.width),Mathf.Max(1,Screen.height),24){name="Tablet display",filterMode=FilterMode.Bilinear};target.Create();
            material=new Material(ScreenMaterial);Output.material=material;Output.texture=target;DisplayCamera.targetTexture=target;
        }
        void OnDisable()
        {
            if (pointer) pointer.enabled = false;
            if (CursorController.Exists) CursorController.Instance.SetSoftwareCursorActive(false);
            Release();
        }

        void OnDestroy()
        {
            if (pointer) Destroy(pointer.gameObject);
            if (pointerTexture) Destroy(pointerTexture);
        }
        void Release()
        {
            if(DisplayCamera)DisplayCamera.targetTexture=null;
            if(Output){Output.texture=null;Output.material=null;}
            if(target){target.Release();Destroy(target);target=null;}
            if(material){Destroy(material);material=null;}
        }
    }
}
