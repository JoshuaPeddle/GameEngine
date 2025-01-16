using GameEngine.Core.Components;

namespace GameEngine.Core.Systems
{
    public class MovementSystem : ISystem
    {
        public void Update(EntityManager entityManager, double deltaMs)
        {
            double deltaSeconds = deltaMs / 1000f;
            double moveSpeed = 600; // TODO: Move somewhere else

            var entities = entityManager.GetEntitiesWithComponent<CTransform>();
            var validEntities = entities.Where(e => e.HasComponent<CMovement>()).ToList();
            foreach (var entity in validEntities)
            {
                var transform = entity.GetComponent<CTransform>();
                if (entity.HasComponent<CInput>())
                {
                    var input = entity.GetComponent<CInput>();

                    double playerSpeedTransform = moveSpeed * deltaSeconds;

                    if (!input.Any)
                    {
                        float decelerationFactor = 0.2f;
                        transform.Velocity *= Math.Pow(decelerationFactor, deltaSeconds);
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
                    transform.PreviousPosition = transform.Position.Clone();

                }
                transform.Position += transform.Velocity * deltaSeconds;
            }
        }
    }
}
