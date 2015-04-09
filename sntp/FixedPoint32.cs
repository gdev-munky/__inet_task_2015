using System.Collections.Generic;
using System.Linq;

namespace SNTP
{
    public struct FixedPoint32
    {
        private byte[] _data;

        public FixedPoint32(IEnumerable<byte> bts, int offset = 0)
        {
            _data = bts.Skip(offset).Take(4).ToArray();
        }
        public FixedPoint32(double value)
        {
            _data = new byte[4];
            var temp = (int) (value/1000.0*0x10000);
            _data[3] = (byte) (temp%256);
            temp = (temp - _data[3]) >> 8;
            _data[2] = (byte) (temp%256);
            temp = (temp - _data[2]) >> 8;
            _data[1] = (byte) (temp%256);
            temp = (temp - _data[1]) >> 8;
            _data[0] = (byte) (temp);
        }
        public double ToDouble()
        {
            var temp = 256*(256*(256*_data[0] + _data[1]) + _data[2]) + _data[3];
            return 1000*(((double) temp)/0x10000);
        }

        public static explicit operator byte[](FixedPoint32 v)
        {
            return v._data;
        }

        public static explicit operator FixedPoint32(double v)
        {
            return new FixedPoint32(v);
        }
    }
}
