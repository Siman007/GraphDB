using System;
using Newtonsoft.Json;

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




        // Generic wrapper around ApiResponse to include typed data
        public class ApiResponse<T>
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            public T Data { get; set; }

            // This property serializes Data to a JSON string
            public string DataJson => JsonConvert.SerializeObject(Data);

            // Factory method for success responses
            public static ApiResponse<T> SuccessResponse(T data, string message)
            {
                return new ApiResponse<T>
                {
                    Success = true,
                    Message = message,
                    Data = data
                };
            }

            // Factory method for error responses
            public static ApiResponse<T> ErrorResponse(string message)
            {
                return new ApiResponse<T>
                {
                    Success = false,
                    Message = message,
                    Data = default

                };
            }
        }

}

