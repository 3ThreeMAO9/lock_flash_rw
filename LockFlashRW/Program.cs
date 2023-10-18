using NLog;
using NLog.Targets.Wrappers;
using System;
using System.IO;
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
            ConfigNLog();

            ConfigFile config = ReadConfig(args);
            if (config == null)
            {
                Console.WriteLine("按任意键退出");
                Console.ReadKey();
                return;
            }

            while (true)
            {
                Run(config);
                Thread.Sleep(200);

                Console.WriteLine();
                Console.WriteLine("==== 按空格键重新开始，按其他键退出 ====");
                ConsoleKeyInfo key = Console.ReadKey();
                if (key.Key != ConsoleKey.Spacebar)
                    break;
            }
        }

        static void ConfigNLog()
        {
            var date = DateTime.Now.ToLocalTime();

            var config = new NLog.Config.LoggingConfiguration();

            // Targets where to log to: File and DebugOutput
            var logfile = new NLog.Targets.FileTarget("logfile")
            {
                FileName = string.Format("log_{0}.txt",
                    date.ToString("yyyyMMdd_HHmmss"))
            };
            var logdebug = new NLog.Targets.DebugSystemTarget();
            var logconsole = new NLog.Targets.ConsoleTarget();

            // Rules for mapping loggers to targets
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, new AsyncTargetWrapper(logdebug));
            config.AddRule(LogLevel.Info, LogLevel.Fatal, new AsyncTargetWrapper(logfile));
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, new AsyncTargetWrapper(logconsole));

            // Apply config           
            LogManager.Configuration = config;
        }
    }
}
