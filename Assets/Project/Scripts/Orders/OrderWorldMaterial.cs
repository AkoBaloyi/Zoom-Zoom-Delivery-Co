using UnityEngine;
using UnityEngine.Rendering;

namespace ZoomZoom.Orders.UI
{
    /// <summary>
    /// One shared transparent unlit material for every in-world order visual (beacon beams,
    /// ground discs). It used to be built privately inside OrderBeacon; moved
    /// here so any later world visual draws with exactly the same material instead of a copy.
    /// Colour is applied per object with a MaterialPropertyBlock, so one material serves them all.
    /// </summary>
    public static class OrderWorldMaterial
    {
        private static Material _material;

        public static Material Get()
        {
            if (_material != null) return _material;

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                            ?? Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Sprites/Default");

            if (shader == null)
            {
                Debug.LogError("[OrderWorldMaterial] Found no usable shader, order visuals cannot be drawn.");
                return null;
            }

            _material = new Material(shader) { name = "Order world visuals" };

            if (_material.HasProperty("_Surface")) _material.SetFloat("_Surface", 1f);
            if (_material.HasProperty("_SrcBlend"))
                _material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (_material.HasProperty("_DstBlend"))
                _material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (_material.HasProperty("_ZWrite")) _material.SetFloat("_ZWrite", 0f);
            if (_material.HasProperty("_Cull")) _material.SetFloat("_Cull", (float)CullMode.Off);

            _material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _material.SetOverrideTag("RenderType", "Transparent");
            _material.renderQueue = (int)RenderQueue.Transparent;
            _material.SetShaderPassEnabled("ShadowCaster", false);

            return _material;
        }

        /// <summary>Sets both colour property names so it works with URP Unlit (_BaseColor) and
        /// the Sprites/Default fallback (_Color).</summary>
        public static void SetColour(MaterialPropertyBlock block, Color colour)
        {
            block.SetColor("_BaseColor", colour);
            block.SetColor("_Color", colour);
        }
    }
}
