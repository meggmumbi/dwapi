﻿using Dwapi.SharedKernel.Model;
using MediatR;

namespace Dwapi.ExtractsManagement.Core.Commands.Dwh
{
    public class ExtractPatient : IRequest<bool>
    {
        public DbExtract Extract { get; set; }
        public DbProtocol DatabaseProtocol { get; set; }
        public bool LoadChangesOnly { get; set; }

        public bool IsValid()
        {
            return null != Extract && null != DatabaseProtocol;
        }
    }
}
