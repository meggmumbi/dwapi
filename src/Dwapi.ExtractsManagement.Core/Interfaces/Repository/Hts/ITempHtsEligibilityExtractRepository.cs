﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dwapi.ExtractsManagement.Core.Model.Source.Hts.NewHts;
using Dwapi.SharedKernel.Interfaces;

namespace Dwapi.ExtractsManagement.Core.Interfaces.Repository.Hts
{
    public interface ITempHtsEligibilityExtractRepository : IRepository<TempHtsEligibilityExtract, Guid>
    {
        Task Clear();
        bool BatchInsert(IEnumerable<TempHtsEligibilityExtract> extracts);
        Task<int> GetCleanCount();
    }
}