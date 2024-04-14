using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Collections.Generic;
using System;

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

        [HttpGet("command")]
        public IActionResult Command([FromQuery] string query)
        {
            var graph = _graphService.GetCurrentGraph();
            if (graph == null)
                return BadRequest("No active graph. Please create or load a graph first.");

            try
            {
                var result = _graphService.ExecuteCypherCommand(query);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error processing command: {ex.Message}");
            }
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
                _graphService.CreateOrLoadDatabase(request.DbName);
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
