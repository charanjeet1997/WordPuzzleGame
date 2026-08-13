using Game.Factories;
using UnityEngine;
using WordPuzzle.Gameplay;
using WordPuzzle.Particles;

namespace WordPuzzle.Factory
{
    public class WordPuzzleFactoryController : MonoBehaviour
    {
        [SerializeField] private FactoryConfig<GridTile> gridTileConfig;
        [SerializeField] private FactoryConfig<LetterNode> letterNodeConfig;
        [SerializeField] private FactoryConfig<ParticleFX> particleFXConfig;

        private void OnEnable()
        {
            if (gridTileConfig != null)
            {
                FactoryManagerByType.RegisterFactoryByType(new GridTileFactory(), gridTileConfig);
            }

            if (letterNodeConfig != null)
            {
                FactoryManagerByType.RegisterFactoryByType(new LetterNodeFactory(), letterNodeConfig);
            }

            if (particleFXConfig != null)
            {
                FactoryManagerByType.RegisterFactoryByType(new ParticleFXFactory(), particleFXConfig);
            }
        }

        private void OnDestroy()
        {
            FactoryManagerByType.CleanupFactory<GridTile>();
            FactoryManagerByType.CleanupFactory<LetterNode>();
            FactoryManagerByType.CleanupFactory<ParticleFX>();
        }
    }
}
