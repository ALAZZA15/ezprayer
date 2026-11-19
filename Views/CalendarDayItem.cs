using System;

namespace EzPrayer.Views
{
    public sealed class CalendarDayItem
    {
        public DateTime? Date { get; init; }
        public string DayNumber { get; init; } = string.Empty;
        public string HijriDay { get; init; } = string.Empty;
        public bool IsSelectable { get; init; }
        public bool IsToday { get; init; }
        public bool IsSelected { get; init; }
    }
}
