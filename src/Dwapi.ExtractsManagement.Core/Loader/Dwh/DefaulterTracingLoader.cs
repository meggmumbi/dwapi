﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using Dwapi.ExtractsManagement.Core.Application.Events;
using Dwapi.ExtractsManagement.Core.Interfaces.Loaders.Dwh;
using Dwapi.ExtractsManagement.Core.Interfaces.Repository.Dwh;
using Dwapi.ExtractsManagement.Core.Model.Destination.Dwh;
using Dwapi.ExtractsManagement.Core.Model.Source.Dwh;
using Dwapi.ExtractsManagement.Core.Notifications;
using Dwapi.ExtractsManagement.Core.Profiles;
using Dwapi.SharedKernel.Enum;
using Dwapi.SharedKernel.Events;
using Dwapi.SharedKernel.Model;
using Dwapi.SharedKernel.Utility;
using MediatR;
using Serilog;

namespace Dwapi.ExtractsManagement.Core.Loader.Dwh
{
    public class DefaulterTracingLoader : IDefaulterTracingLoader
    {
        private readonly IDefaulterTracingExtractRepository _DefaulterTracingExtractRepository;
        private readonly ITempDefaulterTracingExtractRepository _tempDefaulterTracingExtractRepository;
        private readonly IMediator _mediator;

        public DefaulterTracingLoader(IDefaulterTracingExtractRepository DefaulterTracingExtractRepository, ITempDefaulterTracingExtractRepository tempDefaulterTracingExtractRepository, IMediator mediator)
        {
            _DefaulterTracingExtractRepository = DefaulterTracingExtractRepository;
            _tempDefaulterTracingExtractRepository = tempDefaulterTracingExtractRepository;
            _mediator = mediator;
        }

        public async Task<int> Load(Guid extractId, int found, bool diffSupport)
        {
            int count = 0; var mapper = diffSupport ? ExtractDiffMapper.Instance : ExtractMapper.Instance;

            try
            {
                DomainEvents.Dispatch(
                    new ExtractActivityNotification(extractId, new DwhProgress(
                        nameof(DefaulterTracingExtract),
                        nameof(ExtractStatus.Loading),
                        found, 0, 0, 0, 0)));


                StringBuilder query = new StringBuilder();
                query.Append($" SELECT s.* FROM {nameof(TempDefaulterTracingExtract)}s s");
                query.Append($" INNER JOIN PatientExtracts p ON ");
                query.Append($" s.PatientPK = p.PatientPK AND ");
                query.Append($" s.SiteCode = p.SiteCode ");

                const int take = 1000;
                var eCount = await  _tempDefaulterTracingExtractRepository.GetCount(query.ToString());
                var pageCount = _tempDefaulterTracingExtractRepository.PageCount(take, eCount);

                int page = 1;
                while (page <= pageCount)
                {
                    var tempDefaulterTracingExtracts =await
                        _tempDefaulterTracingExtractRepository.ReadAll(query.ToString(), page, take);

                    var batch = tempDefaulterTracingExtracts.ToList();
                    count += batch.Count;

                    //Auto mapper
                    var extractRecords = mapper.Map<List<TempDefaulterTracingExtract>, List<DefaulterTracingExtract>>(batch);
                    foreach (var record in extractRecords)
                    {
                        record.Id = LiveGuid.NewGuid();
                    }
                    //Batch Insert
                    var inserted = _DefaulterTracingExtractRepository.BatchInsert(extractRecords);
                    if (!inserted)
                    {
                        Log.Error($"Extract {nameof(DefaulterTracingExtract)} not Loaded");
                        return 0;
                    }
                    Log.Debug("saved batch");
                    page++;
                    DomainEvents.Dispatch(
                        new ExtractActivityNotification(extractId, new DwhProgress(
                            nameof(DefaulterTracingExtract),
                            nameof(ExtractStatus.Loading),
                            found, count , 0, 0, 0)));
                }

                await _mediator.Publish(new DocketExtractLoaded("NDWH", nameof(DefaulterTracingExtract), 10639));

                return count;
            }
            catch (Exception e)
            {
                Log.Error(e, $"Extract {nameof(DefaulterTracingExtract)} not Loaded");
                return 0;
            }
        }
    }
}
