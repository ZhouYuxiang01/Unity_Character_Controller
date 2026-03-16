using Characters.Player.AI.Data;

namespace Characters.Player.AI.Sensor
{
    public interface INavigatorSensor
    {
        ref readonly NavigationContext GetCurrentContext();
    }
}