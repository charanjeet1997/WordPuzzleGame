using Game.Factories;
using UnityEngine;
using WordPuzzle.Gameplay;
using WordPuzzle.Particles;

namespace WordPuzzle.Factory
{
    public static class FactoryFuncMapping
    {
        public static GridTile CreateGridTile(int factoryIndex = 0)
        {
            var factory = FactoryManagerByType.GetFactory<GridTile>();
            if (factory == null) return null;
            try
            {
                return factory.Create(factoryIndex);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[FactoryFuncMapping] GridTile factory index {factoryIndex} out of bounds in FactoryConfig: {ex.Message}");
                return null;
            }
        }

        public static void RecycleGridTile(GridTile tile, int factoryIndex = 0)
        {
            var factory = FactoryManagerByType.GetFactory<GridTile>();
            factory?.Recycle(tile, factoryIndex);
        }

        public static LetterNode CreateLetterNode(int factoryIndex = 0)
        {
            var factory = FactoryManagerByType.GetFactory<LetterNode>();
            if (factory == null) return null;
            try
            {
                return factory.Create(factoryIndex);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[FactoryFuncMapping] LetterNode factory index {factoryIndex} out of bounds in FactoryConfig: {ex.Message}");
                return null;
            }
        }

        public static void RecycleLetterNode(LetterNode node, int factoryIndex = 0)
        {
            var factory = FactoryManagerByType.GetFactory<LetterNode>();
            factory?.Recycle(node, factoryIndex);
        }

        public static ParticleFX CreateParticleFX(int factoryIndex)
        {
            var factory = FactoryManagerByType.GetFactory<ParticleFX>();
            if (factory == null) return null;
            try
            {
                return factory.Create(factoryIndex);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[FactoryFuncMapping] ParticleFX factory index {factoryIndex} out of bounds in FactoryConfig: {ex.Message}");
                return null;
            }
        }

        public static ParticleFX CreateParticleFX(FXType type) => CreateParticleFX((int)type);

        public static void RecycleParticleFX(ParticleFX fx, int factoryIndex)
        {
            var factory = FactoryManagerByType.GetFactory<ParticleFX>();
            factory?.Recycle(fx, factoryIndex);
        }

        public static void RecycleParticleFX(ParticleFX fx, FXType type) => RecycleParticleFX(fx, (int)type);
    }
}
