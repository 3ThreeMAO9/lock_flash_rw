using System;
using System.Collections.Generic;
using System.IO;

namespace LockFlashRW
{
    internal class CRC16Helper
    {
        private const ushort POLYNOMIAL = 0x8005;

        private static ushort reflect(ushort data)
        {
            const byte bits = 16;
            uint reflection = 0x00000000;
            // Reflect the data about the center bit.
            for (byte bit = 0; bit < bits; bit++)
            {
                // If the LSB bit is set, set the reflection of it.
                if ((data & 0x01) != 0)
                {
                    reflection |= (uint)(1 << ((bits - 1) - bit));
                }
                data = (ushort) (data >> 1);
            }
            return (ushort)reflection;
        }

        private static ushort DoCompute(byte[] data, int start, int length,
            bool reflectIn, bool reflectOut,
            ushort polynomial, ushort xorIn, ushort xorOut,
            ushort msbMask, ushort mask)
        {
            ushort crc = xorIn;

            int j;
            byte c;
            uint bit;

            if (length == 0)
                return crc;

            for (int i = start; i < (start + length); i++)
            {
                c = data[i];

                if (reflectIn)
                    c = (byte)reflect(c);

                j = 0x80;

                while (j > 0)
                {
                    bit = (uint)(crc & msbMask);
                    crc <<= 1;

                    if ((c & j) != 0)
                    {
                        bit = (uint)(bit ^ msbMask);
                    }

                    if (bit != 0)
                    {
                        crc ^= polynomial;
                    }

                    j >>= 1;
                }
            }

            if (reflectOut)
                crc = (ushort)(uint)((reflect((ushort) crc) ^ xorOut) & mask);

            return crc;
        }

        public static ushort Compute(byte[] buf, int length)
        {
            ushort crc = DoCompute(buf, 0, length,
                false, false, POLYNOMIAL,
                0x0000, 0x0000, 0x8000, 0xFFFF);

            int t = crc >> 8;
            t |= (crc & 0xFF) << 8;
            return (ushort)t;
        }

        public static ushort Compute(List<byte> buf, int offset, int length)
        {
            byte[] tmp = new byte[length];
            buf.CopyTo(offset, tmp, 0, length);
            return Compute(tmp, length);
        }

        public static void Test()
        {
            byte[] buf = new byte[] { 0x00, 0x05, 0x01 };
            ushort crc = CRC16Helper.Compute(buf, buf.Length);
            Console.WriteLine("crc16 = {0:X4}", crc);

            byte[] tmp = new byte[5];
            MemoryStream stream = new MemoryStream(tmp);
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(buf);
            writer.Write(crc);
            writer.Close();
            stream.Close();

            Console.Write("tmp: ");
            foreach (byte b in tmp)
                Console.Write("{0:X2} ", b);
            Console.WriteLine();
        }
    }
}
