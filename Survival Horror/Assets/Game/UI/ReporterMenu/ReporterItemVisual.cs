using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HorrorGame.UI
{
    internal sealed class ReporterItemVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        const float ItemScale = 1.26f;
        const float ScalePerSecond = 2.4f;

        RectTransform icon;
        Image outline;
        bool selected;
        bool hovered;
        float highlightScale;

        public event Action<bool> HoverChanged;

        public void Initialize(RectTransform visual, float selectedScale)
        {
            icon = visual;
            highlightScale = selectedScale;
        }

        public void Initialize(Image itemIcon, Material outlineMaterial)
        {
            Initialize(itemIcon.rectTransform, ItemScale);
            if (!outlineMaterial || !itemIcon.sprite) return;

            var layer = new GameObject("Selection outline", typeof(RectTransform), typeof(Image), typeof(Outline));
            layer.layer = itemIcon.gameObject.layer;
            outline = layer.GetComponent<Image>();
            var rect = outline.rectTransform;
            rect.SetParent(icon.parent, false);
            rect.SetSiblingIndex(icon.GetSiblingIndex());
            rect.anchorMin = icon.anchorMin;
            rect.anchorMax = icon.anchorMax;
            rect.offsetMin = icon.offsetMin;
            rect.offsetMax = icon.offsetMax;
            outline.sprite = itemIcon.sprite;
            outline.preserveAspect = true;
            outline.raycastTarget = false;
            outline.material = outlineMaterial;
            var stroke = layer.GetComponent<Outline>();
            stroke.effectColor = Color.white;
            stroke.effectDistance = new Vector2(1.4f, 1.4f);
            outline.enabled = false;
        }

        public void SetSelected(bool value)
        {
            selected = value;
            if (outline) outline.enabled = value;
        }

        public void OnPointerEnter(PointerEventData eventData) => SetHovered(true);
        public void OnPointerExit(PointerEventData eventData) => SetHovered(false);

        void SetHovered(bool value)
        {
            if (hovered == value) return;
            hovered = value;
            HoverChanged?.Invoke(value);
        }

        float TargetScale => selected || hovered ? highlightScale : 1;

        void Update()
        {
            if (!icon || icon.localScale.x == TargetScale) return;
            float scale = Mathf.MoveTowards(icon.localScale.x, TargetScale, Time.unscaledDeltaTime * ScalePerSecond);
            ScaleIcon(scale);
        }

        void OnDisable()
        {
            SetHovered(false);
            if (icon) ScaleIcon(TargetScale);
        }

        void ScaleIcon(float scale)
        {
            icon.localScale = Vector3.one * scale;
            if (outline) outline.rectTransform.localScale = icon.localScale;
        }
    }
}
