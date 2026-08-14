using UnityEngine;
using WordPuzzle.Factory;
using WordPuzzle.Services;

namespace WordPuzzle.Particles
{
    /// <summary>
    /// Pooled one-shot particle effect. ParticleSystem is a Component but not a MonoBehaviour,
    /// so it cannot be pooled through FactoryManagerByType directly - this component wraps it.
    /// </summary>
    public class ParticleFX : MonoBehaviour
    {
        private ParticleSystem[] _systems;
        private int _factoryIndex;

        // An unscaled-time deadline rather than a WaitForSeconds coroutine: this is the hot
        // path (one spawn per revealed tile), so it must not allocate, and effects that play
        // over a paused level-complete overlay would otherwise never be returned to the pool.
        private bool _active;
        private float _recycleAt;

        private void Awake()
        {
            _systems = GetComponentsInChildren<ParticleSystem>(true);
        }

        public void Init(int factoryIndex)
        {
            _factoryIndex = factoryIndex;

            if (_systems == null || _systems.Length == 0)
            {
                _systems = GetComponentsInChildren<ParticleSystem>(true);
            }

            // AddComponent/prefab leaves the built-in particle material, whose shader does not
            // exist under URP and renders magenta.
            ParticleMaterialUtility.EnsureValidMaterial(gameObject);

            float longestLife = 0f;

            foreach (ParticleSystem ps in _systems)
            {
                var main = ps.main;
                main.loop = false;
                // Must not be Destroy: these instances are pooled and returned to the factory.
                main.stopAction = ParticleSystemStopAction.None;
                // Matched to the unscaled recycle deadline below - on scaled time a paused
                // overlay would freeze the visuals while the pool reclaimed them anyway.
                main.useUnscaledTime = true;

                longestLife = Mathf.Max(longestLife, main.duration + main.startLifetime.constantMax);

                ps.Clear(true);
                ps.Play(true);
            }

            _recycleAt = Time.unscaledTime + longestLife;
            _active = true;
        }

        private void Update()
        {
            if (!_active || Time.unscaledTime < _recycleAt) return;

            _active = false;
            FactoryFuncMapping.RecycleParticleFX(this, _factoryIndex);
        }

        private void OnDisable()
        {
            _active = false;
        }
    }
}
