using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DnsCache
{
    public static class BEBitConverter
    {
        public static long DoubleToInt64Bits(double value) { throw new NotImplementedException(); }
        
        public static byte[] GetBytes(bool value)
        {
            return BitConverter.GetBytes(value).Reverse().ToArray();
        }

        public static byte[] GetBytes(char value)
        {
            return BitConverter.GetBytes(value).Reverse().ToArray();
        }

        public static byte[] GetBytes(double value)
        {
            return BitConverter.GetBytes(value).Reverse().ToArray();
        }

        public static byte[] GetBytes(float value)
        {
            return BitConverter.GetBytes(value).Reverse().ToArray();
        }

        public static byte[] GetBytes(int value)
        {
            return BitConverter.GetBytes(value).Reverse().ToArray();
        }

        public static byte[] GetBytes(long value)
        {
            return BitConverter.GetBytes(value).Reverse().ToArray();
        }

        public static byte[] GetBytes(short value)
        {
            return BitConverter.GetBytes(value).Reverse().ToArray();
        }

        [CLSCompliant(false)]
        public static byte[] GetBytes(uint value)
        {
            return BitConverter.GetBytes(value).Reverse().ToArray();
        }
        
        [CLSCompliant(false)]
        public static byte[] GetBytes(ulong value)
        {
            return BitConverter.GetBytes(value).Reverse().ToArray();
        }

        public static byte[] GetBytes(ushort value)
        {
            return BitConverter.GetBytes(value).Reverse().ToArray();
        }

        public static short ToInt16(byte[] value, int startIndex)
        {
            short result;
            unsafe
            {
                var pr = (byte*) &result;
                pr[0] = value[startIndex + 1];
                pr[1] = value[startIndex];
            }
            return result;
    ;    } 

        public static int ToInt32(byte[] value, int startIndex)
        {
            int result;
            unsafe
            {
                var pr = (byte*)&result;
                pr[0] = value[startIndex + 3];
                pr[1] = value[startIndex + 2];
                pr[2] = value[startIndex + 1];
                pr[3] = value[startIndex];
            }
            return result;
        }
        
        public static long ToInt64(byte[] value, int startIndex)
        {
            long result;
            unsafe
            {
                var pr = (byte*)&result;
                pr[0] = value[startIndex + 7];
                pr[1] = value[startIndex + 6];
                pr[2] = value[startIndex + 5];
                pr[3] = value[startIndex + 4];
                pr[4] = value[startIndex + 3];
                pr[5] = value[startIndex + 2];
                pr[6] = value[startIndex + 1];
                pr[7] = value[startIndex];
            }
            return result;
        }
        
        public static float ToSingle(byte[] value, int startIndex)
        {
            float result;
            unsafe
            {
                var pr = (byte*)&result;
                pr[0] = value[startIndex + 3];
                pr[1] = value[startIndex + 2];
                pr[2] = value[startIndex + 1];
                pr[3] = value[startIndex];
            }
            return result;
        }

        public static string ToString(byte[] value)
        {
            return System.BitConverter.ToString(value.Reverse().ToArray());
        }
        
        public static string ToString(byte[] value, int startIndex)
        {
            return System.BitConverter.ToString(value.Reverse().ToArray(), startIndex);
        }

        public static string ToString(byte[] value, int startIndex, int length)
        {
            return System.BitConverter.ToString(value.Reverse().ToArray(), startIndex, length);
        }

        public static ushort ToUInt16(byte[] value, int startIndex)
        {
            ushort result;
            unsafe
            {
                var pr = (byte*)&result;
                pr[0] = value[startIndex + 1];
                pr[1] = value[startIndex];
            }
            return result;
        }
        
        public static uint ToUInt32(byte[] value, int startIndex)
        {
            uint result;
            unsafe
            {
                var pr = (byte*)&result;
                pr[0] = value[startIndex + 3];
                pr[1] = value[startIndex + 2];
                pr[2] = value[startIndex + 1];
                pr[3] = value[startIndex];
            }
            return result;
        }
       
        public static ulong ToUInt64(byte[] value, int startIndex)
        {
            ulong result;
            unsafe
            {
                var pr = (byte*)&result;
                pr[0] = value[startIndex + 7];
                pr[1] = value[startIndex + 6];
                pr[2] = value[startIndex + 5];
                pr[3] = value[startIndex + 4];
                pr[4] = value[startIndex + 3];
                pr[5] = value[startIndex + 2];
                pr[6] = value[startIndex + 1];
                pr[7] = value[startIndex];
            }
            return result;
        }
    }
}
