﻿using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dwapi.ExtractsManagement.Core.Interfaces.Repository;
using Dwapi.ExtractsManagement.Core.Interfaces.Validators.Prep;
using Dwapi.ExtractsManagement.Core.Notifications;
using Dwapi.SharedKernel.Enum;
using Dwapi.SharedKernel.Events;
using Dwapi.SharedKernel.Model;
using Serilog;

namespace Dwapi.ExtractsManagement.Infrastructure.Validators.Prep
{
    public class PrepExtractValidator : IPrepExtractValidator
    {
        private readonly IValidatorRepository _validatorRepository;

        public PrepExtractValidator(IValidatorRepository validatorRepository)
        {
            _validatorRepository = validatorRepository;
        }

        public async Task<bool> Validate(Guid extractId, int extracted, string extractName, string sourceTable)
        {
            DomainEvents.Dispatch(
                new PrepExtractActivityNotification(extractId, new ExtractProgress(
                    extractName,
                    nameof(ExtractStatus.Validating),
                    extracted, 0, 0, 0, 0)));
            try
            {
                var validators = _validatorRepository.GetByExtract(sourceTable);
                int count = 0;
                var validatorList = validators.ToList();

                foreach (var validator in validatorList)
                {
                    using (var command = _validatorRepository.GetConnection().CreateCommand())
                    {
                        var provider = _validatorRepository.GetConnectionProvider();
                        command.CommandText = validator.GenerateValidateSql(provider);

                        try
                        {
                            if(command.Connection.State!=ConnectionState.Open)
                                command.Connection.Open();
                            await GetTask(command);
                        }
                        catch (Exception e)
                        {
                            Log.Debug(e.Message);
                            throw;
                        }
                        count++;
                    }
                }
                DomainEvents.Dispatch(
                    new PrepExtractActivityNotification(extractId, new ExtractProgress(
                        extractName,
                        nameof(ExtractStatus.Validated),
                        extracted, 0, 0, 0, 0)));
                return true;
            }
            catch (Exception e)
            {
                DomainEvents.Dispatch(
                    new PrepExtractActivityNotification(extractId, new ExtractProgress(
                        extractName,
                        nameof(ExtractStatus.Idle),
                        extracted, 0, 0, 0, 0)));
                Console.WriteLine(e);
                return false;
            }
        }

        private Task<int> GetTask(IDbCommand command)
        {
            return Task.Run(() => command.ExecuteNonQuery());
        }
    }
}
