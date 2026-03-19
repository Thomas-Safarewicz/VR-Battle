using _1.Scripts.Grabbables;
using Fusion.XR.Shared.Rig;
using UnityEngine;
using UnityEngine.Events;

namespace Fusion.XR.Shared.Grabbing.NetworkHandColliderBased
{
    /**
     *
     * Allows a NetworkHand to grab NetworkHandColliderGrabbable objects
     *
     **/
    [RequireComponent(typeof(NetworkHand))]
    [DefaultExecutionOrder(NetworkHandColliderGrabber.EXECUTION_ORDER)]
    public class NetworkHandColliderGrabber : NetworkBehaviour
    {
        public const int EXECUTION_ORDER = NetworkHand.EXECUTION_ORDER + 10;

        [Networked]
        public NetworkWeaponGrabbable GrabbedObject { get; set; }

        [HideInInspector]
        public NetworkHand hand;

        public UnityEvent OnGrabPressed;
        
        Collider lastCheckedCollider;
        NetworkWeaponGrabbable lastCheckColliderGrabbable;

        private bool wasGrabPressed = false;

        private void Awake()
        {
            hand = GetComponentInParent<NetworkHand>();
        }

        private void OnTriggerStay(Collider other)
        {
            // We only trigger grabbing for our local hands
            if (!hand.IsLocalNetworkRig || !hand.LocalHardwareHand) return;

            // Exit if an object is already grabbed
            if (GrabbedObject != null)
            {
                // It is already the grabbed object or another, but we don't allow shared grabbing here
                return;
            }

            NetworkWeaponGrabbable grabbable;

            if (lastCheckedCollider == other)
            {
                grabbable = lastCheckColliderGrabbable;
            }
            else
            {
                grabbable = other.GetComponentInParent<NetworkWeaponGrabbable>();
            }

            // To limit the number of GetComponent calls, we cache the latest checked collider grabbable result
            lastCheckedCollider = other;
            lastCheckColliderGrabbable = grabbable;
            if (grabbable != null)
            {
                if (hand.LocalHardwareHand.isGrabbing) Grab(grabbable);
            }
        }

        // Ask the grabbable object to start following the hand
        public void Grab(NetworkWeaponGrabbable grabbable)
        {
            Debug.Log($"Try to grab object {grabbable.gameObject.name} with {gameObject.name}");
            grabbable.Grab(this);
            GrabbedObject = grabbable;
        }

        // Ask the grabbable object to stop following the hand
        public void Ungrab(NetworkHandColliderGrabbable grabbable)
        {
            Debug.Log($"Try to ungrab object {grabbable.gameObject.name} with {gameObject.name}");
            GrabbedObject.Ungrab();
            GrabbedObject = null;
        }

        private void Update()
        {
            if (!hand.IsLocalNetworkRig || !hand.LocalHardwareHand) return;

            if (wasGrabPressed == false && hand.LocalHardwareHand.isGrabbing)
            {
                wasGrabPressed = true;
                OnGrabPressed.Invoke();
            }

            wasGrabPressed = false;

            // Check if the local hand is still grabbing the object
            if (GrabbedObject != null && !hand.LocalHardwareHand.isGrabbing)
            {
                // Object released by this hand (we don't wait for a fun to trigger this, to avoid the object to stay sticked to the hand until the next FUN tick)
                //Ungrab(GrabbedObject);
            }
        }
    }
}