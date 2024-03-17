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
            var fromNode = graph.Nodes.FirstOrDefault(n => n.Id == edgeData.FromId.ToString());
            var toNode = graph.Nodes.FirstOrDefault(n => n.Id == edgeData.ToId.ToString());

            if (fromNode == null || toNode == null)
            {
                return BadRequest("One or both nodes not found.");
            }

            // Assuming edgeData.Properties is a collection of key-value pairs
            Dictionary<string, object> properties = new Dictionary<string, object>();
            if (edgeData.Properties != null)
            {
                foreach (var prop in edgeData.Properties)
                {
                    properties.Add(prop.Name, (object)prop.Value.ToString());
                }
            }

            var newEdge = new Edge
            {
                From = fromNode,
                To = toNode,
                Relationship = edgeData.Relationship,
                Properties = properties
            };

            graph.AddEdge(newEdge);
            return Ok($"Edge from {newEdge.From.Id} to {newEdge.To.Id} added.");
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
