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
            try
            {

                var pattern = new Regex(@"CREATE \((\w+):(\w+) \{(.+)\}\)", RegexOptions.IgnoreCase);
                var match = pattern.Match(cypher);
                if (!match.Success) return  ApiResponse<NodeResponse>.ErrorResponse($"Invalid CREATE syntax for node.");

                string nodeId = match.Groups[1].Value;
                string label = match.Groups[2].Value; // This example uses label, which you may or may not need.
                var properties = ParseProperties(match.Groups[3].Value);

                if (Nodes.Any(n => n.Id == nodeId))
                {
                    return ApiResponse<NodeResponse>.ErrorResponse($"Node with id {nodeId} already exists.");
                }

                var newNode = new Node { Id = nodeId, Properties = properties };
                Nodes.Add(newNode);
                SaveToFile();

                // Return a NodeResponse instead of a string
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
                // Return error response
                return ApiResponse<NodeResponse>.ErrorResponse($"Error creating node: {ex.Message}");
            }

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

        //private string HandleMatchNode(string cypher)
        //{
        //    var pattern = new Regex(@"MATCH \((\w+):(\w+) \{(.+): '(.+)'\}\)", RegexOptions.IgnoreCase);
        //    var match = pattern.Match(cypher);
        //    if (!match.Success) return "Invalid MATCH syntax for node.";

        //    string label = match.Groups[2].Value; // Example uses label, which you may or may not need.
        //    string propertyName = match.Groups[3].Value;
        //    string propertyValue = match.Groups[4].Value;

        //    var matchingNodes = Nodes.Where(n => n.Properties.ContainsKey(propertyName) && n.Properties[propertyName].ToString() == propertyValue).ToList();

        //    return $"Found {matchingNodes.Count} nodes matching {propertyName} = {propertyValue}.";
        //}

        //private string HandleMatchNode(string cypher)
        //{
        //    var pattern = new Regex(@"MATCH \((\w+):(\w+) \{(.+): '(.+)'\}\)", RegexOptions.IgnoreCase);
        //    var match = pattern.Match(cypher);
        //    if (!match.Success) return "Invalid MATCH syntax for node.";

        //    string variable = match.Groups[1].Value;
        //    string label = match.Groups[2].Value;
        //    string propertyName = match.Groups[3].Value;
        //    string propertyValue = match.Groups[4].Value;

        //    var matchingNodes = Nodes.Where(n => n.Label == label
        //                                      && n.Properties.ContainsKey(propertyName)
        //                                      && n.Properties[propertyName].ToString() == propertyValue).ToList();

        //    if (matchingNodes.Count == 0)
        //    {
        //        return "No matching nodes found.";
        //    }

        //    var result = matchingNodes.Select(n =>
        //        $"Node(Id: {n.Id}, Label: {n.Label}, Properties: {JsonConvert.SerializeObject(n.Properties)})")
        //        .ToList();

        //    return $"Found {matchingNodes.Count} nodes:\n" + string.Join("\n", result);
        //}
        //private string HandleMatchCommand(string cypher)
        //{
        //    var nodePattern = new Regex(@"MATCH \((\w+):?(\w*) \{?([^}]*)\}?\)", RegexOptions.IgnoreCase);
        //    var relationshipPattern = new Regex(@"MATCH \((\w+):?(\w*)\)-\[:(\w+)\]-(\w+):?(\w*)", RegexOptions.IgnoreCase);
        //    var pathPattern = new Regex(@"MATCH (\w+) = \((\w+):?(\w*)\)-\[:(\w+)\*(\d*)\.\.(\d*)\]->\((\w+):?(\w*)\)", RegexOptions.IgnoreCase);
        //    var wherePattern = new Regex(@"WHERE (.+)", RegexOptions.IgnoreCase);
        //    var returnPattern = new Regex(@"RETURN (.+)", RegexOptions.IgnoreCase);

        //    var results = new List<string>();

        //    // Extract and process WHERE clause if present
        //    var whereClause = wherePattern.Match(cypher).Groups[1].Value;
        //    Func<Node, bool> nodeCondition = n => true; // Default condition: always true

        //    if (!string.IsNullOrEmpty(whereClause))
        //    {
        //        // Basic implementation for equality conditions in WHERE clause
        //        var conditions = whereClause.Split(new[] { " AND ", " OR " }, StringSplitOptions.RemoveEmptyEntries)
        //                                    .Select(c => c.Split(new[] { '=', '>', '<' }))
        //                                    .Select(parts => new
        //                                    {
        //                                        Property = parts[0].Trim(),
        //                                        Operator = whereClause.Contains("=") ? "=" :
        //                                                   whereClause.Contains(">") ? ">" :
        //                                                   "<", // Add other operators as needed
        //                                        Value = parts[1].Trim().Trim('\'')
        //                                    })
        //                                    .ToList();

        //        nodeCondition = n => conditions.All(cond =>
        //            n.Properties.ContainsKey(cond.Property) &&
        //            n.Properties[cond.Property].ToString() == cond.Value); // Add proper operator handling
        //    }

        //    // Extract and process RETURN clause if present
        //    var returnClause = returnPattern.Match(cypher).Groups[1].Value.Split(',').Select(r => r.Trim()).ToList();

        //    if (nodePattern.IsMatch(cypher))
        //    {
        //        var match = nodePattern.Match(cypher);
        //        string variable = match.Groups[1].Value;
        //        string label = match.Groups[2].Value;
        //        var propertiesString = match.Groups[3].Value;

        //        // Parse properties if provided
        //        var properties = string.IsNullOrEmpty(propertiesString) ?
        //            new Dictionary<string, string>() :
        //            propertiesString.Split(',')
        //                            .Select(p => p.Split(':'))
        //                            .ToDictionary(
        //                                p => p[0].Trim(),
        //                                p => p[1].Trim().Trim('\'')
        //                            );

        //        // Filter nodes by label (if specified) and properties, and apply WHERE condition
        //        var matchingNodes = Nodes.Where(n => (string.IsNullOrEmpty(label) || n.Label == label) &&
        //                                             properties.All(p => n.Properties.ContainsKey(p.Key) &&
        //                                                                 n.Properties[p.Key].ToString() == p.Value) &&
        //                                             nodeCondition(n))
        //                                 .ToList();

        //        if (matchingNodes.Count == 0)
        //            results.Add("No matching nodes found.");
        //        else
        //        {
        //            var formattedResults = matchingNodes.Select(n =>
        //            {
        //                if (returnClause.Count == 0 || returnClause.Contains(variable))
        //                    return $"Node(Id: {n.Id}, Label: {n.Label}, Properties: {JsonConvert.SerializeObject(n.Properties)})";

        //                var filteredProperties = n.Properties.Where(p => returnClause.Contains(p.Key))
        //                                                     .ToDictionary(k => k.Key, v => v.Value);
        //                return $"Node(Id: {n.Id}, Properties: {JsonConvert.SerializeObject(filteredProperties)})";
        //            });

        //            results.AddRange(formattedResults);
        //        }
        //    }
        //    else if (relationshipPattern.IsMatch(cypher))
        //    {
        //        var match = relationshipPattern.Match(cypher);
        //        string startVariable = match.Groups[1].Value;
        //        string startLabel = match.Groups[2].Value;
        //        string relationshipType = match.Groups[3].Value;
        //        string endVariable = match.Groups[4].Value;
        //        string endLabel = match.Groups[5].Value;

        //        var matchingEdges = Edges.Where(e => (string.IsNullOrEmpty(relationshipType) || e.RelationshipType == relationshipType) &&
        //                                             (string.IsNullOrEmpty(startLabel) || Nodes.Any(n => n.Id == e.FromId && n.Label == startLabel)) &&
        //                                             (string.IsNullOrEmpty(endLabel) || Nodes.Any(n => n.Id == e.ToId && n.Label == endLabel)))
        //                                 .ToList();

        //        if (matchingEdges.Count == 0)
        //            results.Add("No matching relationships found.");
        //        else
        //        {
        //            var formattedResults = matchingEdges.Select(e =>
        //            {
        //                if (returnClause.Count == 0 || returnClause.Contains(startVariable) || returnClause.Contains(endVariable))
        //                    return $"Edge(From: {e.FromId}, To: {e.ToId}, Relationship: {e.RelationshipType}, Properties: {JsonConvert.SerializeObject(e.Properties)})";

        //                return string.Empty;
        //            });

        //            results.AddRange(formattedResults.Where(r => !string.IsNullOrEmpty(r)));
        //        }
        //    }
        //    else if (pathPattern.IsMatch(cypher))
        //    {
        //        var match = pathPattern.Match(cypher);
        //        string pathVariable = match.Groups[1].Value;
        //        string startVariable = match.Groups[2].Value;
        //        string startLabel = match.Groups[3].Value;
        //        string relationshipType = match.Groups[4].Value;
        //        int minLength = int.Parse(match.Groups[5].Value);
        //        int maxLength = int.Parse(match.Groups[6].Value);
        //        string endVariable = match.Groups[7].Value;
        //        string endLabel = match.Groups[8].Value;

        //        var paths = FindPaths(startLabel, relationshipType, endLabel, minLength, maxLength);

        //        if (paths.Count == 0)
        //            results.Add("No matching paths found.");
        //        else
        //        {
        //            var formattedPaths = paths.Select(p =>
        //                $"Path: {string.Join(" -> ", p.Select(n => $"Node(Id: {n.Id}, Label: {n.Label})"))}");

        //            results.AddRange(formattedPaths);
        //        }
        //    }
        //    else
        //    {
        //        results.Add("Invalid MATCH syntax.");
        //    }

        //    return string.Join("\n", results);
        //}



        private string HandleMatchCommand(string cypher)
        {
            var nodePattern = new Regex(@"MATCH \((\w+):?(\w*) \{?([^}]*)\}?\)", RegexOptions.IgnoreCase);
            var relationshipPattern = new Regex(@"MATCH \((\w+):?(\w*)\)-\[(\w+):?(\w*)\]->\((\w+):?(\w*)\)", RegexOptions.IgnoreCase);
            var pathPattern = new Regex(@"MATCH (\w+) = \((\w+):?(\w*)\)-\[:(\w+)\*(\d*)\.\.(\d*)\]->\((\w+):?(\w*)\)", RegexOptions.IgnoreCase);
            var wherePattern = new Regex(@"WHERE (.+)", RegexOptions.IgnoreCase);
            var returnPattern = new Regex(@"RETURN (.+)", RegexOptions.IgnoreCase);
            var aggregationPattern = new Regex(@"(COUNT|SUM|AVG|MIN|MAX)\((\w+)\)", RegexOptions.IgnoreCase);
            var groupByPattern = new Regex(@"GROUP BY (\w+)", RegexOptions.IgnoreCase);
            var orderByPattern = new Regex(@"ORDER BY (\w+)\s*(ASC|DESC)?", RegexOptions.IgnoreCase);
            var limitPattern = new Regex(@"\bLIMIT\s+(\d+)", RegexOptions.IgnoreCase);
            var offsetPattern = new Regex(@"\bOFFSET\s+(\d+)", RegexOptions.IgnoreCase);
            var unionPattern = new Regex(@"\bUNION(?: ALL)?\b", RegexOptions.IgnoreCase);
            var intersectPattern = new Regex(@"\bINTERSECT\b", RegexOptions.IgnoreCase);
            var exceptPattern = new Regex(@"\bEXCEPT\b", RegexOptions.IgnoreCase);
            var nestedUnionPattern = new Regex(@"\((.*?UNION.*)\)", RegexOptions.IgnoreCase);

            var results = new List<string>();

            try
            {
                // Check if the query contains a nested UNION query
                var nestedUnionMatch = nestedUnionPattern.Match(cypher);
                if (nestedUnionMatch.Success)
                {
                    var nestedUnionQuery = nestedUnionMatch.Groups[1].Value;

                    // Recursively process the nested UNION query
                    var nestedUnionResult = HandleMatchCommand(nestedUnionQuery);

                    // Replace the nested UNION query in the main query with its result
                    cypher = cypher.Replace($"({nestedUnionQuery})", nestedUnionResult);
                }

                // Check if the query contains an EXCEPT clause
                if (exceptPattern.IsMatch(cypher))
                {
                    var exceptQueries = exceptPattern.Split(cypher).Select(q => q.Trim()).ToList();

                    if (exceptQueries.Count != 2)
                        throw new Exception("EXCEPT queries must have exactly two sub-queries.");

                    // Process the first and second sub-queries
                    var firstQueryResult = HandleMatchCommand(exceptQueries[0]).Split('\n').ToHashSet();
                    var secondQueryResult = HandleMatchCommand(exceptQueries[1]).Split('\n').ToHashSet();

                    // Perform set difference: elements in the first result but not in the second
                    var differenceResults = firstQueryResult.Except(secondQueryResult).ToList();

                    return string.Join("\n", differenceResults);
                }

                // Check if the query contains an INTERSECT clause
                if (intersectPattern.IsMatch(cypher))
                {
                    var intersectQueries = intersectPattern.Split(cypher).Select(q => q.Trim()).ToList();
                    var intersectResults = new List<HashSet<string>>();

                    foreach (var query in intersectQueries)
                    {
                        var queryResult = HandleMatchCommand(query);
                        intersectResults.Add(new HashSet<string>(queryResult.Split('\n')));
                    }

                    // Perform intersection of results from all sub-queries
                    var commonResults = intersectResults.Aggregate((set1, set2) => set1.Intersect(set2).ToHashSet());

                    return string.Join("\n", commonResults);
                }

                // Check if the query contains a UNION or UNION ALL clause
                if (unionPattern.IsMatch(cypher))
                {
                    var unionQueries = unionPattern.Split(cypher).Select(q => q.Trim()).ToList();
                    var unionResults = new List<string>();

                    foreach (var query in unionQueries)
                    {
                        var queryResult = HandleMatchCommand(query);
                        unionResults.AddRange(queryResult.Split('\n'));
                    }

                    // Check if it's UNION or UNION ALL (default is UNION)
                    bool isUnionAll = cypher.Contains("UNION ALL", StringComparison.OrdinalIgnoreCase);

                    // Combine results: UNION removes duplicates, UNION ALL allows duplicates
                    var combinedResults = isUnionAll ? unionResults : unionResults.Distinct().ToList();

                    return string.Join("\n", combinedResults);
                }

                // Extract and process WHERE clause if present
                var whereMatch = wherePattern.Match(cypher);
                var whereClause = whereMatch.Success ? whereMatch.Groups[1].Value : null;
                Func<Node, bool> nodeCondition = n => true;

                if (!string.IsNullOrEmpty(whereClause))
                {
                    var conditions = whereClause.Split(new[] { " AND ", " OR " }, StringSplitOptions.RemoveEmptyEntries)
                                                .Select(condition =>
                                                {
                                                    var match = Regex.Match(condition, @"(\w+)\s*(=|<>|>=|<=|>|<)\s*'(.+)'");
                                                    if (!match.Success) return null;

                                                    return new
                                                    {
                                                        Property = match.Groups[1].Value.Trim(),
                                                        Operator = match.Groups[2].Value.Trim(),
                                                        Value = match.Groups[3].Value.Trim()
                                                    };
                                                })
                                                .Where(c => c != null)
                                                .ToList();

                    nodeCondition = n => conditions.All(cond =>
                    {
                        if (!n.Properties.ContainsKey(cond.Property)) return false;

                        var propertyValue = n.Properties[cond.Property].ToString();

                        return cond.Operator switch
                        {
                            "=" => propertyValue == cond.Value,
                            "<>" => propertyValue != cond.Value,
                            ">" => string.Compare(propertyValue, cond.Value) > 0,
                            "<" => string.Compare(propertyValue, cond.Value) < 0,
                            ">=" => string.Compare(propertyValue, cond.Value) >= 0,
                            "<=" => string.Compare(propertyValue, cond.Value) <= 0,
                            _ => false
                        };
                    });
                }

                // Extract and process RETURN clause if present
                var returnMatch = returnPattern.Match(cypher);
                var returnClause = returnMatch.Success
                    ? returnMatch.Groups[1].Value.Split(',').Select(r => r.Trim()).ToList()
                    : new List<string>();

                // Detect if query contains GROUP BY clause
                var groupByMatch = groupByPattern.Match(cypher);
                var groupByProperty = groupByMatch.Success ? groupByMatch.Groups[1].Value : null;

                // Detect if query contains ORDER BY clause
                var orderByMatch = orderByPattern.Match(cypher);
                var orderByProperty = orderByMatch.Success ? orderByMatch.Groups[1].Value : null;
                var orderByDirection = orderByMatch.Success && orderByMatch.Groups[2].Value.ToUpper() == "DESC" ? "DESC" : "ASC";

                var isAggregation = aggregationPattern.IsMatch(cypher);
                var aggregationResults = new List<string>();

                if (nodePattern.IsMatch(cypher))
                {
                    var match = nodePattern.Match(cypher);
                    string variable = match.Groups[1].Value;
                    string label = match.Groups[2].Value;
                    var propertiesString = match.Groups[3].Value;

                    var properties = string.IsNullOrEmpty(propertiesString)
                        ? new Dictionary<string, string>()
                        : propertiesString.Split(',')
                                          .Select(p => p.Split(':'))
                                          .ToDictionary(
                                              p => p[0].Trim(),
                                              p => p[1].Trim().Trim('\'')
                                          );

                    var matchingNodes = Nodes.Where(n => (string.IsNullOrEmpty(label) || n.Label == label) &&
                                                         properties.All(p => n.Properties.ContainsKey(p.Key) &&
                                                                             n.Properties[p.Key].ToString() == p.Value) &&
                                                         nodeCondition(n))
                                             .ToList();

                    if (matchingNodes.Count == 0)
                    {
                        results.Add("No matching nodes found.");
                    }
                    else if (!string.IsNullOrEmpty(groupByProperty))
                    {
                        // Group by the specified property
                        var groupedNodes = matchingNodes.GroupBy(n => n.Properties.ContainsKey(groupByProperty)
                                                                      ? n.Properties[groupByProperty].ToString()
                                                                      : "NULL");

                        foreach (var group in groupedNodes)
                        {
                            var groupKey = group.Key;
                            var groupValues = group.ToList();

                            if (isAggregation)
                            {
                                // Handle aggregations within each group
                                var aggregationMatch = aggregationPattern.Match(cypher);
                                string aggregationType = aggregationMatch.Groups[1].Value.ToUpper();
                                string aggregationProperty = aggregationMatch.Groups[2].Value;

                                var values = groupValues.Select(n => n.Properties.ContainsKey(aggregationProperty)
                                                                     ? Convert.ToDouble(n.Properties[aggregationProperty])
                                                                     : 0).ToList();

                                double result = aggregationType switch
                                {
                                    "COUNT" => values.Count,
                                    "SUM" => values.Sum(),
                                    "AVG" => values.Average(),
                                    "MIN" => values.Min(),
                                    "MAX" => values.Max(),
                                    _ => 0
                                };

                                aggregationResults.Add($"{aggregationType}({aggregationProperty}) for {groupKey} = {result}");
                            }
                            else
                            {
                                aggregationResults.Add($"Group: {groupKey}, Nodes: [{string.Join(", ", groupValues.Select(v => v.Id))}]");
                            }
                        }
                    }
                    else if (isAggregation)
                    {
                        var aggregationMatch = aggregationPattern.Match(cypher);
                        string aggregationType = aggregationMatch.Groups[1].Value.ToUpper();
                        string property = aggregationMatch.Groups[2].Value;

                        var values = matchingNodes.Select(n => n.Properties.ContainsKey(property) ? Convert.ToDouble(n.Properties[property]) : 0).ToList();

                        double result = aggregationType switch
                        {
                            "COUNT" => values.Count,
                            "SUM" => values.Sum(),
                            "AVG" => values.Average(),
                            "MIN" => values.Min(),
                            "MAX" => values.Max(),
                            _ => 0
                        };

                        aggregationResults.Add($"{aggregationType}({property}) = {result}");
                    }
                    else
                    {
                        var formattedResults = matchingNodes.Select(n =>
                        {
                            if (returnClause.Count == 0)
                                return $"Node(Id: {n.Id}, Label: {n.Label}, Properties: {JsonConvert.SerializeObject(n.Properties)})";

                            var computedValues = returnClause.Select(expr =>
                            {
                                try
                                {// Handle string and numeric functions
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

                                    // Evaluate expressions dynamically using node properties
                                    var expression = expr;
                                    foreach (var prop in n.Properties)
                                    {
                                        expression = Regex.Replace(expression, $@"\b{prop.Key}\b", prop.Value.ToString());

                                    }

                                    var result = new DataTable().Compute(expression, "");
                                    return $"{expr} = {result}";
                                }
                                catch
                                {
                                    return $"Invalid expression: {expr}";
                                }
                            });

                            return string.Join(", ", computedValues);
                        });

                        // Apply ORDER BY if specified
                        if (!string.IsNullOrEmpty(orderByProperty))
                        {
                            formattedResults = orderByDirection == "ASC"
                            ? formattedResults.OrderBy(res => double.TryParse(res, out var num) ? num : double.MaxValue).ToList()
                            : formattedResults.OrderByDescending(res => double.TryParse(res, out var num) ? num : double.MinValue).ToList();
                        }
                        // Apply OFFSET if specified
                        var offsetMatch = offsetPattern.Match(cypher);
                        int offset = offsetMatch.Success ? int.Parse(offsetMatch.Groups[1].Value) : 0;
                        formattedResults = formattedResults.Skip(offset).ToList();

                        // Apply LIMIT if specified
                        var limitMatch = limitPattern.Match(cypher);
                        int limit = limitMatch.Success ? int.Parse(limitMatch.Groups[1].Value) : formattedResults.Count();
                        formattedResults = formattedResults.Take(limit).ToList();

                        results.AddRange(formattedResults);
                    }
                }
                else if (relationshipPattern.IsMatch(cypher))
                {
                    var match = relationshipPattern.Match(cypher);
                    string startVariable = match.Groups[1].Value;
                    string startLabel = match.Groups[2].Value;
                    string relationshipType = match.Groups[3].Value;
                    string endVariable = match.Groups[4].Value;
                    string endLabel = match.Groups[5].Value;

                    // Filter edges by relationship type and node labels
                    var matchingEdges = Edges.Where(e =>
                                (string.IsNullOrEmpty(relationshipType) || e.RelationshipType == relationshipType) &&
                                (string.IsNullOrEmpty(startLabel) || Nodes.Any(n => n.Id == e.FromId && n.Label == startLabel)) &&
                                (string.IsNullOrEmpty(endLabel) || Nodes.Any(n => n.Id == e.ToId && n.Label == endLabel)))
                            .ToList();

                    if (matchingEdges.Count == 0)
                        return "No matching relationships found.";

                    var result = matchingEdges.Select(e =>
                        $"Edge(From: {e.FromId}, To: {e.ToId}, Relationship: {e.RelationshipType}, Properties: {JsonConvert.SerializeObject(e.Properties)})")
                        .ToList();

                    return $"Found {matchingEdges.Count} relationships:\n" + string.Join("\n", result);
                }
                else
                {
                    results.Add("Invalid MATCH syntax.");
                }

                return string.Join("\n", results);
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
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
                    Console.WriteLine("Graph file does not exist, initializing a new graph.");
                    return;
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
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Failed to load the graph from {_graphPath}: {ex.Message}");
                }
            }

        }

    }




