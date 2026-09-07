namespace GameEngine.Demo.Emberbrook;

public sealed partial class EmberbrookWorld
{
    public void SmeltBar() { Stop(); Craft(Recipe.Bar); }
    public void ForgeEquipment() { Stop(); Craft(HasSword ? Recipe.Shield : Recipe.Sword); }
}
