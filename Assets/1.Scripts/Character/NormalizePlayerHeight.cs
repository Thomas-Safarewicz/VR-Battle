using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace _1.Scripts.Character
{
    public class NormalizePlayerHeight : MonoBehaviour
    {
        [SerializeField] private XROrigin xrOrigin;

        [SerializeField] private float targetHeight = 0.35f;

        private IEnumerator Start()
        {
            yield return new WaitUntil(() => xrOrigin.CameraInOriginSpaceHeight > 0.1f);

            ApplyHeight();
        }

        private void ApplyHeight()
        {
            float headsetHeight = xrOrigin.CameraInOriginSpaceHeight;

            Vector3 offset = transform.localPosition;
            offset.y = targetHeight - headsetHeight;

            transform.localPosition = offset;
        }
    }
}