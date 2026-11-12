using System;
using UnityEngine;
using UnityEngine.XR;

namespace Blackout.World
{
    /// <summary>Small helper for controller vibration. Failures are ignored: haptics are always optional.</summary>
    public static class Haptics
    {
        public static void Impulse(float amplitude, float seconds)
        {
            Send(XRNode.LeftHand, amplitude, seconds);
            Send(XRNode.RightHand, amplitude, seconds);
        }

        static void Send(XRNode node, float amplitude, float seconds)
        {
            try
            {
                var d = InputDevices.GetDeviceAtXRNode(node);
                if (d.isValid) d.SendHapticImpulse(0, Mathf.Clamp01(amplitude), seconds);
            }
            catch (Exception) { /* optional */ }
        }
    }
}
