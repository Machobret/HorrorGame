using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HorrorGame.UI
{
    internal sealed class ReporterActionAppearance : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        internal const int BaseFontSize = 14;
        internal static readonly Color IdleInk = new Color32(46, 41, 32, 255);
        internal static readonly Color SelectedInk = new(0.98f, 0.94f, 0.87f);

        Button button;
        Image surface;
        Graphic label;
        ReporterMenuArt art;
        ReporterItemVisual motion;

        internal void Initialize(Button control, Graphic caption, ReporterMenuArt theme)
        {
            button = control;
            surface = control.image;
            label = caption;
            art = theme;
            button.transition = Selectable.Transition.None;
            surface.type = Image.Type.Sliced;
            if (label)
            {
                if (label is TMP_Text text && art && art.InterfaceFont)
                    text.font = art.InterfaceFont;
                motion = gameObject.AddComponent<ReporterItemVisual>();
                motion.Initialize(label.rectTransform, 1.10f);
            }
            SetSelected(EventSystem.current && EventSystem.current.currentSelectedGameObject == gameObject);
        }

        internal void SetSelected(bool selected)
        {
            surface.sprite = art ? selected ? art.Selection : art.ActionPaper : null;
            surface.color = surface.sprite ? Color.white : selected ? ReporterMenuArt.Oxblood : ReporterMenuArt.Paper;
            if (label) label.color = selected ? SelectedInk : IdleInk;
            if (motion) motion.SetSelected(selected);
        }

        void OnEnable()
        {
            if (button && button.navigation.mode != Navigation.Mode.None)
                SetSelected(EventSystem.current && EventSystem.current.currentSelectedGameObject == gameObject);
        }

        public void OnSelect(BaseEventData eventData) => SetSelected(true);
        public void OnDeselect(BaseEventData eventData) => SetSelected(false);
    }
}
