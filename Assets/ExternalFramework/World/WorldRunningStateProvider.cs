using System.Collections.Generic;
using UnityEngine;

namespace Games.WorldSystem
{
    public class WorldRunningStateProvider : IWorldStateProvider, IWorldContinousTick
    {
        public List<IWorldRunningState> entities;

        public WorldRunningStateProvider(List<IWorldEntity> worldEntities)
        {
            entities = new List<IWorldRunningState>();
            foreach (var entity in worldEntities)
            {
                if (entity is IWorldRunningState runningState)
                {
                    entities.Add(runningState);
                }
            }
        }

        public void Act(float transitionPercentage)
        {
            for (int i = 0; i < entities.Count; i++)
            {
                entities[i].Running();
            }
        }
    }
}
