// BlobStorage: This service is disabled until the Azure Storage Account is provisioned.
// Company policy requires Customer-Managed Key (CMK) encryption + restricted network access
// on storage accounts — needs admin assistance to set up.
//
// TO ENABLE:
// 1. Ask admin to create storage account with CMK + Key Vault encryption
// 2. Uncomment Azure.Storage.Blobs package in .csproj
// 3. Uncomment builder.Services.AddScoped<IBlobStorageService, BlobStorageService>() in Program.cs
// 4. Uncomment this entire file
// 5. Add BlobStorage--ConnectionString and BlobStorage--ContainerName secrets to Key Vault

namespace HotelBookingAppWebApi.Services
{
    public interface IBlobStorageService
    {
        Task<string> UploadImageAsync(IFormFile file, string folder);
        Task DeleteImageAsync(string imageUrl);
    }

    // Stub implementation — returns placeholder until blob storage is enabled
    public class BlobStorageService : IBlobStorageService
    {
        public Task<string> UploadImageAsync(IFormFile file, string folder)
        {
            // Returns empty string — image upload not available until storage is configured
            return Task.FromResult(string.Empty);
        }

        public Task DeleteImageAsync(string imageUrl)
        {
            // No-op until storage is configured
            return Task.CompletedTask;
        }
    }
}
