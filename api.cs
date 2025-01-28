using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.Storage;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

public static class CatalogoNetflixFunction
{
    private static readonly string cosmosDbEndpoint = Environment.GetEnvironmentVariable("COSMOS_DB_ENDPOINT");
    private static readonly string cosmosDbKey = Environment.GetEnvironmentVariable("COSMOS_DB_KEY");
    private static readonly string cosmosDbName = Environment.GetEnvironmentVariable("COSMOS_DB_NAME");
    private static readonly string azureStorageConnectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
    
    private static CosmosClient cosmosClient = new CosmosClient(cosmosDbEndpoint, cosmosDbKey);
    private static Container container = cosmosClient.GetContainer(cosmosDbName, "CatalogoNetflix");

    [FunctionName("CatalogoNetflix")]
    public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", "put", "delete")] HttpRequestData req,
        FunctionContext executionContext)
    {
        var logger = executionContext.GetLogger("CatalogoNetflix");
        var action = req.Query["action"];

        // Adicionar filme
        if (action == "add")
        {
            var requestBody = await req.ReadAsStringAsync();
            var movie = Newtonsoft.Json.JsonConvert.DeserializeObject<Movie>(requestBody);
            var videoUrl = string.Empty;
            var thumbUrl = string.Empty;

            // Fazer upload para o Blob Storage (se necessário)
            if (!string.IsNullOrEmpty(movie.Video))
            {
                var blobClient = CloudStorageAccount.Parse(azureStorageConnectionString).CreateCloudBlobClient();
                var containerClient = blobClient.GetContainerReference("catalogo-blob-container");
                var blobName = $"{movie.Title}_video_{DateTime.UtcNow.Ticks}.mp4";
                var blockBlob = containerClient.GetBlockBlobReference(blobName);

                var videoBuffer = Convert.FromBase64String(movie.Video);
                using (var stream = new MemoryStream(videoBuffer))
                {
                    await blockBlob.UploadFromStreamAsync(stream);
                }

                videoUrl = blockBlob.Uri.ToString();
            }

            if (!string.IsNullOrEmpty(movie.Thumb))
            {
                var blobClient = CloudStorageAccount.Parse(azureStorageConnectionString).CreateCloudBlobClient();
                var containerClient = blobClient.GetContainerReference("catalogo-blob-container");
                var blobName = $"{movie.Title}_thumb_{DateTime.UtcNow.Ticks}.jpg";
                var blockBlob = containerClient.GetBlockBlobReference(blobName);

                var thumbBuffer = Convert.FromBase64String(movie.Thumb);
                using (var stream = new MemoryStream(thumbBuffer))
                {
                    await blockBlob.UploadFromStreamAsync(stream);
                }

                thumbUrl = blockBlob.Uri.ToString();
            }

            var item = new Movie
            {
                Id = movie.Id ?? Guid.NewGuid().ToString(),
                Title = movie.Title,
                Year = movie.Year,
                Video = videoUrl ?? movie.Video,
                Thumb = thumbUrl ?? movie.Thumb
            };

            try
            {
                await container.CreateItemAsync(item);
                return new OkObjectResult("Item adicionado com sucesso!");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro ao adicionar item");
                return new StatusCodeResult(500);
            }
        }

        // Obter filmes
        else if (action == "get")
        {
            try
            {
                var query = container.GetItemQueryIterator<Movie>("SELECT * FROM c");
                var movies = await query.ReadNextAsync();

                return new OkObjectResult(movies);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro ao recuperar itens");
                return new StatusCodeResult(500);
            }
        }

        // Atualizar filme
        else if (action == "update")
        {
            var requestBody = await req.ReadAsStringAsync();
            var movie = Newtonsoft.Json.JsonConvert.DeserializeObject<Movie>(requestBody);
            var videoUrl = string.Empty;
            var thumbUrl = string.Empty;

            if (!string.IsNullOrEmpty(movie.Video))
            {
                var blobClient = CloudStorageAccount.Parse(azureStorageConnectionString).CreateCloudBlobClient();
                var containerClient = blobClient.GetContainerReference("catalogo-blob-container");
                var blobName = $"{movie.Title}_video_{DateTime.UtcNow.Ticks}.mp4";
                var blockBlob = containerClient.GetBlockBlobReference(blobName);

                var videoBuffer = Convert.FromBase64String(movie.Video);
                using (var stream = new MemoryStream(videoBuffer))
                {
                    await blockBlob.UploadFromStreamAsync(stream);
                }

                videoUrl = blockBlob.Uri.ToString();
            }

            if (!string.IsNullOrEmpty(movie.Thumb))
            {
                var blobClient = CloudStorageAccount.Parse(azureStorageConnectionString).CreateCloudBlobClient();
                var containerClient = blobClient.GetContainerReference("catalogo-blob-container");
                var blobName = $"{movie.Title}_thumb_{DateTime.UtcNow.Ticks}.jpg";
                var blockBlob = containerClient.GetBlockBlobReference(blobName);

                var thumbBuffer = Convert.FromBase64String(movie.Thumb);
                using (var stream = new MemoryStream(thumbBuffer))
                {
                    await blockBlob.UploadFromStreamAsync(stream);
                }

                thumbUrl = blockBlob.Uri.ToString();
            }

            var updatedItem = new Movie
            {
                Id = movie.Id,
                Title = movie.Title,
                Year = movie.Year,
                Video = videoUrl ?? movie.Video,
                Thumb = thumbUrl ?? movie.Thumb
            };

            try
            {
                await container.UpsertItemAsync(updatedItem, new PartitionKey(movie.Id));
                return new OkObjectResult("Item atualizado com sucesso!");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro ao atualizar item");
                return new StatusCodeResult(500);
            }
        }

        // Deletar filme
        else if (action == "delete")
        {
            var requestBody = await req.ReadAsStringAsync();
            var movie = Newtonsoft.Json.JsonConvert.DeserializeObject<Movie>(requestBody);

            try
            {
                await container.DeleteItemAsync<Movie>(movie.Id, new PartitionKey(movie.Id));
                return new OkObjectResult("Item deletado com sucesso!");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro ao deletar item");
                return new StatusCodeResult(500);
            }
        }

        // Caso a ação não seja reconhecida
        return new BadRequestObjectResult("Ação inválida. Use 'add', 'get', 'update' ou 'delete'.");
    }
}
