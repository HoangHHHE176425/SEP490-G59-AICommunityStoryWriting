namespace AIStory.API.Services;

public interface ICloudinaryImageService
{
    bool IsConfigured { get; }

    /// <summary>Upload ảnh lên Cloudinary. <paramref name="folder"/> là phân đoạn con (ví dụ story-covers, avatars).</summary>
    Task<string> UploadImageAsync(IFormFile file, string folder, CancellationToken cancellationToken = default);
}
