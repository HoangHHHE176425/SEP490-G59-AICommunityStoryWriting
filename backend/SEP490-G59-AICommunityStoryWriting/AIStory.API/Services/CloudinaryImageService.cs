using AIStory.API.Configurations;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

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

    public async Task<bool> DeleteImageByUrlAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) return false;
        if (string.IsNullOrWhiteSpace(imageUrl)) return false;

        var publicId = TryExtractPublicId(imageUrl);
        if (string.IsNullOrWhiteSpace(publicId))
            return false;

        var result = await _cloudinary.DestroyAsync(new DeletionParams(publicId)
        {
            ResourceType = ResourceType.Image,
            Type = "upload"
        });

        if (result.Error != null)
            throw new InvalidOperationException(result.Error.Message);

        return string.Equals(result.Result, "ok", StringComparison.OrdinalIgnoreCase)
            || string.Equals(result.Result, "not found", StringComparison.OrdinalIgnoreCase);
    }

    private string BuildFolderPath(string folder)
    {
        var f = (folder ?? "").Trim().Trim('/');
        if (string.IsNullOrEmpty(f))
            f = "uploads";

        var root = (_settings.RootFolder ?? "").Trim().Trim('/');
        return string.IsNullOrEmpty(root) ? f : $"{root}/{f}";
    }

    private string? TryExtractPublicId(string imageUrl)
    {
        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
            return null;

        if (!uri.Host.Contains("cloudinary.com", StringComparison.OrdinalIgnoreCase))
            return null;

        var marker = "/upload/";
        var path = uri.AbsolutePath;
        var uploadIndex = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (uploadIndex < 0)
            return null;

        var afterUpload = path[(uploadIndex + marker.Length)..].Trim('/');
        if (string.IsNullOrWhiteSpace(afterUpload))
            return null;

        var segments = afterUpload
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        // Skip transformation segments until a version segment (v123...) appears.
        var versionIndex = segments.FindIndex(s => Regex.IsMatch(s, @"^v\d+$", RegexOptions.IgnoreCase));
        if (versionIndex >= 0)
            segments = segments.Skip(versionIndex + 1).ToList();

        if (segments.Count == 0)
            return null;

        segments[^1] = Path.GetFileNameWithoutExtension(segments[^1]);
        var publicId = string.Join("/", segments).Trim('/');
        return string.IsNullOrWhiteSpace(publicId) ? null : publicId;
    }
}
