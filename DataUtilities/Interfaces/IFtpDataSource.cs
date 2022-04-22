using DataUtilities.Model;
using Renci.SshNet;

namespace DataUtilities.Interfaces
{
    public interface IFtpDataSource : IDataSource<SftpMemoryStream, string>
    {
        public string HostName { get; }
        public string Username { get; }
        public string Password { get; }
        public string RemoteFolder { get; }
        public int Port { get; }
        public ConnectionInfo ConnectionInfo { get; }
    }
}
