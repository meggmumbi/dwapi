using Dwapi.ExtractsManagement.Core.Interfaces.Repository.Dwh;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Dwapi.Controller.ExtractDetails
{
    [Produces("application/json")]
    [Route("api/CervicalCancerScreening")]
    public class CervicalCancerScreeningController : Microsoft.AspNetCore.Mvc.Controller
    {
        private readonly ITempCervicalCancerScreeningExtractRepository _tempCervicalCancerScreeningExtractRepository;
        private readonly ICervicalCancerScreeningExtractRepository _cervicalCancerScreeningExtractRepository;
        private readonly ITempCervicalCancerScreeningExtractErrorSummaryRepository _errorSummaryRepository;

        public CervicalCancerScreeningController(ITempCervicalCancerScreeningExtractRepository tempCervicalCancerScreeningExtractRepository, ICervicalCancerScreeningExtractRepository CervicalCancerScreeningExtractRepository, ITempCervicalCancerScreeningExtractErrorSummaryRepository errorSummaryRepository)
        {
            _tempCervicalCancerScreeningExtractRepository = tempCervicalCancerScreeningExtractRepository;
            _cervicalCancerScreeningExtractRepository = CervicalCancerScreeningExtractRepository;
            _errorSummaryRepository = errorSummaryRepository;
        }

        [HttpGet("ValidCount")]
        public async Task<IActionResult> GetValidCount()
        {
            try
            {
                var count = await _cervicalCancerScreeningExtractRepository.GetCount();
                return Ok(count);
            }
            catch (Exception e)
            {
                var msg = $"Error loading valid Patient Extracts";
                Log.Error(msg);
                Log.Error($"{e}");
                return StatusCode(500, msg);
            }
        }

        [HttpGet("LoadValid/{page}/{pageSize}")]
        public async Task<IActionResult> LoadValid(int? page,int pageSize)
        {
            try
            {
                var tempCervicalCancerScreeningExtracts = await _cervicalCancerScreeningExtractRepository.GetAll(page,pageSize);
                return Ok(tempCervicalCancerScreeningExtracts.ToList());
            }
            catch (Exception e)
            {
                var msg = $"Error loading valid CervicalCancerScreening Extracts";
                Log.Error(msg);
                Log.Error($"{e}");
                return StatusCode(500, msg);
            }
        }

        [HttpGet("LoadErrors")]
        public IActionResult LoadErrors()
        {
            try
            {
                var tempCervicalCancerScreeningExtracts = _tempCervicalCancerScreeningExtractRepository.GetAll().Where(n => n.CheckError).ToList();
                return Ok(tempCervicalCancerScreeningExtracts);
            }
            catch (Exception e)
            {
                var msg = $"Error loading CervicalCancerScreening Extracts with errors";
                Log.Error(msg);
                Log.Error($"{e}");
                return StatusCode(500, msg);
            }
        }

        [HttpGet("LoadValidations")]
        public IActionResult LoadValidations()
        {
            try
            {
                var errorSummary = _errorSummaryRepository.GetAll().OrderByDescending(x=>x.Type).ToList();
                return Ok(errorSummary);
            }
            catch (Exception e)
            {
                var msg = $"Error loading Patient Status error summary";
                Log.Error(msg);
                Log.Error($"{e}");
                return StatusCode(500, msg);
            }
        }
    }
}
