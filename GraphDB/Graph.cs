using System;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using System.IO;

using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using GraphDB;
using System.Data;
using Microsoft.Extensions.FileSystemGlobbing;

namespace GraphDB
    {
    public static class GraphManager
    {
        private static Graph? _currentGraph;

        public static bool IsDatabaseLoaded => _currentGraph?.GetDatabaseLoaded() == true;

        public static Graph CurrentGraph
        {
            get => _currentGraph;
            private set => _currentGraph = value; // Private setter ensures only GraphManager can modify it
        }

        public static void SetCurrentGraph(Graph graph)
        {
            CurrentGraph = graph; // This will use the private setter
        }
        public static void LoadGraph(string graphName)
        {
            _currentGraph = new Graph(graphName);
           
        }

        public static void CreateGraph(string graphName)
        {
            _currentGraph = new Graph(graphName);
            _currentGraph.CreateDatabase();
        }

        public static void UnloadGraph()
        {
            _currentGraph = null;
            Console.WriteLine("Database unloaded.");
        }
    }


    public class Graph
        {
            private const string DefaultFilePath = "data"; // Relative path to store graph data
            private static string _graphPath="";
            private static string _graphName="";
            public static bool isDatabaseLoaded=false;

            public List<Node> Nodes { get; set; } = new List<Node>();
            public List<Edge> Edges { get; set; } = new List<Edge>();

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
                case CypherCommandType.MatchCommand:
                    return HandleMatchCommand(cypher);
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
       
                default:
                    return ApiResponse<string>.ErrorResponse("Unsupported Cypher command.");
            }
        }


        public  string GetDatabaseName()
        {
            return _graphName;
            
        }
        public   string GetDatabasePath()
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
                    SaveToFile();
                    return ApiResponse<NodeResponse>.SuccessResponse(
                        new NodeResponse { Id = node.Id, Label = label, Properties = node.Properties },
                        $"Merged (created) new node with id {nodeId}.");
                }
                else
                {
                    foreach (var prop in objectProperties)
                    {
                        node.Properties[prop.Key] = prop.Value;
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
                    SaveToFile();
                    return ApiResponse<NodeResponse>.SuccessResponse(
                        new NodeResponse { Id = node.Id, Label = label, Properties = node.Properties },
                        $"Created new node with id {nodeId}.");
                }
            }
        }


        private ApiResponse<RelationshipResponse> HandleCreateRelationship(string cypher)
        {
            // Updated regex to match relationships with or without properties
            //var pattern = new Regex(@"CREATE \((\w+)\)-\[:(\w+)\](?:\s*\{(.*)\})?->\((\w+)\)", RegexOptions.IgnoreCase);
            var pattern = new Regex(@"CREATE \((\w+)\)-\[:(\w+)(?:\s*\{(.*)\})?\]->\((\w+)\)", RegexOptions.IgnoreCase);


            var match = pattern.Match(cypher);
            if (!match.Success)
            {
                return ApiResponse<RelationshipResponse>.ErrorResponse("Invalid CREATE syntax for relationship.");
            }

            string fromNodeId = match.Groups[1].Value;
            string relationshipType = match.Groups[2].Value;
            string toNodeId = match.Groups[4].Value;
            string propertiesString = match.Groups[3].Success ? match.Groups[3].Value : null; // Handle optional properties

            var fromNode = Nodes.FirstOrDefault(n => n.Id == fromNodeId);
            var toNode = Nodes.FirstOrDefault(n => n.Id == toNodeId);
            if (fromNode == null || toNode == null)
            {
                return ApiResponse<RelationshipResponse>.ErrorResponse("One or both specified nodes do not exist.");
            }

            // Parse the properties from the string
            var properties = !string.IsNullOrEmpty(propertiesString) ? ParseProperties(propertiesString) : new Dictionary<string, object>();

            // Check if a similar relationship already exists
            var existingEdge = Edges.FirstOrDefault(e => e.FromId == fromNodeId && e.ToId == toNodeId && e.RelationshipType == relationshipType);
            if (existingEdge != null)
            {
                return ApiResponse<RelationshipResponse>.ErrorResponse($"A relationship of type {relationshipType} from {fromNodeId} to {toNodeId} already exists.");
            }

            // Create and add the new relationship
            var edge = new Edge
            {
                FromId = fromNodeId,
                ToId = toNodeId,
                RelationshipType = relationshipType,
                Properties = properties
            };
            Edges.Add(edge);
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
            if (string.IsNullOrEmpty(label))
            {
                deletionCriteria = n => n.Id == nodeIdOrLabel;
            }
            else
            {
                deletionCriteria = n => n.Properties.TryGetValue("label", out var nodeLabel) && nodeLabel.ToString() == label;
            }

            Edges.RemoveAll(e => deletionCriteria(Nodes.FirstOrDefault(n => n.Id == e.FromId)) || deletionCriteria(Nodes.FirstOrDefault(n => n.Id == e.ToId)));
            var removed = Nodes.RemoveAll(deletionCriteria) > 0;

            if (removed)
            {
                SaveToFile();
                return ApiResponse<object>.SuccessResponse(null, label == null ? $"Node {nodeIdOrLabel} and all its relationships have been deleted." : $"Nodes with label {label} and all their relationships have been deleted.");
            }
            else
            {
                return ApiResponse<object>.ErrorResponse(label == null ? $"Node {nodeIdOrLabel} does not exist." : $"No nodes with label {label} exist.");
            }
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

            var removed = Edges.RemoveAll(e => e.FromId == fromNodeId && e.ToId == toNodeId && e.RelationshipType == relationshipType) > 0;

            if (removed)
            {
                SaveToFile();
                return ApiResponse<object>.SuccessResponse(null, $"Relationship {relationshipType} from {fromNodeId} to {toNodeId} has been deleted.");
            }
            else
            {
                return ApiResponse<object>.ErrorResponse($"Relationship {relationshipType} from {fromNodeId} to {toNodeId} does not exist.");
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

            // Filtering edges based on the relationship type, property value, and weight conditions
            var filteredEdges = Edges.Where(edge =>
                edge.RelationshipType == relationshipType &&
                edge.Properties.TryGetValue(propertyName, out var value) && value.ToString() == propertyValue &&
                CheckWeightCondition(edge.Weight, weightComparison, weightValue)
            ).ToList();

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
                case "=": return edgeWeight == weightValue;
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

            // Update properties
            foreach (var prop in properties)
            {
                edge.Properties[prop.Key] = prop.Value;
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

            // Set or update the property value
            if (node.Properties.ContainsKey(propertyName))
            {
                node.Properties[propertyName] = propertyValue;
            }
            else
            {
                node.Properties.Add(propertyName, propertyValue);
            }

            SaveToFile(); // Assuming you have a method to save changes to the graph
            return $"Property {propertyName} of node {nodeId} has been set to {propertyValue}.";
        }





        //private Dictionary<string, object> ParseProperties(string propertiesString)
        //{
        //    var properties = new Dictionary<string, object>();
        //    var propsMatches = Regex.Matches(propertiesString, @"(\w+): '([^']*)'");
        //    foreach (Match match in propsMatches)
        //    {
        //        properties[match.Groups[1].Value] = match.Groups[2].Value;
        //    }
        //    return properties;
        //}
        private Dictionary<string, object> ParseProperties(string propertiesString)
        {
            var properties = new Dictionary<string, object>();

            // Match key-value pairs in the format: key: 'value' or key: number
            var propsMatches = Regex.Matches(propertiesString, @"(\w+):\s*'([^']*)'|(\w+):\s*([0-9]+(?:\.[0-9]+)?)");

            foreach (Match match in propsMatches)
            {
                if (match.Groups[1].Success && match.Groups[2].Success) // String value
                {
                    properties[match.Groups[1].Value] = match.Groups[2].Value;
                }
                else if (match.Groups[3].Success && match.Groups[4].Success) // Numeric value
                {
                    if (match.Groups[4].Value.Contains("."))
                    {
                        properties[match.Groups[3].Value] = double.Parse(match.Groups[4].Value);
                    }
                    else
                    {
                        properties[match.Groups[3].Value] = int.Parse(match.Groups[4].Value);
                    }
                }
            }

            return properties;
        }

        public void DeleteEdge(string fromNodeId, string toNodeId)
        {
            Edges.RemoveAll(e => e.FromId == fromNodeId && e.ToId == toNodeId);
            SaveToFile();
        }

        public List<Node> QueryNodesByProperty(string propertyName, object value)
        {
            return Nodes.Where(n => n.Properties.ContainsKey(propertyName) && n.Properties[propertyName].Equals(value)).ToList();
        }

        public List<Edge> QueryEdgesByProperty(string propertyName, object value)
        {
            return Edges.Where(e => e.Properties.ContainsKey(propertyName) && e.Properties[propertyName].Equals(value)).ToList();
        }


        public ApiResponse<List<NodeResponse>> HandleFindNeighbors(string cypher)
        {
            var response = new ApiResponse<List<NodeResponse>>();
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


        // Find all neighbors of a specific node
        public List<Node> FindNeighbors(string nodeId, string label = null)
        {
            var neighbors = new List<Node>();

            // Add nodes that are at the end of an outgoing edge from the given node
            var outgoingNeighbors = Edges
                .Where(e => e.From.Id == nodeId)
                .Select(e => e.To)
                .Where(n => label == null || n.Label == label)
                .ToList();

            neighbors.AddRange(outgoingNeighbors);

            // Add nodes that are at the start of an incoming edge to the given node
            var incomingNeighbors = Edges
                .Where(e => e.To.Id == nodeId)
                .Select(e => e.From)
                .Where(n => label == null || n.Label == label)
                .ToList();

            neighbors.AddRange(incomingNeighbors);

            return neighbors.Distinct().ToList(); // Remove duplicates and return
        }


        private ApiResponse<NodeResponse> HandleCreateNode(string cypher)
        {
            // Updated regex pattern to correctly capture the label
            var pattern = new Regex(@"CREATE \((\w+):(\w+)\s*\{(.*)\}\)", RegexOptions.IgnoreCase);
            var match = pattern.Match(cypher);

            if (!match.Success)
            {
                return ApiResponse<NodeResponse>.ErrorResponse("Invalid CREATE syntax for node.");
            }

            string nodeId = match.Groups[1].Value;
            string nodeLabel = match.Groups[2].Value;  // Capture the label (e.g., Person)
            var propertiesString = match.Groups[3].Value;

            var properties = ParseProperties(propertiesString);

            var newNode = new Node
            {
                Id = nodeId,
                Label = nodeLabel,  // Assign the label here
                Properties = properties
            };

            Nodes.Add(newNode);
            SaveToFile();

            // Create response
            var nodeResponse = new NodeResponse
            {
                Id = newNode.Id,
                Label = newNode.Label,
                Properties = newNode.Properties
            };

            return ApiResponse<NodeResponse>.SuccessResponse(nodeResponse, "Node created successfully.");
        }


        private string HandleMergeNode(string cypher)
        {
            var pattern = new Regex(@"MERGE \((\w+):(\w+) \{(.+)\}\)", RegexOptions.IgnoreCase);
            var match = pattern.Match(cypher);
            if (!match.Success) return "Invalid MERGE syntax for node.";

            string nodeId = match.Groups[1].Value;
            string label = match.Groups[2].Value; // As before, the use of label depends on your implementation.
            var properties = ParseProperties(match.Groups[3].Value);

            var node = Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node == null)
            {
                node = new Node { Id = nodeId, Properties = properties };
                Nodes.Add(node);
                SaveToFile();
                return $"Node {nodeId} merged (created) successfully.";
            }
            else
            {
                // Update existing node properties with new values from MERGE command
                foreach (var prop in properties)
                {
                    if (node.Properties.ContainsKey(prop.Key))
                    {
                        node.Properties[prop.Key] = prop.Value;
                    }
                    else
                    {
                        node.Properties.Add(prop.Key, prop.Value);
                    }
                }
                SaveToFile();
                return $"Node {nodeId} merged (updated) successfully.";
            }
        }


        // IF CONDITION [condition] THEN [action] ELSE [alternative action]
        // Where [condition] is a simple expression (e.g., "node exists"), [action] and [alternative action]
        // are actions to be taken based on the condition. 



        private string HandleConditional(string cypher)
        {
            // For demonstration, let's parse a simplified IF CONDITION statement
            var match = Regex.Match(cypher, @"IF CONDITION\s+\[(.*?)\]\s+THEN\s+\[(.*?)\]\s+ELSE\s+\[(.*?)\]", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return "Invalid IF CONDITION syntax.";
            }

            var condition = match.Groups[1].Value.Trim();
            var action = match.Groups[2].Value.Trim();
            var alternativeAction = match.Groups[3].Value.Trim();

            // Example condition check (you'll need to implement actual logic here)
            if (condition == "node exists")
            {
                // Execute the action if the condition is met
                return ExecuteCypherCommand(action);
            }
            else
            {
                // Execute the alternative action if the condition is not met
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
            // Example: "Node(123).exists"
            var nodeExistsRegex = new Regex(@"Node\((\d+)\)\.exists");
            var match = nodeExistsRegex.Match(condition);
            if (match.Success)
            {
                var nodeId = match.Groups[1].Value;
                return CheckNodeExists(nodeId);
            }

            // Example: "Node(123).property['name'] == 'Alice'"
            var propertyCheckRegex = new Regex(@"Node\((\d+)\)\.property\['(\w+)'\] == '(\w+)'");
            match = propertyCheckRegex.Match(condition);
            if (match.Success)
            {
                var nodeId = match.Groups[1].Value;
                var propertyName = match.Groups[2].Value;
                var propertyValue = match.Groups[3].Value;
                return CheckNodeProperty(nodeId, propertyName, propertyValue);
            }

            Console.WriteLine("Condition not recognised or supported.");
            return false;
        }

        // Checks if a node with the specified ID exists in the graph
        public bool CheckNodeExists(string nodeId)
        {
            return Nodes.Any(n => n.Id == nodeId);
        }

        // Checks if a node with the specified ID has a property with a specific value
        public bool CheckNodeProperty(string nodeId, string propertyName, object propertyValue)
        {
            var node = Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node != null && node.Properties.TryGetValue(propertyName, out var value))
            {
                // Assuming you want to compare the values as strings
                return value.ToString() == propertyValue.ToString();
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

            var matchingRelationships = Edges.Where(e => e.RelationshipType == relationshipType && e.Properties.ContainsKey(propertyName) && e.Properties[propertyName].ToString() == propertyValue).ToList();

            return $"Found {matchingRelationships.Count} relationships of type {relationshipType} matching {propertyName} = {propertyValue}.";
        }

      

        private string HandleMatchCommand(string cypher)
        {
            // Patterns
            var nodePattern = new Regex(@"MATCH \((\w+):?(\w*) \{?([^}]*)\}?\)", RegexOptions.IgnoreCase);
            var pathPattern = new Regex(@"MATCH (\w+) = \((\w+):?(\w*)\)-\[:(\w+)\*(\d*)\.\.(\d*)\]->\((\w+):?(\w*)\)", RegexOptions.IgnoreCase);
            var relationshipPattern = new Regex(
                @"MATCH\s*\((?<alias1>\w+):?(?<label1>\w*)?(?:\s*\{(?<props1>[^}]+)\})?\)" +
                @"-\s*\[:(?<relType>\w+)\]\s*->" +
                @"\((?<alias2>\w+):?(?<label2>\w*)?(?:\s*\{(?<props2>[^}]+)\})?\)\s*" +
                @"RETURN\s+(?<returnAlias>\w+)\.(?<returnProp>\w+)(?:\s+AS\s+(?<returnAs>\w+))?",
                RegexOptions.IgnoreCase
            );

            // Clauses
            var wherePattern = new Regex(@"WHERE (.+)", RegexOptions.IgnoreCase);
            var returnPattern = new Regex(@"RETURN (.+)", RegexOptions.IgnoreCase);
            var groupByPattern = new Regex(@"GROUP BY (\w+)", RegexOptions.IgnoreCase);
            var orderByPattern = new Regex(@"ORDER BY (\w+)\s*(ASC|DESC)?", RegexOptions.IgnoreCase);
            var limitPattern = new Regex(@"\bLIMIT\s+(\d+)", RegexOptions.IgnoreCase);
            var offsetPattern = new Regex(@"\bOFFSET\s+(\d+)", RegexOptions.IgnoreCase);
            var aggregationPattern = new Regex(@"(COUNT|SUM|AVG|MIN|MAX)\((\w+)\)", RegexOptions.IgnoreCase);

            // Set operations
            var unionPattern = new Regex(@"\bUNION(?: ALL)?\b", RegexOptions.IgnoreCase);
            var intersectPattern = new Regex(@"\bINTERSECT\b", RegexOptions.IgnoreCase);
            var exceptPattern = new Regex(@"\bEXCEPT\b", RegexOptions.IgnoreCase);
            var nestedUnionPattern = new Regex(@"\((.*?UNION.*)\)", RegexOptions.IgnoreCase);

            var aliasToNodeMap = new Dictionary<string, Node>();
            var results = new List<string>();

            try
            {
                // Nested UNION
                var nestedUnionMatch = nestedUnionPattern.Match(cypher);
                if (nestedUnionMatch.Success)
                {
                    var nestedUnionQuery = nestedUnionMatch.Groups[1].Value;
                    var nestedUnionResult = HandleMatchCommand(nestedUnionQuery);
                    cypher = cypher.Replace($"({nestedUnionQuery})", nestedUnionResult);
                }

                // EXCEPT
                if (exceptPattern.IsMatch(cypher))
                {
                    var parts = exceptPattern.Split(cypher).Select(x => x.Trim()).ToList();
                    if (parts.Count != 2) throw new Exception("EXCEPT must have two sub-queries.");

                    var leftResult = new HashSet<string>(HandleMatchCommand(parts[0]).Split('\n'));
                    var rightResult = new HashSet<string>(HandleMatchCommand(parts[1]).Split('\n'));
                    var difference = leftResult.Except(rightResult).ToList();
                    return string.Join("\n", difference);
                }

                // INTERSECT
                if (intersectPattern.IsMatch(cypher))
                {
                    var segments = intersectPattern.Split(cypher).Select(x => x.Trim()).ToList();
                    var intersectionSets = new List<HashSet<string>>();

                    foreach (var seg in segments)
                        intersectionSets.Add(new HashSet<string>(HandleMatchCommand(seg).Split('\n')));

                    var common = intersectionSets.Aggregate((s1, s2) => s1.Intersect(s2).ToHashSet());
                    return string.Join("\n", common);
                }

                // UNION / UNION ALL
                if (unionPattern.IsMatch(cypher))
                {
                    var subQueries = unionPattern.Split(cypher).Select(x => x.Trim()).ToList();
                    var unionResults = new List<string>();

                    foreach (var query in subQueries)
                        unionResults.AddRange(HandleMatchCommand(query).Split('\n'));

                    bool isUnionAll = cypher.Contains("UNION ALL", StringComparison.OrdinalIgnoreCase);
                    var combined = isUnionAll ? unionResults : unionResults.Distinct().ToList();
                    return string.Join("\n", combined);
                }

                // WHERE clause
                var whereMatch = wherePattern.Match(cypher);
                var whereClause = whereMatch.Success ? whereMatch.Groups[1].Value : null;
                Func<Node, bool> nodeCondition = _ => true;

                if (!string.IsNullOrEmpty(whereClause))
                {
                    var conditions = whereClause
                        .Split(new[] { " AND ", " OR " }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(cond => Regex.Match(cond, @"(\w+)\s*(=|<>|>=|<=|>|<)\s*'(.+)'"))
                        .Where(m => m.Success)
                        .Select(m => new
                        {
                            Property = m.Groups[1].Value.Trim(),
                            Operator = m.Groups[2].Value.Trim(),
                            Value = m.Groups[3].Value.Trim()
                        })
                        .ToList();

                    nodeCondition = n => conditions.All(cond =>
                    {
                        if (!n.Properties.ContainsKey(cond.Property)) return false;
                        var val = n.Properties[cond.Property].ToString();
                        return cond.Operator switch
                        {
                            "=" => val == cond.Value,
                            "<>" => val != cond.Value,
                            ">" => string.Compare(val, cond.Value) > 0,
                            "<" => string.Compare(val, cond.Value) < 0,
                            ">=" => string.Compare(val, cond.Value) >= 0,
                            "<=" => string.Compare(val, cond.Value) <= 0,
                            _ => false
                        };
                    });
                }

                // RETURN clause
                var returnMatch = returnPattern.Match(cypher);
                var returnClause = returnMatch.Success
                    ? returnMatch.Groups[1].Value.Split(',').Select(x => x.Trim()).ToList()
                    : new List<string>();

                // GROUP BY / ORDER BY / Aggregations
                var groupByMatch = groupByPattern.Match(cypher);
                var groupByProperty = groupByMatch.Success ? groupByMatch.Groups[1].Value : null;

                var orderByMatch = orderByPattern.Match(cypher);
                var orderByProperty = orderByMatch.Success ? orderByMatch.Groups[1].Value : null;
                var orderByDirection = orderByMatch.Success &&
                                       orderByMatch.Groups[2].Value.Equals("DESC", StringComparison.OrdinalIgnoreCase)
                                       ? "DESC" : "ASC";

                var isAggregation = aggregationPattern.IsMatch(cypher);
                var aggregationResults = new List<string>();

                // MATCH relationship
                if (relationshipPattern.IsMatch(cypher))
                {
                    var match = relationshipPattern.Match(cypher);
                    if (!match.Success) return "Invalid MATCH syntax.";

                    string startNodeAlias = match.Groups["alias1"].Value;
                    string startLabel = match.Groups["label1"].Value;
                    var startPropsString = match.Groups["props1"].Value;
                    string relType = match.Groups["relType"].Value;
                    string endNodeAlias = match.Groups["alias2"].Value;
                    string returnAlias = match.Groups["returnAlias"].Value;
                    string returnProperty = match.Groups["returnProp"].Value;
                    string returnAs = match.Groups["returnAs"].Value; // e.g. "FriendName"


                    // Parse the start node properties
                    var startNodeProps = ParseProperties(startPropsString);

                    // Find edges that match
                    var matchingEdges = Edges.Where(e =>
                        (string.IsNullOrEmpty(relType) || e.RelationshipType == relType) &&
                        // from-node must match label & props
                        Nodes.Any(x => x.Id == e.FromId
                                       && (string.IsNullOrEmpty(startLabel) || x.Label == startLabel)
                                       && startNodeProps.All(p => x.Properties.ContainsKey(p.Key)
                                                                  && x.Properties[p.Key].ToString() == p.Value.ToString())) &&
                        // end-node can be anything
                        Nodes.Any(x => x.Id == e.ToId)
                    ).ToList();

                    if (matchingEdges.Count == 0)
                        return JsonConvert.SerializeObject(ApiResponse<string>.ErrorResponse("No matching relationships found."));

                    // Fill alias map so we can do "RETURN friend.name"
                    foreach (var edge in matchingEdges)
                    {
                        var startNode = Nodes.First(n => n.Id == edge.FromId);
                        var endNode = Nodes.First(n => n.Id == edge.ToId);
                        aliasToNodeMap[startNodeAlias] = startNode; // e.g. "alice"
                        aliasToNodeMap[endNodeAlias] = endNode;   // e.g. "friend"
                    }

                    // Build the return list for friend.name AS FriendName
                    var returnList = new List<string>();
                    foreach (var edge in matchingEdges)
                    {
                        if (aliasToNodeMap.TryGetValue(returnAlias, out var node))
                        {
                            if (node.Properties.ContainsKey(returnProperty))
                            {
                                var rawVal = node.Properties[returnProperty];
                                var finalKey = string.IsNullOrEmpty(returnAs) ? returnProperty : returnAs;
                                returnList.Add($"{finalKey} = {rawVal}");
                            }
                            else
                            {
                                returnList.Add($"Node '{returnAlias}' has no property '{returnProperty}'.");
                            }
                        }
                        else
                        {
                            // This means the code didn't capture the alias properly
                            returnList.Add($"Alias '{returnAlias}' not found in aliasToNodeMap.");
                        }
                    }

                    // Return friend.name results
                    return JsonConvert.SerializeObject(ApiResponse<List<string>>
                        .SuccessResponse(returnList, $"Found {matchingEdges.Count} relationships."));
                }

                // MATCH node
                else if (nodePattern.IsMatch(cypher))
                {
                    var m = nodePattern.Match(cypher);
                    string alias = m.Groups[1].Value;
                    string label = m.Groups[2].Value;
                    var propsStr = m.Groups[3].Value;

                    var props = string.IsNullOrEmpty(propsStr)
                        ? new Dictionary<string, string>()
                        : propsStr.Split(',')
                            .Select(p => p.Split(':'))
                            .ToDictionary(
                                p => p[0].Trim(),
                                p => p[1].Trim().Trim('\'')
                            );

                    var matchingNodes = Nodes.Where(n =>
                        (string.IsNullOrEmpty(label) || n.Label == label) &&
                        props.All(p => n.Properties.ContainsKey(p.Key) &&
                                        n.Properties[p.Key].ToString() == p.Value) &&
                        nodeCondition(n))
                        .ToList();

                    if (matchingNodes.Count == 0)
                    {
                        results.Add("No matching nodes found.");
                    }
                    else
                    {
                        // 1) Populate aliasToNodeMap for each matched node
                        foreach (var node in matchingNodes)
                        {
                            aliasToNodeMap[alias] = node;
                        }

                        // 2) Now proceed with grouping/aggregation if needed
                        if (!string.IsNullOrEmpty(groupByProperty))
                        {
                            

                            // Group and possibly aggregate
                            var grouped = matchingNodes.GroupBy(n =>
                                n.Properties.ContainsKey(groupByProperty)
                                    ? n.Properties[groupByProperty].ToString()
                                    : "NULL");

                            foreach (var grp in grouped)
                            {
                                if (isAggregation)
                                {
                                    var agMatch = aggregationPattern.Match(cypher);
                                    var agType = agMatch.Groups[1].Value.ToUpper();
                                    var agProp = agMatch.Groups[2].Value;

                                    var vals = grp.Select(n => n.Properties.ContainsKey(agProp)
                                        ? Convert.ToDouble(n.Properties[agProp])
                                        : 0).ToList();

                                    double agRes = agType switch
                                    {
                                        "COUNT" => vals.Count,
                                        "SUM" => vals.Sum(),
                                        "AVG" => vals.Average(),
                                        "MIN" => vals.Min(),
                                        "MAX" => vals.Max(),
                                        _ => 0
                                    };
                                    aggregationResults.Add($"{agType}({agProp}) for {grp.Key} = {agRes}");
                                }
                                else
                                {
                                    aggregationResults.Add($"Group: {grp.Key}, Nodes: [{string.Join(", ", grp.Select(x => x.Id))}]");
                                }
                            }
                        }

                        else if (isAggregation)
                        {
                            var agMatch = aggregationPattern.Match(cypher);
                            var agType = agMatch.Groups[1].Value.ToUpper();
                            var agProp = agMatch.Groups[2].Value;

                            var vals = matchingNodes.Select(n => n.Properties.ContainsKey(agProp)
                                ? Convert.ToDouble(n.Properties[agProp])
                                : 0).ToList();

                            double agRes = agType switch
                            {
                                "COUNT" => vals.Count,
                                "SUM" => vals.Sum(),
                                "AVG" => vals.Average(),
                                "MIN" => vals.Min(),
                                "MAX" => vals.Max(),
                                _ => 0
                            };
                            aggregationResults.Add($"{agType}({agProp}) = {agRes}");
                        }
                        else
                        {
                            var formatted = matchingNodes.Select(n =>
                            {
                                if (returnClause.Count == 0)
                                    return $"Node(Id: {n.Id}, Label: {n.Label}, Props: {JsonConvert.SerializeObject(n.Properties)})";

                                var computed = returnClause.Select(expr =>
                                {
                                    try
                                    {
                                        // Check 'alias.prop'
                                        var apMatch = Regex.Match(expr, @"(\w+)\.(\w+)");
                                        if (apMatch.Success)
                                        {
                                            var al = apMatch.Groups[1].Value;
                                            var pr = apMatch.Groups[2].Value;
                                            if (aliasToNodeMap.ContainsKey(al) &&
                                                aliasToNodeMap[al].Properties.ContainsKey(pr))
                                                return $"{expr} = {aliasToNodeMap[al].Properties[pr]}";
                                            return $"Cannot resolve {expr}.";
                                        }
                                        // String/number functions
                                        if (expr.StartsWith("CONCAT", StringComparison.OrdinalIgnoreCase))
                                            return $"{expr} = '{HandleConcatFunction(n, expr)}'";
                                        if (expr.StartsWith("SUBSTR", StringComparison.OrdinalIgnoreCase))
                                            return $"{expr} = '{HandleSubstrFunction(n, expr)}'";
                                        if (expr.StartsWith("UPPER", StringComparison.OrdinalIgnoreCase))
                                            return $"{expr} = '{HandleUpperFunction(n, expr)}'";
                                        if (expr.StartsWith("LOWER", StringComparison.OrdinalIgnoreCase))
                                            return $"{expr} = '{HandleLowerFunction(n, expr)}'";
                                        if (expr.StartsWith("LENGTH", StringComparison.OrdinalIgnoreCase))
                                            return $"{expr} = {HandleLengthFunction(n, expr)}";
                                        if (expr.StartsWith("TRIM", StringComparison.OrdinalIgnoreCase))
                                            return $"{expr} = '{HandleTrimFunction(n, expr)}'";
                                        if (expr.StartsWith("ABS", StringComparison.OrdinalIgnoreCase))
                                            return $"{expr} = {HandleAbsFunction(n, expr)}";
                                        if (expr.StartsWith("ROUND", StringComparison.OrdinalIgnoreCase))
                                            return $"{expr} = {HandleRoundFunction(n, expr)}";
                                        if (expr.StartsWith("FLOOR", StringComparison.OrdinalIgnoreCase))
                                            return $"{expr} = {HandleFloorFunction(n, expr)}";
                                        if (expr.StartsWith("CEIL", StringComparison.OrdinalIgnoreCase))
                                            return $"{expr} = {HandleCeilFunction(n, expr)}";

                                        // Otherwise, evaluate expression against node props
                                        var e = expr;
                                        foreach (var p in n.Properties)
                                            e = Regex.Replace(e, $@"\b{p.Key}\b", p.Value.ToString());

                                        var eval = new DataTable().Compute(e, "");
                                        return $"{expr} = {eval}";
                                    }
                                    catch
                                    {
                                        return $"Invalid expression: {expr}";
                                    }
                                });
                                return string.Join(", ", computed);
                            });

                            // ORDER BY
                            if (!string.IsNullOrEmpty(orderByProperty))
                            {
                                formatted = orderByDirection == "ASC"
                                    ? formatted.OrderBy(f => NumericOrMax(f)).ToList()
                                    : formatted.OrderByDescending(f => NumericOrMin(f)).ToList();
                            }

                            // OFFSET
                            var offsetMatch = offsetPattern.Match(cypher);
                            int offset = offsetMatch.Success ? int.Parse(offsetMatch.Groups[1].Value) : 0;
                            formatted = formatted.Skip(offset).ToList();

                            // LIMIT
                            var limitMatch = limitPattern.Match(cypher);
                            int limit = limitMatch.Success ? int.Parse(limitMatch.Groups[1].Value) : formatted.Count();
                            formatted = formatted.Take(limit).ToList();

                            results.AddRange(formatted);
                        }

                        // If we aggregated, return those results instead
                        if (aggregationResults.Count > 0) results.AddRange(aggregationResults);
                    }
                }
                // MATCH path (if you'd like to handle it, similar approach to node/relationship)
                else if (pathPattern.IsMatch(cypher))
                {
                    // You can expand your path logic here if needed
                    results.Add("Path patterns not yet handled in this example.");
                }
                else
                {
                    return JsonConvert.SerializeObject(ApiResponse<string>
                        .ErrorResponse("Invalid MATCH syntax."));
                }

                if (results.Any())
                {
                    // Return a success response with your data
                    return JsonConvert.SerializeObject(ApiResponse<List<string>>
                        .SuccessResponse(results, "Query executed successfully."));
                }
                else
                {
                    // If for some reason results are still empty, produce an error or empty result
                    return JsonConvert.SerializeObject(ApiResponse<string>
                        .ErrorResponse("No results found."));
                }
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }




        // Helper: convert string to numeric or set large/small boundary for ordering
        private double NumericOrMax(string input)
        {
            return double.TryParse(input, out var val) ? val : double.MaxValue;
        }
        private double NumericOrMin(string input)
        {
            return double.TryParse(input, out var val) ? val : double.MinValue;
        }


        // Helper methods for handling various functions

        private string HandleConcatFunction(Node node, string expression)
        {
            var args = ExtractFunctionArguments(expression, "CONCAT");
            return string.Join("", args.Select(arg => GetNodePropertyValue(node, arg)));
        }

        private string HandleSubstrFunction(Node node, string expression)
        {
            var args = ExtractFunctionArguments(expression, "SUBSTR");
            if (args.Count < 2 || args.Count > 3)
                throw new Exception("Invalid SUBSTR usage");

            var value = GetNodePropertyValue(node, args[0]);
            var start = int.Parse(args[1]);
            var length = args.Count == 3 ? int.Parse(args[2]) : value.Length - start;

            return value.Substring(start, Math.Min(length, value.Length - start));
        }

        private string HandleUpperFunction(Node node, string expression)
        {
            var args = ExtractFunctionArguments(expression, "UPPER");
            var value = GetNodePropertyValue(node, args[0]);
            return value.ToUpper();
        }

        private string HandleLowerFunction(Node node, string expression)
        {
            var args = ExtractFunctionArguments(expression, "LOWER");
            var value = GetNodePropertyValue(node, args[0]);
            return value.ToLower();
        }

        private int HandleLengthFunction(Node node, string expression)
        {
            var args = ExtractFunctionArguments(expression, "LENGTH");
            var value = GetNodePropertyValue(node, args[0]);
            return value.Length;
        }

        private string HandleTrimFunction(Node node, string expression)
        {
            var args = ExtractFunctionArguments(expression, "TRIM");
            var value = GetNodePropertyValue(node, args[0]);
            return value.Trim();
        }

        private double HandleAbsFunction(Node node, string expression)
        {
            var args = ExtractFunctionArguments(expression, "ABS");
            var value = double.Parse(GetNodePropertyValue(node, args[0]));
            return Math.Abs(value);
        }

        private double HandleRoundFunction(Node node, string expression)
        {
            var args = ExtractFunctionArguments(expression, "ROUND");
            var value = double.Parse(GetNodePropertyValue(node, args[0]));
            var decimals = args.Count == 2 ? int.Parse(args[1]) : 0;
            return Math.Round(value, decimals);
        }

        private double HandleFloorFunction(Node node, string expression)
        {
            var args = ExtractFunctionArguments(expression, "FLOOR");
            var value = double.Parse(GetNodePropertyValue(node, args[0]));
            return Math.Floor(value);
        }

        private double HandleCeilFunction(Node node, string expression)
        {
            var args = ExtractFunctionArguments(expression, "CEIL");
            var value = double.Parse(GetNodePropertyValue(node, args[0]));
            return Math.Ceiling(value);
        }


        // Helper method to extract function arguments
        private List<string> ExtractFunctionArguments(string expression, string functionName)
        {
            var argsPattern = new Regex($@"{functionName}\((.+)\)", RegexOptions.IgnoreCase);
            var match = argsPattern.Match(expression);
            if (!match.Success) return new List<string>();

            return match.Groups[1].Value.Split(',').Select(arg => arg.Trim()).ToList();
        }

        // Helper method to get node property value
        private string GetNodePropertyValue(Node node, string propertyName)
        {
            return node.Properties.ContainsKey(propertyName) ? node.Properties[propertyName].ToString() : string.Empty;
        }



        private List<List<Node>> FindPaths(string startLabel, string relationshipType, string endLabel, int minLength, int maxLength)
        {
            var paths = new List<List<Node>>();

            // Find all start nodes with the given label
            var startNodes = Nodes.Where(n => string.IsNullOrEmpty(startLabel) || n.Label == startLabel).ToList();

            // Recursive DFS to find paths
            foreach (var startNode in startNodes)
            {
                var visited = new HashSet<string>(); // Track visited nodes to avoid cycles
                var currentPath = new List<Node>();
                DFS(startNode, relationshipType, endLabel, minLength, maxLength, 0, visited, currentPath, paths);
            }

            return paths;
        }

        private void DFS(Node currentNode, string relationshipType, string endLabel, int minLength, int maxLength, int currentLength,
                         HashSet<string> visited, List<Node> currentPath, List<List<Node>> paths)
        {
            visited.Add(currentNode.Id);
            currentPath.Add(currentNode);

            // If current path length is within the desired range and the end label matches, add the path to the result
            if (currentLength >= minLength && currentLength <= maxLength &&
                (string.IsNullOrEmpty(endLabel) || currentNode.Label == endLabel))
            {
                paths.Add(new List<Node>(currentPath)); // Add a copy of the current path
            }

            // Stop if the path exceeds maxLength
            if (currentLength == maxLength)
            {
                visited.Remove(currentNode.Id);
                currentPath.RemoveAt(currentPath.Count - 1);
                return;
            }

            // Explore neighbors via outgoing relationships
            var neighbors = Edges.Where(e => e.FromId == currentNode.Id && e.RelationshipType == relationshipType)
                                 .Select(e => Nodes.FirstOrDefault(n => n.Id == e.ToId))
                                 .Where(n => n != null && !visited.Contains(n.Id))
                                 .ToList();

            foreach (var neighbor in neighbors)
            {
                DFS(neighbor, relationshipType, endLabel, minLength, maxLength, currentLength + 1, visited, currentPath, paths);
            }

            // Backtrack
            visited.Remove(currentNode.Id);
            currentPath.RemoveAt(currentPath.Count - 1);
        }



        //public List<(Node, Node)> MatchPattern(Func<Node, bool> startCondition, string relationshipType, Func<Node, bool> endCondition)
        //{
        //    var matches = new List<(Node, Node)>();
        //    foreach (var edge in Edges.Where(e => e.Relationship.Equals(relationshipType, StringComparison.OrdinalIgnoreCase)))
        //    {
        //        var fromNode = Nodes.FirstOrDefault(n => n.Id == edge.FromId);
        //        var toNode = Nodes.FirstOrDefault(n => n.Id == edge.ToId);
        //        if (fromNode != null && toNode != null && startCondition(fromNode) && endCondition(toNode))
        //        {
        //            matches.Add((fromNode, toNode));
        //        }
        //    }
        //    return matches;
        //}





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
                using (var reader = new StreamReader(filePath))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    csv.Context.RegisterClassMap<NodeMap>(); // Register mapping configuration
                    var records = csv.GetRecords<Node>().ToList();
                    foreach (var record in records)
                    {
                        if (!Nodes.Any(n => n.Id == record.Id))
                        {
                            Nodes.Add(record);
                        }
                        else
                        {
                            // Handle duplicate node ID situation, if necessary
                        }
                    }
                }
                SaveToFile(); // Save changes to your data store
                return "Nodes imported from CSV successfully.";
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
                using (var reader = new StreamReader(filePath))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    var records = csv.GetRecords<Edge>().ToList();
                    foreach (var record in records)
                    {
                        // Add logic to avoid duplicate edges if necessary
                        Edges.Add(record);
                    }
                }
                SaveToFile(); // Save changes
                return "Edges imported from CSV successfully.";
            }
            catch (Exception ex)
            {
                return $"Failed to import edges: {ex.Message}";
            }
        }


        private string HandleImportJSON(string cypher)
        {
            // Example Cypher: IMPORT CSV 'filePath.csv' AS NODES|EDGES
            var pattern = new Regex(@"IMPORT JSON '([^']+)'", RegexOptions.IgnoreCase);
            var match = pattern.Match(cypher);
            if (!match.Success) return "Invalid syntax for IMPORT JSON.";

            string filePath = match.Groups[1].Value;
            
            return ImportJSON(filePath);
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

        private string ImportJSON(string filePath)
        {
            try
            {
                var jsonData = File.ReadAllText(filePath);
                var graphData = JsonConvert.DeserializeObject<GraphData>(jsonData);

                if (graphData?.Nodes != null)
                {
                    foreach (var nodeData in graphData.Nodes)
                    {
                        AddNode(new Node
                        {
                            Id = nodeData.Id,
                            Label = nodeData.Label,
                            Properties = nodeData.Properties
                        });
                    }
                }

                if (graphData?.Edges != null)
                {
                    foreach (var edgeData in graphData.Edges)
                    {
                        AddEdge(edgeData.FromId, edgeData.ToId, edgeData.Weight, edgeData.RelationshipType, edgeData.Properties);
                    }
                }
                SaveToFile(); // Save changes
                return "JSON imported from CSV successfully.";
            }
            catch (Exception ex)
            {
                return $"Failed to import edges: {ex.Message}";
            }
        }

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
            if (string.IsNullOrWhiteSpace(_graphName))
            {
                return "Database name must be provided.";
            }
            else if (File.Exists(_graphPath))
            {
                return $"Database ({_graphPath}) already exists.";
            }
            else
            {
                try
                {
                    Nodes = new List<Node>();
                    Edges = new List<Edge>();
                    SaveToFile();
                    isDatabaseLoaded = true;
                    return $"Graph '{_graphName}' created and saved.";
                }
                catch (Exception ex)
                {
                    return $"Failed to create database ({_graphName})";
                }
            }
        }

        public void SaveToFile()
        {
            try
            {
                Console.WriteLine($"Saving graph to path: {_graphPath}");
                Console.WriteLine($"Nodes count: {Nodes.Count}, Edges count: {Edges.Count}");

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


        public string LoadDatabase()
            {
                isDatabaseLoaded = false;
                if (!File.Exists(_graphPath))
                {
                    Console.WriteLine("Graph database does not exist");
                    return $"Graph database '{_graphPath}' does not exist";
                }

            try
            {
                var json = File.ReadAllText(_graphPath);
                var graphData = JsonConvert.DeserializeObject<GraphData>(json);

                this.Nodes = graphData.Nodes;
                this.Edges = graphData.Edges.Select(e =>
                {
                    e.From = this.Nodes.FirstOrDefault(n => n.Id == e.FromId);
                    e.To = this.Nodes.FirstOrDefault(n => n.Id == e.ToId);
                    return e;
                }).ToList();
                isDatabaseLoaded = true;
            }
            catch (JsonException ex)
            {
                Console.Error.WriteLine($"Error parsing the graph file {_graphPath}: {ex.Message}");
                return $"Error parsing database '{_graphPath}'";
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to load the graph from {_graphPath}: {ex.Message}");
                return $"Failed to load database '{_graphPath}' ";
            }
            Console.WriteLine("Graph database loaded");
            return $"Graph database '{_graphPath}' successfuly loaded (Nodes:{this.Nodes.Count()}, Relationships:{this.Edges.Count()})";
            
          
            }

        }

    }




