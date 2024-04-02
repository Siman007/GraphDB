using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System.Linq;
using System.IO;
using GraphDB; // Assuming GraphDB is the namespace where Graph class is defined

namespace GraphDB.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IHttpClientFactory _clientFactory;

        public string CurrentDatabase { get; private set; }
        public bool IsDatabaseLoaded { get; private set; }
        public string Message { get; private set; }


        public IndexModel(IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
        }

        [BindProperty]
        public CommandModel Command { get; set; } = new CommandModel();

        public void OnGet()
        {
            CurrentDatabase = HttpContext.Session.GetString("CurrentDatabase");
            IsDatabaseLoaded = !string.IsNullOrEmpty(CurrentDatabase);
            Message = IsDatabaseLoaded ? $"Current Database: {CurrentDatabase}" : "No database is currently loaded.";
            LoadCommandModelFromSession();
        }


        private void LoadCommandModelFromSession()
        {
            var modelJson = HttpContext.Session.GetString("CommandModel");
            Command = string.IsNullOrEmpty(modelJson) ? new CommandModel() : JsonSerializer.Deserialize<CommandModel>(modelJson);
        }

        public async Task<IActionResult> OnPostExecuteCommandAsync()
        {
            await ExecuteCommand(Command.Command);
            return RedirectToPage();
        }

        // This method is designed to be triggered by a form submission in the Razor Page, allowing users to re-execute commands from the command history.
        public async Task<IActionResult> OnPostReExecuteCommandAsync(string commandToExecute)
        {
            await ExecuteCommand(commandToExecute);
            return RedirectToPage();
        }

        public IActionResult OnPostDeleteHistory()
        {
            LoadCommandModelFromSession();
            // Clear the command history
            Command.History.Clear();

            // Serialize the updated Command object and save it back into the session
            var modelJson = JsonSerializer.Serialize(Command);
            HttpContext.Session.SetString("CommandModel", modelJson);

            // Redirect back to the same page to reflect changes
            return RedirectToPage();
        }

        private async Task ExecuteCommand(string command)
        {
            LoadCommandModelFromSession();
            if (string.IsNullOrWhiteSpace(command)) return;

            if (!IsDatabaseLoaded && (command.StartsWith("create", System.StringComparison.OrdinalIgnoreCase) ||
               command.StartsWith("load", System.StringComparison.OrdinalIgnoreCase) ||
               command.StartsWith("delete", System.StringComparison.OrdinalIgnoreCase)))
            {
                HandleDatabaseManagementCommand(command);
            }
            else if (!IsDatabaseLoaded && (command.StartsWith("help", System.StringComparison.OrdinalIgnoreCase) ||
               command == "h" || command == "?"))
            {

                // Add the help response to the command history
                var helpResponse = CommandUtility.GetHelpResponse();
                if (helpResponse.Success)
                {
                    // Assuming you want to insert the response data into the command history
                    Command.History.Insert(0, new CommandResponse { Command = command, Response = helpResponse.Data });
                }
                else
                {
                    // Handle the case where getting the help response fails
                    Command.History.Insert(0, new CommandResponse { Command = command, Response = helpResponse.Message });
                }

                // Serialize the updated Command object and save it back into the session
                var modelJson = JsonSerializer.Serialize(Command);
                HttpContext.Session.SetString("CommandModel", modelJson);



            }
            else if (IsDatabaseLoaded && (
                command.StartsWith("save", System.StringComparison.OrdinalIgnoreCase) ||
                command.StartsWith("close", System.StringComparison.OrdinalIgnoreCase)))
            {
                HandleDatabaseManagementCommand(command);
            }
            else if (IsDatabaseLoaded)
            {
                var client = _clientFactory.CreateClient();
                var response = await client.GetStringAsync($"api/Graph/command?query={command}");
                var encodedResponse = System.Web.HttpUtility.JavaScriptStringEncode(response);
                Command.History.Insert(0, new CommandResponse { Command = command, Response = encodedResponse });


                // Assuming deserialization and error handling is done here
                Command.History.Insert(0, new CommandResponse { Command = command, Response = response });

                command = ""; // Reset command input
                var modelJson = JsonSerializer.Serialize(Command);
                HttpContext.Session.SetString("CommandModel", modelJson);

          
            }
            else
            {
                TempData["Error"] = "No database is loaded. Please load or create a database first. Type Help /h or ? for help";
            }

        }

        private void HandleDatabaseManagementCommand(string command)
        {
            var parts = command.Split(' ', 2);
            var action = parts[0].ToLower();
            var parameter = parts.Length > 1 ? parts[1] : string.Empty;

            switch (action)
            {
                case "create":
                    CreateDatabase(parameter);
                    break;
                case "load":
                    LoadDatabase(parameter);
                    break;
                case "save":
                    SaveDatabase();
                    break;
                case "close":
                    CloseDatabase();
                    break;
                case "delete":
                    DeleteDatabase(parameter);
                    break;
            }
        }

        private void CreateDatabase(string databaseName)
        {
            // Logic to create a new database
            // For simplicity, this just sets the session value
            LoadCommandModelFromSession(); // Ensure we have the latest history
          
            var graph = new Graph(databaseName);

          
            Command.History.Insert(0, new CommandResponse { Command = $"create {databaseName}", Response = graph.CreateDatabase()});
            if (graph.GetDatabaseLoaded())
            {
                HttpContext.Session.SetString("CurrentDatabase", databaseName);
            }
            else
            {

            }

            // Serialize the updated Command object and save it back into the session
            var modelJson = JsonSerializer.Serialize(Command);
            HttpContext.Session.SetString("CommandModel", modelJson);
        }

        private void LoadDatabase(string databaseName)
        {
            LoadCommandModelFromSession(); // Ensure we have the latest history
            HttpContext.Session.SetString("CurrentDatabase", databaseName);
        }

        private void SaveDatabase()
        {
            if (!IsDatabaseLoaded) return;

            var graph = new Graph(CurrentDatabase);
            graph.SaveToFile();
        }

        private void CloseDatabase()
        {
            HttpContext.Session.Remove("CurrentDatabase");
        }

        private void DeleteDatabase(string databaseName)
        {
            if (CurrentDatabase != databaseName) return;

            CloseDatabase();
            System.IO.File.Delete(Graph.GetDatabasePath());
        }

        public IActionResult OnPostDeleteCommand(int commandIndex)
        {
            LoadCommandModelFromSession(); // Ensure you have the latest history

            if (commandIndex >= 0 && commandIndex < Command.History.Count)
            {
                // Remove the command at the specified index
                Command.History.RemoveAt(commandIndex);

                // Serialize the updated Command object and save it back into the session
                var modelJson = JsonSerializer.Serialize(Command);
                HttpContext.Session.SetString("CommandModel", modelJson);
            }

            // Redirect back to the same page to reflect changes
            return RedirectToPage();
        }

    }
}
