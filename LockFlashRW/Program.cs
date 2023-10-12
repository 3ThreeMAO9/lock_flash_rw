using NLog;
using System;
using System.IO;
using System.Threading.Tasks;

namespace LockFlashRW
{
    internal class Program
    {
        static void Run(string[] args)
        {
            string configPath;
            if (args.Length > 0)
                configPath = args[0];
            else
                configPath = "config.json";

            if (!File.Exists(configPath))
            {
                Console.WriteLine("配置文件 {0} 不存在", configPath);
                return;
            }

            ConfigFile config = new ConfigFile(configPath);
            string r = config.Check();
            if (r != "")
            {
                Console.WriteLine(r);
                return;
            }

            Logger logger = LogManager.GetCurrentClassLogger();
            logger.Info("读取配置文件 {0} 成功", configPath);

            if (!File.Exists(config.flash.file))
            {
                Console.WriteLine("数据文件 {0} 不存在", config.flash.file);
                return;
            }

            WriteTask wr = new WriteTask(config);
            Task t = wr.Run();
            t.Wait();
        }

        static void Main(string[] args)
        {
            ConfigNLog();
            Run(args);

            Console.WriteLine("按任意键退出");
            Console.ReadKey();
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
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, logdebug);
            config.AddRule(LogLevel.Info, LogLevel.Fatal, logfile);
            config.AddRule(LogLevel.Info, LogLevel.Fatal, logconsole);

            // Apply config           
            LogManager.Configuration = config;
        }
    }
}
