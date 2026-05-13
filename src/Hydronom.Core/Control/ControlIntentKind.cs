namespace Hydronom.Core.Control
{
    /// <summary>
    /// Controller'ın nasıl davranacağını tanımlar.
    /// Decision artık doğrudan wrench üretmez;
    /// yalnızca operasyonel niyet üretir.
    /// </summary>
    public enum ControlIntentKind
    {
        Idle = 0,

        Navigate = 1,

        HoldPosition = 2,

        AvoidObstacle = 3,

        EmergencyStop = 4,

        Manual = 5
    }
}