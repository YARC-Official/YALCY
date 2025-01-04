using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Dmx.Net.Common
{
    public abstract class ControllerBase : IController
    {
        public bool IsOpen { get; protected set; }
        public bool IsDisposed { get; protected set; }
        public DmxTimer? Timer { get; protected set; }

        protected volatile bool canWrite;
        protected volatile byte[] writeBuffer = new byte[512];
        protected object _writeBufferLock = new object();

        public ControllerBase()
        {

        }

        public ControllerBase(DmxTimer timer)
        {
            Timer = timer;
        }

        /// <summary>
        /// Opens device.
        /// </summary>
        public virtual void Open(int deviceIndex)
        {
            canWrite = true;
            IsOpen = true;

            if (Timer != null)
            {
                Timer.Update += Timer_Update;
            }
        }

        private async void Timer_Update(object? sender, EventArgs e)
        {
            await WriteSafe();
        }

        /// <summary>
        /// Closes controller.
        /// </summary>
        public virtual void Close()
        {
            if (IsOpen)
            {
                ClearBuffer();
                WriteBuffer().Wait();
            }

            IsOpen = false;

            if (Timer != null)
            {
                Timer.Update -= Timer_Update;
            }
        }

        /// <summary>
        /// Sets value of a single channel.
        /// </summary>
        public void SetChannel(int channel, byte value)
        {
            if (channel < 1 || channel > 512)
            {
                throw new ArgumentOutOfRangeException(nameof(channel), "Channel number must be between 1 and 512.");
            }

            lock (_writeBufferLock)
            {
                writeBuffer[channel-1] = value;
            }
        }

        /// <summary>
        /// Sets values of a channel range.
        /// </summary>
        public void SetChannelRange(int startChannel, params byte[] values)
        {
            if (startChannel < 1 || startChannel + values.Length > 512)
            {
                throw new ArgumentOutOfRangeException(nameof(startChannel), "Start channel number must be between 1 and 512.");
            }

            lock (_writeBufferLock)
            {
                Buffer.BlockCopy(values, 0, writeBuffer, startChannel - 1, values.Length);
            }
        }

        /// <summary>
        /// Writes buffer to the controller only when a previous write action was finished.
        /// </summary>
        public async Task WriteSafe()
        {
            if (!IsDisposed && IsOpen && canWrite)
            {
                canWrite = false;
                try
                {
                    await WriteBuffer();
                }
                catch (IOException)
                {
                    IsOpen = false;
                }
                finally
                {
                    canWrite = true;
                }
            }
        }

        /// <summary>
        /// Writes buffer to the controller.
        /// </summary>
        public virtual async Task WriteBuffer()
        {
            await Task.CompletedTask;
        }

        /// <summary>
        /// Clears write buffer.
        /// </summary>
        public void ClearBuffer()
        {
            if (!IsDisposed)
            {
                lock (_writeBufferLock)
                {
                    Array.Clear(writeBuffer, 0, writeBuffer.Length);
                }
            }
        }

        public virtual void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;
                Close();
                writeBuffer = new byte[0];
            }
        }

        public static IEnumerable<Device> GetDevices()
        {
            yield break;
        }
    }
}
