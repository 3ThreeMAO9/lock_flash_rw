using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LockFlashRW
{
    internal class WriteTask
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private readonly ConfigFile config;
        private readonly Task task;

        private readonly SerialPort comport;
        private readonly List<byte> rx;

        private byte waitingCommand = (byte)SerialPacket.Command.INVALID;
        private readonly EventWaitHandle eventWaitHandle;

        public WriteTask(ConfigFile cfg)
        {
            this.config = cfg;
            logger.Info("准备在地址 {0} 写入文件 {1}",
                config.flash.address, config.flash.file);

            rx = new List<byte>();
            eventWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            
            comport = new SerialPort
            {
                PortName = config.uart.name,
                BaudRate = config.uart.baudrate,
                DataBits = 8,
                StopBits = StopBits.One,
                Parity = Parity.None,
                Handshake = Handshake.None
            };
            comport.DataReceived += OnPortDataReceived;

            Action action = new Action(delegate 
            {
                try
                {
                    byte[] data = File.ReadAllBytes(config.flash.file);
                    int addr = config.flash.address;
                    int offset = 0;
                    int rest = data.Length;

                    Console.WriteLine("打开串口{0}", config.uart.name);
                    comport.Open();

                    logger.Info("设置SPI");
                    byte[] packet = SerialPacket.SpiSet(config.spi);
                    if (!SendPacket(packet, SerialPacket.Command.SPI_SET))
                    {
                        logger.Error("设置SPI失败");
                        return;
                    }

                    while (rest > 0)
                    {
                        int tx = rest > 512 ? 512 : rest;
                        logger.Info("写入数据, 偏移={0}, 长度={1}", offset, tx);
                        packet = SerialPacket.FlashEraseAndWrite(data, addr, offset, (ushort)tx);
                        if (!SendPacket(packet, SerialPacket.Command.FLASH_ERASE_AND_WRITE))
                        {
                            logger.Error("写入失败");
                            return;
                        }

                        addr += tx;
                        offset += tx;
                        rest -= tx;
                    }

                    logger.Info("写入完成");
                }
                catch (Exception ex)
                {
                    logger.Error("出错！！！");
                    logger.Error(ex);
                }
                finally
                {
                    comport.Close();
                    comport.Dispose();
                    eventWaitHandle.Dispose();
                }
            });

            task = new Task(action);
        }

        private void DumpBytes(string prefix, byte[] buf)
        {
            int total = prefix.Length + buf.Length * 3 + 1;
            StringBuilder builder = new StringBuilder(total);
            builder.Append(prefix);
            foreach (byte b in buf)
                builder.AppendFormat(" {0:X2}", b);
            logger.Debug(builder.ToString());
        }

        private bool SendPacket(byte[] packet, SerialPacket.Command cmd)
        {
            waitingCommand = (byte)cmd;
            eventWaitHandle.Reset();
            rx.Clear();

            int tx = 3;
            while (tx-- > 0)
            {
                DumpBytes("发送:", packet);
                comport.Write(packet, 0, packet.Length);
                if (eventWaitHandle.WaitOne(config.retry))
                {
                    waitingCommand = (byte)SerialPacket.Command.INVALID;
                    return true;
                }
            }

            waitingCommand = (byte)SerialPacket.Command.INVALID;
            return false;
        }

        private void OnPortDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            int rd = comport.BytesToRead;
            byte[] val = new byte[rd];
            comport.Read(val, 0, rd);
            if (waitingCommand == (byte)SerialPacket.Command.INVALID)
                return;

            rx.AddRange(val);

            int r;
            bool quit = false;
            while (!quit)
            {
                r = SerialPacket.FindResultPacket(rx, out byte[] packet);
                if (r == -1)
                    break;

                DumpBytes("接收:", packet);
                if (SerialPacket.GetResult(packet, out byte cmd, out byte result))
                {
                    logger.Info("接收 cmd=0x{0:X}, result=0x{1:X}", cmd, result);
                    if (cmd == waitingCommand && result == (byte)SerialPacket.Result.OK)
                    {
                        waitingCommand = (byte)SerialPacket.Command.INVALID;
                        eventWaitHandle.Set();
                        quit = true;
                    }
                }

                if (r > 0)
                    rx.RemoveRange(0, r);
            }
        }

        public Task Run()
        {
            if (task.Status == TaskStatus.Created)
                task.Start();

            return task;
        }
    }
}
