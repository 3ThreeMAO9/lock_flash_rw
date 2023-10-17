using Newtonsoft.Json;
using System.IO;

namespace LockFlashRW
{
    public class SpiPort
    {
        private const byte PORT_MAX_NO = 3;
        private const byte PIN_MAX_NO = 15;

        public byte port { get; set; }
        public byte pin { get; set; }

        public bool Check()
        {
            return (port < PORT_MAX_NO) && (pin < PIN_MAX_NO);
        }
    }

    public class SpiConfig
    {
        public SpiPort cs { get; set; }
        public SpiPort clk { get; set; }
        public SpiPort mosi { get; set; }
        public SpiPort miso { get; set; }

        public string Check()
        {
            if (cs == null || !cs.Check())
                return "spi.cs 配置错误";
            else if (clk == null || !clk.Check())
                return "spi.clk 配置错误";
            else if (mosi == null || !mosi.Check())
                return "spi.mosi 配置错误";
            else if (miso == null || !miso.Check())
                return "spi.miso 配置错误";
            else
                return "";
        }
    }

    public class UartConfig
    {
        public string name { get; set; }
        public int baudrate { get; set; }

        public string Check()
        {
            if (name == null || name.Length == 0)
                return "uart.name 配置错误";
            else if (baudrate <= 0 || baudrate > 2000000)
                return "uart.baudrate 配置错误";
            else
                return "";
        }
    }

    public class FlashConfig
    {
        public string command { get; set; }
        public string file { get; set; }
        public int address { get; set; }

        public string Check()
        {
            if (command == null || command.Length == 0)
                return "flash.command 配置错误";
            else if (file == null || file.Length == 0 || !File.Exists(file))
                return "flash.file 配置错误";
            else if (address < 0)
                return "flash.address 配置错误";
            else
                return "";
        }
    }

    internal class FileConfig
    {
        public SpiConfig spi { get; set; }
        public UartConfig uart { get; set; }
        public FlashConfig flash { get; set; }
        public uint retry { get; set; } = 100;
        public uint max { get; set; } = 512;
        public bool dump { get; set; } = false;
    }

    internal class ConfigFile
    {
        public SpiConfig spi { get; }
        public UartConfig uart { get; }
        public FlashConfig flash { get; }
        public int retry { get; }
        public int max { get; }
        public bool dump { get; }

        public ConfigFile(string path)
        {
            string text = File.ReadAllText(path);
            FileConfig config = JsonConvert.DeserializeObject<FileConfig>(text);
            if (config != null)
            {
                if (config.spi != null)
                    this.spi = config.spi;
                if (config.uart != null)
                    this.uart = config.uart;
                if (config.flash != null)
                    this.flash = config.flash;
                this.retry = (int)config.retry;
                this.max = (int)config.max;
                this.dump = config.dump;
            }
        }

        public string Check()
        {
            if (this.spi == null || this.uart == null || this.flash == null)
                return "无效的配置文件";
            
            string r = this.spi.Check();
            if (r != "")
                return r;

            r = this.uart.Check();
            if (r != "")
                return r;

            r = this.flash.Check();
            if (r != "")
                return r;

            return "";
        }
    }
}
