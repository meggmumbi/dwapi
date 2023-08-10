﻿using System;

namespace Dwapi.Contracts.Ct
{
    public interface IIITRiskScores
    {
        string FacilityName { get; set; }
        string SourceSysUUID { get; set; }
        decimal? RiskScore  { get; set; }
        string RiskFactors  { get; set; }
        string RiskDescription  { get; set; }
        DateTime? RiskEvaluationDate  { get; set; }
        DateTime? Date_Created  { get; set; }
        DateTime? Date_Last_Modified  { get; set; }
    }
}