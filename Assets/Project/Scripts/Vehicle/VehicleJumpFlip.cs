using System;
using UnityEngine;

namespace ZoomZoom.Vehicle
{
    // Serialized scene/log compatibility only. The taxi has no jump, flip or aerial propulsion.
    public struct FlipReport
    {
        public Vector3 startPosition, direction;
        public float speedBefore, speedAfter, speedGain, requestedForce;
        public bool speedWasCapped;
    }
    public class VehicleJumpFlip : MonoBehaviour
    {
        public event Action<FlipReport> FlipStarted { add { } remove { } }
        public bool IsFlipping => false;
        public float FlipCooldownRemaining => 0f;
        public float JumpCooldownRemaining => 0f;
        public FlipReport LastFlip => default;
        public int FlipCount => 0;
        public int SelfRightCount => GetComponent<VehicleController>()?.SelfRightCount ?? 0;
        public bool CanJumpNow => false;
        public bool CanFlipNow => false;
        public void RequestJump() { }
        public void RequestFlip(Vector2 direction) { }
        public void SetJumpHeld(bool held) { }
        private void Awake() { enabled = false; }
    }
}
