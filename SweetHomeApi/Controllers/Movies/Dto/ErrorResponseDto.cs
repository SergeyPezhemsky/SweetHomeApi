namespace SweetHomeApi.Controllers.Movies.Dto;

public class ErrorResponseDto
{
    public string ErrorCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public List<ErrorDetailDto> Details { get; set; } = [];
    public string? TraceId { get; set; }
}
