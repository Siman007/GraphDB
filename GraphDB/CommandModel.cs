using System;
namespace GraphDB
{
    public class CommandModel
    {
        public string Command { get; set; }
        public List<CommandResponse> History { get; set; } = new List<CommandResponse>();
    }

    public class CommandResponse
    {
        public string Command { get; set; }
        public string Response { get; set; }
        public bool HasError { get; set; }
        public string ErrorMessage { get; set; }
    }
}

