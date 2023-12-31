namespace AspireSample.FileProcessorService;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Options;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly string _folderPath;
    private readonly string _serviceBusConnectionString;
    private readonly string _queueName;
    private readonly IQueueClient _queueClient;

    public Worker(ILogger<Worker> logger, IOptions<ServiceConfiguration> options,
        IOptions<ConnectionStrings> connections)
    {
        _logger = logger;
        _folderPath = options.Value.FolderPath; // Replace with the folder path you want to monitor
        _serviceBusConnectionString = connections.Value.ServiceBusConnectionString;
        _queueName = options.Value.QueueName;
        _queueClient = new QueueClient(_serviceBusConnectionString, _queueName);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

            // Check the folder for new files
            var files = Directory.GetFiles(_folderPath);

            foreach (var file in files)
            {
                // Send a message to Azure Service Bus for each file
                await SendMessageAsync(file);
            }

            // Wait for one minute before checking again
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task SendMessageAsync(string filePath)
    {
        try
        {
            // Construct the message
            var message = new Message
            {
                Body = System.Text.Encoding.UTF8.GetBytes($"{new FileInfo(filePath).Length}"),
                ContentType = "text/plain",
                Label = $"{filePath}",
            };

            // Send the message to the queue
            await _queueClient.SendAsync(message);
            _logger.LogInformation($"Message sent for file: {filePath}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error sending message: {ex.Message}");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _queueClient.CloseAsync();
        await base.StopAsync(cancellationToken);
    }
}
