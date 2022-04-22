using Microsoft.Graph;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DataUtilities
{
    public static class GraphAPIHelper
    {
        public static async Task<IMailFolderRequestBuilder> BuildMailboxPath(GraphServiceClient GraphServiceClient, string mailFolder, string username)
        {
            if(GraphServiceClient==null) throw new ArgumentNullException(nameof(GraphServiceClient));
            if (string.IsNullOrWhiteSpace(mailFolder)) throw new ArgumentNullException("Missing",nameof(mailFolder));
            if (string.IsNullOrWhiteSpace(username)) throw new ArgumentNullException("Missing", nameof(username));

            var folderPathParts = mailFolder.Replace("\\", "/").Split("/");
            var rootFolder = folderPathParts[0];

            var folderRequest = GraphServiceClient.Users[username]
                                                  .MailFolders[rootFolder];

            //traverse to child folder finding each id along the way
            foreach (var p in folderPathParts.Skip(1))
            {
                var childFolders = await folderRequest
                                            .ChildFolders
                                            .Request()
                                            .GetAsync();

                var folder = childFolders.SingleOrDefault(n => n.DisplayName == p);

                if (folder == null)
                {
                    throw new InvalidOperationException($"Child folder '{p}' does not exist");
                }

                folderRequest = folderRequest.ChildFolders[folder.Id];
            }

            return folderRequest;
        }
    }
}
