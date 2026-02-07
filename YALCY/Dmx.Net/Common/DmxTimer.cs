using System;
using System.Threading;

namespace Dmx.Net.Common
{
    public class DmxTimer : IDisposable
    {
        public int Interval { get; }
        public event EventHandler? BeforeUpdate;
        public event EventHandler? AfterUpdate;

        internal event EventHandler? Update;

        private Timer? _timer;

        public DmxTimer(int interval = 22)
        {
            Interval = interval;
        }

        public void Start()
        {
            Stop();

            _timer = new Timer(OnUpdate, null, 0, Interval);
        }

        public void Stop()
        {
            if (_timer != null)
            {
                _timer.Dispose();
                _timer = null;
            }
        }

        private void OnUpdate(object? state)
        {
            BeforeUpdate?.Invoke(this, new EventArgs());
            Update?.Invoke(this, new EventArgs());
            AfterUpdate?.Invoke(this, new EventArgs());
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
