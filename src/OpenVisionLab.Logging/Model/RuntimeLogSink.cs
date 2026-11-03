using log4net.Appender;
using log4net.Core;
using System.Collections.Generic;
using System.Text;

namespace OpenVisionLab.Logging.Model
{
	public class RuntimeLogSink : AppenderSkeleton
	{
		// Bounds apply only to the live display buffer, never to file appenders.
		private const int MaximumQueuedRecords = 8192;
		private const long MaximumQueuedBytes = 8L * 1024 * 1024;
		private const int MaximumReadBytes = 256 * 1024;
		private readonly Queue<string> _logQueue = new Queue<string>();
		private readonly object _syncRoot = new object();
		private string _lastRenderedMessage;
		private long _lastTimestampTicks;
		private long _queuedBytes;
		private long _droppedRecords;
		private long _pendingDroppedRecords;

		public RuntimeLogSink()
		{
		}

		protected override void Append(LoggingEvent loggingEvent)
		{
			if (loggingEvent == null)
			{
				return;
			}

			string renderedMessage = loggingEvent.RenderedMessage ?? string.Empty;
			lock (_syncRoot)
			{
				// An oversized display entry must not be retained in the duplicate cache.
				if ((long)renderedMessage.Length * sizeof(char) > MaximumQueuedBytes - 128)
				{
					_lastRenderedMessage = null;
					RecordDrop();
					return;
				}
				if (ShouldSkipDuplicate(renderedMessage, loggingEvent.TimeStamp))
				{
					return;
				}

				string logMessage = string.Format("[{0}]{1}", loggingEvent.TimeStamp.ToString("yyyy-MM-dd HH:mm:ss.fff"), renderedMessage);
				long bytes = BufferBytes(logMessage);
				while (_logQueue.Count >= MaximumQueuedRecords || _queuedBytes + bytes > MaximumQueuedBytes)
				{
					_queuedBytes -= BufferBytes(_logQueue.Dequeue());
					RecordDrop();
				}
				_logQueue.Enqueue(logMessage);
				_queuedBytes += bytes;
			}
		}

		private bool ShouldSkipDuplicate(string renderedMessage, System.DateTime timestamp)
		{
			lock (_syncRoot)
			{
				long timestampTicks = timestamp.Ticks;
				bool isDuplicate = renderedMessage == _lastRenderedMessage
					&& timestampTicks - _lastTimestampTicks < System.TimeSpan.FromMilliseconds(200).Ticks;

				_lastRenderedMessage = renderedMessage;
				_lastTimestampTicks = timestampTicks;
				return isDuplicate;
			}
		}

		public string ReadBuffer()
		{
			StringBuilder sb = new StringBuilder();
			lock (_syncRoot)
			{
				if (_pendingDroppedRecords > 0)
				{
					sb.Append('[').Append(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
						.Append("][All][Warning][RuntimeLogSink] Display log overflow: ")
						.Append(_pendingDroppedRecords).Append(" record(s) omitted; total ")
						.Append(_droppedRecords).AppendLine(". File logging is unchanged.");
					_pendingDroppedRecords = 0;
				}
				while (_logQueue.Count > 0)
				{
					long bytes = BufferBytes(_logQueue.Peek());
					// Keep records whole; even a single long entry must eventually drain.
					if (sb.Length > 0 && (long)sb.Length * sizeof(char) + bytes > MaximumReadBytes)
					{
						break;
					}
					string log = _logQueue.Dequeue();
					_queuedBytes -= bytes;
					sb.AppendLine(log);
				}
			}
			return sb.ToString();
		}

		private static long BufferBytes(string message) =>
			((long)message.Length + System.Environment.NewLine.Length) * sizeof(char);

		private void RecordDrop()
		{
			_droppedRecords++;
			_pendingDroppedRecords++;
		}
	}
}
