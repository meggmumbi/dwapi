﻿﻿using System;
using System.Collections.Generic;
using System.Linq;
 using Dwapi.ExtractsManagement.Core.Model.Destination.Dwh;
 using Dwapi.SharedKernel.Enum;
 using Dwapi.UploadManagement.Core.Interfaces.Exchange;
using Dwapi.UploadManagement.Core.Interfaces.Exchange.Dwh;
using Dwapi.UploadManagement.Core.Model.Dwh;

namespace Dwapi.UploadManagement.Core.Exchange.Dwh
{
    public class CovidsMessageBag:ICovidMessageBag
    {
        private int stake = 1;
        public string EndPoint => "Covid";
        public IMessage<CovidExtractView> Message { get; set; }
        public List<IMessage<CovidExtractView>> Messages { get; set; }
        public List<Guid> SendIds => GetIds();
        public string ExtractName => "CovidExtract";
        public ExtractType ExtractType => ExtractType.Covid;
        public string Docket  => "NDWH";
        public string DocketExtract => nameof(CovidExtract);

        public int GetProgress(int count, int total)
        {
            if (total == 0)
                return stake;

            var percentageStake=  ((float)count / (float)total) * stake;
            return (int) percentageStake;
        }

        public CovidsMessageBag()
        {
        }

        public CovidsMessageBag(IMessage<CovidExtractView> message)
        {
            Message = message;
        }

        public CovidsMessageBag(List<IMessage<CovidExtractView>> messages)
        {
            Messages = messages;
        }

        public CovidsMessageBag(CovidsMessage message)
        {
            Message = message;
        }

        public static CovidsMessageBag Create(PatientExtractView patient)
        {
            var message = new CovidsMessage(patient);
            return new CovidsMessageBag(message);
        }


        public IMessageBag<CovidExtractView> Generate(List<CovidExtractView> extracts)
        {
            var messages = new List<IMessage<CovidExtractView>>();
            foreach (var artExtractView in extracts)
            {
                var message = new CovidsMessage(artExtractView);
                messages.Add(message);
            }

            return new CovidsMessageBag(messages);
        }

        private List<Guid> GetIds()
        {
            var ids= Messages.SelectMany(x => x.SendIds).ToList();
            return ids;
        }
    }
}
