using System;

namespace Hydronom.Core.Modules.ControlModule
{
    public class ControlManager
    {
        public ControlManager() { }

        public void ApplyControlMode(ControlMode mode)
        {
            switch (mode)
            {
                case ControlMode.Manual:
                    Console.WriteLine("🕹️ Control mode: Manual");
                    break;
                case ControlMode.Autonomous:
                    Console.WriteLine("🚀 Control mode: Autonomous");
                    break;
            }
        }

        public void MoveForward()
        {
            Console.WriteLine("🚀 Moving forward.");
        }
    }
}
