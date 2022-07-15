﻿using Dwapi.ExtractsManagement.Core.Interfaces.Repository.Hts;
using Dwapi.ExtractsManagement.Core.Model.Source.Hts.NewHts;
using Dwapi.ExtractsManagement.Infrastructure.Repository.Hts.Validations.Base;

namespace Dwapi.ExtractsManagement.Infrastructure.Repository.Hts.Validations
{ 
    public class TempHtsEligibilityExtractErrorSummaryRepository : TempHTSExtractErrorSummaryRepository<TempHtsEligibilityExtractErrorSummary>, ITempHtsEligibilityExtractErrorSummaryRepository
    {
        public TempHtsEligibilityExtractErrorSummaryRepository(ExtractsContext context) : base(context)
        {
        }
    }
}