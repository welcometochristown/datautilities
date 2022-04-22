using DataUtilities.Interfaces;
using System;
using System.IO;

namespace DataUtilities.Model
{
    public class FileMemoryStream: MemoryStream, IMemoryStreamKey<string>
    {
        public string Key { get; }
        public FileInfo FileInfo { get; set; }

        public FileMemoryStream(FileInfo file)
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
