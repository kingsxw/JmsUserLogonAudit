using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace JmsUserLogonAudit
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information() // 设置最低日志级别为Information
                .WriteTo.Console(
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] {Message}{NewLine}{Exception}")
                .WriteTo.File(
                    path: "log/.log", // 日志文件路径
                    rollingInterval: RollingInterval.Day, // 按天滚动日志文件
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] {Message}{NewLine}{Exception}",
                    retainedFileCountLimit: 31,
                    restrictedToMinimumLevel: LogEventLevel.Information) // 最低记录级别

                .CreateLogger();
            
#if DEBUG
            var configFile = "config.dev.json";
            var runningMode = "Debug";
#else
            var configFile = "config.json";
            var runningMode = "Release";
#endif
            List<JmsServer> jmsServer = new List<JmsServer>();
            SmbServer smbServer = new SmbServer();
            BackupOption backupOption = new BackupOption();

            if (File.Exists(configFile))
            {
                Log.Information($"当前运行在{runningMode}模式，载入配置文件\"{configFile}\"");
                var fileString = File.ReadAllText(configFile);
                var json = JsonNode.Parse(fileString);
                var server = json["JmsServer"].AsArray();
                if (server.Count == 0)
                {
                    //Base.PrintColoredString("需要配置至少一个Jumpserver服务器参数!!!", ConsoleColor.Red);
                    Log.Error("需要配置至少一个Jumpserver服务器参数!!!终止执行...");
                    Environment.Exit(1);
                }
                else
                {
                    jmsServer = json["JmsServer"].Deserialize<List<JmsServer>>();
                    Log.Information("载入Jumpserver配置：{@JmsServer} ", jmsServer);
                }

                var smb = json["SmbServer"];
                smbServer = smb.Deserialize<SmbServer>();
                Log.Information("载入SMB共享配置：{@SmbServer} ", smbServer);

                var option = json["BackupOption"];
                backupOption = option.Deserialize<BackupOption>();
                Log.Information("载入备份配置：{@BackupOption} ", backupOption);

            }
            else
            {
                //Base.PrintColoredString("配置文件不存在!!!", ConsoleColor.Red);
                Log.Error($"配置文件\"{configFile}\"不存在!!!终止执行...");
                Environment.Exit(2);
            }

            var backup = new Backup(jmsServer, smbServer, backupOption);
            Log.Information("开始本次导出...");
            await backup.ExportAuditFile();
            Log.Information("本次导出结束...");

            //var value = "hello Smb";
            //using var ms = new MemoryStream(Encoding.UTF8.GetBytes(value));

            //using SmbClient client = new SmbClient("192.168.11.8", "sumpay_cifs_server")
            //{
            //    User = "ops",
            //    Domain = "",
            //    Password = "sumpay.cn",
            //    NetBiosOverTCP = false,
            //    Port = 445
            //    //NetBiosOverTCP = true,
            //    //Port = 139
            //};

            ////开始连接
            //client.Connect();

            ////设置工作目录
            //if (!client.DirectoryIsExist("运维安全部\\1-运维资料\\巡检报告\\堡垒机"))
            //{
            //    client.CreateDirectory("运维安全部\\1-运维资料\\巡检报告\\堡垒机", true);
            //}
            //client.SetWorkingDirectory("运维安全部\\1-运维资料\\巡检报告\\堡垒机");

            //string filePath = "test1";
            //string fileName = Path.Combine(filePath, "test2.txt");

            ////目录是否存在
            //var result = client.DirectoryIsExist(filePath);
            //if (!result)
            //{
            //    //创建目录
            //    client.CreateDirectory(filePath, true);
            //}

            ////文件是否存在
            //result = client.FileIsExist(fileName);
            //if (result)
            //{
            //    //删除文件
            //    client.Delete(fileName);
            //}

            ////上传文件
            //client.Upload(fileName, ms);

            ////获取指定目录下的文件
            //var files = client.GetFiles(filePath);
            ////获取指定目录下的子目录
            //var directories = client.GetDirectories("");
            ////获取指定目录下的文件及子目录
            //var list = client.GetList(filePath);

            ////下载文件
            //using var fs = new MemoryStream();
            //client.Download(fileName, fs);

            ////删除目录及它下面的所有文件
            //client.RemoveDirectory(filePath);
        }
    }
}
