using GameEngine.Core;
using GameEngine.Core.Systems;
using System;

namespace GameEngine.Demo
{
    public class SceneEmpty : Scene
    {
        public SceneEmpty() { }

        public override void Initialize(EntityManager entityManager, InputManager inputManager, AudioSystem audioPlayer, Action ResetScene) { }
    }
}
