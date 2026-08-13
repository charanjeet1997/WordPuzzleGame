using System.Collections;
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
        private Coroutine _recycleRoutine;

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

                longestLife = Mathf.Max(longestLife, main.duration + main.startLifetime.constantMax);

                ps.Clear(true);
                ps.Play(true);
            }

            if (_recycleRoutine != null) StopCoroutine(_recycleRoutine);
            _recycleRoutine = StartCoroutine(RecycleAfter(longestLife));
        }

        private IEnumerator RecycleAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            _recycleRoutine = null;
            RecycleSelf();
        }

        private void RecycleSelf()
        {
            FactoryFuncMapping.RecycleParticleFX(this, _factoryIndex);
        }

        private void OnDisable()
        {
            if (_recycleRoutine != null)
            {
                StopCoroutine(_recycleRoutine);
                _recycleRoutine = null;
            }
        }
    }
}
