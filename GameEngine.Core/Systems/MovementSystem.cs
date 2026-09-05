using GameEngine.Core.Components;
using System.Runtime.CompilerServices;

namespace GameEngine.Core.Systems
{
    public class MovementSystem : ISystem
    {
        private const double DecelerationBase = 0.2;
        
        public void Update(EntityManager entityManager, double deltaSeconds)
        {
            // Every transform records where it started the step, not just the ones this system
            // moves: the physics sweep reads that to know the path a body took, and an entity
            // a scene repositions directly would otherwise carry a stale start point forever.
            foreach (var (_, transform) in entityManager.GetEntitiesWithComponents<CTransform>())
                transform.PreviousPosition = transform.Position;

            var entities = entityManager.GetEntitiesWithComponents<CMovement, CTransform>();

            foreach (var (entity, movement, transform) in entities)
            {
                ProcessEntityMovement(entity, movement, transform, deltaSeconds);
            }
        }

        private static void ProcessEntityMovement(Entity entity, CMovement movement, CTransform transform, double deltaSeconds)
        {
            double moveSpeed = movement.Speed;
            
            if (entity.TryGetComponent<CInput>(out var input))
            {
                ProcessInputBasedMovement(input, transform, moveSpeed, deltaSeconds);
            }

            ApplySpeedLimits(transform, movement.MaxSpeed);
            
            UpdatePosition(transform, deltaSeconds);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ProcessInputBasedMovement(CInput input, CTransform transform, double moveSpeed, double deltaSeconds)
        {
            double speedDelta = moveSpeed * deltaSeconds;

            if (!input.Any)
            {
                double decelerationFactor = Math.Pow(DecelerationBase, deltaSeconds);
                transform.Velocity = new Vec2(
                    transform.Velocity.X * decelerationFactor,
                    transform.Velocity.Y * decelerationFactor
                );
                return;
            }

            Vec2 inputVelocity = Vec2.Zero;
            
            if (input.Up)    inputVelocity = new Vec2(inputVelocity.X, inputVelocity.Y - speedDelta);
            if (input.Down)  inputVelocity = new Vec2(inputVelocity.X, inputVelocity.Y + speedDelta);
            if (input.Left)  inputVelocity = new Vec2(inputVelocity.X - speedDelta, inputVelocity.Y);
            if (input.Right) inputVelocity = new Vec2(inputVelocity.X + speedDelta, inputVelocity.Y);

            transform.Velocity = new Vec2(
                transform.Velocity.X + inputVelocity.X,
                transform.Velocity.Y + inputVelocity.Y
            );
        }

        private static void ApplySpeedLimits(CTransform transform, double maxSpeed)
        {
            double currentSpeedSquared = (transform.Velocity.X * transform.Velocity.X) + 
                                       (transform.Velocity.Y * transform.Velocity.Y);
            double maxSpeedSquared = maxSpeed * maxSpeed;

            if (currentSpeedSquared > maxSpeedSquared)
            {
                double currentSpeed = Math.Sqrt(currentSpeedSquared);
                double scale = maxSpeed / currentSpeed;
                transform.Velocity = new Vec2(
                    transform.Velocity.X * scale,
                    transform.Velocity.Y * scale
                );
            }
        }

        private static void UpdatePosition(CTransform transform, double deltaSeconds)
        {
            transform.Position = new Vec2(
                transform.Position.X + (transform.Velocity.X * deltaSeconds),
                transform.Position.Y + (transform.Velocity.Y * deltaSeconds)
            );
        }
    }
}
