#nullable enable
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using serverless.DTOs;

namespace serverless;

public static class PostToDatabase
{
    [FunctionName("PostToDatabase")]
    public static async Task< object?>RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)]
        HttpRequest req,
        ILogger logger)
    {
         string EndpointUrl = Environment.GetEnvironmentVariable("CosmosDBEndpoint");
         string AuthorizationKey = Environment.GetEnvironmentVariable("CosmosDBKey");
         string DatabaseName = Environment.GetEnvironmentVariable("DatabaseName");
         string ContainerName = Environment.GetEnvironmentVariable("ContainerName");
         string PartitionKey = "/id";
        logger.LogInformation("Processing request to send data to Cosmos DB.");
        
        MovieRequestDto? movieRequestDto = null;
       
        try
        {
            CosmosClient cosmosClient = new CosmosClient(EndpointUrl, AuthorizationKey, new CosmosClientOptions() { AllowBulkExecution = true });
            Database database = await cosmosClient.CreateDatabaseIfNotExistsAsync(DatabaseName);
            Container container = database.GetContainer(ContainerName) ?? await database.DefineContainer(ContainerName, PartitionKey)
                .WithIndexingPolicy()
                .WithIndexingMode(IndexingMode.Consistent)
                .WithIncludedPaths()
                .Attach()
                .WithExcludedPaths()
                .Path("/*")
                .Attach()
                .Attach()
                .CreateAsync();
            logger.LogInformation("Cosmos DB client created.");

            var content = await new StreamReader(req.Body).ReadToEndAsync();
            if (string.IsNullOrEmpty(content))
            {
                logger.LogWarning("Request body is empty");
                return new BadRequestObjectResult("Request body cannot be empty");
            }
            
            logger.LogInformation("Request body is not empty");

            movieRequestDto = JsonConvert.DeserializeObject<MovieRequestDto>(content);
            
            if (movieRequestDto == null)
            {
                logger.LogWarning("Failed to deserialize request body.");
                return new BadRequestObjectResult("Invalid JSON format.");
            }
            
            if (string.IsNullOrEmpty(movieRequestDto.Id))
            {
                movieRequestDto.Id = Guid.NewGuid().ToString();
            }
            
            logger.LogInformation($"Deserialized request body: {movieRequestDto}.");
            
            ItemResponse<MovieRequestDto> response = await container.CreateItemAsync(movieRequestDto, new PartitionKey(movieRequestDto.Id));
            logger.LogInformation($"Created item in database with id: {response.Resource.Id}");
            
            return new OkObjectResult(response.Resource);
            
        }
        catch (CosmosException cosmosEx)
        {
            logger.LogError(cosmosEx, $"Cosmos DB Error: {cosmosEx.Message}. Status: {cosmosEx.StatusCode}");
            return new ObjectResult(new { 
                Error = "Failed to save to Cosmos DB", 
                Details = cosmosEx.Message 
            })
            {
                StatusCode = (int)cosmosEx.StatusCode
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deserialize request body.");        logger.LogError(ex, "Unexpected error");
            return new ObjectResult(new { 
                Error = "Internal server error", 
                Details = ex.Message 
            })
            {
                StatusCode = 500
            };
        }
        
    }
}
