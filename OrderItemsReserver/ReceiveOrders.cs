using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Polly;

namespace OrderItemsReserver
{
    public class ReceiveOrders
    {
        [FunctionName("ReceiveOrders")]
        public static async Task RunAsync([ServiceBusTrigger("orderitems", Connection = "ServiceBusConnectionString")] string myQueueItem, ILogger log)
        {
            log.LogInformation($"C# ServiceBus queue trigger function processed message: {myQueueItem}");
            string Connection = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            string containerName = Environment.GetEnvironmentVariable("ContainerName");
            log.LogInformation($"Connection={Connection}, containerName={containerName}.");
            byte[] byteArray = Encoding.ASCII.GetBytes(myQueueItem);
            log.LogInformation($"orderJson = {myQueueItem}.");
            Stream orderStream = new MemoryStream(byteArray);
            var blobClient = new BlobContainerClient(Connection, containerName);
            var blob = blobClient.GetBlobClient("order_" + DateTime.Now.ToString("yyyyMMddHHmmssff") + ".json");
            log.LogInformation($"blob = {blob.Name}.");

            var result = await Policy
                .Handle<Exception>()
                .RetryAsync(3, onRetry: (exception, retryCount) =>
                {
                    // Add logic to be executed before each retry, such as logging
                    log.LogInformation($"Exception: {exception}, retryCount: {retryCount}.");
                })
                .ExecuteAndCaptureAsync(async () =>
                {
                    await blob.UploadAsync(orderStream);
                    log.LogInformation($"uploaded message: {myQueueItem}");
                });

            string ServiceBusConnectionString = Environment.GetEnvironmentVariable("ServiceBusConnectionString");
            string FailedQueueName = Environment.GetEnvironmentVariable("itemsmessagesfailed");
            var client = new ServiceBusClient(ServiceBusConnectionString);
            ServiceBusSender sender = client.CreateSender(FailedQueueName);

            if (result.Outcome == OutcomeType.Failure)
            {
                var message = new ServiceBusMessage(myQueueItem);
                // send to service bus queue
                await sender.SendMessageAsync(message);
                log.LogInformation($"Sending message: {message}");
            }
            else
            {
                log.LogInformation("file uploaded successfylly.");
            }
        }
    }
}