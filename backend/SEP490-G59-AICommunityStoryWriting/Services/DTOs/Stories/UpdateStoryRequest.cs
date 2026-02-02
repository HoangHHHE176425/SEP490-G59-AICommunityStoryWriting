namespace Services.DTOs.Stories
{
    public class UpdateStoryRequest
    {
        public string Title { get; set; } = null!;
        public string? Summary { get; set; }
        public int CategoryId { get; set; }
        public string Status { get; set; } = "DRAFT";
    }
}
