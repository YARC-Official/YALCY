using System;
using System.Threading.Tasks;

namespace Dmx.Net.Common
{
    public interface IController : IDisposable
    {
        public bool IsOpen { get; }
        void Open(int deviceIndex);
        void Close();
        void SetChannel(int channel, byte value);
        void SetChannelRange(int startChannel, params byte[] values);
        Task WriteSafe();
        Task WriteBuffer();
        void ClearBuffer();
    }
}
