using System;


using System.Text.RegularExpressions;
using Newtonsoft.Json;


using CsvHelper;
using CsvHelper.Configuration;

using System.Globalization;
using System.Text;


namespace GraphDB
    {


        public class Graph
        {
            private const string DefaultFilePath = "data"; // Relative path to store graph data
            private static string _graphPath="";
            private static string _graphName="";
            public static bool isDatabaseLoaded=false;

            public List<Node> Nodes { get; set; } = new List<Node>();
            public List<Edge> Edges { get; set; } = new List<Edge>();


            // Indexes
            private Dictionary<string, List<Node>> nodeIndex = new Dictionary<string, List<Node>>();
            private Dictionary<string, List<Edge>> edgeIndex = new Dictionary<string, List<Edge>>();


        public Graph(string graphName)
            {
                _graphName = graphName;
                _graphPath = Path.Combine(DefaultFilePath, $"{graphName}.json");
            }


        public dynamic ExecuteCypherCommand(string cypher)
        {
            CypherCommandType commandType = cypher.ToCommandType();
            switch (commandType)
            {
                case CypherCommandType.CreateNode:
                    return HandleCreateNode(cypher);
                case CypherCommandType.MergeNode:
                    return HandleMergeNode(cypher);
                case CypherCommandType.CreateRelationship:
                    return HandleCreateRelationship(cypher);
                case CypherCommandType.MatchNode:
                    return HandleMatchNode(cypher);
                case CypherCommandType.MatchRelationship:
                    return HandleMatchRelationship(cypher);
                case CypherCommandType.DeleteNode:
                    return HandleDeleteNode(cypher);
                case CypherCommandType.DetachDeleteNode:
                    return HandleDetachDelete(cypher);
                case CypherCommandType.DeleteRelationship:
                    return HandleDeleteRelationship(cypher);
                case CypherCommandType.SetNodeProperty:
                    return HandleSetNodeProperty(cypher);
                case CypherCommandType.SetRelationshipProperty:
                    return HandleSetRelationshipProperty(cypher);
                case CypherCommandType.ImportCsv:
                    return HandleImportCsv(cypher);
                case CypherCommandType.ImportJSON:
                    return HandleImportJSON(cypher);
                case CypherCommandType.ExportCsvNodes:
                    return HandleExportCsvNodes(cypher);
                case CypherCommandType.ExportCsvEdges:
                    return HandleExportCsvEdges(cypher);
                case CypherCommandType.Conditional:
                    return HandleConditional(cypher);
                case CypherCommandType.Case:
                    return HandleCase(cypher);
                case CypherCommandType.Help:
                    return HandleDisplayHelp(cypher);
                case CypherCommandType.CountNodes:
                    return HandleCountNodes();
                case CypherCommandType.CountEdges:
                    return HandleCountEdges();
                case CypherCommandType.AggregateSum:
                    return HandleAggregateSum(cypher);
                case CypherCommandType.AggregateAvg:
                    return HandleAggregateAvg(cypher);
                case CypherCommandType.FindRelationships:
                    return HandleFindRelationships(cypher);
                case CypherCommandType.FindNeighbors:
                    return HandleFindNeighbors(cypher);
                    
                case CypherCommandType.MatchPattern:
                    return HandleMatchPattern(cypher);
                default:
                    return ApiResponse<string>.ErrorResponse("Unsupported Cypher command.");
            }
        }


        public static string GetDatabaseName()
        {
            return _graphName;
            
        }
        public static  string GetDatabasePath()
        {
            return _graphPath;
        }
        public  Boolean  GetDatabaseLoaded()
        {
            return isDatabaseLoaded;
        }



        private ApiResponse<object> HandleDisplayHelp(string cypher)
        {
            var parts = cypher.Trim().Split(new char[] { ' ' }, 2);
            var command = parts[0].ToUpper();
            var message = "";

            if (command == "HELP")
            {
                var specificCommand = parts.Length > 1 ? parts[1] : "";
                message = GraphHelp.GetHelp(specificCommand);
            }
            else
            {
                message = GraphHelp.GetHelp();
            }

            // Since this method doesn't return specific data, the Data property is set to null.
            return ApiResponse<object>.SuccessResponse(null, message);
        }




        private ApiResponse<NodeResponse> HandleNodeCreationOrMerge(Match nodeMatch)
        {
            string operation = nodeMatch.Groups[1].Value.ToUpper();
            string nodeId = nodeMatch.Groups[2].Value;
            string label = nodeMatch.Groups[3].Value;
            var properties = ParseProperties(nodeMatch.Groups[4].Value);
            var objectProperties = properties.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value);

            var node = Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (operation == "MERGE")
            {
                if (node == null)
                {
                    node = new Node { Id = nodeId, Properties = objectProperties };
                    Nodes.Add(node);
                    UpdateNodeIndex(node); // Update index when a node is added or merged
                    SaveToFile();
                    return ApiResponse<NodeResponse>.SuccessResponse(
                        new NodeResponse { Id = node.Id, Label = label, Properties = node.Properties },
                        $"Merged (created) new node with id {nodeId}.");
                }
                else
                {
                    foreach (var prop in objectProperties)
                    {
                        // Update properties and re-index if necessary
                        if (node.Properties.ContainsKey(prop.Key) && !node.Properties[prop.Key].Equals(prop.Value))
                        {
                            node.Properties[prop.Key] = prop.Value;
                            UpdateNodeIndex(node); // Re-index if properties affecting indexing are updated
                        }
                        else if (!node.Properties.ContainsKey(prop.Key))
                        {
                            node.Properties[prop.Key] = prop.Value;
                        }
                    }
                    SaveToFile();
                    return ApiResponse<NodeResponse>.SuccessResponse(
                        new NodeResponse { Id = node.Id, Label = label, Properties = node.Properties },
                        $"Merged (updated) node {nodeId} with new properties.");
                }
            }
            else // CREATE
            {
                if (node != null)
                {
                    return ApiResponse<NodeResponse>.ErrorResponse(
                        $"Node with id {nodeId} already exists. Cannot create duplicate.");
                }
                else
                {
                    node = new Node { Id = nodeId, Properties = objectProperties };
                    Nodes.Add(node);
                    UpdateNodeIndex(node); // Update index when a node is added
                    SaveToFile();
                    return ApiResponse<NodeResponse>.SuccessResponse(
                        new NodeResponse { Id = node.Id, Label = label, Properties = node.Properties },
                        $"Created new node with id {nodeId}.");
                }
            }
        }



        private ApiResponse<RelationshipResponse> HandleCreateRelationship(string cypher)
        {
            var pattern = new Regex(@"CREATE \((\w+)\)-\[:(\w+)\]->\((\w+)\) \{(.*)\}", RegexOptions.IgnoreCase);
            var match = pattern.Match(cypher);
            if (!match.Success)
            {
                return ApiResponse<RelationshipResponse>.ErrorResponse("Invalid CREATE syntax for relationship.");
            }

            string fromNodeId = match.Groups[1].Value;
            string relationshipType = match.Groups[2].Value;
            string toNodeId = match.Groups[3].Value;
            var propertiesString = match.Groups[4].Value;

            var fromNode = Nodes.FirstOrDefault(n => n.Id == fromNodeId);
            var toNode = Nodes.FirstOrDefault(n => n.Id == toNodeId);
            if (fromNode == null || toNode == null)
            {
                return ApiResponse<RelationshipResponse>.ErrorResponse("One or both specified nodes do not exist.");
            }

            var properties = ParseProperties(propertiesString);
            var existingEdge = Edges.FirstOrDefault(e => e.FromId == fromNodeId && e.ToId == toNodeId && e.RelationshipType == relationshipType);
            if (existingEdge != null)
            {
                return ApiResponse<RelationshipResponse>.ErrorResponse($"A relationship of type {relationshipType} from {fromNodeId} to {toNodeId} already exists.");
            }

            var edge = new Edge
            {
                FromId = fromNodeId,
                ToId = toNodeId,
                RelationshipType = relationshipType,
                Properties = properties.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value) // Ensure properties are correctly typed as object
            };
            Edges.Add(edge);
            UpdateEdgeIndex(edge); // Update index when an edge is added
            SaveToFile();

            // Create the response DTO
            var relationshipResponse = new RelationshipResponse
            {
                FromId = edge.FromId,
                ToId = edge.ToId,
                RelationshipType = edge.RelationshipType,
                Properties = edge.Properties
            };

            return ApiResponse<RelationshipResponse>.SuccessResponse(relationshipResponse, $"Created relationship of type {relationshipType} from {fromNodeId} to {toNodeId}.");
        }



        public void AddNode(Node node)
        {
            if (!Nodes.Exists(n => n.Id == node.Id))
            {
                Nodes.Add(node);
                UpdateNodeIndex(node); // Update index when a node is added
                SaveToFile();
            }
        }

        public void AddEdge(string fromId, string toId, double weight, string relationshipType, Dictionary<string, object> properties = null)
        {
            var fromNode = Nodes.FirstOrDefault(n => n.Id == fromId);
            var toNode = Nodes.FirstOrDefault(n => n.Id == toId);

            if (fromNode != null && toNode != null && !Edges.Any(e => e.FromId == fromId && e.ToId == toId && e.RelationshipType == relationshipType))
            {
                var edge = new Edge
                {
                    FromId = fromId,
                    ToId = toId,
                    Weight = weight,
                    RelationshipType = relationshipType,
                    Properties = properties ?? new Dictionary<string, object>()
                };
                Edges.Add(edge);
                UpdateEdgeIndex(edge); // Update index when an edge is added
                SaveToFile();
            }
        }



    


        private ApiResponse<object> HandleDeleteNode(string cypher)
        {
            var deleteByIdPattern = new Regex(@"MATCH \(n\) WHERE n\.id = '(\w+)' DELETE n", RegexOptions.IgnoreCase);
            var deleteByLabelPattern = new Regex(@"MATCH \(n:(\w+)\) DELETE n", RegexOptions.IgnoreCase);

            // Attempt to match DELETE by ID pattern
            var matchById = deleteByIdPattern.Match(cypher);
            if (matchById.Success)
            {
                string nodeId = matchById.Groups[1].Value;
                var nodeToDelete = Nodes.FirstOrDefault(n => n.Properties.ContainsKey("id") && n.Properties["id"].ToString() == nodeId);
                if (nodeToDelete != null)
                {
                    Nodes.Remove(nodeToDelete);
                    // Explicitly manage index on delete
                    if (nodeToDelete.Properties.ContainsKey("id"))
                    {
                        var idValue = nodeToDelete.Properties["id"].ToString();
                        if (nodeIndex.ContainsKey(idValue))
                        {
                            nodeIndex[idValue].Remove(nodeToDelete);
                        }
                    }
                    SaveToFile();
                    return ApiResponse<object>.SuccessResponse(null, $"Node with ID {nodeId} deleted successfully.");
                }
                return ApiResponse<object>.ErrorResponse($"No node found with ID {nodeId}.");
            }

            // Attempt to match DELETE by Label pattern
            var matchByLabel = deleteByLabelPattern.Match(cypher);
            if (matchByLabel.Success)
            {
                string nodeLabel = matchByLabel.Groups[1].Value;
                var nodesToDelete = Nodes.Where(n => n.Label == nodeLabel).ToList();
                if (nodesToDelete.Any())
                {
                    foreach (var node in nodesToDelete)
                    {
                        Nodes.Remove(node);

                        // Explicitly manage index on delete
                        if (node.Properties.ContainsKey("id"))
                        {
                            var idValue = node.Properties["id"].ToString();
                            if (nodeIndex.ContainsKey(idValue))
                            {
                                nodeIndex[idValue].Remove(node);
                            }
                        }
                    }
                    
                    SaveToFile();
                    return ApiResponse<object>.SuccessResponse(null, $"Nodes with label {nodeLabel} deleted successfully.");
                }
                return ApiResponse<object>.ErrorResponse($"No nodes found with label {nodeLabel}.");
            }

            return ApiResponse<object>.ErrorResponse("Invalid DELETE syntax.");
        }




        public ApiResponse<object> HandleDetachDelete(string cypher)
        {
            var pattern = new Regex(@"DETACH DELETE (\w+)(?:\s*:\s*(\w+))?", RegexOptions.IgnoreCase);
            var match = pattern.Match(cypher);
            if (!match.Success)
            {
                return ApiResponse<object>.ErrorResponse("Invalid DETACH DELETE syntax.");
            }

            string nodeIdOrLabel = match.Groups[1].Value;
            string label = match.Groups[2].Success ? match.Groups[2].Value : null;

            Predicate<Node> deletionCriteria;
            List<Node> nodesToDelete;
            if (string.IsNullOrEmpty(label))
            {
                // If deleting by ID, find the node directly
                var node = Nodes.FirstOrDefault(n => n.Id == nodeIdOrLabel);
                if (node == null) return ApiResponse<object>.ErrorResponse($"Node {nodeIdOrLabel} does not exist.");
                nodesToDelete = new List<Node> { node };
            }
            else
            {
                // If deleting by label, find all nodes matching the label
                nodesToDelete = Nodes.Where(n => n.Properties.TryGetValue("label", out var nodeLabel) && nodeLabel.ToString() == label).ToList();
                if (!nodesToDelete.Any()) return ApiResponse<object>.ErrorResponse($"No nodes with label {label} exist.");
            }

            foreach (var node in nodesToDelete)
            {
                // Remove the node from its index
                if (node.Properties.ContainsKey("id"))
                {
                    var idValue = node.Properties["id"].ToString();
                    if (nodeIndex.ContainsKey(idValue))
                    {
                        nodeIndex[idValue].Remove(node);
                    }
                }

                // Detach and delete all connected edges
                var connectedEdges = Edges.Where(e => e.FromId == node.Id || e.ToId == node.Id).ToList();
                foreach (var edge in connectedEdges)
                {
                    // Remove the edge from its index
                    if (edge.Properties.ContainsKey("type"))
                    {
                        var typeValue = edge.Properties["type"].ToString();
                        if (edgeIndex.ContainsKey(typeValue))
                        {
                            edgeIndex[typeValue].Remove(edge);
                        }
                    }
                    Edges.Remove(edge);
                }

                // Finally, remove the node
                Nodes.Remove(node);
            }

            SaveToFile(); // Save changes after all deletions
            return ApiResponse<object>.SuccessResponse(null, label == null ? $"Node {nodeIdOrLabel} and all its relationships have been deleted." : $"Nodes with label {label} and all their relationships have been deleted.");
        }






        public ApiResponse<object> HandleDeleteRelationship(string cypher)
        {
            var pattern = new Regex(@"DELETE RELATIONSHIP FROM \((\w+)\) TO \((\w+)\) TYPE (\w+)", RegexOptions.IgnoreCase);
            var match = pattern.Match(cypher);
            if (!match.Success)
            {
                return ApiResponse<object>.ErrorResponse("Invalid DELETE RELATIONSHIP syntax.");
            }

            string fromNodeId = match.Groups[1].Value;
            string toNodeId = match.Groups[2].Value;
            string relationshipType = match.Groups[3].Value;

            // Find the specific edge to remove
            var edgeToRemove = Edges.FirstOrDefault(e => e.FromId == fromNodeId && e.ToId == toNodeId && e.RelationshipType == relationshipType);
            if (edgeToRemove == null)
            {
                return ApiResponse<object>.ErrorResponse($"Relationship {relationshipType} from {fromNodeId} to {toNodeId} does not exist.");
            }

            // Remove the edge from the edge index if necessary
            if (edgeToRemove.Properties.ContainsKey("type"))
            {
                var typeValue = edgeToRemove.Properties["type"].ToString();
                if (edgeIndex.ContainsKey(typeValue))
                {
                    edgeIndex[typeValue].Remove(edgeToRemove);
                    if (edgeIndex[typeValue].Count == 0)
                    {
                        edgeIndex.Remove(typeValue); // Remove the entry from the index if no more edges of this type exist
                    }
                }
            }

            // Remove the edge from the Edges list
            Edges.Remove(edgeToRemove);

            SaveToFile(); // Save changes after the deletion
            return ApiResponse<object>.SuccessResponse(null, $"Relationship {relationshipType} from {fromNodeId} to {toNodeId} has been deleted.");
        }

        // Method to update a node's property and adjust the index accordingly
        public void UpdateNodeProperty(string nodeId, string propertyName, object propertyValue)
        {
            var node = Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node != null && propertyName == "id") // Assuming we're indexing nodes by 'id'
            {
                // Remove from old index entry if necessary
                if (node.Properties.ContainsKey(propertyName))
                {
                    var oldPropertyValue = node.Properties[propertyName].ToString();
                    nodeIndex[oldPropertyValue].Remove(node);
                }

                // Update the node's property
                node.Properties[propertyName] = propertyValue.ToString();

                // Add to new index entry
                UpdateNodeIndex(node);
                SaveToFile();
            }
        }

        // Method to update an edge's property and adjust the index accordingly
        public void UpdateEdgeProperty(string fromId, string toId, string relationshipType, string propertyName, object propertyValue)
        {
            var edge = Edges.FirstOrDefault(e => e.FromId == fromId && e.ToId == toId && e.RelationshipType == relationshipType);
            if (edge != null && propertyName == "type") // Assuming we're indexing edges by 'type'
            {
                // Remove from old index entry if necessary
                if (edge.Properties.ContainsKey(propertyName))
                {
                    var oldPropertyValue = edge.Properties[propertyName].ToString();
                    edgeIndex[oldPropertyValue].Remove(edge);
                }

                // Update the edge's property
                edge.Properties[propertyName] = propertyValue.ToString();

                // Add to new index entry
                UpdateEdgeIndex(edge);
                SaveToFile();
            }
        }


        public ApiResponse<List<Edge>> HandleMatchRelationship(string cypher)
        {
            var pattern = new Regex(@"MATCH \((\w+)\)-\[r:(\w+)\s*\{(?:weight: '([><=]?)(\d+)')?\}\]->\((\w+)\) WHERE r\.(\w+) = '([^']+)' RETURN r", RegexOptions.IgnoreCase);
            var match = pattern.Match(cypher);
            if (!match.Success)
            {
                return ApiResponse<List<Edge>>.ErrorResponse("Invalid MATCH syntax for relationship.");
            }

            // Extracting values from the command
            string startNodeAlias = match.Groups[1].Value;
            string relationshipType = match.Groups[2].Value;
            string weightComparison = match.Groups[3].Value;
            string weightValue = match.Groups[4].Value;
            string endNodeAlias = match.Groups[5].Value;
            string propertyName = match.Groups[6].Value;
            string propertyValue = match.Groups[7].Value;

            // Utilize indexing for improved query performance
            IEnumerable<Edge> potentialEdges;
            if (propertyName.Equals("type", StringComparison.OrdinalIgnoreCase) && edgeIndex.ContainsKey(propertyValue))
            {
                potentialEdges = edgeIndex[propertyValue];
            }
            else
            {
                // Fallback to scanning all edges if not querying by the indexed property or if the property value is not indexed
                potentialEdges = Edges;
            }

            // Filter edges based on the detailed criteria
            var filteredEdges = potentialEdges
                .Where(edge => edge.RelationshipType == relationshipType &&
                               CheckWeightCondition(edge.Weight, weightComparison, weightValue) &&
                               edge.Properties.TryGetValue(propertyName, out var value) && value.ToString() == propertyValue)
                .ToList();

            if (filteredEdges.Any())
            {
                return ApiResponse<List<Edge>>.SuccessResponse(filteredEdges, "Relationships found matching criteria.");
            }
            else
            {
                return ApiResponse<List<Edge>>.ErrorResponse("No relationships found matching criteria.");
            }
        }

        private bool CheckWeightCondition(double edgeWeight, string comparison, string value)
        {
            if (string.IsNullOrEmpty(comparison) || string.IsNullOrEmpty(value))
            {
                // If no weight condition is specified, don't filter by weight
                return true;
            }

            double weightValue = double.Parse(value);
            switch (comparison)
            {
                case ">": return edgeWeight > weightValue;
                case "<": return edgeWeight < weightValue;
                case "=": return Math.Abs(edgeWeight - weightValue) < 0.001; // Use a tolerance for floating point comparison
                default: throw new ArgumentException("Invalid weight comparison operator.");
            }
        }




        //private string FormatRelationships(List<Edge> edges)
        //{
        //    return JsonConvert.SerializeObject(edges); // Convert the list of edges directly to JSON
        //}


        private string FormatRelationships(List<Edge> edges)
        {
            var stringBuilder = new StringBuilder();
            foreach (var edge in edges)
            {
                stringBuilder.AppendLine($"Relationship: {edge.RelationshipType}, From: {edge.FromId}, To: {edge.ToId}, Weight: {edge.Weight}");
                foreach (var prop in edge.Properties)
                {
                    stringBuilder.AppendLine($"\t{prop.Key}: {prop.Value}");
                }
            }
            return stringBuilder.ToString();
        }




        public ApiResponse<RelationshipResponse> HandleSetRelationshipProperty(string cypher)
        {
            var pattern = new Regex(@"SET RELATIONSHIP \((\w+)\)-\[(\w+)\]->\((\w+)\) \{(.+)\}", RegexOptions.IgnoreCase); // Fixed Regex
            var match = pattern.Match(cypher);
            if (!match.Success)
            {
                return ApiResponse<RelationshipResponse>.ErrorResponse("Invalid syntax for SET RELATIONSHIP.");
            }

            string fromId = match.Groups[1].Value;
            string toId = match.Groups[3].Value; // Adjusted index based on corrected regex
            string relationshipType = match.Groups[2].Value; // Extracting relationshipType
            var propertiesString = match.Groups[4].Value;
            var properties = ParseProperties(propertiesString);

            var edge = Edges.FirstOrDefault(e => e.FromId == fromId && e.ToId == toId && e.RelationshipType == relationshipType);
            if (edge == null)
            {
                return ApiResponse<RelationshipResponse>.ErrorResponse("Relationship does not exist.");
            }

            // Track if any indexed property is updated to determine if re-indexing is necessary
            bool needsReindexing = false;

            // Update properties
            foreach (var prop in properties)
            {
                if (edge.Properties.ContainsKey(prop.Key) && edge.Properties[prop.Key] != prop.Value)
                {
                    needsReindexing = needsReindexing || IsIndexedProperty(prop.Key);
                    edge.Properties[prop.Key] = prop.Value;
                }
                else if (!edge.Properties.ContainsKey(prop.Key))
                {
                    needsReindexing = needsReindexing || IsIndexedProperty(prop.Key);
                    edge.Properties.Add(prop.Key, prop.Value);
                }
            }

            if (needsReindexing)
            {
                // If an indexed property was updated, re-index this edge
                UpdateEdgeIndex(edge); // Assuming UpdateEdgeIndex handles adding/updating the index entry
            }

            SaveToFile();

            var relationshipResponse = new RelationshipResponse
            {
                FromId = edge.FromId,
                ToId = edge.ToId,
                RelationshipType = edge.RelationshipType,
                Properties = edge.Properties
            };

            return ApiResponse<RelationshipResponse>.SuccessResponse(relationshipResponse, $"Updated properties of relationship from {fromId} to {toId}.");
        }

        private bool IsIndexedProperty(string propertyName)
        {
            // Here, define logic to determine if a property is indexed
            // For example, if indexing edges by 'type', then:
            return propertyName.Equals("type", StringComparison.OrdinalIgnoreCase);
            // Extend this logic based on actual indexed properties
        }




        private string HandleSetNodeProperty(string cypher)
        {
            var pattern = new Regex(@"SET NODE \((\w+)\) SET (\w+) = '(.+)'", RegexOptions.IgnoreCase);
            var match = pattern.Match(cypher);
            if (!match.Success) return "Invalid SET NODE syntax.";

            string nodeId = match.Groups[1].Value;
            string propertyName = match.Groups[2].Value;
            string propertyValue = match.Groups[3].Value;

            var node = Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node == null)
            {
                return $"Node {nodeId} does not exist.";
            }

            // Determine if the property update requires re-indexing
            bool needsReindexing = node.Properties.ContainsKey(propertyName) && !node.Properties[propertyName].Equals(propertyValue);
            needsReindexing = needsReindexing || IsIndexedProperty(propertyName);

            // Set or update the property value
            node.Properties[propertyName] = propertyValue;

            if (needsReindexing)
            {
                // Update the index if necessary
                UpdateNodeIndex(node); // Ensure this method handles both adding new and updating existing index entries
            }

            SaveToFile(); // Assuming you have a method to save changes to the graph
            return $"Property {propertyName} of node {nodeId} has been set to {propertyValue}.";
        }





        private Dictionary<string, object> ParseProperties(string propertiesString)
        {
            var properties = new Dictionary<string, object>();
            var propsMatches = Regex.Matches(propertiesString, @"(\w+): '([^']*)'");
            foreach (Match match in propsMatches)
            {
                properties[match.Groups[1].Value] = match.Groups[2].Value;
            }
            return properties;
        }

        public void DeleteEdge(string fromNodeId, string toNodeId)
        {
            Edges.RemoveAll(e => e.FromId == fromNodeId && e.ToId == toNodeId);
            SaveToFile();
        }

        //public List<Node> QueryNodesByProperty(string propertyName, object value)
        //{
        //    return Nodes.Where(n => n.Properties.ContainsKey(propertyName) && n.Properties[propertyName].Equals(value)).ToList();
        //}

        public List<Node> QueryNodesByProperty(string propertyName, string propertyValue)
        {
            // Use the index for direct access if querying by the indexed property
            if (propertyName == "id" && nodeIndex.ContainsKey(propertyValue))
            {
                return nodeIndex[propertyValue];
            }
            else
            {
                // Fallback to scanning all nodes if not querying by the indexed property
                return Nodes.Where(n => n.Properties.ContainsKey(propertyName) && n.Properties[propertyName].ToString() == propertyValue).ToList();
            }
        }

        //public List<Edge> QueryEdgesByProperty(string propertyName, object value)
        //{
        //    return Edges.Where(e => e.Properties.ContainsKey(propertyName) && e.Properties[propertyName].Equals(value)).ToList();
        //}

        public List<Edge> QueryEdgesByProperty(string propertyName, string propertyValue)
        {
            // Directly access the index if querying by the indexed property (e.g., 'type')
            if (propertyName == "type" && edgeIndex.ContainsKey(propertyValue))
            {
                return edgeIndex[propertyValue];
            }
            else
            {
                // Fallback to scanning all edges if not querying by the indexed property
                return Edges.Where(e => e.Properties.ContainsKey(propertyName) && e.Properties[propertyName].ToString() == propertyValue).ToList();
            }
        }

        public ApiResponse<List<NodeResponse>> HandleFindNeighbors(string cypher)
        {
            var pattern = new Regex(@"FIND NEIGHBORS \(id:\s*'([^']*)'(?:,\s*label:\s*'([^']*)')?\)", RegexOptions.IgnoreCase);
            var match = pattern.Match(cypher);

            if (!match.Success)
            {
                return ApiResponse<List<NodeResponse>>.ErrorResponse("Invalid FIND NEIGHBORS syntax.");
            }

            string nodeId = match.Groups[1].Value;
            string label = match.Groups[2].Success ? match.Groups[2].Value : null;

            var neighbors = FindNeighbors(nodeId, label);
            if (neighbors.Any())
            {
                var neighborResponses = neighbors.Select(n => new NodeResponse
                {
                    Id = n.Id,
                    Label = n.Label,
                    Properties = n.Properties
                }).ToList();

                return ApiResponse<List<NodeResponse>>.SuccessResponse(neighborResponses, $"Found {neighbors.Count} neighbors.");
            }
            else
            {
                return ApiResponse<List<NodeResponse>>.ErrorResponse("No neighbors found.");
            }
        }

        // Adjusted FindNeighbors method to correctly reference node IDs in edge relationships
        public List<Node> FindNeighbors(string nodeId, string label = null)
        {
            var neighbors = new List<Node>();

            // Adjusted to correctly use node IDs for finding edges
            var outgoingNeighborsIds = Edges
                .Where(e => e.FromId == nodeId)
                .Select(e => e.ToId);

            var incomingNeighborsIds = Edges
                .Where(e => e.ToId == nodeId)
                .Select(e => e.FromId);

            var allNeighborIds = outgoingNeighborsIds.Concat(incomingNeighborsIds).Distinct();

            foreach (var neighborId in allNeighborIds)
            {
                var neighbor = Nodes.FirstOrDefault(n => n.Id == neighborId && (label == null || n.Label == label));
                if (neighbor != null)
                {
                    neighbors.Add(neighbor);
                }
            }

            return neighbors; // Distinct is already ensured by the Concat and FirstOrDefault operations
        }


        private ApiResponse<NodeResponse> HandleCreateNode(string cypher)
        {
            try
            {
                var pattern = new Regex(@"CREATE \((\w+):(\w+) \{(.+)\}\)", RegexOptions.IgnoreCase);
                var match = pattern.Match(cypher);
                if (!match.Success) return ApiResponse<NodeResponse>.ErrorResponse("Invalid CREATE syntax for node.");

                string nodeId = match.Groups[1].Value;
                string label = match.Groups[2].Value; // Assuming the label might be used for indexing or queries.
                var propertiesString = match.Groups[3].Value;
                var properties = ParseProperties(propertiesString);

                // Check if the node already exists.
                if (Nodes.Any(n => n.Id == nodeId))
                {
                    return ApiResponse<NodeResponse>.ErrorResponse($"Node with id {nodeId} already exists.");
                }

                // Create and add the new node.
                var newNode = new Node { Id = nodeId, Label = label, Properties = properties };
                Nodes.Add(newNode);

                // Update indexes with the new node. This method should handle both the primary index (e.g., on ID) and any secondary indexes you've implemented.
                UpdateNodeIndex(newNode);

                SaveToFile(); // Save changes to the database.

                // Return a success response with the created node details.
                var nodeResponse = new NodeResponse
                {
                    Id = newNode.Id,
                    Label = newNode.Label,
                    Properties = newNode.Properties
                };
                return ApiResponse<NodeResponse>.SuccessResponse(nodeResponse, "Node created successfully.");
            }
            catch (Exception ex)
            {
                // Handle any exceptions during the node creation process and return an error response.
                return ApiResponse<NodeResponse>.ErrorResponse($"Error creating node: {ex.Message}");
            }
        }

      


        private string HandleMergeNode(string cypher)
        {
            var pattern = new Regex(@"MERGE \((\w+):(\w+) \{(.+)\}\)", RegexOptions.IgnoreCase);
            var match = pattern.Match(cypher);
            if (!match.Success) return "Invalid MERGE syntax for node.";

            string nodeId = match.Groups[1].Value;
            string label = match.Groups[2].Value; // The label might be useful for certain types of queries or indexes.
            var propertiesString = match.Groups[3].Value;
            var properties = ParseProperties(propertiesString);

            var nodeExists = Nodes.Any(n => n.Id == nodeId);
            var node = Nodes.FirstOrDefault(n => n.Id == nodeId);
            bool needsIndexUpdate = false;

            if (node == null)
            {
                // Node creation
                node = new Node { Id = nodeId, Label = label, Properties = properties };
                Nodes.Add(node);
            }
            else
            {
                // Node update - determine if properties being set impact indexed fields
                foreach (var prop in properties)
                {
                    var oldValue = node.Properties.ContainsKey(prop.Key) ? node.Properties[prop.Key] : null;
                    var newValue = prop.Value;

                    if (!Equals(oldValue, newValue))
                    {
                        needsIndexUpdate = needsIndexUpdate || IsIndexedProperty(prop.Key);
                        node.Properties[prop.Key] = newValue;
                    }
                }
            }

            // Update indexes if necessary, particularly after creation or if indexed properties were modified
            if (!nodeExists || needsIndexUpdate)
            {
                UpdateNodeIndex(node);
            }

            SaveToFile();
            return nodeExists ? $"Node {nodeId} merged (updated) successfully." : $"Node {nodeId} merged (created) successfully.";
        }

        // IF CONDITION [condition] THEN [action] ELSE [alternative action]
        // Where [condition] is a simple expression (e.g., "node exists"), [action] and [alternative action]
        // are actions to be taken based on the condition. 



        private string HandleConditional(string cypher)
        {
            // Parsing the conditional syntax with expanded capability
            var match = Regex.Match(cypher, @"IF CONDITION\s+\[(.*?)\]\s+THEN\s+\[(.*?)\]\s+ELSE\s+\[(.*?)\]", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return "Invalid IF CONDITION syntax.";
            }

            var condition = match.Groups[1].Value.Trim();
            var action = match.Groups[2].Value.Trim();
            var alternativeAction = match.Groups[3].Value.Trim();

            // Evaluate the condition
            bool conditionResult = EvaluateCondition(condition);

            // Depending on the condition's evaluation, execute the corresponding action
            if (conditionResult)
            {
                // Condition is true, execute the action
                return ExecuteCypherCommand(action);
            }
            else
            {
                // Condition is false, execute the alternative action
                return ExecuteCypherCommand(alternativeAction);
            }
        }

        private string HandleCase(string cypher)
        {
            var casePattern = @"CASE\s+WHEN\s+(.*?)\s+THEN\s+(.*?)\s+(ELSE\s+(.*?))?\s+END";
            var matches = Regex.Matches(cypher, casePattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (matches.Count == 0)
            {
                return "Invalid CASE syntax or no CASE statement found.";
            }

            foreach (Match match in matches)
            {
                var condition = match.Groups[1].Value.Trim();
                var trueAction = match.Groups[2].Value.Trim();
                var hasElse = match.Groups[3].Success;
                var falseAction = hasElse ? match.Groups[4].Value.Trim() : "";

                bool conditionResult;
                try
                {
                    conditionResult = EvaluateCondition(condition);
                }
                catch (Exception ex)
                {
                    return $"Error evaluating condition '{condition}': {ex.Message}";
                }

                try
                {
                    if (conditionResult)
                    {
                        // Condition is true, execute trueAction
                        return ExecuteCypherCommand(trueAction);
                    }
                    else if (hasElse)
                    {
                        // Condition is false and an ELSE exists, execute falseAction
                        return ExecuteCypherCommand(falseAction);
                    }
                }
                catch (Exception ex)
                {
                    // Provide detailed error information, depending on which part of the CASE execution failed
                    var actionType = conditionResult ? "trueAction" : "falseAction or ELSE part";
                    return $"Error executing {actionType} of CASE statement: {ex.Message}";
                }
            }

            // This point should not be reached if the input is correct
            return "Error processing CASE statement. Please check the syntax.";
        }

        private string HandleCountNodes()
        {
            int nodeCount = Nodes.Count;
            return $"Total nodes: {nodeCount}.";
        }

        private string HandleCountEdges()
        {
            int edgeCount = Edges.Count;
            return $"Total edges: {edgeCount}.";
        }

        private string HandleAggregateSum(string cypher)
        {
            var match = Regex.Match(cypher, @"AGGREGATE SUM (\w+) ON (\w+)", RegexOptions.IgnoreCase);
            if (!match.Success) return "Invalid AGGREGATE SUM syntax.";

            string propertyName = match.Groups[1].Value;
            string targetType = match.Groups[2].Value.ToLower();

            double sum = 0;
            if (targetType == "nodes")
            {
                sum = Nodes.Where(n => n.Properties.ContainsKey(propertyName))
                           .Sum(n => Convert.ToDouble(n.Properties[propertyName]));
            }
            else if (targetType == "edges")
            {
                sum = Edges.Where(e => e.Properties.ContainsKey(propertyName))
                           .Sum(e => Convert.ToDouble(e.Properties[propertyName]));
            }
            else
            {
                return "Target type for AGGREGATE SUM must be either 'nodes' or 'edges'.";
            }

            return $"Sum of {propertyName} on {targetType}: {sum}.";
        }
        private string HandleAggregateAvg(string cypher)
        {
            var match = Regex.Match(cypher, @"AGGREGATE AVG (\w+) ON (\w+)", RegexOptions.IgnoreCase);
            if (!match.Success) return "Invalid AGGREGATE AVG syntax.";

            string propertyName = match.Groups[1].Value;
            string targetType = match.Groups[2].Value.ToLower();

            double avg = 0;
            if (targetType == "nodes")
            {
                avg = Nodes.Where(n => n.Properties.ContainsKey(propertyName))
                           .Average(n => Convert.ToDouble(n.Properties[propertyName]));
            }
            else if (targetType == "edges")
            {
                avg = Edges.Where(e => e.Properties.ContainsKey(propertyName))
                           .Average(e => Convert.ToDouble(e.Properties[propertyName]));
            }
            else
            {
                return "Target type for AGGREGATE AVG must be either 'nodes' or 'edges'.";
            }

            return $"Average of {propertyName} on {targetType}: {avg}.";
        }


        private string HandleFindRelationships(string cypher)
        {
            var match = Regex.Match(cypher, @"FIND RELATIONSHIPS FROM (\w+) TO (\w+)", RegexOptions.IgnoreCase);
            if (!match.Success) return "Invalid FIND RELATIONSHIPS syntax.";

            string fromNodeId = match.Groups[1].Value;
            string toNodeId = match.Groups[2].Value;

            var relationships = Edges.Where(e => e.FromId == fromNodeId && e.ToId == toNodeId).ToList();
            return $"Found {relationships.Count} relationships from {fromNodeId} to {toNodeId}.";
        }




        public bool EvaluateCondition(string condition)
        {
            // Checking if a node exists
            var nodeExistsRegex = new Regex(@"Node\((\d+)\)\.exists");
            var match = nodeExistsRegex.Match(condition);
            if (match.Success)
            {
                var nodeId = match.Groups[1].Value;
                return CheckNodeExists(nodeId);
            }

            // Checking if a node property matches a specific value
            var propertyCheckRegex = new Regex(@"Node\((\d+)\)\.property\['(\w+)'\] == '([^']*)'");
            match = propertyCheckRegex.Match(condition);
            if (match.Success)
            {
                var nodeId = match.Groups[1].Value;
                var propertyName = match.Groups[2].Value;
                var propertyValue = match.Groups[3].Value;
                return CheckNodeProperty(nodeId, propertyName, propertyValue);
            }

            // Checking if an edge exists between two nodes
            var edgeExistsRegex = new Regex(@"Edge\((\d+), (\d+)\)\.exists");
            match = edgeExistsRegex.Match(condition);
            if (match.Success)
            {
                var fromNodeId = match.Groups[1].Value;
                var toNodeId = match.Groups[2].Value;
                return CheckEdgeExists(fromNodeId, toNodeId);
            }

            // Example for numeric property comparison
            var numericPropertyCheckRegex = new Regex(@"Node\((\d+)\)\.property\['(\w+)'\] ([><=]+) (\d+)");
            match = numericPropertyCheckRegex.Match(condition);
            if (match.Success)
            {
                var nodeId = match.Groups[1].Value;
                var propertyName = match.Groups[2].Value;
                var operatorSymbol = match.Groups[3].Value;
                var valueToCompare = int.Parse(match.Groups[4].Value);
                return CheckNumericNodeProperty(nodeId, propertyName, operatorSymbol, valueToCompare);
            }

            Console.WriteLine("Condition not recognised or supported.");
            return false;
        }

        private bool CheckNodeExists(string nodeId)
        {
            return Nodes.Any(n => n.Id == nodeId);
        }

        private bool CheckNodeProperty(string nodeId, string propertyName, string propertyValue)
        {
            var node = Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node != null && node.Properties.TryGetValue(propertyName, out var value))
            {
                return value.ToString() == propertyValue;
            }
            return false;
        }

        private bool CheckEdgeExists(string fromNodeId, string toNodeId)
        {
            return Edges.Any(e => e.FromId == fromNodeId && e.ToId == toNodeId);
        }

        private bool CheckNumericNodeProperty(string nodeId, string propertyName, string operatorSymbol, int valueToCompare)
        {
            var node = Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node != null && node.Properties.TryGetValue(propertyName, out var value))
            {
                int nodePropertyValue = Convert.ToInt32(value);
                switch (operatorSymbol)
                {
                    case ">": return nodePropertyValue > valueToCompare;
                    case "<": return nodePropertyValue < valueToCompare;
                    case "=": return nodePropertyValue == valueToCompare;
                    default: return false;
                }
            }
            return false;
        }




        private string HandleMatchPattern(string cypher)
        {
            var pattern = new Regex(@"MATCH \((\w+)\)-\[\:(\w+) \{(.+): '(.+)'\}\]->\((\w+)\)", RegexOptions.IgnoreCase);
            var match = pattern.Match(cypher);
            if (!match.Success) return "Invalid MATCH syntax for relationship.";

            string relationshipType = match.Groups[2].Value;
            string propertyName = match.Groups[3].Value;
            string propertyValue = match.Groups[4].Value;

            // Potentially leverage indexing here if relationships are indexed by type and/or properties
            var matchingRelationships = Edges
                .Where(e => e.RelationshipType.Equals(relationshipType, StringComparison.OrdinalIgnoreCase) &&
                            e.Properties.TryGetValue(propertyName, out var value) &&
                            value.ToString().Equals(propertyValue, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Building a detailed response
            var response = new StringBuilder($"Found {matchingRelationships.Count} relationships of type '{relationshipType}' matching {propertyName} = '{propertyValue}':");
            foreach (var rel in matchingRelationships)
            {
                response.AppendLine($"\nFrom Node ID: {rel.FromId}, To Node ID: {rel.ToId}");
            }

            return response.ToString();
        }

        private string HandleMatchNode(string cypher)
        {
            var pattern = new Regex(@"MATCH \((\w+):(\w+) \{(.+): '(.+)'\}\)", RegexOptions.IgnoreCase);
            var match = pattern.Match(cypher);
            if (!match.Success) return "Invalid MATCH syntax for node.";

            string label = match.Groups[2].Value; // Assuming label might be used for querying
            string propertyName = match.Groups[3].Value;
            string propertyValue = match.Groups[4].Value;

            // Leverage indexing for nodes if applicable, especially if nodes are indexed by labels or properties
            var matchingNodes = Nodes
                .Where(n => (string.IsNullOrEmpty(label) || n.Label.Equals(label, StringComparison.OrdinalIgnoreCase)) &&
                            n.Properties.TryGetValue(propertyName, out var value) &&
                            value.ToString().Equals(propertyValue, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Constructing a detailed response
            var response = new StringBuilder($"Found {matchingNodes.Count} nodes matching {propertyName} = '{propertyValue}':");
            foreach (var node in matchingNodes)
            {
                response.AppendLine($"\nNode ID: {node.Id}, Label: {node.Label}");
            }

            return response.ToString();
        }





        public List<(Node, Node)> MatchPattern(Func<Node, bool> startCondition, string relationshipType, Func<Node, bool> endCondition)
        {
            // Assuming Nodes is a List, consider converting it to a Dictionary for faster lookups if not already done.
            // This approach assumes an indexed or dictionary-based access method for Nodes for optimal performance.
            var nodeLookup = Nodes.ToDictionary(n => n.Id, n => n);

            var matches = Edges
                .Where(e => e.RelationshipType.Equals(relationshipType, StringComparison.OrdinalIgnoreCase))
                .Select(e => (FromNode: nodeLookup.TryGetValue(e.FromId, out var fromNode) ? fromNode : null,
                              ToNode: nodeLookup.TryGetValue(e.ToId, out var toNode) ? toNode : null))
                .Where(pair => pair.FromNode != null && pair.ToNode != null)
                .Where(pair => startCondition(pair.FromNode) && endCondition(pair.ToNode))
                .ToList();

            return matches;
        }


        // Method to update node index
        private void UpdateNodeIndex(Node node)
        {
            // Assuming 'id' is the property we're indexing on for simplicity
            if (node.Properties.ContainsKey("id"))
            {
                var id = node.Properties["id"].ToString();
                if (!nodeIndex.ContainsKey(id))
                {
                    nodeIndex[id] = new List<Node>();
                }
                nodeIndex[id].Add(node);
            }
        }

        // Method to update edge index
        private void UpdateEdgeIndex(Edge edge)
        {
            // Example: Indexing on 'type' property
            if (edge.Properties.ContainsKey("type"))
            {
                var type = edge.Properties["type"].ToString();
                if (!edgeIndex.ContainsKey(type))
                {
                    edgeIndex[type] = new List<Edge>();
                }
                edgeIndex[type].Add(edge);
            }
        }

      



        public (bool Success, List<string> Path, double Cost, string ErrorMessage) FindPathBellmanFord(string startId, string endId)
            {
                var distances = new Dictionary<string, double>();
                var predecessors = new Dictionary<string, string>();

                // Initialize distances and predecessors
                foreach (var node in Nodes)
                {
                    distances[node.Id] = double.MaxValue;
                    predecessors[node.Id] = null;
                }

                distances[startId] = 0;

                // Relax all edges |V| - 1 times
                for (int i = 0; i < Nodes.Count - 1; i++)
                {
                    foreach (var edge in Edges)
                    {
                        string u = edge.From.Id;
                        string v = edge.To.Id;
                        double weight = edge.Weight;

                        if (distances[u] != double.MaxValue && distances[u] + weight < distances[v])
                        {
                            distances[v] = distances[u] + weight;
                            predecessors[v] = u;
                        }
                    }
                }

                // Check for negative-weight cycles
                foreach (var edge in Edges)
                {
                    string u = edge.From.Id;
                    string v = edge.To.Id;
                    double weight = edge.Weight;

                    if (distances[u] != double.MaxValue && distances[u] + weight < distances[v])
                    {
                        return (false, null, 0, "Graph contains a negative-weight cycle");
                    }
                }

                // Reconstruct path from endId back to startId
                List<string> path = new List<string>();
                string current = endId;

                while (current != null && predecessors.ContainsKey(current))
                {
                    path.Add(current);
                    current = predecessors[current];
                }

                path.Reverse();

                // If the start node is not in the path or path is empty, no path exists
                if (!path.Contains(startId))
                {
                    return (false, null, 0, "No path exists from start to end node");
                }

                double cost = distances[endId];
                return (true, path, cost, string.Empty);
            }

        // This method uses CsvHelper to read records from a CSV file as dynamic types,
        // allowing for flexibility in handling various CSV structures.
        // It checks whether the CSV represents nodes or edges using the isNodeCsv
        // parameter and adds them to the graph accordingly.


        private string HandleImportCsv(string cypher)
        {
            // Example Cypher: IMPORT CSV 'filePath.csv' AS NODES|EDGES
            var pattern = new Regex(@"IMPORT CSV '([^']+)' AS (NODES|EDGES)", RegexOptions.IgnoreCase);
            var match = pattern.Match(cypher);
            if (!match.Success) return "Invalid syntax for IMPORT CSV.";

            string filePath = match.Groups[1].Value;
            string type = match.Groups[2].Value.ToUpper();

            switch (type)
            {
                case "NODES":
                    return ImportCsvNodes(filePath);
                case "EDGES":
                    return ImportCsvEdges(filePath);
                default:
                    return "Invalid type for IMPORT CSV. Use 'NODES' or 'EDGES'.";
            }
        }

        private string ImportCsvNodes(string filePath)
        {
            try
            {
                int addedCount = 0, duplicateCount = 0;
                var existingNodeIds = new HashSet<string>(Nodes.Select(n => n.Id));

                using (var reader = new StreamReader(filePath))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    csv.Context.RegisterClassMap<NodeMap>(); // Register mapping configuration
                    var records = csv.GetRecords<Node>();

                    foreach (var record in records)
                    {
                        // Check for duplicates using a HashSet for efficient lookup
                        if (!existingNodeIds.Contains(record.Id))
                        {
                            Nodes.Add(record);
                            existingNodeIds.Add(record.Id); // Update the hash set with the new ID
                            addedCount++;
                        }
                        else
                        {
                            duplicateCount++;
                            // Optionally handle duplicates, e.g., log or update existing records
                        }
                    }
                }

                SaveToFile(); // Save changes to your data store

                // Provide detailed feedback on the import process
                return $"{addedCount} nodes imported successfully. {duplicateCount} duplicates found and ignored.";
            }
            catch (Exception ex)
            {
                return $"Failed to import nodes: {ex.Message}";
            }
        }



        // Define a CsvHelper class map to handle the custom mapping
        public class NodeMap : ClassMap<Node>
        {
            public NodeMap()
            {
                Map(m => m.Id).Name("Id");
                Map(m => m.Label).Name("Label");
                // Add mappings for properties if they are included in the CSV in a manageable way
            }
        }




        private string ImportCsvEdges(string filePath)
        {
            try
            {
                int addedCount = 0, duplicateCount = 0;
                // Define a way to check for duplicates. This example assumes edges are uniquely identified by FromId, ToId, and RelationshipType.
                var existingEdges = new HashSet<string>(Edges.Select(e => $"{e.FromId}-{e.ToId}-{e.RelationshipType}"));

                using (var reader = new StreamReader(filePath))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    var records = csv.GetRecords<Edge>();

                    foreach (var record in records)
                    {
                        string edgeKey = $"{record.FromId}-{record.ToId}-{record.RelationshipType}";
                        if (!existingEdges.Contains(edgeKey))
                        {
                            Edges.Add(record);
                            existingEdges.Add(edgeKey); // Update the set with the new edge identifier
                            addedCount++;
                        }
                        else
                        {
                            duplicateCount++;
                        }
                    }
                }

                SaveToFile(); // Save changes
                return $"{addedCount} edges imported successfully. {duplicateCount} duplicates found and ignored.";
            }
            catch (Exception ex)
            {
                return $"Failed to import edges: {ex.Message}";
            }
        }



        private string HandleImportJSON(string filePath)
        {
            try
            {
                var jsonData = File.ReadAllText(filePath);
                var graphData = JsonConvert.DeserializeObject<GraphData>(jsonData);

                if (graphData == null) throw new InvalidOperationException("Unable to parse JSON data.");

                int nodesAdded = 0, edgesAdded = 0;
                if (graphData.Nodes != null)
                {
                    foreach (var node in graphData.Nodes)
                    {
                        // Assuming Nodes list is unique by Id; adjust based on actual constraints
                        if (!Nodes.Any(n => n.Id == node.Id))
                        {
                            Nodes.Add(node);
                            nodesAdded++;
                        }
                    }
                }

                if (graphData.Edges != null)
                {
                    foreach (var edge in graphData.Edges)
                    {
                        // Assuming Edges list does not strictly enforce uniqueness; adjust as necessary
                        Edges.Add(edge);
                        edgesAdded++;
                    }
                }

                SaveToFile(); // Save changes
                return $"{nodesAdded} nodes and {edgesAdded} edges imported successfully from JSON.";
            }
            catch (Exception ex)
            {
                return $"Failed to import from JSON: {ex.Message}";
            }
        }



        //  {
        //  "nodes": [
        //    {"id": "1", "label": "Person", "properties": {"name": "Alice"}
        //},
        //    { "id": "2", "label": "Person", "properties": { "name": "Bob"} }
        //  ],
        //  "edges": [
        //    {"fromId": "1", "toId": "2", "relationshipType": "KNOWS", "properties": {"since": "2022"}}
        //  ]
        //}

        

        private string HandleExportCsvNodes(string cypher)
        {
            // Example Cypher: EXPORT CSV NODES 'filePath.csv'
            var pattern = new Regex(@"EXPORT CSV NODES '([^']+)'", RegexOptions.IgnoreCase);
            var match = pattern.Match(cypher);
            if (!match.Success) return "Invalid syntax for EXPORT CSV NODES.";

            string filePath = match.Groups[1].Value;

            // Placeholder for the actual export logic
            ExportNodesToCsv(filePath);

            return $"Nodes exported to CSV successfully at {filePath}.";
        }

        private void ExportNodesToCsv(string filePath)
        {
            try
            {
                using (var writer = new StreamWriter(filePath))
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                {
                    csv.WriteRecords(Nodes);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to export nodes to CSV: {ex.Message}");
            }
        }

        private string HandleExportCsvEdges(string cypher)
        {
            // Example Cypher: EXPORT CSV EDGES 'filePath.csv'
            var pattern = new Regex(@"EXPORT CSV EDGES '([^']+)'", RegexOptions.IgnoreCase);
            var match = pattern.Match(cypher);
            if (!match.Success) return "Invalid syntax for EXPORT CSV EDGES.";

            string filePath = match.Groups[1].Value;

            // Placeholder for the actual export logic
            ExportEdgesToCsv(filePath);

            return $"Edges exported to CSV successfully at {filePath}.";
        }

        private void ExportEdgesToCsv(string filePath)
        {
            try
            {
                using (var writer = new StreamWriter(filePath))
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                {
                    csv.WriteRecords(Edges);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to export edges to CSV: {ex.Message}");
            }
        }



        public String CreateDatabase()
        {
            isDatabaseLoaded = false;
            // Validate the graph name to ensure it's provided and conforms to any naming conventions you may have.
            if (string.IsNullOrWhiteSpace(_graphName))
            {
                return "Database name must be provided.";
            }

            // Construct the full path to where the database file would be saved, using a standardized method if available.
            // This example directly uses _graphPath, assuming it's correctly set based on _graphName.
            _graphPath = ConstructGraphPath(_graphName);

            if (File.Exists(_graphPath))
            {
                return $"Database '{_graphName}' already exists at '{_graphPath}'.";
            }
            else
            {
                try
                {
                    // Initialize the lists to ensure the database starts empty.
                    Nodes = new List<Node>();
                    Edges = new List<Edge>();

                    // Call a method to actually save/create the database file.
                    SaveToFile();

                    isDatabaseLoaded = true;
                    return $"Graph '{_graphName}' created and saved successfully at '{_graphPath}'.";
                }
                catch (Exception ex)
                {
                    // Log the exception details to help with debugging if needed.
                    LogException(ex);
                    return $"Failed to create the database '{_graphName}': {ex.Message}";
                }
            }
        }

        private string ConstructGraphPath(string graphName)
        {
            // Assuming DefaultFilePath is a base directory where databases are stored.
            // Adjust the logic here if your path construction is more complex.
            return Path.Combine(DefaultFilePath, $"{graphName}.json");
        }

        private void LogException(Exception ex)
        {
            // Implement logging according to your application's logging strategy.
            // This could be as simple as writing to a console or as complex as using a logging framework.
            Console.WriteLine($"Error: {ex.Message}\n{ex.StackTrace}");
        }

      


        public void SaveToFile()
        {
            try
            {
                // Create a GraphData object that directly uses the current lists of nodes and edges.
                // There's no need to recreate the Edge objects if they already have the correct structure.
                var graphData = new GraphData
                {
                    Nodes = this.Nodes,
                    Edges = this.Edges
                };

                // Serialize the GraphData object to JSON, formatting it for easy reading.
                var json = JsonConvert.SerializeObject(graphData, Formatting.Indented);

                // Ensure the directory for the graph data file exists before trying to write the file.
                var directory = Path.GetDirectoryName(_graphPath) ?? string.Empty; // Handle null or empty directory.
                Directory.CreateDirectory(directory); // This method is safe to call even if the directory already exists.

                // Write the JSON string to the file path designated for graph data storage.
                File.WriteAllText(_graphPath, json);
            }
            catch (Exception ex)
            {
                // Log the exception to the console. In a real-world application, consider using a logging framework.
                Console.Error.WriteLine($"Failed to save the graph to {_graphPath}: {ex.Message}");
            }
        }


        public void LoadGraph()
        {
            isDatabaseLoaded = false;
            if (!File.Exists(_graphPath))
            {
                Console.WriteLine("Graph file does not exist. Initializing a new graph.");
                // Initialize with empty collections to avoid null references.
                Nodes = new List<Node>();
                Edges = new List<Edge>();
                return;
            }

            try
            {
                var json = File.ReadAllText(_graphPath);
                var graphData = JsonConvert.DeserializeObject<GraphData>(json);

                if (graphData == null)
                {
                    Console.Error.WriteLine("Graph data is null after deserialization. Initializing an empty graph.");
                    Nodes = new List<Node>();
                    Edges = new List<Edge>();
                }
                else
                {
                    // Assuming GraphData contains Lists or other collections of Nodes and Edges.
                    Nodes = graphData.Nodes ?? new List<Node>(); // Safeguard against null lists
                    Edges = graphData.Edges ?? new List<Edge>();

                    // Link nodes directly if needed. Assuming e.From and e.To are not used for serialization but for runtime convenience.
                    foreach (var edge in Edges)
                    {
                        edge.From = Nodes.FirstOrDefault(n => n.Id == edge.FromId);
                        edge.To = Nodes.FirstOrDefault(n => n.Id == edge.ToId);
                    }
                }

                isDatabaseLoaded = true;
                Console.WriteLine("Graph loaded successfully.");
            }
            catch (JsonException ex)
            {
                Console.Error.WriteLine($"Error parsing the graph file {_graphPath}: {ex.Message}");
                // Initialize graph to avoid working with potentially partially loaded or inconsistent state.
                Nodes = new List<Node>();
                Edges = new List<Edge>();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to load the graph from {_graphPath}: {ex.Message}");
                // Similar initialization to safeguard against inconsistent state.
                Nodes = new List<Node>();
                Edges = new List<Edge>();
            }
        }
    }

    }




