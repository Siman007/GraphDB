using System;
namespace GraphDB
{
    public class NodeResponse
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public Dictionary<string, object> Properties { get; set; }
    }

    public class RelationshipResponse
    {
        public string FromId { get; set; }
        public string ToId { get; set; }
        public string RelationshipType { get; set; }
        public Dictionary<string, object> Properties { get; set; }
    }

    public class AggregateResponse
    {
        public double Value { get; set; }
    }

    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T Data { get; set; }
        public string Message { get; set; }

        public ApiResponse(bool success, T data, string message = null)
        {
            Success = success;
            Data = data;
            Message = message;
        }

        public static ApiResponse<T> SuccessResponse(T data, string message = null)
        {
            return new ApiResponse<T>(true, data, message);
        }

        public static ApiResponse<T> ErrorResponse(string message)
        {
            return new ApiResponse<T>(false, default(T), message);
        }
    }
}

