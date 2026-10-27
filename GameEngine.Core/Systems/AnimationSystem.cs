using GameEngine.Core.Components;

namespace GameEngine.Core.Systems
{
    public class AnimationSystem : ISystem
    {
        public void Update(EntityManager entityManager, double deltaSeconds)
        {
            var entities = entityManager.GetEntitiesWithComponent<CAnimation>();
            for (int i = 0; i < entities.Count; i++)
            {
                var animation = entities[i].GetComponent<CAnimation>();
                animation.Update(deltaSeconds);
            }
        }
    }
}