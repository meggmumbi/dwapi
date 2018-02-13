﻿using System;
using Dwapi.ExtractsManagement.Core.Interfaces.Stage.Repository;
using Dwapi.ExtractsManagement.Core.Model.Stage.Psmart;

namespace Dwapi.ExtractsManagement.Core.Interfaces.Stage.Psmart.Repository
{
    public interface IPsmartStageRepository:IStageRepository<PsmartStage,Guid>
    {
    }
}