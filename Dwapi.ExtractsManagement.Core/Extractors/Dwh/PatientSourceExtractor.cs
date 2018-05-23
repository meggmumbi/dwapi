﻿using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using AutoMapper;
using Dwapi.Domain.Utils;
using Dwapi.ExtractsManagement.Core.Interfaces.Extratcors;
using Dwapi.ExtractsManagement.Core.Interfaces.Reader.Dwh;
using Dwapi.ExtractsManagement.Core.Interfaces.Repository.Dwh;
using Dwapi.ExtractsManagement.Core.Model.Source.Dwh;
using Dwapi.ExtractsManagement.Core.Notifications;
using Dwapi.SharedKernel.Model;
using MediatR;
using Serilog;

namespace Dwapi.ExtractsManagement.Core.Extractors.Dwh
{
    public class PatientSourceExtractor : IPatientSourceExtractor
    {
        private readonly IPatientSourceReader _reader;
        private readonly IMediator _mediator;
        private readonly ITempPatientExtractRepository _extractRepository;

        public PatientSourceExtractor(IPatientSourceReader reader, IMediator mediator, ITempPatientExtractRepository extractRepository)
        {
            _reader = reader;
            _mediator = mediator;
            _extractRepository = extractRepository;
        }

        public async Task<bool> Extract(DbExtract extract, DbProtocol dbProtocol)
        {
            int batch = 500;
            // TODO: Notify started...


            var list = new List<TempPatientExtract>();

            int count = 0;

            using (var rdr = await _reader.ExecuteReader(dbProtocol, extract))
            {
                while (rdr.Read())
                {
                    count++;        
                    // AutoMapper profiles
                    var extractRecord = Mapper.Map<IDataRecord, TempPatientExtract>(rdr);
                    extractRecord.Id = LiveGuid.NewGuid();
                    list.Add(extractRecord);

                    if (count == batch)
                    {
                        // TODO: batch and save
                        _extractRepository.BatchInsert(list);

                        count = 0;
                        Log.Debug("saved batch");
                    }
                    // TODO: Notify progress...

                    await _mediator.Publish(
                        new ExtractActivityNotification(new DwhProgress(
                            nameof(PatientExtract),
                            "loading",
                            list.Count)));
                }

                if (count > 0)
                {
                    // save remaining list;
                    _extractRepository.BatchInsert(list);
                }
                _extractRepository.CloseConnection();
            }

            // TODO: Notify Completed;

            return true;
        }
    }
}