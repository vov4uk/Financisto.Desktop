using Financisto.Common.Entities;
using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Financisto.Common.Filters
{
    [ExcludeFromCodeCoverage]
    public partial class PeriodFilter : UserControl
    {
        public static readonly StyledProperty<PeriodType> SelectedPeriodTypeProperty =
            AvaloniaProperty.Register<PeriodFilter, PeriodType>(nameof(SelectedPeriodType), PeriodType.AllTime);

        public static readonly StyledProperty<Orientation> OrientationProperty =
            AvaloniaProperty.Register<PeriodFilter, Orientation>(nameof(Orientation), Orientation.Horizontal);

        // DateTimeOffset?, not DateTime? like the WPF version: Avalonia's DatePicker/TimePicker use DateTimeOffset?/TimeSpan?.
        public static readonly StyledProperty<DateTimeOffset?> FromProperty =
            AvaloniaProperty.Register<PeriodFilter, DateTimeOffset?>(nameof(From));

        public static readonly StyledProperty<DateTimeOffset?> ToProperty =
            AvaloniaProperty.Register<PeriodFilter, DateTimeOffset?>(nameof(To));

        public PeriodFilter()
        {
            InitializeComponent();

            FromDatePicker.SelectedDateChanged += (_, _) => MergeDate(isFrom: true, FromDatePicker.SelectedDate);
            ToDatePicker.SelectedDateChanged += (_, _) => MergeDate(isFrom: false, ToDatePicker.SelectedDate);
            //FromTimePicker.SelectedTimeChanged += (_, _) => MergeTime(isFrom: true, FromTimePicker.SelectedTime);
            //ToTimePicker.SelectedTimeChanged += (_, _) => MergeTime(isFrom: false, ToTimePicker.SelectedTime);
        }

        public PeriodType SelectedPeriodType
        {
            get => GetValue(SelectedPeriodTypeProperty);
            set => SetValue(SelectedPeriodTypeProperty, value);
        }

        public Orientation Orientation
        {
            get => GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        public DateTimeOffset? From
        {
            get => GetValue(FromProperty);
            set => SetValue(FromProperty, value);
        }

        public DateTimeOffset? To
        {
            get => GetValue(ToProperty);
            set => SetValue(ToProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SelectedPeriodTypeProperty)
            {
                // Set From/To directly (not via the pickers) so preset end-of-day times like
                // 23:59:59.999 aren't discarded by MergeDate's "preserve existing time" logic,
                // which is meant for user-driven date-only edits, not programmatic full timestamps.
                var dates = UpdatePeriod((PeriodType)change.NewValue!, From, To);
                From = dates.from;
                To = dates.to;
            }
            //else if (change.Property == FromProperty)
            //{
            //    SyncPickers(FromDatePicker, FromTimePicker, From);
            //}
            //else if (change.Property == ToProperty)
            //{
            //    SyncPickers(ToDatePicker, ToTimePicker, To);
            //}
        }

        private void MergeDate(bool isFrom, DateTimeOffset? date)
        {
            var current = isFrom ? From : To;
            var time = current?.TimeOfDay ?? TimeSpan.Zero;
            var merged = date == null ? (DateTimeOffset?)null : new DateTimeOffset(date.Value.Date + time, date.Value.Offset);
            if (isFrom) From = merged; else To = merged;
        }

        private void MergeTime(bool isFrom, TimeSpan? time)
        {
            var current = isFrom ? From : To;
            if (current == null || time == null) return;
            var merged = new DateTimeOffset(current.Value.Date + time.Value, current.Value.Offset);
            if (isFrom) From = merged; else To = merged;
        }

        private static void SyncPickers(DatePicker datePicker, TimePicker timePicker, DateTimeOffset? value)
        {
            if (datePicker.SelectedDate != value)
            {
                datePicker.SelectedDate = value;
            }
            var time = value?.TimeOfDay;
            if (timePicker.SelectedTime != time)
            {
                timePicker.SelectedTime = time;
            }
        }

        private static (DateTimeOffset? from, DateTimeOffset? to) UpdatePeriod(PeriodType type, DateTimeOffset? currentFrom, DateTimeOffset? currentTo)
        {
            DateTime? from = currentFrom?.DateTime;
            DateTime? to = currentTo?.DateTime;
            switch (type)
            {
                case PeriodType.Custom:
                case PeriodType.AllTime:
                    {
                        from = default;
                        to = default;
                    }
                    break;
                case PeriodType.Today:
                    {
                        from = DateTime.Today;
                        to = DateTime.Today.AddDays(1).AddMilliseconds(-1);
                    }
                    break;
                case PeriodType.Yesterday:
                    {
                        from = DateTime.Today.AddDays(-1);
                        to = DateTime.Today.AddMilliseconds(-1);
                    }
                    break;
                case PeriodType.PreviousWeek:
                    {
                        DayOfWeek weekStart = DayOfWeek.Monday;
                        DateTime startingDate = DateTime.Today;

                        while (startingDate.DayOfWeek != weekStart)
                            startingDate = startingDate.AddDays(-1);

                        from = startingDate.AddDays(-7);
                        to = startingDate.AddMilliseconds(-1);
                    }
                    break;
                case PeriodType.PreviousMonth:
                    {
                        var today = DateTime.Today;

                        from = new DateTime(today.AddMonths(-1).Year, today.AddMonths(-1).Month, 1, 0, 0, 0, DateTimeKind.Local);
                        to = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Local).AddMilliseconds(-1);
                    }
                    break;
                case PeriodType.CurrentWeek:
                    {
                        DayOfWeek weekStart = DayOfWeek.Monday;
                        DateTime startingDate = DateTime.Today;

                        while (startingDate.DayOfWeek != weekStart)
                            startingDate = startingDate.AddDays(-1);

                        from = startingDate;
                        to = DateTime.Today.AddDays(1).AddMilliseconds(-1);
                    }
                    break;
                case PeriodType.CurrentMonth:
                    {
                        var today = DateTime.Today;

                        from = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Local);
                        to = new DateTime(today.AddMonths(1).Year, today.AddMonths(1).Month, 1, 0, 0, 0, DateTimeKind.Local).AddMilliseconds(-1);
                    }
                    break;
                case PeriodType.PreviousAndCurrentWeek:
                    {
                        DayOfWeek weekStart = DayOfWeek.Monday;
                        DateTime startingDate = DateTime.Today;

                        while (startingDate.DayOfWeek != weekStart)
                            startingDate = startingDate.AddDays(-1);

                        from = startingDate.AddDays(-7);
                        to = DateTime.Today.AddDays(1).AddMilliseconds(-1);
                    }
                    break;
                case PeriodType.PreviousAndCurrentMonth:
                    {
                        var today = DateTime.Today;

                        from = new DateTime(today.AddMonths(-1).Year, today.AddMonths(-1).Month, 1, 0, 0, 0, DateTimeKind.Local);
                        to = new DateTime(today.AddMonths(1).Year, today.AddMonths(1).Month, 1, 0, 0, 0, DateTimeKind.Local).AddMilliseconds(-1);
                    }
                    break;
                default:
                    break;
            }

            return (from == null ? null : new DateTimeOffset(from.Value), to == null ? null : new DateTimeOffset(to.Value));
        }
    }
}
