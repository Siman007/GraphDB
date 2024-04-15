using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Text.RegularExpressions;

namespace GraphDB
{
    [ApiController]
    [Route("api/[controller]")]
    public class GraphController : ControllerBase
    {
        private readonly GraphService _graphService;

        public GraphController()
        {
            _graphService = GraphService.Instance;  // Assuming GraphService is a singleton
        }

        [HttpPost("node")]
        public IActionResult AddNode([FromBody] Node node)
        {
            var graph = _graphService.GetCurrentGraph();
            if (graph == null)
                return BadRequest("No active graph. Please create or load a graph first.");

            graph.AddNode(node);
            _graphService.SaveGraph(); // Ensure changes are persisted
            return Ok($"Node {node.Id} added.");
        }

        [HttpPost("edge")]
        public IActionResult AddEdge([FromBody] dynamic edgeData)
        {
            var graph = _graphService.GetCurrentGraph();
            if (graph == null)
                return BadRequest("No active graph. Please create or load a graph first.");

            var fromId = (string)edgeData.FromId;
            var toId = (string)edgeData.ToId;
            var weight = (double)(edgeData.Weight ?? 1.0); // Default weight to 1.0 if not provided
            var relationshipType = (string)edgeData.Relationship;

            var properties = new Dictionary<string, object>();
            if (edgeData.Properties != null)
            {
                foreach (var prop in edgeData.Properties)
                {
                    properties.Add((string)prop.Name, (object)prop.Value.Value); // Assuming prop.Value is dynamic
                }
            }

            graph.AddEdge(fromId, toId, weight, relationshipType, properties);
            _graphService.SaveGraph(); // Ensure changes are persisted
            return Ok($"Edge from {fromId} to {toId} with relationship {relationshipType} added.");
        }
        [HttpPost("commands")]
        public IActionResult ExecuteCommands([FromBody] string commands)
        {
            var graph = _graphService.GetCurrentGraph();
            if (graph == null)
            {
                return BadRequest("No active graph. Please create or load a graph first.");
            }

            // Splitting the commands string into individual commands using a sophisticated pattern
            var commandList = SplitCypherCommands(commands);

            var results = new List<dynamic>();
            foreach (var command in commandList)
            {
                try
                {
                    // Normalize and execute each command
                    var result = _graphService.ExecuteCypherCommands(command.Trim()); // Note the plural in method name if you decide to handle each command as potentially containing multiple sub-commands
                    results.Add(new { Command = command, Result = result });
                }
                catch (Exception ex)
                {
                    // Collect errors for each command that fails
                    results.Add(new { Command = command, Error = $"Error processing command: {ex.Message}" });
                }
            }

            return Ok(results);
        }

        private IEnumerable<string> SplitCypherCommands(string commands)
        {
            // Remove or ignore lines that start with "//" which are comments
            var lines = commands.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                .Where(line => !line.TrimStart().StartsWith("//"));

            // Rejoin the lines back into a single string to apply the semicolon split
            var filteredCommands = string.Join(" ", lines);

            // Regex to split by semicolons that are not within quotes
            var regex = new Regex(@"\s*;\s*(?=(?:[^""]*""[^""]*"")*[^""]*$)");
            return regex.Split(filteredCommands)
                        .Where(cmd => !string.IsNullOrWhiteSpace(cmd));
        }




        [HttpGet("findneighbors")]
        public ActionResult<List<NodeResponse>> FindNeighbors([FromQuery] string cypher)
        {
            var graph = _graphService.GetCurrentGraph();
            if (graph == null)
                return BadRequest("No active graph. Please create or load a graph first.");

            var response = _graphService.HandleFindNeighbors(cypher);

            if (!response.Success || response.Data == null || response.Data.Count == 0)
                return NotFound(response.Message);

            return Ok(response.Data);
        }

        [HttpPost("create")]
        public IActionResult CreateGraph([FromBody] GraphDbRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.DbName))
                return BadRequest("Database name must be provided.");

            _graphService.CreateDatabase(request.DbName);
            return Ok($"Graph '{request.DbName}' created and initialized.");
        }

        [HttpPost("load")]
        public IActionResult LoadGraph([FromBody] GraphDbRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.DbName))
                return BadRequest("Database name must be provided.");

            try
            {
                _graphService.LoadDatabase(request.DbName);
                return Ok($"Graph loaded from {request.DbName}.");
            }
            catch (FileNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }
    }

    public class GraphDbRequest
    {
        public string DbName { get; set; }
    }
}
