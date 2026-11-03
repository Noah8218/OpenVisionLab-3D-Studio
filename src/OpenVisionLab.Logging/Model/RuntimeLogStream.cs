using log4net;
using log4net.Core;
using log4net.Repository.Hierarchy;
using System;

namespace OpenVisionLab.Logging.Model
{
    public sealed class RuntimeLogStream : IDisposable
    {
        private readonly RuntimeLogSink _appender;
        private readonly Hierarchy _logRepository;
        private readonly object _lifecycleGate = new object();
        private bool _attached;
        private volatile bool _disposed;

        public RuntimeLogStream()
        {
            _appender = new RuntimeLogSink();
            _logRepository = (Hierarchy)LogManager.GetRepository();
            _logRepository.Root.AddAppender(_appender);
            _attached = true;
            _logRepository.Root.Level = Level.All;
            _logRepository.Configured = true;
            _logRepository.RaiseConfigurationChanged(EventArgs.Empty);
        }

        public string GetLog() => _disposed ? string.Empty : _appender.ReadBuffer();

        public string[] GetLogs() => GetLog().Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

        public void Suspend()
        {
            lock (_lifecycleGate)
            {
                if (_disposed || !_attached) return;
                _logRepository.Root.RemoveAppender(_appender);
                _attached = false;
            }
            _logRepository.RaiseConfigurationChanged(EventArgs.Empty);
        }

        public void Resume()
        {
            lock (_lifecycleGate)
            {
                if (_disposed || _attached) return;
                _logRepository.Root.AddAppender(_appender);
                _attached = true;
            }
            _logRepository.RaiseConfigurationChanged(EventArgs.Empty);
        }

        public void Dispose()
        {
            lock (_lifecycleGate)
            {
                if (_disposed) return;
                if (_attached)
                {
                    _logRepository.Root.RemoveAppender(_appender);
                    _attached = false;
                }
                _disposed = true;
                _appender.Close();
            }
            _logRepository.RaiseConfigurationChanged(EventArgs.Empty);
        }
    }
}
