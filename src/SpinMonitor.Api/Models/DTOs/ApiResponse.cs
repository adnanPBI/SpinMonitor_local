namespace SpinMonitor.Api.Models.DTOs
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
        public object? Error { get; set; }

        public static ApiResponse<T> SuccessResponse(T data, string? message = null)
        {
            return new ApiResponse<T>
            {
                Success = true,
                Message = message,
                Data = data
            };
        }

        public static ApiResponse<T> ErrorResponse(string error, object? details = null)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = error,
                Error = details
            };
        }
    }

    public class PaginationResponse<T>
    {
        public bool Success { get; set; } = true;
        public List<T> Data { get; set; } = new();
        public PaginationInfo Pagination { get; set; } = new();
    }

    public class PaginationInfo
    {
        public int Page { get; set; }
        public int Limit { get; set; }
        public int Total { get; set; }
        public int TotalPages { get; set; }
        public bool HasMore { get; set; }
    }
}
