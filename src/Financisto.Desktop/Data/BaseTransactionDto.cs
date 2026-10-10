using Prism.Mvvm;
using System;

namespace Financisto.Desktop.Data
{
    public abstract class BaseTransactionDto : BindableBase
    {
        protected DateTime date;
        protected DateTime time;
        protected int id;
        protected string note;
        protected string templateName;
        protected double rate;

        public DateTime Date
        {
            get => date;
            set
            {
                if (SetProperty(ref date, value))
                {
                    RaisePropertyChanged(nameof(Date));
                }
            }
        }

        public DateTime Time
        {
            get => time;
            set
            {
                if (SetProperty(ref time, value))
                {
                    RaisePropertyChanged(nameof(Time));
                }
            }
        }

        public DateTime DateTime
        {
            get { return new DateTime(date.Year, date.Month, date.Day, time.Hour, time.Minute, time.Second, DateTimeKind.Local); }
        }

        public int Id
        {
            get => id;
            set
            {
                if (SetProperty(ref id, value))
                {
                    RaisePropertyChanged(nameof(Id));
                }
            }
        }

        public string Note
        {
            get => note;
            set
            {
                if (SetProperty(ref note, value))
                {
                    RaisePropertyChanged(nameof(Note));
                }
            }
        }

        public bool IsSubTransaction { get; set; }

        /// <summary>The dialog edits a template (<c>is_template = 1</c>): it asks for a name and keeps no date of its own.</summary>
        public bool IsTemplate { get; set; }

        public string TemplateName
        {
            get => templateName;
            set
            {
                if (SetProperty(ref templateName, value))
                {
                    RaisePropertyChanged(nameof(TemplateName));
                }
            }
        }

        /// <summary>Android disables the date and time of a template; the part of a split has the parent's.</summary>
        public bool IsDateTimeEditable => !IsSubTransaction && !IsTemplate;

        public virtual long RealFromAmount { get; }

        public virtual string SubTransactionTitle { get; }

        public virtual bool IsAmountNegative { get; set; }

        public double Rate
        {
            get => rate;
            set
            {
                if (SetProperty(ref rate, value))
                {
                    RaisePropertyChanged(nameof(Rate));
                    RaisePropertyChanged("RateString");
                }
            }
        }
    }
}
