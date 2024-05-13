using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using GraphDB;

namespace GraphDB.Pages
{
    public class IndexModel : PageModel
    {
        private GraphService _graphService;

        public string Message { get; private set; }

        public IndexModel(GraphService graphService)
        {
            _graphService = graphService;
        }

        [BindProperty]
        public CommandModel Command { get; set; } = new CommandModel();

        public void OnGet()
        {
            Message = _graphService.IsDatabaseLoaded ? $"Current Database: {_graphService.GetDatabaseName()}" : "No database is currently loaded.";
            Command = LoadCommandModelFromSession() ?? new CommandModel();
        }

        public IActionResult OnPostExecuteCommand()
        {
            var response = _graphService.ExecuteCypherCommands(Command.Command);
            UpdateCommandHistory(Command.Command, response);
            return RedirectToPage();
        }

        public IActionResult OnPostReExecuteCommand(string commandToExecute)
        {
            var response = _graphService.ExecuteCypherCommands(commandToExecute);
            UpdateCommandHistory(commandToExecute, response.ToString());
            return RedirectToPage();
        }

        public IActionResult OnPostDeleteHistory(int commandIndex)
        {
            if (commandIndex >= 0 && commandIndex < Command.History.Count)
            {
                Command = LoadCommandModelFromSession();
                Command.History.RemoveAt(commandIndex);
                SaveCommandModelToSession();
            }
            return RedirectToPage();
        }

        //private void UpdateCommandHistory(string command, dynamic response)
        //{
        //    var responseString = response is string ? response : JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
        //    Command = LoadCommandModelFromSession() ?? new CommandModel();
        //    Command.History.Insert(0, new CommandResponse { Command = command, Response = responseString });
        //    SaveCommandModelToSession();
        //}

        //private void UpdateCommandHistory(string command, dynamic response)
        //{
        //    var responseString = "";
        //    if (response is string)
        //    {
        //        responseString = response;
        //    }
        //    else if (response != null)
        //    {
        //        // Serialize the response to JSON with indentation if it's not a simple string
        //        responseString = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
        //    }

        //    Command.History.Insert(0, new CommandResponse { Command = command, Response = responseString });
        //    SaveCommandModelToSession();
        //}

        private void UpdateCommandHistory(string command, dynamic response)
        {
            string responseString;
            if (response is string)
            {
                responseString = response;
            }
            else if (response != null)
            {
                responseString = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                responseString = "No data returned.";
            }
            Command = LoadCommandModelFromSession() ?? new CommandModel();
            Command.History.Insert(0, new CommandResponse { Command = command, Response = responseString });
            SaveCommandModelToSession();
        }


        private void SaveCommandModelToSession()
        {
            var modelJson = JsonSerializer.Serialize(Command);
            HttpContext.Session.SetString("CommandModel", modelJson);
        }

        private CommandModel LoadCommandModelFromSession()
        {
            var sessionData = HttpContext.Session.GetString("CommandModel");
            return sessionData != null ? JsonSerializer.Deserialize<CommandModel>(sessionData) : new CommandModel();
        }
    }

    public class CommandModel
    {
        public string Command { get; set; }
        public List<CommandResponse> History { get; set; } = new List<CommandResponse>();
    }

    public class CommandResponse
    {
        public string Command { get; set; }
        public string Response { get; set; }
    }
}
