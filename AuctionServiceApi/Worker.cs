using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BidServiceWorker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _config;
        private IConnection _connection;
        private IModel _channel;

        public Worker(ILogger<Worker> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;
            _logger.LogInformation($"Connecting to RabbitMQ on {_config["rabbithostname"]}");

        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); // 10 sekunder delay på connect til rabbitMQ - fikser coreDump-fejl

            var factory = new ConnectionFactory()
            {
                HostName = _config["rabbithostname"],
                UserName = "worker",
                Password = "1234",
                VirtualHost = ConnectionFactory.DefaultVHost
            };
            _logger.LogInformation($"Using {_config["rabbithostname"]}");



            using var connection = factory.CreateConnection();
            _connection = connection;
            _channel = connection.CreateModel();


            _channel.QueueDeclare(queue: "new-bid-queue",
                            durable: false,
                            exclusive: false,
                            autoDelete: false,
                            arguments: null);

            _channel.QueueDeclare(queue: "bid-data-queue",
                            durable: false,
                            exclusive: false,
                            autoDelete: false,
                            arguments: null);

            try
            {
                var consumer = new EventingBasicConsumer(_channel);
                consumer.Received += (model, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    Console.WriteLine($" [x] Received {message}");

                    // Parse the received message as JSON
                    var jsonDocument = JsonDocument.Parse(message);
                    /*
                                        // Extract the "BidAmount" value
                                        if (jsonDocument.RootElement.TryGetProperty("BidAmount", out var bidAmountProperty) && bidAmountProperty.ValueKind == JsonValueKind.Number)
                                        {
                                            var bidAmount = bidAmountProperty.GetInt32(); // Assumes the "BidAmount" is an integer
                                            Console.WriteLine($" [x] Received BidAmount: {bidAmount}");

                                            // Extract the "AuctionId" value
                                            if (jsonDocument.RootElement.TryGetProperty("AuctionId", out var auctionIdProperty) && auctionIdProperty.ValueKind == JsonValueKind.Number)
                                            {
                                                var auctionId = auctionIdProperty.GetInt32(); // Assumes the "AuctionId" is an integer
                                                Console.WriteLine($" [x] Received AuctionId: {auctionId}");

                                                // Send the bidAmount and auctionId back to RabbitMQ or perform any required processing
                                               // PublishBidAmountAndAuctionId(bidAmount, auctionId);
                                            }
                                            else
                                            {
                                                Console.WriteLine("Invalid or missing AuctionId property in the received message.");
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine("Invalid or missing BidAmount property in the received message.");
                                        }
                                    };
                    */
                };
                    _channel.BasicConsume(queue: "bid-data-queue", autoAck: true, consumer: consumer);
                
            }
            catch (Exception ex)
            {

                _logger.LogError(ex.Message);

            }    // Background service loop
            while (!stoppingToken.IsCancellationRequested)
            {
                //      _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                await Task.Delay(1000, stoppingToken);
            }

        }
        /*
        private void PublishBidAmountAndAuctionId(int bidAmount, int auctionId)
        {
            // Create a new message with the extracted bid amount and auction ID
            var bidData = new
            {
                BidAmount = bidAmount,
                AuctionId = auctionId
            };
            var bidDataJson = JsonSerializer.Serialize(bidData);
            var body = Encoding.UTF8.GetBytes(bidDataJson);

            // Publish the message to the desired queue
            _channel.BasicPublish(
                exchange: "",
                routingKey: "bid-data-queue", // Specify the queue to which you want to send the message
                basicProperties: null,
                body: body);

            Console.WriteLine($" [x] Published BidAmount: {bidAmount} and AuctionId: {auctionId} to bid-data-queue");
        }
        */
        public override void Dispose()
        {
            _channel?.Close();
            _channel?.Dispose();
            _connection?.Close();
            _connection?.Dispose();
            base.Dispose();
        }
    }
}
