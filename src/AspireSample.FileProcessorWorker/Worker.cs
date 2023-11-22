namespace AspireSample.FileProcessorWorker;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly string _serviceBusConnectionString;
    private readonly string _queueName;
    private readonly IQueueClient _queueClient;
    private readonly string _blobStorageConnectionString;
    private readonly string _blobContainerName;
    private readonly BlobServiceClient _blobServiceClient;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
        _serviceBusConnectionString = "YOUR_SERVICE_BUS_CONNECTION_STRING";
        _queueName = "YOUR_QUEUE_NAME";
        _queueClient = new QueueClient(_serviceBusConnectionString, _queueName);

        _blobStorageConnectionString = "YOUR_BLOB_STORAGE_CONNECTION_STRING";
        _blobContainerName = "YOUR_BLOB_CONTAINER_NAME";
        _blobServiceClient = new BlobServiceClient(_blobStorageConnectionString);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var messageHandlerOptions = new MessageHandlerOptions(ExceptionReceivedHandler)
        {
            MaxConcurrentCalls = 1,
            AutoComplete = false
        };

        _queueClient.RegisterMessageHandler(ProcessMessagesAsync, messageHandlerOptions);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000);
        }

        await _queueClient.CloseAsync();
    }

    private async Task ProcessMessagesAsync(Message message, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation($"Received message: {message.Label}");

            // Check if the file is already being processed
            if (!IsFileInUse(message.Label))
            {
                // Read the file content
                var fileContent = File.ReadAllText(message.Label);

                // Process the file content (e.g., store it in Azure Blob Storage)
                await StoreFileInBlobStorageAsync(new FileInfo(message.Label).Name, fileContent);

                // Move the file to another location
                MoveFileToProcessedFolder(message.Label);

                // Complete the message to remove it from the queue
                await _queueClient.CompleteAsync(message.SystemProperties.LockToken);

                _logger.LogInformation($"File processed and message removed: {message.Label}");
            }
            else
            {
                _logger.LogInformation($"File is already being processed: {message.Label}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error processing message: {ex.Message}");
        }
    }

    private bool IsFileInUse(string filePath)
    {
        try
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                return false;
            }
        }
        catch (IOException)
        {
            return true;
        }
    }

    private async Task StoreFileInBlobStorageAsync(string fileName, string content)
    {
        var blobContainerClient = _blobServiceClient.GetBlobContainerClient(_blobContainerName);
        var blobClient = blobContainerClient.GetBlobClient(fileName);

        using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)))
        {
            await blobClient.UploadAsync(stream, true);
        }

        _logger.LogInformation($"File content stored in Azure Blob Storage: {fileName}");
    }

    private void MoveFileToProcessedFolder(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) {
            throw new InvalidDataException("Missing file path");
        }
        
        var processedFolder = Path.Combine(Path.GetDirectoryName(filePath), "Processed");
        Directory.CreateDirectory(processedFolder);

        var destinationPath = Path.Combine(processedFolder, Path.GetFileName(filePath));
        File.Move(filePath, destinationPath);

        _logger.LogInformation($"File moved to processed folder: {destinationPath}");
    }

    private Task ExceptionReceivedHandler(ExceptionReceivedEventArgs exceptionReceivedEventArgs)
    {
        _logger.LogError($"Message handler encountered an exception: {exceptionReceivedEventArgs.Exception}");

        var context = exceptionReceivedEventArgs.ExceptionReceivedContext;
        _logger.LogError($"Exception context - Endpoint: {context.Endpoint}, Entity Path: {context.EntityPath}, Action: {context.Action}");

        return Task.CompletedTask;
    }
}
