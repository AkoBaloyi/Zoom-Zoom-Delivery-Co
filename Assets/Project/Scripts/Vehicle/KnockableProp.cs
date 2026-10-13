using UnityEngine;

namespace ZoomZoom.Vehicle
{
    /// <summary>
    /// Marks a street prop the car can knock over. Added by Tools > Zoom Zoom > Make Props
    /// Knockable, alongside the Rigidbody and BoxCollider that do the actual work.
    ///
    /// It holds almost nothing, and that is deliberate. Its presence is the record of what the
    /// baker touched, so the undo tool can strip exactly those objects and nothing else, and so
    /// anything that wants to treat knockable props differently from walls (the cargo condition's
    /// collision rule, a scoring hook, a sound) has one component to look for instead of a list
    /// of names. It lives in the runtime assembly because a component serialised into a scene has
    /// to exist in the build; a marker defined in an Editor folder would come up as "missing
    /// script" on every prop the moment anyone built the player.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KnockableProp : MonoBehaviour
    {
        [Tooltip("Which family the baker matched this object to. For reading, not editing.")]
        public string family;

        [Tooltip("Mass the baker gave it, kept here so the number is visible without opening the " +
                 "Rigidbody. The Rigidbody is the one that counts.")]
        public float bakedMassKg;

        [Tooltip("Where this prop lived before the baker pulled it to the scene root. The city's " +
                 "group nodes carry scales like -426, which PhysX cannot run a Rigidbody under, so " +
                 "the prop had to come out. Undo Knockable Props puts it back here.")]
        public Transform originalParent;

        [Tooltip("The local scale it had under that parent, restored on undo.")]
        public Vector3 originalLocalScale = Vector3.one;
    }
}
