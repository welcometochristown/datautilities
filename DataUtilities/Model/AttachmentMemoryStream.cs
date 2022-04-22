using DataUtilities.Interfaces;
using Microsoft.Graph;
using System;
using System.IO;

namespace DataUtilities.Model
{
    public class AttachmentMemoryStream : MemoryStream, IMemoryStreamKey<(string emailId, string attachmentId)>
    {
        public (string emailId, string attachmentId) Key { get; }

        public Attachment AttachmentInfo { get; set; }
        public Message MessageInfo { get; set; }

        public AttachmentMemoryStream(Attachment attachmentInfo, Message messageInfo)
        {
            if (attachmentInfo == null) throw new ArgumentException(nameof(attachmentInfo));
            if (messageInfo == null) throw new ArgumentException(nameof(messageInfo));

            AttachmentInfo = attachmentInfo;
            MessageInfo = messageInfo;
            Key = (messageInfo.Id, attachmentInfo.Id);
        }

        public override string ToString()
        {
            return $"'{MessageInfo.Subject}' from '{MessageInfo.From.EmailAddress.Address}' on {MessageInfo.ReceivedDateTime.Value.ToLocalTime()} -> '{AttachmentInfo.Name}'";
        }
    }
}
