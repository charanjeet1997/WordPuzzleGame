using UnityEngine;

namespace WordPuzzle.Services
{
    /// <summary>
    /// Assigns a render-pipeline-appropriate material to particle systems.
    /// <para>
    /// AddComponent&lt;ParticleSystem&gt;() attaches Unity's built-in Default-ParticleSystem
    /// material, which references a built-in-pipeline shader that does not exist under URP.
    /// A null or unresolvable shader renders as magenta, so both the generated prefab and the
    /// procedural fallback have to be repaired explicitly.
    /// </para>
    /// </summary>
    public static class ParticleMaterialUtility
    {
        private const string ErrorShaderName = "Hidden/InternalErrorShader";

        private static readonly string[] ShaderCandidates =
        {
            "Universal Render Pipeline/Particles/Unlit",
            "Sprites/Default",
            "Universal Render Pipeline/Unlit"
        };

        private static Material _cachedMaterial;

        /// <summary>
        /// Repairs the material on <paramref name="target"/> and any nested particle systems.
        /// Leaves a already-valid material untouched.
        /// </summary>
        public static void EnsureValidMaterial(GameObject target)
        {
            if (target == null) return;

            foreach (ParticleSystemRenderer renderer in target.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                if (IsUsable(renderer.sharedMaterial)) continue;

                Material material = GetMaterial();
                if (material != null) renderer.sharedMaterial = material;
            }
        }

        private static bool IsUsable(Material material)
        {
            return material != null
                   && material.shader != null
                   && material.shader.name != ErrorShaderName;
        }

        private static Material GetMaterial()
        {
            if (_cachedMaterial != null) return _cachedMaterial;

            foreach (string shaderName in ShaderCandidates)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader == null) continue;

                _cachedMaterial = new Material(shader) { name = "WordPuzzle_ParticleRuntime" };
                return _cachedMaterial;
            }

            Debug.LogWarning("[WordPuzzle] No usable particle shader found; particles will render magenta.");
            return null;
        }
    }
}
