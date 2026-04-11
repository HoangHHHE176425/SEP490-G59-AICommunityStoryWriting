namespace AIStory.API.Services;

public interface ICloudinaryImageService
{
    bool IsConfigured { get; }

    /// <summary>Upload ảnh lên Cloudinary. <paramref name="folder"/> là phân đoạn con (ví dụ story-covers, avatars).</summary>
    Task<string> UploadImageAsync(IFormFile file, string folder, CancellationToken cancellationToken = default);

    /// <summary>Xóa ảnh trên Cloudinary theo URL đầy đủ. Trả về true nếu đã xóa hoặc ảnh không còn tồn tại.</summary>
    Task<bool> DeleteImageByUrlAsync(string imageUrl, CancellationToken cancellationToken = default);
}
