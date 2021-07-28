﻿namespace Dwapi.UploadManagement.Core.Exchange.Mnch
{
    public class SendMnchResponse
    {
        public string BatchKey { get; set; }

        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(BatchKey);
        }
        public override string ToString()
        {
            return $"{BatchKey}";
        }
    }
}
