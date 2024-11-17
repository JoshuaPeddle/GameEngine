namespace GameEngine.Core
{
    public interface ISystem
    {
        void Update(EntityManager entityManager, double deltaTime);
    }
}
