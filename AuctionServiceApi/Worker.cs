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

namespace AuctionServiceWorker
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
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            var factory = new ConnectionFactory()
            {
                HostName = _config["rabbithostname"],
                UserName = "worker",
                Password = "1234",
                VirtualHost = ConnectionFactory.DefaultVHost
            };

            using var connection = factory.CreateConnection();
            _connection = connection;
            _channel = connection.CreateModel();

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

                            // Perform any required processing with the bidAmount and auctionId
                            // ...

                            // Acknowledge the message
                            _channel.BasicAck(ea.DeliveryTag, multiple: false);
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

                _channel.BasicConsume(queue: "bid-data-queue", autoAck: false, consumer: consumer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }

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
