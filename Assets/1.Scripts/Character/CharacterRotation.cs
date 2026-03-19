using UnityEngine;

namespace _1.Scripts.Character
{
    public class CharacterRotation : MonoBehaviour
    {
        public Transform target;

        private void Update()
        {
            MatchY(target);
        }

        private void MatchY(Transform t)
        {
            transform.rotation = Quaternion.Euler(
                transform.eulerAngles.x,
                t.eulerAngles.y,
                transform.eulerAngles.z
            );
        }
    }
}
