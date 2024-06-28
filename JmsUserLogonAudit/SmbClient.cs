using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SMBLibrary;
using SMBLibrary.Client;
using SMBLibrary.SMB1;
using FileAttributes = SMBLibrary.FileAttributes;

namespace JmsUserLogonAudit
{
    public class SmbClient : IDisposable
    {
        bool disposed = false;
        ISMBClient? client;
        ISMBFileStore? store;
        string workingDirectory = "";
        string seperator = "\\";

        public SmbClient(string host, string shareName)
        {
            Host = host;
            ShareName = shareName;
        }

        /// <summary>
        /// Host
        /// </summary>
        public string Host { get; }
        /// <summary>
        /// 名称
        /// </summary>
        public string ShareName { get; }
        /// <summary>
        /// 端口(DirectTCPTransport默认端口445, NetBiosOverTCP默认端口139)
        /// </summary>
        public int Port { get; set; } = 445;
        /// <summary>
        /// 域
        /// </summary>
        public string Domain { get; set; } = string.Empty;
        /// <summary>
        /// 账号
        /// </summary>
        public string? User { get; set; }
        /// <summary>
        /// 密码
        /// </summary>
        public string? Password { get; set; }
        /// <summary>
        /// 是否使用NetBios
        /// </summary>
        public bool NetBiosOverTCP { get; set; }
        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected => client?.IsConnected ?? false;
        /// <summary>
        /// 当前工作目录
        /// </summary>
        public string WorkingDirectory => workingDirectory;

        /// <summary>
        /// 设置工作目录
        /// </summary>
        /// <param name="path"></param>
        public void SetWorkingDirectory(string path)
        {
            if (IsAbsolute(path))
            {
                workingDirectory = FormatPath("", path);
            }
            else
            {
                workingDirectory = FormatPath(workingDirectory, path);
            }
        }
        /// <summary>
        /// 开始连接
        /// </summary>
        /// <returns></returns>
        public bool Connect()
        {
            GenerateClient();
            return IsConnected;
        }
        /// <summary>
        /// 获取指定目录下的所有文件
        /// </summary>
        /// <param name="path"></param>
        /// <param name="pattern"></param>
        /// <returns></returns>
        public string[] GetFiles(string path, string pattern = "*")
        {
            CheckDisposed();
            CheckConnected();

            path = FormatPath(workingDirectory, path);
            return GetList(path, pattern, false).Select(f => f.Name).ToArray();
        }
        /// <summary>
        /// 获取指定目录下的所有目录
        /// </summary>
        /// <param name="path"></param>
        /// <param name="pattern"></param>
        /// <returns></returns>
        public string[] GetDirectories(string path, string pattern = "*")
        {
            CheckDisposed();
            CheckConnected();

            path = FormatPath(workingDirectory, path);
            return GetList(path, pattern, true).Select(f => f.Name).ToArray();
        }
        /// <summary>
        /// 获取指定目录下的所有目录及文件
        /// </summary>
        /// <param name="path"></param>
        /// <param name="pattern"></param>
        /// <returns></returns>
        public SmbClientFile[] GetList(string path, string pattern = "*")
        {
            CheckDisposed();
            CheckConnected();

            path = FormatPath(workingDirectory, path);
            return GetList(path, pattern, null);
        }
        /// <summary>
        /// 创建目录
        /// </summary>
        /// <param name="path"></param>
        /// <param name="rescure"></param>
        /// <returns></returns>
        public void CreateDirectory(string path, bool rescure = false)
        {
            CheckDisposed();
            CheckConnected();

            var isAbsolute = IsAbsolute(path);
            var splits = path.Split(new string[] { "\\", "/" }, StringSplitOptions.RemoveEmptyEntries);
            if (!rescure)
            {
                splits = new string[] { string.Join(seperator, splits) };
            }
            var dir = isAbsolute ? "" : workingDirectory;
            foreach (var split in splits)
            {
                dir = FormatPath(dir, split);
                var status = store!.CreateFile(out var directoryHandle, out var _, dir, AccessMask.GENERIC_WRITE, FileAttributes.Directory,
                    ShareAccess.Write, CreateDisposition.FILE_OPEN_IF, CreateOptions.FILE_DIRECTORY_FILE, null);
                CheckStatus(status, nameof(store.CreateFile));

                status = store.CloseFile(directoryHandle);
                CheckStatus(status, nameof(store.CloseFile));
            }
        }
        /// <summary>
        /// 删除目录
        /// </summary>
        /// <param name="path"></param>
        public void RemoveDirectory(string path)
        {
            CheckDisposed();
            CheckConnected();

            var currentPath = FormatPath(workingDirectory, path);
            var items = GetList(currentPath, "*", null);
            foreach (var item in items)
            {
                var name = $"{path}{seperator}{item.Name}";
                if (item.IsDirectory)
                {
                    RemoveDirectory(name);
                }
                else
                {
                    Delete(name);
                }
            }

            var status = store!.CreateFile(out var directoryHandle, out _, currentPath, AccessMask.GENERIC_READ | AccessMask.GENERIC_WRITE | AccessMask.DELETE | AccessMask.SYNCHRONIZE,
                FileAttributes.Normal, ShareAccess.None, CreateDisposition.FILE_OPEN, CreateOptions.FILE_DIRECTORY_FILE, null);
            if (status == NTStatus.STATUS_OBJECT_PATH_NOT_FOUND)
            {
                return;
            }
            CheckStatus(status, nameof(store.CreateFile));

            FileDispositionInformation fileDispositionInformation = new FileDispositionInformation();
            fileDispositionInformation.DeletePending = true;
            status = store.SetFileInformation(directoryHandle, fileDispositionInformation);
            CheckStatus(status, nameof(store.SetFileInformation), path);

            status = store.CloseFile(directoryHandle);
            CheckStatus(status, nameof(store.CloseFile));
        }
        /// <summary>
        /// 删除文件
        /// </summary>
        /// <param name="remoteFile"></param>
        public void Delete(string remoteFile)
        {
            CheckDisposed();
            CheckConnected();

            remoteFile = FormatPath(workingDirectory, remoteFile);
            var status = store!.CreateFile(out var fileHandle, out _, remoteFile, AccessMask.GENERIC_WRITE | AccessMask.DELETE | AccessMask.SYNCHRONIZE,
                FileAttributes.Normal, ShareAccess.None, CreateDisposition.FILE_OPEN, CreateOptions.FILE_NON_DIRECTORY_FILE, null);
            if (status == NTStatus.STATUS_OBJECT_NAME_NOT_FOUND)
            {
                return;
            }
            CheckStatus(status, nameof(store.CreateFile));

            FileDispositionInformation fileDispositionInformation = new FileDispositionInformation();
            fileDispositionInformation.DeletePending = true;
            status = store.SetFileInformation(fileHandle, fileDispositionInformation);
            CheckStatus(status, nameof(store.SetFileInformation));

            status = store.CloseFile(fileHandle);
            CheckStatus(status, nameof(store.CloseFile));
        }
        /// <summary>
        /// 目录是否存在
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public bool DirectoryIsExist(string path)
        {
            CheckDisposed();
            CheckConnected();

            try
            {
                path = FormatPath(workingDirectory, path);
                var status = store!.CreateFile(out var directoryHandle, out var _, path, AccessMask.GENERIC_READ, FileAttributes.Directory,
                    ShareAccess.Read, CreateDisposition.FILE_OPEN, CreateOptions.FILE_DIRECTORY_FILE, null);
                if (status != NTStatus.STATUS_SUCCESS)
                {
                    return false;
                }

                store.CloseFile(directoryHandle);
                return true;
            }
            catch
            {
                return false;
            }
        }
        /// <summary>
        /// 文件是否存在
        /// </summary>
        /// <param name="remoteFile"></param>
        /// <returns></returns>
        public bool FileIsExist(string remoteFile)
        {
            CheckDisposed();
            CheckConnected();

            try
            {
                remoteFile = FormatPath(workingDirectory, remoteFile);
                var status = store!.CreateFile(out var directoryHandle, out var _, remoteFile, AccessMask.GENERIC_READ, FileAttributes.Archive,
                    ShareAccess.Read, CreateDisposition.FILE_OPEN, CreateOptions.FILE_NON_DIRECTORY_FILE, null);
                if (status != NTStatus.STATUS_SUCCESS)
                {
                    return false;
                }

                store.CloseFile(directoryHandle);
                return true;
            }
            catch
            {
                return false;
            }
        }
        /// <summary>
        /// 上传文件
        /// </summary>
        /// <param name="remoteFile"></param>
        /// <param name="localFile"></param>
        public void Upload(string remoteFile, string localFile)
        {
            using var fs = File.OpenRead(localFile);
            Upload(remoteFile, fs);
        }
        /// <summary>
        /// 上传文件
        /// </summary>
        /// <param name="remoteFile"></param>
        /// <param name="stream"></param>
        public void Upload(string remoteFile, Stream stream)
        {
            CheckDisposed();
            CheckConnected();

            remoteFile = FormatPath(workingDirectory, remoteFile);
            var status = store!.CreateFile(out var fileHandle, out _, remoteFile, AccessMask.GENERIC_WRITE | AccessMask.SYNCHRONIZE,
                FileAttributes.Normal, ShareAccess.Write, CreateDisposition.FILE_CREATE, CreateOptions.FILE_NON_DIRECTORY_FILE, null);
            CheckStatus(status, nameof(store.CreateFile));

            int length = (int)client!.MaxWriteSize;
            int writeOffset = 0;
            while (true)
            {
                byte[] buffer = new byte[length];
                int count = stream.Read(buffer, 0, length);
                if (count == 0)
                {
                    break;
                }
                if (count < length)
                {
                    Array.Resize(ref buffer, count);
                }
                store.WriteFile(out _, fileHandle, writeOffset, buffer);
                CheckStatus(status, nameof(store.WriteFile));
                writeOffset += count;
            }

            status = store.CloseFile(fileHandle);
            CheckStatus(status, nameof(store.CloseFile));
        }
        /// <summary>
        /// 下载文件
        /// </summary>
        /// <param name="remoteFile"></param>
        /// <param name="localFile"></param>
        public void Download(string remoteFile, string localFile)
        {
            using var fs = File.OpenWrite(localFile);
            Download(remoteFile, fs);
        }
        /// <summary>
        /// 下载文件
        /// </summary>
        /// <param name="remoteFile"></param>
        /// <param name="stream"></param>
        public void Download(string remoteFile, Stream stream)
        {
            CheckDisposed();
            CheckConnected();

            remoteFile = FormatPath(workingDirectory, remoteFile);
            var status = store!.CreateFile(out var fileHandle, out _, remoteFile, AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE,
                FileAttributes.Normal, ShareAccess.Read, CreateDisposition.FILE_OPEN, CreateOptions.FILE_NON_DIRECTORY_FILE, null);
            CheckStatus(status, nameof(store.CreateFile));

            var length = (int)client!.MaxReadSize;
            long bytesRead = 0;
            while (true)
            {
                status = store.ReadFile(out var data, fileHandle, bytesRead, length);
                if (status == NTStatus.STATUS_END_OF_FILE)
                {
                    break;
                }
                CheckStatus(status, nameof(store.ReadFile));

                if (data.Length == 0)
                {
                    break;
                }
                bytesRead += data.Length;
                stream.Write(data, 0, data.Length);
            }

            if (stream.CanSeek)
            {
                stream.Seek(0, SeekOrigin.Begin);
            }

            status = store.CloseFile(fileHandle);
            CheckStatus(status, nameof(store.CloseFile));
        }

        public void Dispose()
        {
            disposed = true;
            if (store != null)
            {
                store.Disconnect();
            }
            if (client != null)
            {
                if (!string.IsNullOrEmpty(User))
                {
                    client.Logoff();
                }
                client.Disconnect();
            }
        }

        private void CheckDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(SmbClient));
            }
        }
        private void CheckConnected()
        {
            if (client == null)
            {
                throw new InvalidOperationException("Connection needs to be established first");
            }
            if (!IsConnected)
            {
                throw new Exception($"Connection lost");
            }
        }
        private void CheckStatus(NTStatus status, string method, params string[] args)
        {
            if (status != NTStatus.STATUS_SUCCESS)
            {
                throw new Exception($"Error Occurs:{method}-{status}" + (args.Length > 0 ? string.Join("", args.Select(f => $"[{f}]")) : ""));
            }
        }
        private void GenerateClient()
        {
            CheckDisposed();
            if (client != null)
            {
                throw new Exception("client is exists");
            }

            if (!IPAddress.TryParse(Host, out var serverAddress))
            {
                IPAddress[] hostAddresses = Dns.GetHostAddresses(Host);
                if (hostAddresses.Length == 0)
                {
                    throw new Exception($"Cannot resolve host name {Host} to an IP address");
                }

                serverAddress = IPAddressHelper.SelectAddressPreferIPv4(hostAddresses);
            }

            var tranportType = NetBiosOverTCP ? SMBTransportType.NetBiosOverTCP : SMBTransportType.DirectTCPTransport;
            client = new SMB2Client();
            var connect = typeof(SMB2Client).GetMethod(nameof(client.Connect), BindingFlags.NonPublic | BindingFlags.Instance);
            connect?.Invoke(client, new object[] { serverAddress, tranportType, Port, 5000 });

            if (!IsConnected)
            {
                throw new Exception($"Cannot resolve host name {Host} to an IP address");
            }
            NTStatus status;
            if (!string.IsNullOrEmpty(User))
            {
                status = client.Login(Domain, User, Password);
                CheckStatus(status, nameof(client.Login));
            }
            var shares = client.ListShares(out status);
            store = client.TreeConnect(ShareName, out status);
            CheckStatus(status, nameof(client.TreeConnect));
        }
        private bool IsAbsolute(string path)
        {
            return path.StartsWith(@"\") || path.StartsWith(@"/");
        }
        private string FormatPath(string workingDirectory, string path)
        {
            List<string> paths = new List<string>();
            paths.AddRange(path.Split(new string[] { "/", "\\" }, StringSplitOptions.RemoveEmptyEntries));

            //如果是绝对路径，那么直接使用
            if (!IsAbsolute(path))
            {
                paths.Insert(0, workingDirectory);
            }
            return string.Join(seperator, paths.Where(f => !string.IsNullOrEmpty(f)));
        }
        private SmbClientFile[] GetList(string path, string pattern, bool? directory)
        {
            var status = store!.CreateFile(out var directoryHandle, out var _, path, AccessMask.GENERIC_READ, FileAttributes.Directory,
                ShareAccess.Read | ShareAccess.Write, CreateDisposition.FILE_OPEN_IF, CreateOptions.FILE_DIRECTORY_FILE, null);
            CheckStatus(status, nameof(store.CreateFile), path);

            List<SmbClientFile> files = new List<SmbClientFile>();
            if (store is SMB1FileStore fileStore)
            {
                pattern = $"{path}{seperator}{pattern}";
                status = ((SMB1FileStore)store).QueryDirectory(out var list, pattern, FindInformationLevel.SMB_FIND_FILE_DIRECTORY_INFO);
                CheckStatus(status, nameof(store.QueryDirectory), pattern);

                Action<FindFileDirectoryInfo> action = directory switch
                {
                    false => fileDirectoryInfo =>
                    {
                        if (!fileDirectoryInfo.ExtFileAttributes.HasFlag(ExtendedFileAttributes.Directory))
                        {
                            var modified = fileDirectoryInfo.LastWriteTime ?? fileDirectoryInfo.CreationTime;
                            files.Add(new SmbClientFile(fileDirectoryInfo.FileName, false, modified, fileDirectoryInfo.CreationTime));
                        }
                    }
                    ,
                    true => fileDirectoryInfo =>
                    {
                        if (fileDirectoryInfo.ExtFileAttributes.HasFlag(ExtendedFileAttributes.Directory))
                        {
                            var modified = fileDirectoryInfo.LastWriteTime ?? fileDirectoryInfo.CreationTime;
                            files.Add(new SmbClientFile(fileDirectoryInfo.FileName, true, modified, fileDirectoryInfo.CreationTime));
                        }
                    }
                    ,
                    _ => fileDirectoryInfo =>
                    {
                        var isDirectory = fileDirectoryInfo.ExtFileAttributes.HasFlag(ExtendedFileAttributes.Directory);
                        var modified = fileDirectoryInfo.LastWriteTime ?? fileDirectoryInfo.CreationTime;
                        files.Add(new SmbClientFile(fileDirectoryInfo.FileName, isDirectory, modified, fileDirectoryInfo.CreationTime));
                    }
                };

                foreach (var l in list)
                {
                    if (l is FindFileDirectoryInfo fileDirectoryInfo)
                    {
                        if (fileDirectoryInfo.FileName == "." || fileDirectoryInfo.FileName == "..")
                        {
                            continue;
                        }
                        action(fileDirectoryInfo);
                    }
                }
            }
            else
            {
                status = store.QueryDirectory(out var list, directoryHandle, pattern, FileInformationClass.FileDirectoryInformation);
                if (status != NTStatus.STATUS_NO_MORE_FILES)
                {
                    CheckStatus(status, nameof(store.QueryDirectory), pattern);
                }

                Action<FileDirectoryInformation> action = directory switch
                {
                    false => file =>
                    {
                        if (!file.FileAttributes.HasFlag(FileAttributes.Directory))
                        {
                            files.Add(new SmbClientFile(file.FileName, false, file.ChangeTime, file.CreationTime));
                        }
                    }
                    ,
                    true => file =>
                    {
                        if (file.FileAttributes.HasFlag(FileAttributes.Directory))
                        {
                            files.Add(new SmbClientFile(file.FileName, true, file.ChangeTime, file.CreationTime));
                        }
                    }
                    ,
                    _ => file =>
                    {
                        files.Add(new SmbClientFile(file.FileName, file.FileAttributes.HasFlag(FileAttributes.Directory), file.ChangeTime, file.CreationTime));
                    }
                };

                foreach (var l in list)
                {
                    if (l is FileDirectoryInformation file)
                    {
                        if (file.FileName == "." || file.FileName == "..")
                        {
                            continue;
                        }
                        action(file);
                    }
                }
            }

            status = store.CloseFile(directoryHandle);
            CheckStatus(status, nameof(store.CloseFile));

            return files.ToArray();
        }
    }
    public class SmbClientFile
    {
        public SmbClientFile(string name, bool isDirectory, DateTime? lastModifiedTime, DateTime? creationTime)
        {
            Name = name;
            IsDirectory = isDirectory;
            if (lastModifiedTime != null)
            {
                if (lastModifiedTime.Value.Kind == DateTimeKind.Utc)
                {
                    lastModifiedTime = lastModifiedTime.Value.ToLocalTime();
                }
            }
            LastModifiedTime = lastModifiedTime;

            if (creationTime != null)
            {
                if (creationTime.Value.Kind == DateTimeKind.Utc)
                {
                    creationTime = creationTime.Value.ToLocalTime();
                }
            }
            CreationTime = creationTime;
        }

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// 是否是文件
        /// </summary>
        public bool IsDirectory { get; }
        /// <summary>
        /// 最后修改时间
        /// </summary>
        public DateTime? LastModifiedTime { get; }
        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime? CreationTime { get; }
    }
}
