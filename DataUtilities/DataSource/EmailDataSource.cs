using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using DataUtilities.Interfaces;
using DataUtilities.Model;
using Azure.Identity;
using System.Text;
using System.IO;

namespace DataUtilities.DataSource
{

    /// <summary>
    /// This class can be used to access email attachments from an office 365 mailbox
    /// </summary>
    public class EmailDataSource : IEmailDataSource
    {
        private IMailFolderRequestBuilder _mailboxRequest;

        public string MailFolder { get; }
        public string Name { get; }
        public string Username { get; }

        protected readonly GraphServiceClient GraphServiceClient;

        /// <summary>
        /// GetMailBoxRequest  must be run once to build a fully valid reqest using Id's only available on the server.
        /// </summary>
        /// <returns></returns>
        public async Task<IMailFolderRequestBuilder> GetMailBoxRequest()
        {
            if (_mailboxRequest == null)
            {
                _mailboxRequest = await GraphAPIHelper.BuildMailboxPath(GraphServiceClient, MailFolder, Username);
            }

            return _mailboxRequest; 
        }

        public EmailDataSource(string name, string clientId, string clientSecret, string tenantId, string username, string mailFolder = "Inbox")
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Missing", nameof(name));
            if (string.IsNullOrWhiteSpace(clientId)) throw new ArgumentException("Missing", nameof(clientId));
            if (string.IsNullOrWhiteSpace(clientSecret)) throw new ArgumentException("Missing", nameof(clientSecret));
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("Missing", nameof(tenantId));
            if (string.IsNullOrWhiteSpace(username)) throw new ArgumentException("Missing", nameof(username));

            Name = name;
            MailFolder = mailFolder;
            Username = username;

            // The client credentials flow requires that you request the
            // /.default scope, and preconfigure your permissions on the
            // app registration in Azure. An administrator must grant consent
            // to those permissions beforehand.
            var scopes = new[] { "https://graph.microsoft.com/.default" };

            // using Azure.Identity;
            var options = new TokenCredentialOptions
            {
                AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
            };

            // https://docs.microsoft.com/dotnet/api/azure.identity.clientsecretcredential
            var clientSecretCredential = new ClientSecretCredential(
                tenantId, clientId, clientSecret, options);

            GraphServiceClient = new GraphServiceClient(clientSecretCredential, scopes);
        }

        /// <summary>
        /// Get a single item from the datasource with the given {key}
        /// </summary>
        /// <param name="key">key containing emailId and attachmentId</param>
        /// <returns></returns>
        public async Task<AttachmentMemoryStream> GetItemAsync((string emailId, string attachmentId) key)
        {
            if (string.IsNullOrWhiteSpace(key.emailId)) throw new ArgumentException("Missing", nameof(key.emailId));
            if (string.IsNullOrWhiteSpace(key.attachmentId)) throw new ArgumentException("Missing", nameof(key.attachmentId));

            var mailBoxRequest = await GetMailBoxRequest();

            var message = await mailBoxRequest.Messages[key.emailId]
                                            .Request()
                                            .GetAsync();

            var attachment = await mailBoxRequest.Messages[key.emailId]
                                               .Attachments[key.attachmentId]
                                               .Request()
                                               .GetAsync();

            return await GetItemAsync(message, attachment);
        }

        /// <summary>
        /// Get all items from the datasource
        /// </summary>
        /// <param name="recievedDateRange">filter items by recieved date range (dates are inclusive)</param>
        /// <param name="pagingOptions">optional paramter to provide paging values</param>
        /// <returns></returns>
        public async IAsyncEnumerable<AttachmentMemoryStream> GetItemsAsync(DataSourceDateRange recievedDateRange = null, DataSourcePagingOptions pagingOptions = null)
        {
            List<QueryOption> options = new List<QueryOption>();
            List<string> filters = new List<string>();

            if (recievedDateRange != null)
            {
                filters.AddRange(GetDateRangeFilterValues(recievedDateRange, "ReceivedDateTime"));
            }

            if (filters.Any())
            {
                //build a filter query option 
                options.Add(new QueryOption("$filter", string.Join(" and ", filters)));
            }

            var mailBoxRequest = await GetMailBoxRequest();

            var request = mailBoxRequest.Messages.Request(options);

            if (pagingOptions != null)
            {
                request = request.Skip(pagingOptions.PageSize * pagingOptions.Page)
                                 .Top(pagingOptions.PageSize);
            }

            var messages = await request.GetAsync();

            await foreach (var attachments in GetAttachmentsAsync(messages))
            {
                foreach (var attachment in attachments)
                {
                    yield return await GetItemAsync(attachment.message, attachment.attachment);
                }
            }
        }

        /// <summary>
        /// Retrieve attachments for a specific {emailId}
        /// </summary>
        /// <param name="emailId">message id</param>
        /// <returns>collection of attachements</returns>
        public async IAsyncEnumerable<AttachmentMemoryStream> GetItemsAsync(string emailId)
        {
            if (string.IsNullOrWhiteSpace(emailId)) throw new ArgumentException("Missing", nameof(emailId));

            var mailBoxRequest = await GetMailBoxRequest();

            var message = await mailBoxRequest.Messages[emailId]
                                            .Request()
                                            .GetAsync();

            await foreach (var attachments in GetAttachmentsAsync(new[] { message }))
            {
                foreach (var attachment in attachments)
                {
                    yield return await GetItemAsync(attachment.message, attachment.attachment);
                }
            }
        }

        /// <summary>
        /// Test the authentication for this data source
        /// </summary>
        public void TestAuthentication()
        {
            var mailBoxRequest = GetMailBoxRequest().Result;

            mailBoxRequest.Messages
                        .Request()
                        .GetAsync()
                        .Wait();
        }

        /// <summary>
        /// Recieve a single attachment memory stream given the {message} and {attachment} object
        /// </summary>
        /// <param name="attachment">attachment to read</param>
        /// <param name="message">attachment parent message</param>
        /// <returns></returns>
        protected async Task<AttachmentMemoryStream> GetItemAsync(Message message, Attachment attachment)
        {
            if (attachment == null) throw new ArgumentNullException(nameof(attachment));
            if (message == null) throw new ArgumentNullException(nameof(message));

            //check if this attachment is the body attachment we generated from the emails body
            if (attachment is EmailBodyAttachment)
            {
                return await GetEmailBodyAttachmentMemoryStream(attachment as EmailBodyAttachment, message);
            }

            return await GetAttachmentMemoryStreamAsync(attachment, message);
        }

        protected async Task<AttachmentMemoryStream> GetEmailBodyAttachmentMemoryStream(EmailBodyAttachment attachment, Message message)
        {
            AttachmentMemoryStream attachmentMemoryStream = new AttachmentMemoryStream(attachment, message);
            await attachmentMemoryStream.ReadAsync(attachment.BodyContent);
            return attachmentMemoryStream;
        }

        /// <summary>
        /// Read attachment memory stream content bytes from office365
        /// </summary>
        /// <param name="attachment">attachment to read</param>
        /// <param name="message">attachment parent message</param>
        /// <returns></returns>
        protected async Task<AttachmentMemoryStream> GetAttachmentMemoryStreamAsync(Attachment attachment, Message message)
        {
            if(attachment == null) throw new ArgumentNullException(nameof(attachment));
            if (message == null) throw new ArgumentNullException(nameof(message));

            AttachmentMemoryStream attachmentMemoryStream = new AttachmentMemoryStream(attachment, message);
            var attachmentRequestBuilder = GraphServiceClient.Users[Username]
                                                             .Messages[message.Id]
                                                             .Attachments[attachment.Id];

            var fileRequestBuilder = new FileAttachmentRequestBuilder(
                attachmentRequestBuilder.RequestUrl, GraphServiceClient);

            var stream = await fileRequestBuilder.Content.Request().GetAsync();
            await stream.CopyToAsync(attachmentMemoryStream);
            return attachmentMemoryStream;
        }

        private string GetEmailBodyFilename(Message message)
        {
            var fileName = "EmailBody";
            if (message.Body.ContentType == BodyType.Html)
                return fileName + ".html";
            return fileName += ".txt";
        }

        // <summary>
        // Asyncronously retrieve attachments from a collection of messages
        // </summary>
        // <param name = "messages" > messages to get retrieve attachments from</param>
        // <returns> message and attachment as a tuple</returns>
        protected async IAsyncEnumerable<IEnumerable<(Message message, Attachment attachment)>> GetAttachmentsAsync(IEnumerable<Message> messages)
        {
            if (messages == null) throw new ArgumentNullException(nameof(messages));

            var mailBoxRequest = await GetMailBoxRequest();

            foreach (var m in messages)
            {
                var attachments = await mailBoxRequest.Messages[m.Id]
                                                    .Attachments                                   
                                                    .Request()
                                                    .GetAsync();

                ///create mock attachment with the email body content
                var bodyBytes = Encoding.UTF8.GetBytes(m.Body.Content);
                Attachment bodyAttachment = new EmailBodyAttachment()
                {
                    Name = GetEmailBodyFilename(m),
                    BodyContent = bodyBytes,
                    Size = bodyBytes.Length,
                    ContentType = "email-body",
                    LastModifiedDateTime = m.ReceivedDateTime,
                    Id = $"{m.Id}-email-body",
                };

                //only return attachments that are NOT inline (i.e message signatures, images etc) and only return file attachments (not outlook item types)
                //Note : These conditions can only be done locally as these filters dont work on graph api.
                yield return attachments.Where(n => (!n.IsInline.HasValue || !n.IsInline.Value) && n.ODataType == "#microsoft.graph.fileAttachment")
                                        .Select(n => (m, n))
                                        .Append((m, bodyAttachment));
            }
        }

        /// <summary>
        /// Add filters based on a date range (exclusive)
        /// Dates in graph api are stored in UTC 
        /// </summary>
        /// <param name="dateRange"></param>
        /// <returns></returns>
        private IEnumerable<string> GetDateRangeFilterValues(DataSourceDateRange dateRange, string field)
        {
            if (dateRange == null) throw new ArgumentNullException(nameof(dateRange));
            if (string.IsNullOrWhiteSpace(field)) throw new ArgumentNullException("Missing", nameof(field));

            List<string> filters = new List<string>();

            if (dateRange.From.HasValue)
                filters.Add($"{field} gt {dateRange.From.Value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")}");

            if (dateRange.To.HasValue)
                filters.Add($"{field} lt {dateRange.To.Value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")}");

            return filters;
        }
    }

    public class EmailBodyAttachment : Attachment
    {
        public byte [] BodyContent { get; set; }
    }
}
