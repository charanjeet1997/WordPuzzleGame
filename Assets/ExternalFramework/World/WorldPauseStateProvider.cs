using System.Collections.Generic;
using UnityEngine;

namespace Games.WorldSystem
{
    public class WorldPauseStateProvider : IWorldStateProvider, IWorldSingleTick
    {
        public List<IWorldPauseState> entities;

        public WorldPauseStateProvider(List<IWorldEntity> worldEntities)
        {
            entities = new List<IWorldPauseState>();
            foreach (var entity in worldEntities)
            {
                if (entity is IWorldPauseState pauseState)
                {
                    entities.Add(pauseState);
                }
            }
        }

        public void Act(float transitionPercentage)
        {
            for (int i = 0; i < entities.Count; i++)
            {
                entities[i].Pause();
            }
        }
    }
}
