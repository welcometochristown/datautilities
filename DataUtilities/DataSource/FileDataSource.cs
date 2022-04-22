using DataUtilities.Interfaces;
using DataUtilities.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DataUtilities.DataSource
{
    public class FileDataSource : IFileDataSource
    {
        public string Name { get; }
        public string LocalFolder { get; }

        public FileDataSource(string name, string localFolder)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Missing", nameof(name));
            if (string.IsNullOrWhiteSpace(localFolder)) throw new ArgumentException("Missing", nameof(localFolder));

            Name = name;
            LocalFolder = localFolder;
        }

        /// <summary>
        /// Retreive a single item for a given {key}
        /// </summary>
        /// <param name="key">key identifier for item</param>
        /// <returns>File memory stream of item</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public async Task<FileMemoryStream> GetItemAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException("Missing", nameof(key));

            FileInfo f = new FileInfo(key);
            return await ReadFileContent(f);
        }

        /// <summary>
        /// Asyncronously read contents of each file 
        /// </summary>
        /// <param name="files">Files to read</param>
        /// <returns>Collection of file streams</returns>
        /// <exception cref="ArgumentNullException"></exception>
        protected async IAsyncEnumerable<FileMemoryStream> ReadFilesContent(IEnumerable<FileInfo> files)
        {
            if (files == null) throw new ArgumentNullException(nameof(files));

            foreach (var f in files)
            {
                yield return await ReadFileContent(f);
            }
        }

        /// <summary>
        /// Read file contents of a single {file}
        /// </summary>
        /// <param name="file">file to read</param>
        /// <returns>File stream for file</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public async Task<FileMemoryStream> ReadFileContent(FileInfo file)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));

            byte[] result;
            FileMemoryStream fileMemoryStream = new FileMemoryStream(file);
            using (FileStream fileStream = file.Open(FileMode.Open, FileAccess.Read))
            {
                result = new byte[fileStream.Length];

                await fileStream.ReadAsync(result, 0, (int)fileStream.Length);
                await fileMemoryStream.WriteAsync(result, 0, (int)fileStream.Length);
            }

            return fileMemoryStream;
        }

        /// <summary>
        /// Retrieve items 
        /// </summary>
        /// <param name="lastWrittenDateRange">date range to filter by (dates are exclusive)</param>
        /// <param name="pagingOptions">paging options for search</param>
        /// <returns></returns>
        public async IAsyncEnumerable<FileMemoryStream> GetItemsAsync(DataSourceDateRange lastWrittenDateRange = null, DataSourcePagingOptions pagingOptions = null)
        {
            //Get a list of all the remote files
            DirectoryInfo directoryInfo = new DirectoryInfo(LocalFolder);
            var remoteFiles = directoryInfo.GetFiles().AsEnumerable();

            if (lastWrittenDateRange != null)
            {
                if (lastWrittenDateRange.From != null)
                    remoteFiles = remoteFiles.Where(n => n.LastWriteTime > lastWrittenDateRange.From.Value);

                if (lastWrittenDateRange.To != null)
                    remoteFiles = remoteFiles.Where(n => n.LastWriteTime < lastWrittenDateRange.To.Value);
            }

            if (pagingOptions != null)
            {
                remoteFiles = remoteFiles.Skip(pagingOptions.Page * pagingOptions.PageSize)
                                            .Take(pagingOptions.PageSize);
            }

            await foreach (var file in ReadFilesContent(remoteFiles.ToList()))
            {
                yield return file;
            }
        }

        /// <summary>
        /// Authenticate we have access to the local folder
        /// </summary>
        public void TestAuthentication()
        {
            Directory.GetFiles(LocalFolder);
        }
    }
}
