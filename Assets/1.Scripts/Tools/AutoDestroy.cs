using UnityEngine;

namespace _1.Scripts.Tools
{
    public class AutoDestroy : MonoBehaviour
    {
        public float lifetime = 3f;

        private void Start()
        {
            Destroy(gameObject, lifetime);
        }
    }
}