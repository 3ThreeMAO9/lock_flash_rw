using NLog;
using NLog.Targets.Wrappers;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LockFlashRW
{
    internal class Program
    {
        static void Run(ConfigFile config)
        {
            if (config.flash.command.Trim() != "write")
            {
                Console.WriteLine("目前只支持flash.command: \"write\"");
                return;
            }

            if (!File.Exists(config.flash.file))
            {
                Console.WriteLine("数据文件 {0} 不存在", config.flash.file);
                return;
            }

            WriteTask wr = new WriteTask(config);
            Task t = wr.Run();
            t.Wait();
        }

        static ConfigFile ReadConfig(string[] args)
        {
            string configPath;
            if (args.Length > 0)
                configPath = args[0];
            else
                configPath = "config.json";

            if (!File.Exists(configPath))
            {
                Console.WriteLine("配置文件 {0} 不存在", configPath);
                return null;
            }

            ConfigFile config = new ConfigFile(configPath);
            string r = config.Check();
            if (r != "")
            {
                Console.WriteLine(r);
                return null;
            }

            Logger logger = LogManager.GetCurrentClassLogger();
            logger.Info("读取配置文件 {0} 成功", configPath);

            return config;
        }

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            ConfigNLog();

            ConfigFile config = ReadConfig(args);
            if (config == null)
            {
                Console.WriteLine("按任意键退出");
                Console.ReadKey();
                return;
            }

            Run(config);

            Console.WriteLine("按任意键退出");
            Console.ReadKey();
        }

        static void ConfigNLog()
        {
            var date = DateTime.Now.ToLocalTime();

            var config = new NLog.Config.LoggingConfiguration();

            // Targets where to log to: File and DebugOutput
            string logDir = "logs";
            Directory.CreateDirectory(logDir);
            var logfile = new NLog.Targets.FileTarget("logfile")
            {
                FileName = string.Format("{0}\\log_{1}.txt",
                    logDir, date.ToString("yyyyMMdd_HHmmss"))
            };
            var logdebug = new NLog.Targets.DebugSystemTarget();
            var logconsole = new NLog.Targets.ConsoleTarget();

            // Rules for mapping loggers to targets
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, new AsyncTargetWrapper(logdebug));
            config.AddRule(LogLevel.Info, LogLevel.Fatal, new AsyncTargetWrapper(logfile));
            config.AddRule(LogLevel.Error, LogLevel.Fatal, new AsyncTargetWrapper(logconsole));

            // Apply config           
            LogManager.Configuration = config;
        }
    }
}
