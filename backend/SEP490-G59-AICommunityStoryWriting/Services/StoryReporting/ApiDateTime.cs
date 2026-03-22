namespace Services;

/// <summary>EF/SQL thường trả <see cref="DateTimeKind.Unspecified"/> cho cột lưu UTC — gắn UTC để System.Text.Json xuất ISO có <c>Z</c>, client parse đúng.</summary>
public static class ApiDateTime
{
    public static DateTime AsUtcForJson(DateTime value)
    {
        if (value.Kind == DateTimeKind.Unspecified)
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value;
    }

    public static DateTime? AsUtcForJson(DateTime? value) =>
        value.HasValue ? AsUtcForJson(value.Value) : null;
}
