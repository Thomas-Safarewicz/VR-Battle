using System.Collections.Generic;
using UnityEngine.XR;

namespace _1.Scripts.Managers
{
    public static class Haptics
    {
        private static InputDevice leftDevice;
        private static InputDevice rightDevice;

        private static bool initialized;

        private static void Initialize()
        {
            if (initialized)
            {
                return;
            }

            List<InputDevice> devices = new();

            InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, devices);
            if (devices.Count > 0)
            {
                leftDevice = devices[0];
            }

            devices.Clear();

            InputDevices.GetDevicesAtXRNode(XRNode.RightHand, devices);
            if (devices.Count > 0)
            {
                rightDevice = devices[0];
            }

            initialized = true;
        }

        public static void PulseLeft(float amplitude, float duration)
        {
            Initialize();

            if (leftDevice.isValid)
            {
                leftDevice.SendHapticImpulse(0, amplitude, duration);
            }
        }

        public static void PulseRight(float amplitude, float duration)
        {
            Initialize();

            if (rightDevice.isValid)
            {
                rightDevice.SendHapticImpulse(0, amplitude, duration);
            }
        }

        public static void Pulse(XRNode node, float amplitude, float duration)
        {
            Initialize();

            InputDevice device = node == XRNode.LeftHand ? leftDevice : rightDevice;

            if (device.isValid)
            {
                device.SendHapticImpulse(0, amplitude, duration);
            }
        }
    }
}