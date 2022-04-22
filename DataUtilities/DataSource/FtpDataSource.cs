using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DataUtilities.Interfaces;
using DataUtilities.Model;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace DataUtilities.DataSource
{
    public class FtpDataSource : IFtpDataSource
    {
        public string Name { get; }
        public string HostName { get; }
        public string Username { get; }
        public string Password { get; }
        public string RemoteFolder { get; }
        public int Port { get; }
        public ConnectionInfo ConnectionInfo { get; }

        public FtpDataSource(string name, string hostname, string username, string password, string remoteFolder, int port = 22)
         {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Missing", nameof(name));
            if (string.IsNullOrWhiteSpace(hostname)) throw new ArgumentException("Missing", nameof(hostname));
            if (string.IsNullOrWhiteSpace(username)) throw new ArgumentException("Missing", nameof(username));
            if (string.IsNullOrWhiteSpace(password)) throw new ArgumentException("Missing", nameof(password));
            if (string.IsNullOrWhiteSpace(remoteFolder)) throw new ArgumentException("Missing", nameof(remoteFolder));

            Name = name;
            HostName = hostname;
            Username = username;
            Password = password;
            RemoteFolder = remoteFolder;
            Port = port;

            ConnectionInfo = new ConnectionInfo(HostName, Port, Username, new[] { new PasswordAuthenticationMethod(Username, Password) });
        }

        /// <summary>
        /// Download {files} from a remote folder using {client}
        /// </summary>
        /// <param name="client">client to access sftp remote directory</param>
        /// <param name="files">files to download</param>
        /// <returns>collection of sftp memory streams</returns>
        /// <exception cref="ArgumentNullException"></exception>
        protected async IAsyncEnumerable<SftpMemoryStream> DownloadFilesAsync(SftpClient client, IEnumerable<SftpFile> files)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (files == null) throw new ArgumentNullException(nameof(files));

            foreach (var f in files)
            {
                yield return await DownloadFileAsync(client, f);
            }
        }

        /// <summary>
        /// Download a single {file} using {client}
        /// </summary>
        /// <param name="client">client to access sftp remote directory</param>
        /// <param name="file">file to download</param>
        /// <returns>a single sftp memory stream </returns>
        /// <exception cref="ArgumentNullException"></exception>
        protected async Task<SftpMemoryStream> DownloadFileAsync(SftpClient client, SftpFile file)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (file == null) throw new ArgumentNullException(nameof(file));

            SftpMemoryStream stream = new SftpMemoryStream(file);
            await Task.Factory.FromAsync(client.BeginDownloadFile(file.FullName, stream), client.EndDownloadFile);
            return stream;
        }

        /// <summary>
        /// Get a single item for a given {key}
        /// </summary>
        /// <param name="key">unique key identifier for item</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public async Task<SftpMemoryStream> GetItemAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));

            using (var client = new SftpClient(ConnectionInfo))
            {
                client.Connect();
                var remoteFile = client.Get(key);
                return await DownloadFileAsync(client, remoteFile);
            }
        }

        /// <summary>
        /// Retreive items
        /// </summary>
        /// <param name="lastWrittenDateRange">date range filter (dates are exclusive)</param>
        /// <param name="pagingOptions">paging options for search</param>
        /// <returns></returns>
        public async IAsyncEnumerable<SftpMemoryStream> GetItemsAsync(DataSourceDateRange lastWrittenDateRange = null, DataSourcePagingOptions pagingOptions = null)
        {
            using (var client = new SftpClient(ConnectionInfo))
            {
                client.Connect();

                //Get a list of all the remote files
                var remoteFiles = client.ListDirectory(RemoteFolder)
                                        .Where(n => !n.IsDirectory)
                                        .AsEnumerable();

                if (lastWrittenDateRange != null)
                {
                    if (lastWrittenDateRange.From != null)
                        remoteFiles = remoteFiles.Where(n => n.LastWriteTime.ToUniversalTime() > lastWrittenDateRange.From.Value);

                    if (lastWrittenDateRange.To != null)
                        remoteFiles = remoteFiles.Where(n => n.LastWriteTime.ToUniversalTime() < lastWrittenDateRange.To.Value);
                }

                if (pagingOptions != null)
                {
                    remoteFiles = remoteFiles.Skip(pagingOptions.Page * pagingOptions.PageSize)
                                             .Take(pagingOptions.PageSize);
                }

                await foreach (var file in DownloadFilesAsync(client, remoteFiles.ToList()))
                {
                    yield return file;
                }
            }
        }

        /// <summary>
        /// Test authentication to sftp remote folder
        /// </summary>
        public void TestAuthentication()
        {
            using (var session = new SftpClient(ConnectionInfo))
            {
                session.Connect();
                session.Disconnect();
            }
        }
    }


}
