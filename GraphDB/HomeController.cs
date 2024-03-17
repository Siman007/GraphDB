using System;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using static GraphDB.Graph;
using Microsoft.AspNetCore.Http; // For session extension methods
using System.Text.Json; // For JSON serialization

namespace GraphDB
{
    public class HomeController : Controller
    {
        private readonly IHttpClientFactory _clientFactory;

        public HomeController(IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var modelJson = HttpContext.Session.GetString("CommandModel");
            var model = string.IsNullOrEmpty(modelJson) ? new CommandModel() : JsonSerializer.Deserialize<CommandModel>(modelJson);
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ExecuteCommand(CommandModel model)
        {
            var client = _clientFactory.CreateClient();
            var response = await client.GetStringAsync($"api/Graph/command?query={model.Command}");

            // Assuming deserialization and error handling is done here
            model.History.Add(new CommandResponse { Command = model.Command, Response = response });

            model.Command = ""; // Reset command input
            var modelJson = JsonSerializer.Serialize(model);
            HttpContext.Session.SetString("CommandModel", modelJson);

            return View("Index", model);
        }

        [HttpPost]
        public IActionResult DeleteCommand(int index)
        {
            var modelJson = HttpContext.Session.GetString("CommandModel");
            var model = string.IsNullOrEmpty(modelJson) ? new CommandModel() : JsonSerializer.Deserialize<CommandModel>(modelJson);

            if (model.History.Count > index)
            {
                model.History.RemoveAt(index);
                var updatedModelJson = JsonSerializer.Serialize(model);
                HttpContext.Session.SetString("CommandModel", updatedModelJson);
            }

            return RedirectToAction("Index");
        }

    }
}

