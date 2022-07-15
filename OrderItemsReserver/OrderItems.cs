using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;
using Azure.Storage.Blobs;

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
            await blob.UploadAsync(orderStream);
            log.LogInformation("file uploaded successfylly.");
            return new OkObjectResult("file uploaded successfylly");
        }
    }
}
