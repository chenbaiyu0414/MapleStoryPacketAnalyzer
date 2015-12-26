using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MapleStoryPacketAnalyzer
{
    class MapleAES : IDisposable
    {

        private byte[] iv;
        private RijndaelManaged cipher = new RijndaelManaged();
        private ICryptoTransform cipherTransform = null;
        private ushort mapleVersion;


        private readonly static byte[] sSecretKey = new byte[] {
            0x76,0x00, 0x00, 0x00,0xC9,0x00, 0x00, 0x00,0x49,0x00, 0x00, 0x00,0x08,0x00, 0x00, 0x00,
            0xB1,0x00, 0x00, 0x00,0x11,0x00, 0x00, 0x00,0xD6,0x00, 0x00, 0x00,0x97,0x00, 0x00, 0x00
        };

        private readonly static byte[] funnyBytes = new byte[]{
            0xEC, 0x3F, 0x77, 0xA4, 0x45, 0xD0, 0x71, 0xBF, 0xB7, 0x98, 0x20, 0xFC, 0x4B, 0xE9, 0xB3, 0xE1,
            0x5C, 0x22, 0xF7, 0x0C, 0x44, 0x1B, 0x81, 0xBD, 0x63, 0x8D, 0xD4, 0xC3, 0xF2, 0x10, 0x19, 0xE0,
            0xFB, 0xA1, 0x6E, 0x66, 0xEA, 0xAE, 0xD6, 0xCE, 0x06, 0x18, 0x4E, 0xEB, 0x78, 0x95, 0xDB, 0xBA,
            0xB6, 0x42, 0x7A, 0x2A, 0x83, 0x0B, 0x54, 0x67, 0x6D, 0xE8, 0x65, 0xE7, 0x2F, 0x07, 0xF3, 0xAA,
            0x27, 0x7B, 0x85, 0xB0, 0x26, 0xFD, 0x8B, 0xA9, 0xFA, 0xBE, 0xA8, 0xD7, 0xCB, 0xCC, 0x92, 0xDA,
            0xF9, 0x93, 0x60, 0x2D, 0xDD, 0xD2, 0xA2, 0x9B, 0x39, 0x5F, 0x82, 0x21, 0x4C, 0x69, 0xF8, 0x31,
            0x87, 0xEE, 0x8E, 0xAD, 0x8C, 0x6A, 0xBC, 0xB5, 0x6B, 0x59, 0x13, 0xF1, 0x04, 0x00, 0xF6, 0x5A,
            0x35, 0x79, 0x48, 0x8F, 0x15, 0xCD, 0x97, 0x57, 0x12, 0x3E, 0x37, 0xFF, 0x9D, 0x4F, 0x51, 0xF5,
            0xA3, 0x70, 0xBB, 0x14, 0x75, 0xC2, 0xB8, 0x72, 0xC0, 0xED, 0x7D, 0x68, 0xC9, 0x2E, 0x0D, 0x62,
            0x46, 0x17, 0x11, 0x4D, 0x6C, 0xC4, 0x7E, 0x53, 0xC1, 0x25, 0xC7, 0x9A, 0x1C, 0x88, 0x58, 0x2C,
            0x89, 0xDC, 0x02, 0x64, 0x40, 0x01, 0x5D, 0x38, 0xA5, 0xE2, 0xAF, 0x55, 0xD5, 0xEF, 0x1A, 0x7C,
            0xA7, 0x5B, 0xA6, 0x6F, 0x86, 0x9F, 0x73, 0xE6, 0x0A, 0xDE, 0x2B, 0x99, 0x4A, 0x47, 0x9C, 0xDF,
            0x09, 0x76, 0x9E, 0x30, 0x0E, 0xE4, 0xB2, 0x94, 0xA0, 0x3B, 0x34, 0x1D, 0x28, 0x0F, 0x36, 0xE3,
            0x23, 0xB4, 0x03, 0xD8, 0x90, 0xC8, 0x3C, 0xFE, 0x5E, 0x32, 0x24, 0x50, 0x1F, 0x3A, 0x43, 0x8A,
            0x96, 0x41, 0x74, 0xAC, 0x52, 0x33, 0xF0, 0xD9, 0x29, 0x80, 0xB1, 0x16, 0xD3, 0xAB, 0x91, 0xB9,
            0x84, 0x7F, 0x61, 0x1E, 0xCF, 0xC5, 0xD1, 0x56, 0x3D, 0xCA, 0xF4, 0x05, 0xC6, 0xE5, 0x08, 0x49
        };

        public MapleAES(byte[] iv, ushort mapleVersion)
        {

            cipher.Key = sSecretKey;
            cipher.Mode = CipherMode.ECB;
            //cipher.Padding = PaddingMode.PKCS7;
            cipherTransform = cipher.CreateEncryptor();

            this.setIv(iv);
            this.mapleVersion = (ushort)(((mapleVersion >> 8) & 0xFF) | ((mapleVersion << 8) & 0xFF00));
        }

        private void setIv(byte[] iv)
        {
            this.iv = iv;
        }

        private static byte[] multiplyBytes(byte[] newIn, int count, int mul)
        {
            byte[] ret = new byte[count * mul];
            for (int x = 0; x < count * mul; x++)
            {
                ret[x] = newIn[x % count];
            }
            return ret;
        }

        public byte[] crypt(byte[] data)
        {
            byte[] morphKey = new byte[16];
            int remaining = data.Length;
            int start = 0;
            int length = 0x5B0;

            while (remaining > 0)
            {
                for (int i = 0; i < 16; i++)
                    morphKey[i] = iv[i % 4];

                if (remaining < length)
                    length = remaining;

                for (int index = start; index < (start + length); index++)
                {
                    if ((index - start) % 16 == 0)
                        cipherTransform.TransformBlock(morphKey, 0, 16, morphKey, 0);

                    data[index] ^= morphKey[(index - start) % 16];
                }

                start += length;
                remaining -= length;
                length = 0x5B4;
            }
            updateIv();
            return data;
        }

        public void updateIv()
        {
            this.iv = getNewIv(this.iv);
        }
        public byte[] getIv()
        {
            return this.iv;
        }
        public byte[] getPacketHeader(int length)
        {
            int iiv = (iv[3]) & 0xFF;
            iiv |= (iv[2] << 8) & 0xFF00;
            iiv ^= mapleVersion;
            int mlength = ((length << 8) & 0xFF00) | BitTools.MoveByte(length, 8);
            int xoredIv = iiv ^ mlength;
            byte[] ret = new byte[4];
            ret[0] = (byte)(BitTools.MoveByte(iiv, 8) & 0xFF);
            ret[1] = (byte)(iiv & 0xFF);
            ret[2] = (byte)(BitTools.MoveByte(xoredIv, 8) & 0xFF);
            ret[3] = (byte)(xoredIv & 0xFF);
            return ret;
        }

        //public static int getPacketLength(int packetHeader)
        //{
        //    int packetLength = (BitTool.MoveByte(packetHeader, 16) ^ (packetHeader & 0xFFFF));
        //    packetLength = ((packetLength << 8) & 0xFF00) | (BitTool.MoveByte(packetLength, 8) & 0xFF);
        //    return packetLength;
        //}

        public static int getPacketLength(byte[] packetHeader)
        {
            if (packetHeader.Length < 4)
            {
                return -1;
            }
            return ((packetHeader[0] ^ packetHeader[2]) & 0xFF) | (((packetHeader[1] ^ packetHeader[3]) << 8) & 0xFF00);
        }

        public bool checkPacket(byte[] packet)
        {
            return ((((packet[0] ^ iv[2]) & 0xFF) == ((mapleVersion >> 8) & 0xFF)) && (((packet[1] ^ iv[3]) & 0xFF) == (mapleVersion & 0xFF)));
        }

        public bool checkPacket(int packetHeader)
        {
            byte[] packetHeaderBuf = new byte[2];
            packetHeaderBuf[0] = (byte)((packetHeader >> 24) & 0xFF);
            packetHeaderBuf[1] = (byte)((packetHeader >> 16) & 0xFF);
            return checkPacket(packetHeaderBuf);
        }

        public static byte[] getNewIv(byte[] oldIv)
        {
            byte[] newIn = { 0xF2, 0x53, 0x50, 0xC6 };
            for (int x = 0; x < 4; x++)
            {
                funnyShit(oldIv[x], newIn);
            }
            return newIn;
        }
        public override string ToString()
        {
            return "IV: " + BitTools.GetHexString(this.iv);
        }

        private static byte[] funnyShit(byte inputByte, byte[] newIn)
        {
            byte elina = newIn[1];
            byte anna = inputByte;
            byte moritz = funnyBytes[elina & 0xFF];
            moritz -= inputByte;
            newIn[0] += moritz;
            moritz = newIn[2];
            moritz ^= funnyBytes[anna & 0xFF];
            elina -= (byte)(moritz & 0xFF);
            newIn[1] = elina;
            elina = newIn[3];
            moritz = elina;
            elina -= (byte)(newIn[0] & 0xFF);
            moritz = funnyBytes[moritz & 0xFF];
            moritz += inputByte;
            moritz ^= newIn[2];
            newIn[2] = moritz;
            elina += (byte)(funnyBytes[anna & 0xFF] & 0xFF);
            newIn[3] = elina;

            long merry = (newIn[0]) & 0xFF;
            merry |= Convert.ToInt64((newIn[1] << 8) & 0xFF00);
            merry |= Convert.ToInt64((newIn[2] << 16) & 0xFF0000);
            merry |= (newIn[3] << 24) & 0xFF000000;
            long ret_value = merry;
            ret_value = BitTools.MoveByte(ret_value, 0x1D);
            merry = merry << 3;
            ret_value = ret_value | merry;

            newIn[0] = (byte)(ret_value & 0xFF);
            newIn[1] = (byte)((ret_value >> 8) & 0xFF);
            newIn[2] = (byte)((ret_value >> 16) & 0xFF);
            newIn[3] = (byte)((ret_value >> 24) & 0xFF);
            return newIn;
        }

        public void Dispose()
        {
            cipher.Dispose();
            cipherTransform.Dispose();
        }
    }
}
