using System;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using System.Net;

public class GetImageFunction
{
    private readonly ILogger _logger;

    public GetImageFunction(ILoggerFactory loggerFactory) =>
        _logger = loggerFactory.CreateLogger<GetImageFunction>();

    [Function("GetImageFunction")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "GetImageFunction")] HttpRequestData req)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var itemName = query["itemName"];
        var response = req.CreateResponse();

        if (string.IsNullOrWhiteSpace(itemName))
        {
            response.StatusCode = HttpStatusCode.BadRequest;
            await response.WriteAsJsonAsync(new { error = "itemName is required" });
            return response;
        }

        string connStr = Environment.GetEnvironmentVariable("IMAGE_STORAGE_CONNECTION_STRING");
        string containerName = "food-images";
        string blobName = $"{itemName}.png";

        try
        {
            var bsc = new BlobServiceClient(connStr);
            var cc = bsc.GetBlobContainerClient(containerName);
            var bc = cc.GetBlobClient(blobName);

            if (!await bc.ExistsAsync())
            {
                response.StatusCode = HttpStatusCode.NotFound;
                await response.WriteAsJsonAsync(new { error = "Image not found" });
                return response;
            }

            var sasUri = bc.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddMinutes(10));
            response.StatusCode = HttpStatusCode.OK;
            await response.WriteAsJsonAsync(new { imageUrl = sasUri.ToString() });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch image.");
            response.StatusCode = HttpStatusCode.InternalServerError;
            await response.WriteAsJsonAsync(new { error = "Internal server error" });
            return response;
        }
    }
}
