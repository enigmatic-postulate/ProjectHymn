using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjectHymn.UI
{
    /// <summary>Runs a callback when its button is selected or hovered (menus show a description).</summary>
    public class SelectHint : MonoBehaviour, ISelectHandler, IPointerEnterHandler
    {
        public Action OnSelected;
        public void OnSelect(BaseEventData e) => OnSelected?.Invoke();
        public void OnPointerEnter(PointerEventData e) => OnSelected?.Invoke();
    }

}
