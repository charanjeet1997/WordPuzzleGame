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
        void PlayConfetti(Vector3 position);
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
                levelCompletedObs.Bind(_bindingOwner, (lvl) =>
                {
                    Vector3 center = ViewCenter();
                    PlayLevelCompleteFireworks(center);
                    PlayConfetti(center);
                });
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

        public void PlayConfetti(Vector3 position)
        {
            Camera cam = MainCamera();
            float halfHeight = cam != null && cam.orthographic ? cam.orthographicSize : 4.5f;
            float halfWidth = cam != null ? halfHeight * cam.aspect : halfHeight * 0.5625f;

            // Dual cannon fountain barrage arcing from bottom corners plus celebratory center burst
            Spawn(FXType.Confetti, position + new Vector3(-halfWidth * 0.5f, -halfHeight * 0.3f, 0f));
            Spawn(FXType.Confetti, position + new Vector3(halfWidth * 0.5f, -halfHeight * 0.3f, 0f));
            Spawn(FXType.Confetti, position + new Vector3(0f, halfHeight * 0.15f, 0f));
        }

        public void PlayLevelCompleteFireworks(Vector3 position)
        {
            // Spread derived from the visible frame rather than fixed world units: at any other
            // aspect ratio or orthographic size, the old +-2 offsets landed off-screen or bunched.
            Camera cam = MainCamera();
            float halfHeight = cam != null && cam.orthographic ? cam.orthographicSize : 4.5f;
            float halfWidth = cam != null ? halfHeight * cam.aspect : halfHeight * 0.5625f;

            Spawn(FXType.LevelCompleteFireworks, position + new Vector3(-halfWidth * 0.55f, halfHeight * 0.25f, 0f));
            Spawn(FXType.LevelCompleteFireworks, position + new Vector3(halfWidth * 0.55f, halfHeight * 0.25f, 0f));
            Spawn(FXType.LevelCompleteFireworks, position + new Vector3(0f, halfHeight * 0.55f, 0f));
        }

        private Camera _mainCamera;

        private Camera MainCamera()
        {
            // Camera.main is a tagged search; this fires on every level completion.
            if (_mainCamera == null) _mainCamera = Camera.main;
            return _mainCamera;
        }

        /// <summary>World point at the centre of the visible frame, independent of where this service sits.</summary>
        private Vector3 ViewCenter()
        {
            Camera cam = MainCamera();
            if (cam == null) return transform.position;

            Vector3 center = cam.transform.position;
            center.z = transform.position.z;
            return center;
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
