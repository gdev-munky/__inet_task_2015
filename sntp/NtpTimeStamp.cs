using System;
using System.Collections.Generic;
using System.Linq;

namespace SNTP
{
    public struct NtpTimeStamp
    {
        private byte[] _data;

        public NtpTimeStamp(IEnumerable<byte> bts, int offset = 0)
        {
            _data = bts.Skip(offset).Take(8).ToArray();
        }
        public NtpTimeStamp(DateTime time)
        {
            _data = new byte[8];
            var utcTime = (time.ToUniversalTime() - new DateTime(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));
            var seconds = utcTime.TotalSeconds;
            var integral = (uint) Math.Floor(seconds);
            var fraction = (seconds - integral)*(UInt32.MaxValue + 1L);
            var bts = BitConverter.GetBytes(integral);
            var bts2 = BitConverter.GetBytes((uint)fraction);
            for (var i = 0; i < 4; ++i)
            {
                _data[i] = bts[3-i];
                _data[i+4] = bts2[3-i];
            }
        }
        public DateTime GetTime()
        {
            var time = ComputeDate(GetMilliSeconds());
            var offspan = TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now);
            return time + offspan;
        }
        private static DateTime ComputeDate(double milliseconds)
        {
            var span = TimeSpan.FromMilliseconds((double)milliseconds);
            var time = new DateTime(1900, 1, 1);
            time += span;
            return time;
        }
        private ulong GetMilliSeconds()
        {
            ulong intpart = 0, fractpart = 0;

            for (int i = 0; i <= 3; i++)
                intpart = 256 * intpart + _data[i];
            
            for (int i = 4; i <= 7; i++)
                fractpart = 256 * fractpart + _data[i];
            
            var milliseconds = intpart * 1000 + (fractpart * 1000) / 0x100000000L;
            return milliseconds;
        }

        public static explicit operator byte[](NtpTimeStamp v)
        {
            return v._data;
        }

        public static explicit operator NtpTimeStamp(DateTime v)
        {
            return new NtpTimeStamp(v);
        }
    }
}
