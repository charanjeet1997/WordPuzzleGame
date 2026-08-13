using UnityEngine;
using ServiceLocatorFramework;
using DataBindingFramework;
using WordPuzzle.Factory;
using WordPuzzle.Models;
using WordPuzzle.Particles;

namespace WordPuzzle.Services
{
    public interface IParticleService
    {
        void PlayTileRevealSparkle(Vector3 position);
        void PlayWordMatchBurst(Vector3 position);
        void PlayBonusWordSparkle(Vector3 position);
        void PlayLevelCompleteFireworks(Vector3 position);
    }

    public class ParticleService : MonoBehaviour, IParticleService
    {
        private UnityEngine.Object _bindingOwner;

        private void Awake()
        {
            // Nothing else registers this, so every _particleService lookup was returning null
            // and word-match / hint effects never played.
            if (ServiceLocator.Current != null && !ServiceLocator.Current.Has<IParticleService>())
            {
                ServiceLocator.Current.Register<IParticleService>(this);
            }
        }

        private void OnDestroy()
        {
            if (ServiceLocator.Current != null && ServiceLocator.Current.Has<IParticleService>())
            {
                ServiceLocator.Current.Unregister<IParticleService>();
            }
        }

        private void Start()
        {
            _bindingOwner = this;

            if (ServiceLocator.Current.Has<IObserverManager>())
            {
                var observerManager = ServiceLocator.Current.Get<IObserverManager>();
                var levelCompletedObs = observerManager.GetOrCreateObserver<int>(WondersOfWordGameModel.OBS_LEVEL_COMPLETED);
                levelCompletedObs.Bind(_bindingOwner, (lvl) => PlayLevelCompleteFireworks(transform.position));
            }
        }

        public void PlayTileRevealSparkle(Vector3 position)
        {
            Spawn(FXType.TileReveal, position);
        }

        public void PlayWordMatchBurst(Vector3 position)
        {
            Spawn(FXType.WordMatchBurst, position);
        }

        public void PlayBonusWordSparkle(Vector3 position)
        {
            Spawn(FXType.BonusWordSparkle, position);
        }

        public void PlayLevelCompleteFireworks(Vector3 position)
        {
            Spawn(FXType.LevelCompleteFireworks, position + Vector3.left * 2f);
            Spawn(FXType.LevelCompleteFireworks, position + Vector3.right * 2f);
            Spawn(FXType.LevelCompleteFireworks, position + Vector3.up * 2f);
        }

        private static void Spawn(FXType type, Vector3 position)
        {
            ParticleFX fx = FactoryFuncMapping.CreateParticleFX(type);
            if (fx == null) return;

            fx.transform.position = position;
            fx.Init((int)type);
        }
    }
}
