using System;

namespace Dmx.Net.Attributes
{
    /// <summary>
    /// Marks the class as controller. Will be supported by ControllerManager.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    internal class ControllerAttribute : Attribute
    {
        public string Name { get; }

        public ControllerAttribute(string name)
        {
            Name = name;
        }
    }
}
