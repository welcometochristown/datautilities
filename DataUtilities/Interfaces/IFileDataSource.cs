using DataUtilities.Model;

namespace DataUtilities.Interfaces
{
    public interface IFileDataSource : IDataSource<FileMemoryStream, string>
    {
        public string LocalFolder { get; }
    }
}
