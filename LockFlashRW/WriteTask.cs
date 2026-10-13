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

                    string cs = string.Format("{0}.{1}", config.spi.cs.port, config.spi.cs.pin);
                    string clk = string.Format("{0}.{1}", config.spi.clk.port, config.spi.clk.pin);
                    string mosi = string.Format("{0}.{1}", config.spi.mosi.port, config.spi.mosi.pin);
                    string miso = string.Format("{0}.{1}", config.spi.miso.port, config.spi.miso.pin);
                    Console.WriteLine("开始烧写: 串口 {0}, 波特率{1}, cs[{2}] clk[{3}] mosi[{4}] miso[{5}], 文件 {6}, 共 {7} 字节",
                        config.uart.name, config.uart.baudrate, cs, clk, mosi, miso, config.flash.file, data.Length);
                    comport.Open();

                    logger.Info("设置SPI");
                    byte[] packet = SerialPacket.SpiSet(config.spi);
                    if (!SendPacket(packet, SerialPacket.Command.SPI_SET))
                    {
                        logger.Error("设置SPI失败");
                        return;
                    }

                    DateTime t0 = DateTime.Now;
                    int total = data.Length;
                    while (rest > 0)
                    {
                        int tx = rest > config.max ? config.max : rest;
                        packet = SerialPacket.FlashEraseAndWrite(data, addr, offset, (ushort)tx);
                        if (!SendPacket(packet, SerialPacket.Command.FLASH_ERASE_AND_WRITE))
                        {
                            logger.Error("写入失败, 偏移={0}", offset);
                            return;
                        }

                        addr += tx;
                        offset += tx;
                        rest -= tx;
                        ShowProgress(offset, total, t0);
                    }

                    ShowProgress(total, total, t0);
                    Console.WriteLine();
                    double sec = (DateTime.Now - t0).TotalSeconds;
                    string elapsed = TimeSpan.FromSeconds(sec).ToString(@"hh\:mm\:ss");
                    logger.Info("写入完成, 用时 {0}", elapsed);
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
            if (!config.dump)
                return;

            int total = prefix.Length + buf.Length * 3 + 1;
            StringBuilder builder = new StringBuilder(total);
            builder.Append(prefix);
            foreach (byte b in buf)
                builder.AppendFormat(" {0:X2}", b);
            logger.Debug(builder.ToString());
        }

        private void ShowProgress(int done, int total, DateTime t0)
        {
            int pct = total <= 0 ? 100 : (int)((long)done * 100 / total);
            double sec = (DateTime.Now - t0).TotalSeconds;
            const int width = 20;
            int filled = (int)((long)pct * width / 100);
            string bar = new string('█', filled) + new string('░', width - filled);
            string label = (done >= total) ? "烧写完成" : "进度";
            Console.Write("\r{0,-4} {1,3}% [{2}] 已用 {3}", label, pct, bar, TimeSpan.FromSeconds(sec).ToString(@"hh\:mm\:ss"));
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

                try
                {
                    if (r > 0)
                        rx.RemoveRange(0, r);
                }
                catch
                {
                    rx.Clear();
                }
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
