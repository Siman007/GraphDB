using System;
using Newtonsoft.Json;

namespace GraphDB
{
    public class GraphData
    {
        [JsonProperty("nodes")]
        public List<Node> Nodes { get; set; } = new List<Node>(); // Initialization ensures that we avoid null references

        [JsonProperty("edges")]
        public List<Edge> Edges { get; set; } = new List<Edge>();
    }
}

