using DataUtilities.Interfaces;
using Renci.SshNet.Sftp;
using System;
using System.IO;

namespace DataUtilities.Model
{
    public class SftpMemoryStream : MemoryStream, IMemoryStreamKey<string>
    {
        public string Key { get; }
        public SftpFile FileInfo { get; set; }

        public SftpMemoryStream(SftpFile file)
        {
            if (file == null) throw new ArgumentException(nameof(file));

            FileInfo = file;
            Key = file.FullName;
        }
        public override string ToString()
        {
            return $"{FileInfo.FullName}";
        }
    }
}
