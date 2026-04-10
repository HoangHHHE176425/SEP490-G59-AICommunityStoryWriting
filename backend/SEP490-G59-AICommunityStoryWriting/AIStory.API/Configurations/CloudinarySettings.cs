namespace AIStory.API.Configurations;

public class CloudinarySettings
{
    public const string SectionName = "Cloudinary";

    public string CloudName { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string ApiSecret { get; set; } = "";
    /// <summary>Thư mục gốc trên Cloudinary (ví dụ aistory). Để trống thì chỉ dùng tên folder con.</summary>
    public string? RootFolder { get; set; }
}
