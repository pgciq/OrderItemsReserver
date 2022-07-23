using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text;
using Azure.Storage.Blobs;
using System.Text.Json;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents;
using Polly;
using Azure;
using Azure.Messaging.ServiceBus;

namespace OrderItemsReserver
{
    public static class OrderItems
    {
        [FunctionName("OrderItems")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            string Connection = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            string containerName = Environment.GetEnvironmentVariable("ContainerName");
            log.LogInformation($"Connection={Connection}, containerName={containerName}.");
            string orderDetail;
            using (StreamReader stream = new StreamReader(req.Body))
            {
                orderDetail = stream.ReadToEnd();
            }
            log.LogInformation($"orderDetail = {orderDetail}.");

            byte[] byteArray = Encoding.ASCII.GetBytes(orderDetail);
            log.LogInformation($"orderJson = {orderDetail}.");
            Stream orderStream = new MemoryStream(byteArray);
            var blobClient = new BlobContainerClient(Connection, containerName);
            var blob = blobClient.GetBlobClient("order_" + orderDetail.GetHashCode()+".json");
            log.LogInformation($"blob = {blob.Name}.");

            string ServiceBusConnectionString = Environment.GetEnvironmentVariable("ServiceBusConnectionString");
            string QueueName = Environment.GetEnvironmentVariable("itemsmessages");
            await using var client = new ServiceBusClient(ServiceBusConnectionString);
            await using ServiceBusSender sender = client.CreateSender(QueueName);
            var message = new ServiceBusMessage(orderDetail);


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
                    log.LogInformation($"uploaded message: {orderDetail}");
                });

            if (result.Outcome == OutcomeType.Failure)
            {
                // send to service bus queue
                await sender.SendMessageAsync(message);
                log.LogInformation($"Sending message: {message}");
                return new OkObjectResult("send message successfylly");
            }
            else
            {
                log.LogInformation("file uploaded successfylly.");
                return new OkObjectResult("file uploaded successfylly");
            }



            /*
            string accountEndpoint = Environment.GetEnvironmentVariable("accountEndpoint");
            string accountKey = Environment.GetEnvironmentVariable("accountKey");
            DocumentClient client = new DocumentClient(new Uri(accountEndpoint), accountKey);
            await client.CreateDatabaseIfNotExistsAsync(new Database { Id = "Orders" });
            await client.CreateDocumentCollectionIfNotExistsAsync(UriFactory.CreateDatabaseUri("Orders"), new DocumentCollection { Id = "orderDetail" });
            Order order = JsonSerializer.Deserialize<Order>(orderDetail);
            await client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri("Orders", "orderDetail"), order); 
            */
        }
    }



    public class CatalogItemOrdered
    {
        public int CatalogItemId { get; set; }
        public string ProductName { get; set; }
        public string PictureUri { get; set; }
    }


    public class OrderItem
    {
        public int Id { get; set; }
        public CatalogItemOrdered ItemOrdered { get; set; }
        public decimal UnitPrice { get; set; }
        public int Units { get; set; }
    }

    public class Address
    {
        public string Street { get; set; }

        public string City { get; set; }

        public string State { get; set; }

        public string Country { get; set; }

        public string ZipCode { get; set; }
    }

    public class Order
    {
        public int Id { get; set; }
        public string BuyerId { get; set; }
        public DateTimeOffset OrderDate { get; set; }
        public Address ShipToAddress { get; set; }

        public OrderItem[] OrderItems { get; set; }
        public decimal Total()
        {
            var total = 0m;
            foreach (var item in OrderItems)
            {
                total += item.UnitPrice * item.Units;
            }
            return total;
        }
    }

}
