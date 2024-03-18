using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Collections.Generic;


namespace GraphDB
{
    [ApiController]
    [Route("api/[controller]")]

    public class GraphController : ControllerBase
    {
        private static Graph graph;


        [HttpPost("node")]
        public IActionResult AddNode([FromBody] Node node)
        {
            graph.AddNode(node);
            return Ok($"Node {node.Id} added.");
        }

        [HttpPost("edge")]
        public IActionResult AddEdge([FromBody] dynamic edgeData)
        {
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

            return Ok($"Edge from {fromId} to {toId} with relationship {relationshipType} added.");
        }




        [HttpGet("command")]
        public IActionResult Command([FromQuery] string query)
        {
            try
            {
                var result = graph.ExecuteCypherCommand(query); // Use ExecuteCypherCommand for Cypher-style queries
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error processing command: {ex.Message}");
            }
        }


        [HttpPost("create")]
        public IActionResult CreateGraph([FromBody] GraphDbRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.DbName))
            {
                return BadRequest("Database name must be provided.");
            }
            graph = new Graph(request.DbName);

            return Ok($"Graph '{request.DbName}' created and saved.");
        }

        [HttpPost("load")]
        public IActionResult LoadGraph([FromBody] GraphDbRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.DbName))
            {
                return BadRequest("Database name must be provided.");
            }


            try
            {
                graph = new Graph(request.DbName);
                graph.LoadGraph();
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
