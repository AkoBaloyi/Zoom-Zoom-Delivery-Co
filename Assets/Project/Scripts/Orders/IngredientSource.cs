using System.Collections.Generic;
using UnityEngine;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// A place on the map that supplies ingredients, such as the Butcher (Meat, Chicken) or the
    /// Market (Vegetables, Rice). Put one on an empty GameObject where the car should stop, and
    /// list what it supplies. Driving through it collects any ingredient an active order still
    /// needs, as long as the car has room.
    ///
    /// Orders pick a source for each ingredient when they spawn, which is what the card and the
    /// map markers point at, but any source that supplies that ingredient works.
    /// </summary>
    [DisallowMultipleComponent]
    public class IngredientSource : MonoBehaviour
    {
        private static readonly List<IngredientSource> _all = new List<IngredientSource>();

        /// <summary>Every enabled source in the scene.</summary>
        public static IReadOnlyList<IngredientSource> All => _all;

        [Tooltip("Shown on the order card, for example \"Butcher\". Leave empty to use this " +
                 "GameObject's name.")]
        [SerializeField] private string placeName = "";

        [Tooltip("The ingredients this place supplies.")]
        [SerializeField] private IngredientType[] supplies;

        [Header("Label")]
        [Tooltip("Floats the place name and what it sells above this point, so the greybox is " +
                 "readable without art. Turn off once there is a real building here.")]
        [SerializeField] private bool showLabel = true;
        [SerializeField] private float labelHeight = 6f;
        [SerializeField] private float labelCharacterSize = 0.18f;
        [SerializeField] private Color labelColour = new Color(0.20f, 0.60f, 1.00f, 1f);

        public string PlaceName => string.IsNullOrWhiteSpace(placeName) ? gameObject.name : placeName.Trim();
        public IReadOnlyList<IngredientType> Supplies => supplies ?? System.Array.Empty<IngredientType>();
        public Vector3 Position => transform.position;

        private TextMesh _label;
        private Camera _camera;

        public bool SuppliesIngredient(IngredientType ingredient)
        {
            if (ingredient == null || supplies == null) return false;
            foreach (IngredientType s in supplies)
                if (s == ingredient) return true;
            return false;
        }

        /// <summary>Set up by the Create Default Kitchen editor command.</summary>
        public void Configure(string newPlaceName, IngredientType[] newSupplies)
        {
            placeName = newPlaceName;
            supplies = newSupplies;
        }

        private void OnEnable()
        {
            if (!_all.Contains(this)) _all.Add(this);
        }

        private void OnDisable()
        {
            _all.Remove(this);
        }

        private void Start()
        {
            if (supplies == null || supplies.Length == 0)
                Debug.LogWarning($"[IngredientSource] '{PlaceName}' supplies nothing. Add " +
                                 "ingredients to its Supplies list.", this);

            if (showLabel) BuildLabel();
        }

        private void LateUpdate()
        {
            if (_label == null) return;
            if (_camera == null) _camera = Camera.main;
            if (_camera != null) _label.transform.rotation = _camera.transform.rotation;
        }

        private void BuildLabel()
        {
            var names = new List<string>();
            if (supplies != null)
                foreach (IngredientType s in supplies)
                    if (s != null) names.Add(s.displayName);

            var go = new GameObject("Source label");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * labelHeight;

            _label = go.AddComponent<TextMesh>();
            _label.text = $"{PlaceName.ToUpper()}\n{string.Join(", ", names)}";
            _label.fontSize = 64;
            _label.characterSize = labelCharacterSize;
            _label.anchor = TextAnchor.LowerCenter;
            _label.alignment = TextAlignment.Center;
            _label.fontStyle = FontStyle.Bold;
            _label.color = labelColour;

            MeshRenderer r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.20f, 0.60f, 1.00f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 6f);
        }
    }
}
