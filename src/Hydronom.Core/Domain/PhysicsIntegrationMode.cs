namespace Hydronom.Core.Domain
{
    /// <summary>
    /// Fizik entegrasyonu yÃ¶ntemi.
    /// SemiImplicitEuler varsayÄ±lan olarak daha kararlÄ± olduÄŸu iÃ§in tercih edilir.
    /// </summary>
    public enum PhysicsIntegrationMode
    {
        ExplicitEuler = 0,
        SemiImplicitEuler = 1
    }
}
