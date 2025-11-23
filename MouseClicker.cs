using SharpHook;
using SharpHook.Data;

namespace OpenClicker
{
    class MouseClicker
    {
        private static readonly IEventSimulator _simulator = new EventSimulator();

        public static Task Down(MouseButton button)
        {
            _simulator.SimulateMousePress(button);
            return Task.CompletedTask;
        }

        public static Task Up(MouseButton button)
        {
            _simulator.SimulateMouseRelease(button);
            return Task.CompletedTask;
        }
    }
}
