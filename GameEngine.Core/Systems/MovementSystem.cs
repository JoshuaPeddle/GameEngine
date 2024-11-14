using GameEngine.Core.Components;

namespace GameEngine.Core.Systems
{
    public class MovementSystem : ISystem
    {
        public void Update(EntityManager entityManager, float deltaMs)
        {
            float deltaSeconds = deltaMs / 1000f;
            float moveSpeed = 600f; // TODO: Move somewhere else

            var entities = entityManager.GetEntitiesWithComponent<CTransform>();
            foreach (var entity in entities)
            {
                var transform = entity.GetComponent<CTransform>();
                if (entity.HasComponent<CInput>())
                {
                    var input = entity.GetComponent<CInput>();

                    float playerSpeedTransform = moveSpeed * deltaSeconds;

                    if (!input.Any)
                    {
                        float decelerationFactor = 0.2f;
                        transform.Velocity *= MathF.Pow(decelerationFactor, deltaSeconds);
                    }
                    if (input.Up)
                    {
                        transform.Velocity.Y -= playerSpeedTransform;
                    }
                    if (input.Down)
                    {
                        transform.Velocity.Y += playerSpeedTransform;
                    }
                    if (input.Left)
                    {
                        transform.Velocity.X -= playerSpeedTransform;
                    }
                    if (input.Right)
                    {
                        transform.Velocity.X += playerSpeedTransform;
                    }

                    float maxSpeed = 250f;
                    if (transform.Velocity.Length() > maxSpeed)
                    {
                        transform.Velocity = transform.Velocity.Normalize() * maxSpeed;
                    }
                }

                transform.Position += transform.Velocity * deltaSeconds;
            }
        }
    }
}
