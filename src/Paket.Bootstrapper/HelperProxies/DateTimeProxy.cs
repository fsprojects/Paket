using System;

namespace Paket.Bootstrapper
{
    public static class DateTimeProxy
    {
        public static Func<DateTime> GetNow 
        { 
            get { return _getNow ?? (_getNow = () => DateTime.Now); }
            set { _getNow = value;}
        }

        private static Func<DateTime> _getNow;

        public static DateTime Now { get { return GetNow(); } }
    }
}

