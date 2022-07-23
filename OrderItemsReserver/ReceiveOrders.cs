using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace OrderItemsReserver
{
    public class ReceiveOrders
    {
        [FunctionName("ReceiveOrders")]
        public void Run([ServiceBusTrigger("orderitemsfailed", Connection = "ServiceBusConnectionString")]string myQueueItem, ILogger log)
        {
            log.LogInformation($"C# ServiceBus queue trigger function processed message: {myQueueItem}");
        }
    }
}
