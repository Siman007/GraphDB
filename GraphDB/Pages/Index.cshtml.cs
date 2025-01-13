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
        public bool IsDatabaseLoaded => GraphManager.IsDatabaseLoaded;
        public string Message { get; private set; }


        public IndexModel(IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
        }

        [BindProperty]
        public CommandModel Command { get; set; } = new CommandModel();

        public void OnGet()
        {
            LoadCommandModelFromSession();

            var currentDatabaseName = HttpContext.Session.GetString("CurrentDatabase");
            if (string.IsNullOrEmpty(currentDatabaseName) || GraphManager.CurrentGraph == null)
            {
                Message = "No database is currently loaded.";
                return;
            }

            CurrentDatabase = GraphManager.CurrentGraph.GetDatabaseName();

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

            if (!IsDatabaseLoaded && (command.StartsWith("create database", System.StringComparison.OrdinalIgnoreCase) ||
               command.StartsWith("load database", System.StringComparison.OrdinalIgnoreCase) ||
               command.StartsWith("delete database", System.StringComparison.OrdinalIgnoreCase)))
            {
                HandleDatabaseManagementCommand(command);
            }
            else if (!IsDatabaseLoaded && (command.StartsWith("help", System.StringComparison.OrdinalIgnoreCase) ||
               command == "h" || command == "?"))
            {

                //// Add the help response to the command history
                //var helpResponse = CommandUtility.GetHelpResponse();
                //if (helpResponse.Success)
                //{
                //    // Assuming you want to insert the response data into the command history
                //    Command.History.Insert(0, new CommandResponse { Command = command, Response = helpResponse.Data });
                //}
                //else
                //{
                //    // Handle the case where getting the help response fails
                //    Command.History.Insert(0, new CommandResponse { Command = command, Response = helpResponse.Message });
                //}

                //// Serialize the updated Command object and save it back into the session
                //var modelJson = JsonSerializer.Serialize(Command);
                //HttpContext.Session.SetString("CommandModel", modelJson);

                
                    var helpMessage = GraphHelp.GetHelp(command);
                    Command.History.Insert(0, new CommandResponse { Command = command, Response = helpMessage });

                    var modelJson = JsonSerializer.Serialize(Command);
                    HttpContext.Session.SetString("CommandModel", modelJson);
                

            }
            else if (IsDatabaseLoaded && (
                command.StartsWith("save", System.StringComparison.OrdinalIgnoreCase) ||
                command.StartsWith("close", System.StringComparison.OrdinalIgnoreCase)))
            {
                HandleDatabaseManagementCommand(command);
            }
            else if (GraphManager.CurrentGraph != null && GraphManager.CurrentGraph.GetDatabaseLoaded())
            {
                var client = _clientFactory.CreateClient();
                client.BaseAddress = new Uri("http://localhost:7211/"); 

                var requestUri = $"api/Graph/command?query={command}";
                var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

                try
                {
                    var response = await client.SendAsync(request);

                    if (response.IsSuccessStatusCode)
                    {
                        var jsonResponse = await response.Content.ReadAsStringAsync();
                        // Deserialize into ApiResponse object
                        var apiResponse = Newtonsoft.Json.JsonConvert.DeserializeObject<ApiResponse<object>>(jsonResponse);


                        if (apiResponse != null && apiResponse.Success)
                        {
                            var formattedMessage = $"Command executed successfully.\nMessage: {apiResponse.Message}\nData: {Newtonsoft.Json.JsonConvert.SerializeObject(apiResponse.Data, Newtonsoft.Json.Formatting.Indented)}";
                            Command.History.Insert(0, new CommandResponse
                            {
                                Command = command,
                                Response = formattedMessage,
                                HasError = false
                            });
                        }
                        else
                        {
                            Command.History.Insert(0, new CommandResponse
                            {
                                Command = command,
                                Response = $"Error: {apiResponse?.Message ?? "An unknown error occurred."}",
                                HasError = true
                            });
                        }



                        // Add response to command history
                       // Command.History.Insert(0, new CommandResponse { Command = command, Response = Newtonsoft.Json.formattedMessage, HasError = !apiResponse.Success });
                    }
                    else
                    {
                        Command.History.Insert(0, new CommandResponse
                        {
                            Command = command,
                            Response = $"Error: {response.StatusCode} - {response.ReasonPhrase}"
                        });
                    }
                }
                catch (Exception ex)
                {
                    // Handle any exceptions (e.g., network issues)
                    Command.History.Insert(0, new CommandResponse
                    {
                        Command = command,
                        Response = $"Error: Failed to execute command. Exception: {ex.Message}"
                    });
                }

                // Reset command input
                command = "";

                // Serialize the updated Command object and save it back into the session
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
            var parts = command.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                Console.WriteLine("Error: Invalid command format. Please provide a valid action and database name.");
                return;
            }

            var action = parts[0].ToLower();
            var parameter = parts[1].Trim();

            if (string.IsNullOrEmpty(parameter))
            {
                Console.WriteLine("Error: Database name is required.");
                return;
            }

            switch (action)
            {
                case "create":
                    if (parameter.StartsWith("database", StringComparison.OrdinalIgnoreCase))
                    {
                        var dbName = parameter.Substring(8).Trim();
                        if (string.IsNullOrEmpty(dbName))
                        {
                            Console.WriteLine("Error: Please specify a valid database name.");
                        }
                        else
                        {
                            if (CreateDatabase(dbName))
                            {
                                Console.WriteLine($"Database '{dbName}' created successfully.");
                            }
                            else
                            {
                                Console.WriteLine($"Error: Failed to  create '{dbName}'.");
                            }
                           
                        }
                    }
                    else
                    {
                        Console.WriteLine("Error: Unknown command. Did you mean 'CREATE DATABASE [dbname]'?");
                    }
                    break;

                case "load":
                    if (parameter.StartsWith("database", StringComparison.OrdinalIgnoreCase))
                    {
                        var dbName = parameter.Substring(8).Trim();
                        if (string.IsNullOrEmpty(dbName))
                        {
                            Message = "Error: Please specify a valid database name.";
                            Console.WriteLine("Error: Please specify a valid database name.");
                        }
                        else
                        {
                            if (LoadDatabase(dbName))
                            {
                                Console.WriteLine($"Database '{dbName}' loaded successfully.");
                            }
                            else
                            {
                                Console.WriteLine($"Error: Failed to  find or load '{dbName}'.");
                            }
                        }
                    }
                    else
                    {
                        Message = "Error: Unknown command. Did you mean 'LOAD DATABASE [dbname]'?";
                        Console.WriteLine("Error: Unknown command. Did you mean 'LOAD DATABASE [dbname]'?");
                    }
                    break;

                case "close":
                    CloseDatabase();
                    Console.WriteLine("Database closed successfully.");
                    break;

                case "delete":
                    DeleteDatabase(parameter);
                    Console.WriteLine($"Database '{parameter}' deleted successfully.");
                    break;

                default:
                    Console.WriteLine("Error: Unknown command. Please use a valid database management command.");
                    break;
            }
        }


        private bool CreateDatabase(string databaseName)
        {
            LoadCommandModelFromSession(); // Ensure we have the latest history

            var graph = new Graph(databaseName);
            Command.History.Insert(0, new CommandResponse { Command = $"create database {databaseName}", Response = graph.CreateDatabase() });

            var success = graph.GetDatabaseLoaded();
            if (success)
            {
                GraphManager.SetCurrentGraph(graph); // Use the method to set the current graph
                HttpContext.Session.SetString("CurrentDatabase", databaseName);
            }

            // Serialize the updated Command object and save it back into the session
            var modelJson = JsonSerializer.Serialize(Command);
            HttpContext.Session.SetString("CommandModel", modelJson);
            return success;
        }


        private bool LoadDatabase(string databaseName)
        {
            LoadCommandModelFromSession(); // Ensure we have the latest history
            var graph = new Graph(databaseName);
            Command.History.Insert(0, new CommandResponse { Command = $"load database {databaseName}", Response = graph.LoadDatabase() });

            var success = graph.GetDatabaseLoaded();

            if (success)
            {
                


                GraphManager.SetCurrentGraph(graph); // Use the method to set the current graph
                HttpContext.Session.SetString("CurrentDatabase", databaseName);
            }

            // Serialize the updated Command object and save it back into the session
            var modelJson = JsonSerializer.Serialize(Command);
            HttpContext.Session.SetString("CommandModel", modelJson);
            return success;
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
            GraphManager.SetCurrentGraph(null); // Use the method to set the current graph
        }


        private void DeleteDatabase(string databaseName)
        {
            if (CurrentDatabase != databaseName) return;

            CloseDatabase();

            // Use the current graph instance from GraphManager to get the database path
            var graphPath = GraphManager.CurrentGraph.GetDatabasePath();
            System.IO.File.Delete(graphPath);
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
