using System;
using System.Text.RegularExpressions;

namespace OpenVisionLab.Logging.Controls.Model
{
    public sealed class LogLine
    {
        private static readonly Regex LogPattern = new Regex(
            @"^\[(?<time>[^\]]*)\]\s*\[(?<category>[^\]]*)\]\[(?<level>[^\]]*)\](?:\[(?<source>[^\]]*)\])?\s*(?<message>.*)$",
            RegexOptions.None);

        public LogLine(
            DateTime timestamp,
            string timestampText,
            string category,
            string level,
            string source,
            string message,
            string rawText)
        {
            Timestamp = timestamp;
            TimestampText = timestampText ?? string.Empty;
            Category = category ?? string.Empty;
            Level = level ?? string.Empty;
            Source = source ?? string.Empty;
            Message = message ?? string.Empty;
            RawText = rawText ?? string.Empty;
        }

        public DateTime Timestamp { get; }

        public string TimestampText { get; }

        public string DisplayTime => Timestamp == DateTime.MinValue
            ? string.Empty
            : Timestamp.ToString("HH:mm:ss.fff");

        public string Category { get; }

        public string Level { get; }

        public string Source { get; }

        public string Message { get; }

        public string RawText { get; }

        public static LogLine Parse(string rawText)
        {
            string text = rawText ?? string.Empty;
            Match match = LogPattern.Match(text);
            if (!match.Success)
            {
                return new LogLine(DateTime.MinValue, string.Empty, string.Empty, string.Empty, string.Empty, text, text);
            }

            string timestampText = match.Groups["time"].Value;
            DateTime timestamp;
            if (!TryReadLoggerTimestamp(timestampText, out timestamp))
                DateTime.TryParse(timestampText, out timestamp);

            return new LogLine(
                timestamp,
                timestampText,
                match.Groups["category"].Value,
                match.Groups["level"].Value,
                match.Groups["source"].Value,
                match.Groups["message"].Value,
                text);
        }

        // The logger emits this exact Gregorian form. Avoid the general date parser's
        // first-use cost on the refresh callback; every other form/culture retains
        // the existing DateTime.TryParse fallback and its result semantics.
        private static bool TryReadLoggerTimestamp(string text, out DateTime value)
        {
            value = DateTime.MinValue;
            if (text.Length != 23 || text[4] != '-' || text[7] != '-'
                || text[10] != ' ' || text[13] != ':' || text[16] != ':' || text[19] != '.'
                || System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.Calendar.GetType()
                    != typeof(System.Globalization.GregorianCalendar))
            {
                return false;
            }

            int year = ReadDigits(text, 0, 4), month = ReadDigits(text, 5, 2);
            int day = ReadDigits(text, 8, 2), hour = ReadDigits(text, 11, 2);
            int minute = ReadDigits(text, 14, 2), second = ReadDigits(text, 17, 2);
            int millisecond = ReadDigits(text, 20, 3);
            if (year < 1 || month < 1 || month > 12 || day < 1
                || day > DateTime.DaysInMonth(year, month) || hour < 0 || hour > 23
                || minute < 0 || minute > 59 || second < 0 || second > 59 || millisecond < 0)
            {
                return false;
            }

            value = new DateTime(year, month, day, hour, minute, second, millisecond);
            return true;
        }

        private static int ReadDigits(string text, int start, int count)
        {
            int value = 0;
            for (int index = start; index < start + count; index++)
            {
                int digit = text[index] - '0';
                if (digit < 0 || digit > 9) return -1;
                value = value * 10 + digit;
            }
            return value;
        }

    }
}
