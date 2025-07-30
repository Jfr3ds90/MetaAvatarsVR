using UnityEngine;
using Fusion;

namespace HackMonkeys.Gameplay
{
    /// <summary>
    /// Estructura de input para sincronizar datos VR del jugador
    /// </summary>
    public struct NetworkPlayerInput : INetworkInput
    {
        public Vector3 headPosition;
        public Quaternion headRotation;
        public Vector3 leftHandPosition;
        public Quaternion leftHandRotation;
        public Vector3 rightHandPosition;
        public Quaternion rightHandRotation;
        public float leftHandGrip;
        public float rightHandGrip;
        public float leftHandTrigger;
        public float rightHandTrigger;
        public NetworkBool isVRConnected;
    }
}