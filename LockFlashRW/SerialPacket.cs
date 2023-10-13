using System;
using System.Collections.Generic;
using System.IO;

namespace LockFlashRW
{
    internal class SerialPacket
    {
        public enum Command
        {
            FLASH_QUERY = 0x01,
            SPI_SET = 0x02,
            FLASH_ERASE = 0x03,
            FLASH_WRITE = 0x04,
            FLASH_READ = 0x05,
            FLASH_ERASE_AND_WRITE = 0x06,
            INVALID = 0xFF
        }

        public enum Result
        {
            OK = 0x00,
            FAIL = 0x01,
            CRC = 0x02,
            FRAME_SIZE = 0x03,
            OVERSIZE = 0x04,
            INVALID = 0xFF
        }

        private const byte HEADER = 0x7F;

        static private BinaryWriter GetWrite(Command cmd, int length)
        {
            int total = length + 6;
            MemoryStream stream = new MemoryStream(total);
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(HEADER);

            ushort size = (ushort)(length + 5);
            writer.Write(ByteSwap(size));
            writer.Write((byte)cmd);

            return writer;
        }

        static private byte[] GetBuffer(BinaryWriter writer)
        {
            MemoryStream stream = writer.BaseStream as MemoryStream;
            byte[] buffer = stream.GetBuffer();

            ushort crc = CRC16Helper.Compute(buffer, 1, buffer.Length - 3);
            writer.Write(ByteSwap(crc));

            writer.Close();
            stream.Close();

            return buffer;
        }

        static private ushort ByteSwap(ushort value)
        {
            byte[] b = BitConverter.GetBytes(value);
            byte[] r = new byte[b.Length];
            for (int i = 0; i < b.Length; i++)
                r[i] = b[b.Length - 1 - i];
            return BitConverter.ToUInt16(r, 0);
        }

        static private int ByteSwap(int value)
        {
            byte[] b = BitConverter.GetBytes(value);
            byte[] r = new byte[b.Length];
            for (int i = 0; i < b.Length; i++)
                r[i] = b[b.Length - 1 - i];
            return BitConverter.ToInt32(r, 0);
        }

        static private uint ByteSwap(uint value)
        {
            byte[] b = BitConverter.GetBytes(value);
            byte[] r = new byte[b.Length];
            for (int i = 0; i < b.Length; i++)
                r[i] = b[b.Length - 1 - i];
            return BitConverter.ToUInt32(r, 0);
        }

        static public byte[] FlashQuery()
        {
            BinaryWriter writer = GetWrite(Command.FLASH_QUERY, 0);
            return GetBuffer(writer);
        }

        static public byte[] SpiSet(SpiConfig config)
        {
            BinaryWriter writer = GetWrite(Command.SPI_SET, 8);
            writer.Write(config.cs.port);
            writer.Write(config.cs.pin);
            writer.Write(config.clk.port);
            writer.Write(config.clk.pin);
            writer.Write(config.mosi.port);
            writer.Write(config.mosi.pin);
            writer.Write(config.miso.port);
            writer.Write(config.miso.pin);
            return GetBuffer(writer);
        }

        static public byte[] FlashErase(byte type, uint addr)
        {
            if (type == 0 || type > 5)
                return null;

            BinaryWriter writer = GetWrite(Command.FLASH_ERASE, 5);
            writer.Write(type);
            writer.Write(ByteSwap(addr));
            return GetBuffer(writer);
        }

        static public byte[] FlashWrite(byte[] data, int addr, int offset, ushort length)
        {
            BinaryWriter writer = GetWrite(Command.FLASH_WRITE, 6 + length);
            writer.Write(ByteSwap(offset));
            writer.Write(ByteSwap(length));
            writer.Write(data, offset, length);
            return GetBuffer(writer);
        }

        static public byte[] FlashRead(uint addr, ushort length)
        {
            BinaryWriter writer = GetWrite(Command.FLASH_ERASE, 6);
            writer.Write(ByteSwap(addr));
            writer.Write(ByteSwap(length));
            return GetBuffer(writer);
        }

        static public byte[] FlashEraseAndWrite(byte[] data, int addr, int offset, ushort length)
        {
            BinaryWriter writer = GetWrite(Command.FLASH_ERASE_AND_WRITE, 6 + length);
            writer.Write(ByteSwap(addr));
            writer.Write(ByteSwap(length));
            writer.Write(data, offset, length);
            return GetBuffer(writer);
        }

        static public bool GetResult(byte[] packet, out byte cmd, out byte result)
        {
            if (packet.Length < 4)
            {
                cmd = 0;
                result = 0;
                return false;
            }

            cmd = packet[3];
            result = packet[4];
            return true;
        }

        static public int FindResultPacket(List<byte> list, out byte[] packet)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != HEADER)
                    continue;

                int length = list[i + 2] | (list[i + 1] << 8);
                if ((length + 1) > list.Count)
                    continue;

                ushort crc1 = CRC16Helper.Compute(list, i + 1, length - 2);
                ushort crc2 = (ushort)(list[i + length] | (list[i + length - 1] << 8));
                if (crc1 != crc2)
                    continue;

                packet = new byte[length + 1];
                list.CopyTo(i, packet, 0, packet.Length);
                return i + length + 1;
            }

            packet = null;
            return -1;
        }
    }
}
