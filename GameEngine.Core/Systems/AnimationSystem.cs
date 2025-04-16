using GameEngine.Core.Components;

namespace GameEngine.Core.Systems
{
    public class AnimationSystem : ISystem
    {
        public void Update(EntityManager entityManager, double deltaTime)
        {
            var entities = entityManager.GetEntitiesWithComponents<CAnimation>();
            foreach (var entity in entities)
            {
                entity.Item2.Update(deltaTime);
            }
        }
    }
}