using System.Web;
using Serilog;
using SMBLibrary.Server;

namespace JmsUserLogonAudit
{
    internal class Backup
    {
        private List<JmsServer> _jmsServer;
        private SmbServer _smbServer;
        private BackupOption _backupOption;
        private SmbClient _smbClient;
        private DateTime _refDateTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

        public Backup(List<JmsServer> jmsServer, SmbServer smbServer, BackupOption backupOption)
        {
            _jmsServer = jmsServer;
            _smbServer = smbServer;
            _backupOption = backupOption;
            _smbClient = new SmbClient(_smbServer.host, _smbServer.shareName)
            {
                User = smbServer.username,
                Domain = smbServer.domain,
                Password = smbServer.password,
                NetBiosOverTCP = smbServer.netBiosOverTCP,
                Port = smbServer.port
                //NetBiosOverTCP = true,
                //Port = 139
            };
            try
            {
                _smbClient.Connect();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Log.Error(e.Message);
                throw;
            }

            if (_smbClient.IsConnected)
            {
                Log.Information("成功连接SMB共享服务器. ");
            }
            else
            {
                Log.Error("无法连接SMB共享服务器!!!请检查相关配置和共享服务器状态, 确认无误后再进行尝试...");
                Environment.Exit(3);
            }
            if (!_smbClient.DirectoryIsExist(smbServer.sharePath))
            {
                Log.Information($"SMB共享中不存在{smbServer.sharePath}目录, 创建... ");
                _smbClient.CreateDirectory(smbServer.sharePath, true);
            }
            _smbClient.SetWorkingDirectory(smbServer.sharePath);
        }

        public async Task ExportAuditFile()
        {

            foreach (var jms in _jmsServer)
            {
                var localPath = Path.Combine(Directory.GetCurrentDirectory(), _backupOption.localExportPath);
                var jmsName = jms.name;
                var jmsBaseUri = jms.baseUri;
                var jmsToken = jms.token;
                Log.Information($"开始导出Jumpserver服务器\"{jmsName}（{jms.baseUri}）\"上用户登陆信息...");
                for (int i = _backupOption.monthFromOffset; i <= _backupOption.monthToOffset; i++)
                {
                    DateTime firstDay = _refDateTime.AddMonths(i).AddMilliseconds(1);
                    DateTime lastDay = _refDateTime.AddMonths(i + 1).AddMilliseconds(-2);
                    string dateFrom = HttpUtility.UrlEncode(firstDay.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
                    string dateTo = HttpUtility.UrlEncode(lastDay.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
                    var year = firstDay.Year;
                    var month = firstDay.Month;
                    var filePath = Path.Combine(jmsName, $"{year}");
                    var fileName = jmsName + "-" + year + "-" + (month.ToString().Length == 1 ? "0" : "") + month.ToString() +
                                   ".xlsx";
                    var fullFileName = Path.Combine(filePath, fileName);
                    var fullLocalPath = Path.Combine(localPath, fullFileName);
                    var localFileInfo = new FileInfo(fullLocalPath);
                    //var fileInfo = new FileInfo(Path.Combine(path, fileInfo1.Name)); 
                    if (_backupOption.overwrite || !File.Exists(localFileInfo.FullName))
                    {
                        var jmsClient = new Jms(jms);
                        await jmsClient.GetLogonStatisticsByMonth(year, month, dateFrom, dateTo);
                        if (!jmsClient.IsConnected())
                        {
                            Log.Error("跳过此服务器导出，请检查配置和服务器运行状态...");
                            break;
                        }
                        if (!jmsClient.IsAuthorized())
                        {
                            Log.Error("跳过此服务器导出，请检查Token和Token权限");
                            break;
                        }
                        if (!Directory.Exists(localFileInfo.DirectoryName))
                        {
                            Log.Information($"本地不存在\"{localFileInfo.DirectoryName}\"目录, 创建... ");
                            Directory.CreateDirectory(localFileInfo.DirectoryName);
                        }
                        Log.Information($"导出{year}年{month}月用户登陆信息...");
                        var full = jmsClient.GetFullStatistic();
                        Excel.OutXls(full, localFileInfo);
                    }
                    else
                    {
                        Log.Information($"本地已存在\"{localFileInfo.FullName}\"，且配置了不覆盖，跳过导出...");
                    }

                    if (!_smbClient.DirectoryIsExist(filePath))
                    {
                        Log.Information($"SMB共享中不存在\"{filePath}\"目录, 创建...");
                        _smbClient.CreateDirectory(filePath, true);
                    }

                    if (_smbServer.overwrite && _smbClient.FileIsExist(fullFileName))
                    {
                        _smbClient.Delete(fullFileName);
                    }

                    if (!_smbClient.FileIsExist(fullFileName))
                    {
                        Log.Information($"上传\"{fileName}\"到SMB共享\"{fullFileName}\"");
                        _smbClient.Upload(fullFileName, fullLocalPath);
                    }
                    else
                    {
                        Log.Information($"SMB共享中已存在\"{fullFileName}\"，且配置了不覆盖，跳过上传...");
                    }
                }
            }
        }
    }
}
