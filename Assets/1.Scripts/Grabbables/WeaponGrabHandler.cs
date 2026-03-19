using Fusion.XR.Shared.Grabbing.NetworkHandColliderBased;
using UnityEngine;

namespace _1.Scripts.Grabbables
{
    public class WeaponGrabHandler : MonoBehaviour
    {
        private NetworkWeapon weapon;
        private NetworkWeaponGrabbable grabbable;

        private void Awake()
        {
            weapon = GetComponent<NetworkWeapon>();
            grabbable = GetComponent<NetworkWeaponGrabbable>();

            grabbable.onDidGrab.AddListener(OnGrab);
            grabbable.onDidUngrab.AddListener(OnUngrab);
        }

        private void OnGrab(NetworkHandColliderGrabber grabber)
        {
            weapon.OnGrab(grabber);
        }

        private void OnUngrab()
        {
            weapon.OnRelease();
        }
    }
}