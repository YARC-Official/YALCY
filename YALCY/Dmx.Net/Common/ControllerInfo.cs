using System;
using System.Linq;
using Dmx.Net.Attributes;

namespace Dmx.Net.Common
{
    public sealed class ControllerInfo
    {
        public string Name { get; }
        public Type Type { get; }

        public ControllerInfo(Type type)
        {
            var attrib = type.GetCustomAttributes(typeof(ControllerAttribute), false).FirstOrDefault() as ControllerAttribute;

            if(attrib is null)
            {
                throw new ArgumentException("Provided type is not a controller.");
            }

            Name = attrib.Name;
            Type = type;
        }
    }
}
