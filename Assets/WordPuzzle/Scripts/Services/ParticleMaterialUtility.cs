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
        private static Material _cachedConfettiMaterial;

        /// <summary>
        /// Repairs the material on <paramref name="target"/> and any nested particle systems.
        /// Leaves a already-valid material untouched.
        /// </summary>
        public static void EnsureValidMaterial(GameObject target)
        {
            if (target == null) return;

            bool isConfetti = target.name.IndexOf("confetti", System.StringComparison.OrdinalIgnoreCase) >= 0;

            foreach (ParticleSystemRenderer renderer in target.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                if (IsUsable(renderer.sharedMaterial)) continue;

                Material material = isConfetti ? GetConfettiMaterial() : GetMaterial();
                if (material != null) renderer.sharedMaterial = material;
            }
        }

        public static bool IsUsable(Material material)
        {
            return material != null
                   && material.shader != null
                   && material.shader.name != ErrorShaderName;
        }

        public static Material GetConfettiMaterial()
        {
            if (_cachedConfettiMaterial != null) return _cachedConfettiMaterial;

#if UNITY_EDITOR
            _cachedConfettiMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/WordPuzzle/Materials/Confetti_Particle.mat");
            if (_cachedConfettiMaterial != null) return _cachedConfettiMaterial;
#endif
            _cachedConfettiMaterial = Resources.Load<Material>("Materials/Confetti_Particle");
            if (_cachedConfettiMaterial != null) return _cachedConfettiMaterial;

            Material baseMat = GetMaterial();
            if (baseMat != null)
            {
                _cachedConfettiMaterial = new Material(baseMat) { name = "WordPuzzle_ConfettiParticleRuntime" };
                return _cachedConfettiMaterial;
            }

            return null;
        }

        public static Material GetMaterial()
        {
            if (_cachedMaterial != null) return _cachedMaterial;

#if UNITY_EDITOR
            _cachedMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/WordPuzzle/Materials/Particle.mat");
            if (_cachedMaterial != null) return _cachedMaterial;
#endif
            _cachedMaterial = Resources.Load<Material>("Materials/Particle");
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
