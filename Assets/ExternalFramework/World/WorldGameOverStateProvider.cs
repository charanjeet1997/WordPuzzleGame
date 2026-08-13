using System.Collections.Generic;
using UnityEngine;

namespace Games.WorldSystem
{
    public class WorldGameOverStateProvider : IWorldStateProvider, IWorldSingleTick
    {
        public List<IGameOverState> entities;

        public WorldGameOverStateProvider(List<IWorldEntity> worldEntities)
        {
            entities = new List<IGameOverState>();
            foreach (var entity in worldEntities)
            {
                if (entity is IGameOverState gameOverState)
                {
                    entities.Add(gameOverState);
                }
            }
        }

        public void Act(float transitionPercentage)
        {
            for (int i = 0; i < entities.Count; i++)
            {
                entities[i].GameOver();
            }
        }
    }
}
