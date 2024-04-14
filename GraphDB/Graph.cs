using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace GraphDB
{
    public class Graph
    {
        private const string DefaultFilePath = "data";
        private readonly string _graphPath;
        private readonly string _graphName;
        private bool _isDatabaseLoaded;

        public List<Node> Nodes { get; private set; } = new List<Node>();
        public List<Edge> Edges { get; private set; } = new List<Edge>();

        private readonly Dictionary<string, List<Node>> _nodeIndex = new Dictionary<string, List<Node>>();
        private readonly Dictionary<string, List<Edge>> _edgeIndex = new Dictionary<string, List<Edge>>();

        public Graph(string graphName)
        {
            _graphName = graphName ?? throw new ArgumentNullException(nameof(graphName));
            _graphPath = Path.Combine(DefaultFilePath, $"{_graphName}.json");
            LoadGraph();
        }

        public bool IsDatabaseLoaded => _isDatabaseLoaded;

        public void AddNode(Node node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (!Nodes.Exists(n => n.Id == node.Id))
            {
                Nodes.Add(node);
                UpdateNodeIndex(node);
            }
        }

        public void AddEdge(string fromId, string toId, double weight, string relationshipType, Dictionary<string, object> properties = null)
        {
            if (string.IsNullOrEmpty(fromId) || string.IsNullOrEmpty(toId)) return;

            var fromNode = Nodes.FirstOrDefault(n => n.Id == fromId);
            var toNode = Nodes.FirstOrDefault(n => n.Id == toId);
            if (fromNode == null || toNode == null) return;

            var edge = new Edge
            {
                FromId = fromId,
                ToId = toId,
                Weight = weight,
                RelationshipType = relationshipType,
                Properties = properties ?? new Dictionary<string, object>()
            };

            if (!Edges.Any(e => e.Equals(edge)))
            {
                Edges.Add(edge);
                UpdateEdgeIndex(edge);
            }
        }

        public void UpdateNodeIndex(Node node)
        {
            if (node == null || string.IsNullOrEmpty(node.Id)) return;
            if (!_nodeIndex.TryGetValue(node.Id, out var nodes))
            {
                nodes = new List<Node>();
                _nodeIndex[node.Id] = nodes;
            }
            nodes.Add(node);
        }

        public void UpdateEdgeIndex(Edge edge)
        {
            if (edge == null || string.IsNullOrEmpty(edge.RelationshipType)) return;
            if (!_edgeIndex.TryGetValue(edge.RelationshipType, out var edges))
            {
                edges = new List<Edge>();
                _edgeIndex[edge.RelationshipType] = edges;
            }
            edges.Add(edge);
        }

        //public void SaveGraph()
        //{
        //    var graphData = new GraphData
        //    {
        //        Nodes = Nodes,
        //        Edges = Edges
        //    };

        //    var json = JsonConvert.SerializeObject(graphData, Formatting.Indented);
        //    File.WriteAllText(_graphPath, json);
        //    _isDatabaseLoaded = true;
        //}

        private void LoadGraph()
        {
            if (File.Exists(_graphPath))
            {
                try
                {
                    var json = File.ReadAllText(_graphPath);
                    var graphData = JsonConvert.DeserializeObject<GraphData>(json);
                    if (graphData != null)
                    {
                        Nodes = graphData.Nodes ?? new List<Node>();
                        Edges = graphData.Edges ?? new List<Edge>();
                        foreach (var node in Nodes) UpdateNodeIndex(node);
                        foreach (var edge in Edges) UpdateEdgeIndex(edge);
                    }
                    _isDatabaseLoaded = true;
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Failed to parse the graph data: {ex.Message}");
                    InitializeEmptyGraph();
                }
            }
            else
            {
                InitializeEmptyGraph();
            }
        }

        public List<Node> QueryNodesByProperty(string propertyName, string propertyValue)
        {
            return Nodes.Where(n => n.Properties.ContainsKey(propertyName) &&
                                    n.Properties[propertyName].ToString() == propertyValue).ToList();
        }



        // This method finds all neighbors of a given node identified by nodeId. Optionally filters by label if provided.
        public List<Node> FindNeighbors(string nodeId, string label = null)
        {
            var neighbors = new List<Node>();

            // Find all edges where the nodeId is either the starting or ending point
            var connectedEdges = Edges.Where(e => e.FromId == nodeId || e.ToId == nodeId);

            foreach (var edge in connectedEdges)
            {
                // Determine the neighbor node's id based on whether the nodeId is the 'From' or 'To' in the edge
                var neighborId = edge.FromId == nodeId ? edge.ToId : edge.FromId;

                // Fetch the neighbor node from Nodes collection
                var neighbor = Nodes.FirstOrDefault(n => n.Id == neighborId);

                // If a label is specified, filter nodes by label
                if (neighbor != null && (label == null || neighbor.Label == label))
                {
                    neighbors.Add(neighbor);
                }
            }

            return neighbors.Distinct().ToList(); // Ensure unique nodes are returned if multiple edges connect to the same neighbor
        }
        private void InitializeEmptyGraph()
        {
            Nodes = new List<Node>();
            Edges = new List<Edge>();
            _isDatabaseLoaded = false;
        }
    }

    public class GraphData
    {
        public List<Node> Nodes { get; set; }
        public List<Edge> Edges { get; set; }
    }
}
