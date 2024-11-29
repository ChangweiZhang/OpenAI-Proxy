using Azure.Messaging.EventHubs.Producer;

namespace AOAIProxy.Services
{
    public class EventHubService
    {
        private readonly ILogger<EventHubService> logger;
        EventHubProducerClient producerClient;
        public EventHubService(IConfiguration configuration,ILogger<EventHubService> logger)
        {
            var eventHubConStr = configuration.GetValue<string>("EventHubCredential");
            producerClient = new EventHubProducerClient(
                             eventHubConStr,
                            "openai");
            this.logger = logger;
        }


        public async Task<bool> SendAsync(string message)
        {
            try
            {
                using EventDataBatch eventBatch = await producerClient.CreateBatchAsync();
                eventBatch.TryAdd(new Azure.Messaging.EventHubs.EventData(message));
                await producerClient.SendAsync(eventBatch);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "send eventhub message error");
                return false;
            }
            finally
            {
                await producerClient.CloseAsync();
            }
            return true;
        }


    }
}
