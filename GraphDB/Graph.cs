using Newtonsoft.Json;

namespace GraphDB
{
    /// <summary>
    /// Represents a graph containing nodes and edges with basic graph operations.
    /// </summary>
    public class Graph
    {
        private const string DefaultFilePath = "data";
        private readonly string _graphPath;
        private readonly string _graphName;
        private bool _isDatabaseLoaded = false;
        private bool _isDatabaseNew = true;
        public string DataBaseName = "";

        public List<Node> Nodes { get; private set; } = new List<Node>();
        public List<Edge> Edges { get; private set; } = new List<Edge>();

        public readonly Dictionary<string, List<Node>> _nodeIndex = new Dictionary<string, List<Node>>();
        public readonly Dictionary<string, List<Edge>> _edgeIndex = new Dictionary<string, List<Edge>>();

        public Graph(string graphName)
        {
            _graphName = graphName ?? throw new ArgumentNullException(nameof(graphName));
            _graphPath = Path.Combine(DefaultFilePath, $"{_graphName}.json");
            Nodes = new List<Node>();
            Edges = new List<Edge>();
            InitializeEmptyGraph(); //LoadOrCreateGraph();
        }

        public string GetDatabaseName()
        {
            return _graphName;
        }

        public bool IsDatabaseLoaded => _isDatabaseLoaded;
        public bool IsDatabaseNew => _isDatabaseNew;
       
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
        public void DeleteEdge(string fromNodeId, string toNodeId, string relationshipType)
        {
            // Find the edge with the given start, end, and relationship type
            var edgeToRemove = Edges.FirstOrDefault(e => e.FromId == fromNodeId && e.ToId == toNodeId && e.RelationshipType == relationshipType);
            if (edgeToRemove == null)
                return;

            // Remove the edge from the list and its index
            Edges.Remove(edgeToRemove);
            if (_edgeIndex.ContainsKey(relationshipType))
            {
                _edgeIndex[relationshipType].Remove(edgeToRemove);
                if (_edgeIndex[relationshipType].Count == 0)
                    _edgeIndex.Remove(relationshipType);
            }
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


        public bool CheckEdgeExists(string fromNodeId, string toNodeId)
        {
            // Check if any edge exists with the given fromNodeId and toNodeId
            return Edges.Any(edge => edge.FromId == fromNodeId && edge.ToId == toNodeId);
        }

       
        public List<Edge> QueryEdgesByProperty(string propertyName, string propertyValue)
        {
            // Check if the property name or value provided is null or empty to avoid errors during querying.
            if (string.IsNullOrEmpty(propertyName) || propertyValue == null)
            {
                throw new ArgumentException("Property name and value must be provided.");
            }

            // Filter and return edges where the property exists and matches the specified value.
            return Edges.Where(e => e.Properties.ContainsKey(propertyName) &&
                                    e.Properties[propertyName].ToString() == propertyValue).ToList();
        }
        public void RemoveEdgeFromIndex(Edge edge)
        {
            if (edge == null)
            {
                throw new ArgumentNullException(nameof(edge), "Edge cannot be null when trying to remove from index.");
            }

            // Assuming the edge index is by 'RelationshipType'
            if (_edgeIndex.ContainsKey(edge.RelationshipType))
            {
                // Remove the edge from the list in the index
                _edgeIndex[edge.RelationshipType].Remove(edge);

                // If there are no more edges of this type, consider cleaning up the dictionary
                if (_edgeIndex[edge.RelationshipType].Count == 0)
                {
                    _edgeIndex.Remove(edge.RelationshipType);
                }
            }
        }

        public void AddNode(Node node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (!Nodes.Exists(n => n.Id == node.Id))
            {
                Nodes.Add(node);
                UpdateNodeIndex(node);
            }
        }

        public void DeleteNode(string nodeId)
        {
            // Check if the node exists
            var nodeToRemove = Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (nodeToRemove == null)
                return;

            // Remove all edges connected to this node
            var connectedEdges = Edges.Where(e => e.FromId == nodeId || e.ToId == nodeId).ToList();
            foreach (var edge in connectedEdges)
            {
                Edges.Remove(edge);
                if (_edgeIndex.ContainsKey(edge.RelationshipType))
                {
                    _edgeIndex[edge.RelationshipType].Remove(edge);
                    if (_edgeIndex[edge.RelationshipType].Count == 0)
                        _edgeIndex.Remove(edge.RelationshipType);
                }
            }

            // Remove the node
            Nodes.Remove(nodeToRemove);
            RemoveNodeFromIndex(nodeToRemove);
        }

        public bool CheckNodeProperty(string nodeId, string propertyName, string propertyValue)
        {
            var node = Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node != null && node.Properties.TryGetValue(propertyName, out var value))
            {
                return value.ToString() == propertyValue;
            }
            return false;
        }
        public bool CheckNodeExists(string nodeId)
        {

            return Nodes.Any(node => node.Id == nodeId);
        }
        public bool CheckNumericNodeProperty(string nodeId, string propertyName, string comparison, double comparisonValue)
        {
            var node = Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node == null || !node.Properties.ContainsKey(propertyName))
            {
                return false;
            }

            // Try to parse the property value as double
            if (double.TryParse(node.Properties[propertyName].ToString(), out double propertyValue))
            {
                switch (comparison)
                {
                    case ">":
                        return propertyValue > comparisonValue;
                    case "<":
                        return propertyValue < comparisonValue;
                    case "=":
                        return Math.Abs(propertyValue - comparisonValue) < 0.0001; // Considering a small tolerance for floating-point comparisons
                    default:
                        throw new ArgumentException("Invalid comparison operator. Only '>', '<', '=' are supported.");
                }
            }
            return false;
        }
        public void RemoveNodeFromIndex(Node node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node), "Node cannot be null when trying to remove from index.");
            }

            // Check if the node's ID is indexed, and if so, remove the node from that list.
            if (_nodeIndex.ContainsKey(node.Id))
            {
                _nodeIndex[node.Id].Remove(node);

                // If there are no more nodes in the list for this ID, clean up by removing the key from the dictionary.
                if (_nodeIndex[node.Id].Count == 0)
                {
                    _nodeIndex.Remove(node.Id);
                }
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


        public void loadNodes(List<Node> nodes)
        {
            Nodes = nodes;
            foreach (var node in Nodes) UpdateNodeIndex(node);
        }
        public void loadEdges(List<Edge> edges)
        {
            Edges = edges;
            foreach (var edge in Edges) UpdateEdgeIndex(edge);
        }
        //private void LoadOrCreateGraph()
        //{
        //    if (File.Exists(_graphPath))
        //    {
        //        try
        //        {
        //            var json = File.ReadAllText(_graphPath);
        //            var graphData = JsonConvert.DeserializeObject<GraphData>(json);
        //            if (graphData == null) throw new JsonException("Deserialized graph data is null.");

        //            Nodes = graphData.Nodes ?? new List<Node>();
        //            Edges = graphData.Edges ?? new List<Edge>();
        //            foreach (var node in Nodes) UpdateNodeIndex(node);
        //            foreach (var edge in Edges) UpdateEdgeIndex(edge);

        //            _isDatabaseLoaded = true;
        //            _isDatabaseNew = false;
        //        }
        //        catch (JsonException ex)
        //        {
        //            Console.WriteLine($"Failed to parse the graph data: {ex.Message}");
        //            InitializeEmptyGraph();

        //        }
        //        catch (Exception ex)
        //        {
        //            Console.WriteLine($"An error occurred while loading the graph: {ex.Message}");
        //            InitializeEmptyGraph();
        //        }
        //    }
        //    else
        //    {
        //        InitializeEmptyGraph();
        //    }
        //}
        private void InitializeEmptyGraph()
        {
            Nodes = new List<Node>();
            Edges = new List<Edge>();
            _isDatabaseLoaded = true;
            _isDatabaseNew = true;
        }
    }

  
}
