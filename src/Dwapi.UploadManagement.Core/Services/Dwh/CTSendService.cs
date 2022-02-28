using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Dwapi.ExtractsManagement.Core.Application.Events;
using Dwapi.ExtractsManagement.Core.Model.Destination.Dwh;
using Dwapi.ExtractsManagement.Core.Notifications;
using Dwapi.SettingsManagement.Core.Application.Metrics.Events;
using Dwapi.SharedKernel.DTOs;
using Dwapi.SharedKernel.Enum;
using Dwapi.SharedKernel.Events;
using Dwapi.SharedKernel.Exchange;
using Dwapi.SharedKernel.Model;
using Dwapi.SharedKernel.Utility;
using Dwapi.UploadManagement.Core.Event.Dwh;
using Dwapi.UploadManagement.Core.Exceptions;
using Dwapi.UploadManagement.Core.Exchange.Dwh;
using Dwapi.UploadManagement.Core.Interfaces.Exchange;
using Dwapi.UploadManagement.Core.Interfaces.Packager.Dwh;
using Dwapi.UploadManagement.Core.Interfaces.Reader;
using Dwapi.UploadManagement.Core.Interfaces.Services.Dwh;
using Dwapi.UploadManagement.Core.Notifications.Dwh;
using MediatR;
using Serilog;

namespace Dwapi.UploadManagement.Core.Services.Dwh
{
    public class CTSendService : ICTSendService
    {
        private readonly string _endPoint;
        private readonly IDwhPackager _packager;
        private readonly IMediator _mediator;
        private IEmrMetricReader _reader;

        public HttpClient Client { get; set; }

        public CTSendService(IDwhPackager packager, IMediator mediator, IEmrMetricReader reader)
        {
            _packager = packager;
            _mediator = mediator;
            _reader = reader;
            _endPoint = "api/";
        }

        public Task<List<SendDhwManifestResponse>> SendManifestAsync(SendManifestPackageDTO sendTo)
        {
            return SendManifestAsync(sendTo,
                DwhManifestMessageBag.Create(_packager.GenerateWithMetrics(sendTo.GetEmrDto()).ToList()));
        }

        public async Task<List<SendDhwManifestResponse>> SendManifestAsync(SendManifestPackageDTO sendTo,
            DwhManifestMessageBag messageBag)
        {
            var responses = new List<SendDhwManifestResponse>();
            HttpClientHandler handler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            var client = Client ?? new HttpClient(handler);
            client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
            client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
            foreach (var message in messageBag.Messages)
            {
                try
                {
                    var response = await client.PostAsJsonAsync(sendTo.GetUrl($"{_endPoint.HasToEndsWith("/")}spot"),
                        message.Manifest);
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        responses.Add(new SendDhwManifestResponse(content));
                    }
                    else
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        throw new Exception(error);
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e, $"Send Manifest Error");
                    throw;
                }
            }

            return responses;
        }

        public void NotifyPreSending()
        {
            DomainEvents.Dispatch(new DwhMessageNotification(false, $"Sending started..."));

        }

        public async Task<List<SendCTResponse>> SendBatchExtractsAsync<T>(
            SendManifestPackageDTO sendTo,
            int batchSize,
            IMessageBag<T> messageBag)
            where T : ClientExtract
        {
            HttpClientHandler handler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            var client = Client ?? new HttpClient(handler);

            var responses = new List<SendCTResponse>();
            var packageInfo = _packager.GetPackageInfo<T>(batchSize);
            int sendCound = 0;
            int count = 0;
            int total = packageInfo.PageCount;
            int overall = 0;

            DomainEvents.Dispatch(new CTStatusNotification(sendTo.ExtractId, sendTo.GetExtractId(messageBag.ExtractName), ExtractStatus.Sending));
            long recordCount = 0;

            try
            {
                for (int page = 1; page <= packageInfo.PageCount; page++)
                {
                    count++;
                    var extracts = _packager.GenerateBatchExtracts<T>(page, packageInfo.PageSize).ToList();
                    recordCount = recordCount + extracts.Count;
                    Log.Debug(
                        $">>>> Sending {messageBag.ExtractName} {recordCount}/{packageInfo.TotalRecords} Page:{page} of {packageInfo.PageCount}");
                    messageBag = messageBag.Generate(extracts);
                    var message = messageBag.Messages;
                    try
                    {
                        int retryCount = 0;
                        bool allowSend = true;
                        while (allowSend)
                        {
                            var response = await client.PostAsJsonAsync(
                                sendTo.GetUrl($"{_endPoint.HasToEndsWith("/")}v2/{messageBag.EndPoint}"), message);
                            if (response.IsSuccessStatusCode)
                            {
                                allowSend = false;
                                responses.Add(new SendCTResponse());

                                var sentIds = messageBag.SendIds;
                                sendCound += sentIds.Count;
                                DomainEvents.Dispatch(new CTExtractSentEvent(sentIds, SendStatus.Sent,
                                    messageBag.ExtractType));
                            }
                            else
                            {
                                retryCount++;
                                if (retryCount == 4)
                                {
                                    var sentIds = messageBag.SendIds;
                                    var error = await response.Content.ReadAsStringAsync();
                                    DomainEvents.Dispatch(new CTExtractSentEvent(
                                        sentIds, SendStatus.Failed, messageBag.ExtractType,
                                        error));
                                    throw new Exception(error);
                                }
                            }
                        }

                    }
                    catch (Exception e)
                    {
                        Log.Error(e, $"Send Extracts{messageBag.ExtractName} Error");
                        throw;
                    }

                    DomainEvents.Dispatch(new CTSendNotification(new SendProgress(messageBag.ExtractName,messageBag.GetProgress(count, total),recordCount)));

                }

                await _mediator.Publish(new DocketExtractSent(messageBag.Docket, messageBag.DocketExtract));
            }
            catch (Exception e)
            {
                Log.Error(e, $"Send Extracts {messageBag.ExtractName} Error");
                throw;
            }

            DomainEvents.Dispatch(new CTSendNotification(new SendProgress(messageBag.ExtractName,
                messageBag.GetProgress(count, total), recordCount,true)));

            DomainEvents.Dispatch(new CTStatusNotification(sendTo.ExtractId,sendTo.GetExtractId(messageBag.ExtractName), ExtractStatus.Sent, sendCound)
                {UpdatePatient = (messageBag is ArtMessageBag || messageBag is BaselineMessageBag || messageBag is StatusMessageBag)}
            );

            return responses;
        }

        public async Task<List<SendCTResponse>> SendDiffBatchExtractsAsync<T>(SendManifestPackageDTO sendTo, int batchSize, IMessageBag<T> messageBag) where T : ClientExtract
        {
          HttpClientHandler handler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            var client = Client ?? new HttpClient(handler);

            var responses = new List<SendCTResponse>();
            var packageInfo = _packager.GetPackageInfo<T>(batchSize);
            int sendCound = 0;
            int count = 0;
            int total = packageInfo.PageCount;
            int overall = 0;

            DomainEvents.Dispatch(new CTStatusNotification(sendTo.ExtractId, sendTo.GetExtractId(messageBag.ExtractName), ExtractStatus.Sending));
            long recordCount = 0;

            try
            {
                for (int page = 1; page <= packageInfo.PageCount; page++)
                {
                    count++;
                    var extracts = _packager.GenerateDiffBatchExtracts<T>(page, packageInfo.PageSize, messageBag.Docket,
                        messageBag.DocketExtract).ToList();
                    recordCount = recordCount + extracts.Count;

                    if (!extracts.Any())
                    {
                        count = total;
                        recordCount = packageInfo.TotalRecords;
                        DomainEvents.Dispatch(new CTSendNotification(new SendProgress(messageBag.ExtractName,messageBag.GetProgress(count, total),recordCount)));
                        break;
                    }

                    Log.Debug(
                        $">>>> Sending {messageBag.ExtractName} {recordCount}/{packageInfo.TotalRecords} Page:{page} of {packageInfo.PageCount}");
                    messageBag = messageBag.Generate(extracts);
                    var message = messageBag.Messages;
                    try
                    {
                        int retryCount = 0;
                        bool allowSend = true;
                        while (allowSend)
                        {
                            var response = await client.PostAsJsonAsync(
                                sendTo.GetUrl($"{_endPoint.HasToEndsWith("/")}v2/{messageBag.EndPoint}"), message);
                            if (response.IsSuccessStatusCode)
                            {
                                allowSend = false;
                                // var content = await response.Content.ReadAsJsonAsync<SendCTResponse>();
                                responses.Add(new SendCTResponse());

                                var sentIds = messageBag.SendIds;
                                sendCound += sentIds.Count;
                                DomainEvents.Dispatch(new CTExtractSentEvent(sentIds, SendStatus.Sent,
                                    messageBag.ExtractType));
                            }
                            else
                            {
                                retryCount++;
                                if (retryCount == 4)
                                {
                                    var sentIds = messageBag.SendIds;
                                    var error = await response.Content.ReadAsStringAsync();
                                    DomainEvents.Dispatch(new CTExtractSentEvent(
                                        sentIds, SendStatus.Failed, messageBag.ExtractType,
                                        error));
                                    throw new Exception(error);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, $"Send Extracts{messageBag.ExtractName} Error");
                        throw;
                    }

                    DomainEvents.Dispatch(new CTSendNotification(new SendProgress(messageBag.ExtractName,messageBag.GetProgress(count, total),recordCount)));

                }

                await _mediator.Publish(new DocketExtractSent(messageBag.Docket, messageBag.DocketExtract));
            }
            catch (Exception e)
            {
                Log.Error(e, $"Send Extracts {messageBag.ExtractName} Error");
                throw;
            }

            DomainEvents.Dispatch(new CTSendNotification(new SendProgress(messageBag.ExtractName,
                messageBag.GetProgress(count, total), recordCount,true)));

            DomainEvents.Dispatch(new CTStatusNotification(sendTo.ExtractId,sendTo.GetExtractId(messageBag.ExtractName), ExtractStatus.Sent, sendCound)
                {UpdatePatient = (messageBag is ArtMessageBag || messageBag is BaselineMessageBag || messageBag is StatusMessageBag)}
            );

            return responses;
        }

        public async Task NotifyPostSending(SendManifestPackageDTO sendTo,string version)
        {
            int maxRetries = 4;
            int retries = 0;
            var notificationend = new HandshakeEnd("CTSendEnd", version);
            DomainEvents.Dispatch(new DwhMessageNotification(false, $"Sending completed"));
            await _mediator.Publish(new HandshakeEnd("CTSendEnd", version));

            Thread.Sleep(3000);

            var client = Client ?? new HttpClient();

            while (retries < maxRetries)
            {
                try
                {
                    var session = _reader.GetSession(notificationend.EndName);
                    var response =
                        await client.PostAsync(
                            sendTo.GetUrl($"{_endPoint.HasToEndsWith("/")}Handshake?session={session}"), null);
                    retries++;
                }
                catch (Exception e)
                {

                    Log.Error(e, $"Send Handshake Error");
                }
            }

        }
    }
}
