using Fusion;
using Fusion.XR.Shared.Grabbing.NetworkHandColliderBased;

namespace _1.Scripts.Grabbables
{
    public abstract class NetworkWeapon : NetworkBehaviour
    {
        protected NetworkHandColliderGrabber grabber;

        public void OnGrab(NetworkHandColliderGrabber g)
        {
            if (grabber != null)
            {
                return;
            }
            grabber = g;
            grabber.OnGrabPressed.AddListener(OnGrabPressed);
        }

        public void OnRelease()
        {
            grabber.OnGrabPressed.RemoveListener(OnGrabPressed);
            grabber = null;
        }

        protected virtual void OnGrabPressed()
        {
        }

        protected PlayerRef GetOwner()
        {
            return !grabber ? PlayerRef.None : grabber.Object.InputAuthority;
        }
    }
}