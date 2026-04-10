using AIStory.API.Configurations;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;

namespace AIStory.API.Services;

public class CloudinaryImageService : ICloudinaryImageService
{
    private readonly CloudinarySettings _settings;
    private readonly Cloudinary _cloudinary;

    public CloudinaryImageService(IOptions<CloudinarySettings> options)
    {
        _settings = options.Value;
        if (IsConfigured)
        {
            var account = new Account(_settings.CloudName.Trim(), _settings.ApiKey.Trim(), _settings.ApiSecret.Trim());
            _cloudinary = new Cloudinary(account) { Api = { Secure = true } };
        }
        else
        {
            _cloudinary = null!;
        }
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_settings.CloudName)
        && !string.IsNullOrWhiteSpace(_settings.ApiKey)
        && !string.IsNullOrWhiteSpace(_settings.ApiSecret);

    public async Task<string> UploadImageAsync(IFormFile file, string folder, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Cloudinary chưa được cấu hình. Thêm CloudName, ApiKey, ApiSecret vào cấu hình.");

        if (file == null || file.Length == 0)
            throw new ArgumentException("File không hợp lệ.", nameof(file));

        var folderPath = BuildFolderPath(folder);

        await using var stream = file.OpenReadStream();
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            Folder = folderPath,
            PublicId = Guid.NewGuid().ToString("N"),
            Overwrite = false,
            UseFilename = false,
            UniqueFilename = false
        };

        var result = await _cloudinary.UploadAsync(uploadParams, cancellationToken);
        if (result.Error != null)
            throw new InvalidOperationException(result.Error.Message);

        var url = result.SecureUrl?.AbsoluteUri ?? result.Url?.AbsoluteUri;
        if (string.IsNullOrEmpty(url))
            throw new InvalidOperationException("Upload Cloudinary không trả về URL.");

        return url;
    }

    private string BuildFolderPath(string folder)
    {
        var f = (folder ?? "").Trim().Trim('/');
        if (string.IsNullOrEmpty(f))
            f = "uploads";

        var root = (_settings.RootFolder ?? "").Trim().Trim('/');
        return string.IsNullOrEmpty(root) ? f : $"{root}/{f}";
    }
}
