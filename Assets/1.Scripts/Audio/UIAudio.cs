using _1.Scripts.Managers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR;

namespace _1.Scripts.Audio
{
    /// <summary>
    /// Adds audio effect triggers to buttons
    /// </summary>
    public class UIAudio : MonoBehaviour, IPointerEnterHandler
    {
        private Button btn;

        private void Awake()
        {
            if (TryGetComponent(out btn))
            {
                btn.onClick.AddListener(() => AudioManager.Play("SFX_Click"));
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (btn && !btn.interactable)
            {
                return;
            }

            AudioManager.Play("SFX_Hover");
            XRNode node = XRNode.RightHand;

            GameObject pointerObj = eventData.pointerPressRaycast.module?.eventCamera?.gameObject;

            if (pointerObj && pointerObj.name.ToLower().Contains("left"))
                node = XRNode.LeftHand;
            Haptics.Pulse(node, 0.7f, 0.05f);
        }
    }
}