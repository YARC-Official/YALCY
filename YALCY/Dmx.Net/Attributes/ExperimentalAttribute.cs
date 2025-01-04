using System;

namespace Dmx.Net.Attributes
{
    /// <summary>
    /// Marks the controller as experimental. It will not be supported by the ControllerManager.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    internal class ExperimentalAttribute : Attribute
    {

    }
}
