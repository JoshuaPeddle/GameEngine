namespace GameEngine.Core
{
    public interface ISystem
    {
        /// <param name="deltaSeconds">Seconds elapsed since the previous frame.</param>
        void Update(EntityManager entityManager, double deltaSeconds);
    }
}
