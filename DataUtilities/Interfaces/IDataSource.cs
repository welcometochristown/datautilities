using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DataUtilities.Interfaces
{
    public interface IDataSource<T, TKey> where T : MemoryStream, IMemoryStreamKey<TKey>
    {
        string Name { get; }
        Task<T> GetItemAsync(TKey key);
        IAsyncEnumerable<T> GetItemsAsync(DataSourceDateRange dateRange = null, DataSourcePagingOptions pagingOptions = null);
        void TestAuthentication();
    }
}