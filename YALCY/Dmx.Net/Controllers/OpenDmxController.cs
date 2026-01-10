using System;
using System.Collections.Generic;
using System.IO;
using Dmx.Net.Attributes;
using Dmx.Net.Common;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Dmx.Net.Controllers
{
    [Controller("Open DMX")]
    public class OpenDmxController : ControllerBase
    {
        public new bool IsOpen => _handle != IntPtr.Zero;

#if WINDOWS
        private const string DllName = "ftd2xx.dll";
#elif LINUX
        private const string DllName = "libftd2xx.so";
#endif

        #region INTEROP

        [DllImport(DllName, EntryPoint = "FT_Open")]
        private static extern Status FT_Open(uint index, ref IntPtr ftHandle);

        [DllImport(DllName, EntryPoint = "FT_Close")]
        private static extern Status FT_Close(IntPtr ftHandle);

        [DllImport(DllName, EntryPoint = "FT_Write")]
        private static extern Status FT_Write(IntPtr ftHandle, byte[] lpBuffer, uint dwBytesToWrite, ref uint lpdwBytesWritten);

        [DllImport(DllName, EntryPoint = "FT_SetDataCharacteristics")]
        private static extern Status FT_SetDataCharacteristics(IntPtr ftHandle, DataBits uWordLength, StopBits uStopBits, Parity uParity);

        [DllImport(DllName, EntryPoint = "FT_SetFlowControl")]
        private static extern Status FT_SetFlowControl(IntPtr ftHandle, FlowControl usFlowControl, byte uXon, byte uXoff);

        [DllImport(DllName, EntryPoint = "FT_Purge")]
        private static extern Status FT_Purge(IntPtr ftHandle, PurgeFlags dwMask);

        [DllImport(DllName, EntryPoint = "FT_ClrRts")]
        private static extern Status FT_ClrRts(IntPtr ftHandle);

        [DllImport(DllName, EntryPoint = "FT_SetBreakOn")]
        private static extern Status FT_SetBreakOn(IntPtr ftHandle);

        [DllImport(DllName, EntryPoint = "FT_SetBreakOff")]
        private static extern Status FT_SetBreakOff(IntPtr ftHandle);

        [DllImport(DllName, EntryPoint = "FT_ResetDevice")]
        private static extern Status FT_ResetDevice(IntPtr ftHandle);

        [DllImport(DllName, EntryPoint = "FT_SetDivisor")]
        private static extern Status FT_SetDivisor(IntPtr ftHandle, char usDivisor);

        [DllImport(DllName, EntryPoint = "FT_CreateDeviceInfoList")]
        private static extern Status FT_CreateDeviceInfoList(ref uint numdevs);

        [DllImport(DllName, EntryPoint = "FT_GetDeviceInfoDetail")]
        private static extern Status FT_GetDeviceInfoDetail(uint index, ref uint flags, ref DeviceType chiptype, ref uint id, ref uint locid, byte[] serialnumber, byte[] description, ref IntPtr ftHandle);

        #endregion

        private IntPtr _handle = IntPtr.Zero;
        private Status _status;

        public OpenDmxController()
        {
        }

        public OpenDmxController(DmxTimer timer) : base(timer)
        {
        }

        public override void Open(int deviceIndex)
        {
            _status = FT_Open((uint)deviceIndex, ref _handle);
            _status = FT_ResetDevice(_handle);
            _status = FT_SetDivisor(_handle, (char)12);
            _status = FT_SetDataCharacteristics(_handle, DataBits.Bits8, StopBits.StopBits2, Parity.None);
            _status = FT_SetFlowControl(_handle, FlowControl.None, 0, 0);
            _status = FT_ClrRts(_handle);

            if (_status != Status.Ok)
            {
                throw new IOException($"Device could not be initialized ({_status}).");
            }

            ClearBuffer();

            base.Open(deviceIndex);
        }

        public override void Close()
        {
            base.Close();

            FT_Close(_handle);
            _handle = IntPtr.Zero;
        }

        public override async Task WriteBuffer()
        {
            if (IsOpen && !IsDisposed)
            {
                _status = FT_Purge(_handle, PurgeFlags.PurgeTx);
                _status = FT_Purge(_handle, PurgeFlags.PurgeRx);
                _status = FT_SetBreakOn(_handle);
                _status = FT_SetBreakOff(_handle);

                uint bytesWritten = 0;
                var buffer = new byte[writeBuffer.Length + 1];
                Buffer.BlockCopy(writeBuffer, 0, buffer, 1, writeBuffer.Length);

                _status = FT_Write(_handle, buffer, (uint)writeBuffer.Length, ref bytesWritten);

                if (_status != Status.Ok)
                {
                    FT_Close(_handle);
                    _handle = IntPtr.Zero;
                    throw new IOException($"Data write error ({_status}).");
                }
            }

            await Task.CompletedTask;
        }

        public static new IEnumerable<Device> GetDevices()
        {
            uint count = 0;

            var status = FT_CreateDeviceInfoList(ref count);
            if (status != Status.Ok)
            {
                throw new IOException("Could not get devices count.");
            }

            var devices = new FTDevice[count];
            var serial = new byte[16];
            var description = new byte[64];

            for (var i = 0; i < count; i++)
            {
                devices[i] = new FTDevice();

                status = FT_GetDeviceInfoDetail((uint)i, ref devices[i].Flags, ref devices[i].Type, ref devices[i].ID, ref devices[i].LocId, serial, description, ref devices[i].ftHandle);
                if (status != Status.Ok)
                {
                    throw new IOException("Could not get device info.");
                }

                devices[i].DeviceIndex = (uint)i;
                devices[i].SerialNumber = Encoding.ASCII.GetString(serial);
                devices[i].Description = Encoding.ASCII.GetString(description);

                var nullIndex = devices[i].SerialNumber.IndexOf('\0');

                if (nullIndex != -1)
                {
                    devices[i].SerialNumber = devices[i].SerialNumber.Substring(0, nullIndex);
                }

                nullIndex = devices[i].Description.IndexOf('\0');
                if (nullIndex != -1)
                {
                    devices[i].Description = devices[i].Description.Substring(0, nullIndex);
                }

                yield return new Device(
                    new ControllerInfo(typeof(OpenDmxController)),
                    i,
                    devices[i].SerialNumber,
                    devices[i].Description
                );
            }
        }

        public override void Dispose()
        {
            if (!IsDisposed)
            {
                Close();
                FT_ResetDevice(_handle);
                writeBuffer = Array.Empty<byte>();
                IsDisposed = true;

                GC.SuppressFinalize(this);
            }
        }

        #region STRUCTS

        private class FTDevice
        {
            public uint DeviceIndex;
            public uint Flags;
            public DeviceType Type;
            public uint ID;
            public uint LocId;
            public string SerialNumber;
            public string Description;
            public IntPtr ftHandle;
        }

        private enum DeviceType
        {
            DeviceBM = 0,
            DeviceAM,
            Device100AX,
            DeviceUNKNOWN,
            Device2232,
            Device232R,
            Device2232H,
            Device4232H,
            Device232H,
            DeviceX_SERIES,
            Device4222H_0,
            Device4222H_1_2,
            Device4222H_3,
            Device4222_PROG,
            DeviceFT900,
            DeviceFT930,
            DeviceUMFTPD3A,
            Device2233HP,
            Device4233HP,
            Device2232HP,
            Device4232HP,
            Device233HP,
            Device232HP,
            Device2232HA,
            Device4232HA
        };

        private enum Status
        {
            Ok = 0,
            InvalidHandle,
            DeviceNotFound,
            DeviceNotOpen,
            IoError,
            InsufficientResources,
            InvalidParameter,
            InvalidBaudRate,
            deviceNotOpenedForErase,
            DeviceNotOpenedForWrite,
            FailedToWriteDevice,
            EepromReadFailed,
            EepromWriteFailed,
            EepromEraseFailed,
            EepromNotPresent,
            EepromNotProgrammed,
            InvalidArgs,
            OtherError
        };

        private enum DataBits : byte
        {
            Bits8 = 0x08,
            Bits7 = 0x07
        }

        private enum StopBits : byte
        {
            StopBits1 = 0x00,
            StopBits2 = 0x02
        }

        private enum Parity : byte
        {
            None = 0x00,
            Odd = 0x01,
            Even = 0x02,
            Mark = 0x03,
            Space = 0x04
        }

        private enum FlowControl : ushort
        {
            None = 0x0000,
            RtsCts = 0x0100,
            DtrDsr = 0x0200,
            XonXoff = 0x0400
        }

        private enum PurgeFlags : byte
        {
            PurgeRx = 0x01,
            PurgeTx = 0x02
        }

        #endregion
    }
}
