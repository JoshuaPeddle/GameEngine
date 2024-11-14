using GameEngine.Core.Components;

namespace GameEngine.Core.Systems
{
    public class AnimationSystem : ISystem
    {
        public void Update(EntityManager entityManager, float deltaTime)
        {
            var entities = entityManager.GetEntitiesWithComponent<CAnimation>();
            foreach (var entity in entities)
            {
                var animation = entity.GetComponent<CAnimation>();
                animation.Update(deltaTime);
            }
        }
    }
}
