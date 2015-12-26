using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapleStoryPacketAnalyzer
{
    class BitTools
    {
        public static byte[] StringToBytes(string s)
        {
            string[] strArray = s.Split(' ');
            byte[] buffer = new byte[strArray.Length];
            for(int i=0 ;i<strArray.Length;i++)
            {
                buffer[i] = Convert.ToByte( strArray[i],16);
            }
            return buffer;
        }
        public static string GetHexString (byte[] b)
        {
            return BitConverter.ToString(b).Replace("-", " ");
        }
        public static string GetHexStringWithTrim(byte[] b)
        {
            string s = "0x";
            s= string.Concat(s, BitConverter.ToString(b).Replace("-", ",0x"));
            return s;
        }
        public static int MoveByte(int value, int pos)
        {
            if (value < 0)
            {
                string s = Convert.ToString(value, 2);    // 转换为二进制
                for (int i = 0; i < pos; i++)
                {
                    s = "0" + s.Substring(0, 31);
                }
                return Convert.ToInt32(s, 2);            // 将二进制数字转换为数字
            }
            else
            {
                return value >> pos;
            }
        }
        public static long MoveByte(long value, int pos)
        {
            if (value < 0)
            {
                string s = Convert.ToString(value, 2);    // 转换为二进制
                for (int i = 0; i < pos; i++)
                {
                    s = "0" + s.Substring(0, 31);
                }
                return Convert.ToInt64(s, 2);            // 将二进制数字转换为数字
            }
            else
            {
                return value >> pos;
            }
        }
    }
}
