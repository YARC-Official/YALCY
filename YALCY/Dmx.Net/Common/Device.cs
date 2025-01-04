namespace Dmx.Net.Common
{
    public sealed class Device
    {
        public ControllerInfo Controller { get; }
        public int? DeviceIndex { get; }
        public string? SerialNumber { get; }
        public string? Description { get; }

        public Device(ControllerInfo controller, int? deviceIndex, string? serialNumber, string? description)
        {
            Controller = controller;
            DeviceIndex = deviceIndex;
            SerialNumber = serialNumber;
            Description = description;
        }

        public override string ToString()
        {
            return $"Index={DeviceIndex}, Serial={SerialNumber}, Description={Description}";
        }
    }
}
