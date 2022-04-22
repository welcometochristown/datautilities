using DataUtilities.Model;
using System.Collections.Generic;

namespace DataUtilities.Interfaces
{
    public interface IEmailDataSource : IDataSource<AttachmentMemoryStream, (string emailId, string attachmentId)>
    {
        string MailFolder { get; }

        IAsyncEnumerable<AttachmentMemoryStream> GetItemsAsync(string emailId);
    }
}
