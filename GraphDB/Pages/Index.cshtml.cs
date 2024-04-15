using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using GraphDB; 

namespace GraphDB.Pages
{
    public class IndexModel : PageModel
    {
        private GraphService _graphService;

        public string CurrentDatabase => _graphService.GetDatabaseName();
        public bool IsDatabaseLoaded => _graphService.IsDatabaseLoaded;
        public string Message { get; private set; }

        public IndexModel(GraphService graphService)
        {
            _graphService = graphService;
        }

        [BindProperty]
        public CommandModel Command { get; set; } = new CommandModel();

        public void OnGet()
        {
            Message = IsDatabaseLoaded ? $"Current Database: {CurrentDatabase}" : "No database is currently loaded.";
        }

        public IActionResult OnPostExecuteCommand()
        {
            var response = _graphService.ExecuteCypherCommands(Command.Command);
            UpdateCommandHistory(Command.Command, response.ToString());
            return RedirectToPage();
        }

        public IActionResult OnPostReExecuteCommand(string commandToExecute)
        {
            var response = _graphService.ExecuteCypherCommands(commandToExecute);
            UpdateCommandHistory(commandToExecute, response.ToString());
            return RedirectToPage();
        }

        public IActionResult OnPostDeleteHistory()
        {
            Command.History.Clear();
            SaveCommandModelToSession();
            return RedirectToPage();
        }

        private void UpdateCommandHistory(string command, string response)
        {
            Command.History.Insert(0, new CommandResponse { Command = command, Response = response });
            SaveCommandModelToSession();
        }

        private void SaveCommandModelToSession()
        {
            var modelJson = JsonSerializer.Serialize(Command);
            HttpContext.Session.SetString("CommandModel", modelJson);
        }

        public IActionResult OnPostCreateDatabase(string databaseName)
        {
            var result = _graphService.CreateDatabase(databaseName);
            Message = result;
            SaveCommandModelToSession();
            return RedirectToPage();
        }

        public IActionResult OnPostLoadDatabase(string databaseName)
        {
            _graphService.LoadDatabase(databaseName);
            Message = "Database loaded successfully.";
            SaveCommandModelToSession();
            return RedirectToPage();
        }

        public IActionResult OnPostSaveDatabase()
        {
            if (IsDatabaseLoaded)
            {
                _graphService.SaveCurrentGraph();
                Message = "Database saved successfully.";
            }
            else
            {
                Message = "No database is loaded.";
            }
            SaveCommandModelToSession();
            return RedirectToPage();
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
