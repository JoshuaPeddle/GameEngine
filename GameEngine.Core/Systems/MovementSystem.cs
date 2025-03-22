using GameEngine.Core.Components;

namespace GameEngine.Core.Systems
{
    public class MovementSystem : ISystem
    {
        public void Update(EntityManager entityManager, double deltaMs)
        {
            double deltaSeconds = deltaMs / 1000f;

            var entities = entityManager.GetEntitiesWithComponents<CMovement, CTransform>();

            foreach ((var entity, var cMovvement, var transform) in entities)
            {
                double moveSpeed = cMovvement.Speed; 
                if (entity.TryGetComponent<CInput>(out var input))
                {
                    double playerSpeedTransform = moveSpeed * deltaSeconds;

                    if (!input.Any)
                    {
                        float decelerationFactor = 0.2f;
                        transform.Velocity *= Math.Pow(decelerationFactor, deltaSeconds);
                    }
                    if (input.Up)
                    {
                        transform.Velocity -= new Vec2(0, playerSpeedTransform);
                    }
                    if (input.Down)
                    {
                        transform.Velocity += new Vec2(0, playerSpeedTransform);
                    }
                    if (input.Left)
                    {
                        transform.Velocity -= new Vec2(playerSpeedTransform, 0);
                    }
                    if (input.Right)
                    {
                        transform.Velocity += new Vec2(playerSpeedTransform, 0);
                    }
                }

                float maxSpeed = 250f;
                if (transform.Velocity.Length() > maxSpeed)
                {
                    transform.Velocity = transform.Velocity.Normalize() * maxSpeed;
                }
                transform.PreviousPosition = transform.Position.Clone();

                transform.Position += transform.Velocity * deltaSeconds;
            }
        }
    }
}
