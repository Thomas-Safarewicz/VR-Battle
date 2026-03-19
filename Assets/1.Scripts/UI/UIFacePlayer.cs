using UnityEngine;

namespace _1.Scripts.UI
{
    public class UIFacePlayer : MonoBehaviour
    {
        private Transform cam;

        private void Start()
        {
            if (Camera.main != null)
            {
                cam = Camera.main.transform;
            }
        }

        public void LateUpdate()
        {
            Vector3 lookDir = transform.position - cam.position;
            lookDir.y = 0f;

            transform.rotation = Quaternion.LookRotation(lookDir);
        }
    }
}