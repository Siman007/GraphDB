﻿using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using Newtonsoft.Json;

namespace GraphDB
{
    public class GraphService
    {
        private static readonly Lazy<GraphService> _instance = new Lazy<GraphService>(() => new GraphService());
        public static GraphService Instance => _instance.Value;

        private const string DefaultFilePath = "data"; // Relative path to store graph data
        private Graph _currentGraph;
        private string _databaseName;

        public GraphService()
        {
            // Constructor is now parameterless
        }

        public Graph GetCurrentGraph()
        {
            if (_currentGraph == null || !_currentGraph.IsDatabaseLoaded)
            {
                throw new InvalidOperationException("No graph is currently loaded.");
            }
            return _currentGraph;
        }

        public string GetDatabaseName() => _databaseName;

        public string GetDatabasePath() => _currentGraph != null ? Path.Combine(DefaultFilePath, $"{_databaseName}.json") : null;

        public bool IsDatabaseLoaded = false;




       

        public dynamic ExecuteCypherCommands(string cypherScript)
        {
            // Split the script into separate commands by a delimiter, e.g., a semicolon followed by a newline
            var commands = cypherScript.Split(new[] { ";\n", ";\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            var results = new List<dynamic>();

            foreach (var command in commands)
            {
                try
                {
                    var trimmedCommand = command.Trim();
                    if (!string.IsNullOrEmpty(trimmedCommand))
                    {
                        results.Add(ExecuteSingleCypherCommand(trimmedCommand));
                    }
                }
                catch (Exception ex)
                {
                    results.Add(new { Error = $"Error processing command '{command}': {ex.Message}" });
                }
            }

            return results; // Return all results, including successes and errors
        }

        private dynamic ExecuteSingleCypherCommand(string cypher)
        {
            
            CypherCommandType commandType = cypher.ToCommandType();
            switch (commandType)
            {
                case CypherCommandType.CreateDatabase:
                    return HandleCreateDatabase(ExtractDatabaseName(cypher));
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
                case CypherCommandType.DeleteEdge:
                    return HandleDeleteEdge(cypher);
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

        public string HandleCreateDatabase(string databaseName)
        {
            if (string.IsNullOrWhiteSpace(databaseName))
            {
                return "Database name must be provided.";
            }

            CreateDatabase(databaseName);

           
            return $"Database '{databaseName}' created successfully.";
        }

        //public void SaveNewGraph()
        //{
        //    string filePath = Path.Combine(DefaultFilePath, $"{_currentGraph.GetDatabaseName()}.json");
        //    SaveGraph();
        //}

        

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

                // Access the singleton instance of GraphService and retrieve the current graph
                var graphService = GraphService.Instance;
                var graph = graphService.GetCurrentGraph();

                // Check if the node already exists.
                if (graph.Nodes.Any(n => n.Id == nodeId))
                {
                    return ApiResponse<NodeResponse>.ErrorResponse($"Node with id {nodeId} already exists.");
                }

                // Create and add the new node to the graph
                var newNode = new Node { Id = nodeId, Label = label, Properties = properties };
                graph.Nodes.Add(newNode);

                // Update indexes with the new node. This method should handle both the primary index (e.g., on ID) and any secondary indexes you've implemented.
                graph.UpdateNodeIndex(newNode);

                // Saving the current graph state is now the responsibility of GraphService
                SaveGraph();

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
            // Access the singleton instance of GraphService and retrieve the current graph
            var graphService = GraphService.Instance;
            var graph = graphService.GetCurrentGraph();

            var pattern = new Regex(@"MERGE \((\w+):(\w+) \{(.+)\}\)", RegexOptions.IgnoreCase);
            var match = pattern.Match(cypher);
            if (!match.Success) return "Invalid MERGE syntax for node.";

            string nodeId = match.Groups[1].Value;
            string label = match.Groups[2].Value; // The label might be useful for certain types of queries or indexes.
            var propertiesString = match.Groups[3].Value;
            var properties = ParseProperties(propertiesString);

            var nodeExists = graph.Nodes.Any(n => n.Id == nodeId);
            var node = graph.Nodes.FirstOrDefault(n => n.Id == nodeId);
            bool needsIndexUpdate = false;

            if (node == null)
            {
                // Node creation
                node = new Node { Id = nodeId, Label = label, Properties = properties };
                graph.Nodes.Add(node);
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
                graph.UpdateNodeIndex(node);
            }

            SaveGraph();
            return nodeExists ? $"Node {nodeId} merged (updated) successfully." : $"Node {nodeId} merged (created) successfully.";
        }

        // IF CONDITION [condition] THEN [action] ELSE [alternative action]
        // Where [condition] is a simple expression (e.g., "node exists"), [action] and [alternative action]
        // are actions to be taken based on the condition. 

        private ApiResponse<RelationshipResponse> HandleCreateRelationship(string cypher)
        {
            var graphService = GraphService.Instance; // Access the singleton GraphService
            var graph = graphService.GetCurrentGraph(); // Retrieve the current graph instance

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

            var fromNode = graph.Nodes.FirstOrDefault(n => n.Id == fromNodeId);
            var toNode = graph.Nodes.FirstOrDefault(n => n.Id == toNodeId);
            if (fromNode == null || toNode == null)
            {
                return ApiResponse<RelationshipResponse>.ErrorResponse("One or both specified nodes do not exist.");
            }

            var properties = ParseProperties(propertiesString);
            var existingEdge = graph.Edges.FirstOrDefault(e => e.FromId == fromNodeId && e.ToId == toNodeId && e.RelationshipType == relationshipType);
            if (existingEdge != null)
            {
                return ApiResponse<RelationshipResponse>.ErrorResponse($"A relationship of type {relationshipType} from {fromNodeId} to {toNodeId} already exists.");
            }

            var edge = new Edge
            {
                FromId = fromNodeId,
                ToId = toNodeId,
                RelationshipType = relationshipType,
                Properties = properties.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value)
            };
            graph.Edges.Add(edge);
            graph.UpdateEdgeIndex(edge);
            // Saving the current graph state is now the responsibility of GraphService
            SaveGraph();

            var relationshipResponse = new RelationshipResponse
            {
                FromId = edge.FromId,
                ToId = edge.ToId,
                RelationshipType = edge.RelationshipType,
                Properties = edge.Properties
            };

            return ApiResponse<RelationshipResponse>.SuccessResponse(relationshipResponse, $"Created relationship of type {relationshipType} from {fromNodeId} to {toNodeId}.");
        }


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
                return ExecuteCypherCommands(action);
            }
            else
            {
                // Condition is false, execute the alternative action
                return ExecuteCypherCommands(alternativeAction);
            }
        }

        public ApiResponse<object> HandleDisplayHelp(string cypher)
        {
            var parts = cypher.Trim().Split(new char[] { ' ' }, 2);
            var command = parts[0].ToUpper();
            var message = "";

            if (command == "HELP")
            {
                var specificCommand = parts.Length > 1 ? parts[1] : "";
                message = GraphHelp.GetHelp(specificCommand); // Assume GraphHelp is a static class that handles help queries
            }
            else
            {
                message = GraphHelp.GetHelp(); // Default help response
            }

            // Since this method doesn't return specific data, the Data property is set to null.
            return ApiResponse<object>.SuccessResponse(null, message);
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
                        return ExecuteCypherCommands(trueAction);
                    }
                    else if (hasElse)
                    {
                        // Condition is false and an ELSE exists, execute falseAction
                        return ExecuteCypherCommands(falseAction);
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




        public ApiResponse<string> HandleCountNodes()
        {
            int nodeCount = _currentGraph.Nodes.Count;
            return ApiResponse<string>.SuccessResponse("",$"Total nodes: {nodeCount}.");
        }

        public ApiResponse<string> HandleCountEdges()
        {
            int edgeCount = _currentGraph.Edges.Count;
            return ApiResponse<string>.SuccessResponse("",$"Total edges: {edgeCount}.");
        }

        public ApiResponse<string> HandleAggregateSum(string cypher)
        {
            var match = Regex.Match(cypher, @"AGGREGATE SUM (\w+) ON (\w+)", RegexOptions.IgnoreCase);
            if (!match.Success) return ApiResponse<string>.ErrorResponse("Invalid AGGREGATE SUM syntax.");

            string propertyName = match.Groups[1].Value;
            string targetType = match.Groups[2].Value.ToLower();

            double sum = 0;
            switch (targetType)
            {
                case "nodes":
                    sum = _currentGraph.Nodes
                        .Where(n => n.Properties.ContainsKey(propertyName))
                        .Sum(n => Convert.ToDouble(n.Properties[propertyName]));
                    break;
                case "edges":
                    sum = _currentGraph.Edges
                        .Where(e => e.Properties.ContainsKey(propertyName))
                        .Sum(e => Convert.ToDouble(e.Properties[propertyName]));
                    break;
                default:
                    return ApiResponse<string>.ErrorResponse("Target type for AGGREGATE SUM must be either 'nodes' or 'edges'.");
            }

            return ApiResponse<string>.SuccessResponse("",$"Sum of {propertyName} on {targetType}: {sum}.");
        }

        public string HandleAggregateAvg(string cypher)
        {
            var match = Regex.Match(cypher, @"AGGREGATE AVG (\w+) ON (\w+)", RegexOptions.IgnoreCase);
            if (!match.Success) return "Invalid AGGREGATE AVG syntax.";

            string propertyName = match.Groups[1].Value;
            string targetType = match.Groups[2].Value.ToLower();

            double avg = 0;
            if (targetType == "nodes")
            {
                avg = _currentGraph.Nodes
                    .Where(n => n.Properties.ContainsKey(propertyName))
                    .Average(n => Convert.ToDouble(n.Properties[propertyName]));
            }
            else if (targetType == "edges")
            {
                avg = _currentGraph.Edges
                    .Where(e => e.Properties.ContainsKey(propertyName))
                    .Average(e => Convert.ToDouble(e.Properties[propertyName]));
            }
            else
            {
                return "Target type for AGGREGATE AVG must be either 'nodes' or 'edges'.";
            }

            return $"Average of {propertyName} on {targetType}: {avg}.";
        }


        public string HandleFindRelationships(string cypher)
        {
            var graph = GetCurrentGraph(); // Get the current graph instance
            var match = Regex.Match(cypher, @"FIND RELATIONSHIPS FROM (\w+) TO (\w+)", RegexOptions.IgnoreCase);
            if (!match.Success) return "Invalid FIND RELATIONSHIPS syntax.";

            string fromNodeId = match.Groups[1].Value;
            string toNodeId = match.Groups[2].Value;

            // Query the graph's edges
            var relationships = graph.Edges.Where(e => e.FromId == fromNodeId && e.ToId == toNodeId).ToList();
            return $"Found {relationships.Count} relationships from {fromNodeId} to {toNodeId}.";
        }


        public ApiResponse<List<NodeResponse>> HandleFindNeighbors(string cypher)
        {
            var graph = GetCurrentGraph();
            var pattern = new Regex(@"FIND NEIGHBORS \(id:\s*'([^']*)'(?:,\s*label:\s*'([^']*)')?\)", RegexOptions.IgnoreCase);
            var match = pattern.Match(cypher);

            if (!match.Success)
            {
                return ApiResponse<List<NodeResponse>>.ErrorResponse("Invalid FIND NEIGHBORS syntax.");
            }

            string nodeId = match.Groups[1].Value;
            string label = match.Groups[2].Success ? match.Groups[2].Value : null;

            var neighbors = graph.FindNeighbors(nodeId, label);
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

       


        public string HandleMatchPattern(string cypher)
        {
            var pattern = new Regex(@"MATCH \((\w+)\)-\[\:(\w+) \{(.+): '(.+)'\}\]->\((\w+)\)", RegexOptions.IgnoreCase);
            var match = pattern.Match(cypher);
            if (!match.Success) return "Invalid MATCH syntax for relationship.";

            string relationshipType = match.Groups[2].Value;
            string propertyName = match.Groups[3].Value;
            string propertyValue = match.Groups[4].Value;

            // Utilizing the singleton instance of the graph
            var matchingRelationships = _currentGraph.Edges
                .Where(e => e.RelationshipType.Equals(relationshipType, StringComparison.OrdinalIgnoreCase) &&
                            e.Properties.TryGetValue(propertyName, out var value) &&
                            value.ToString().Equals(propertyValue, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var response = new StringBuilder($"Found {matchingRelationships.Count} relationships of type '{relationshipType}' matching {propertyName} = '{propertyValue}':");
            foreach (var rel in matchingRelationships)
            {
                response.AppendLine($"\nFrom Node ID: {rel.FromId}, To Node ID: {rel.ToId}");
            }

            return response.ToString();
        }

        public List<(Node, Node)> MatchPattern(Func<Node, bool> startCondition, string relationshipType, Func<Node, bool> endCondition)
        {
            var nodeLookup = _currentGraph.Nodes.ToDictionary(n => n.Id, n => n);

            var matches = _currentGraph.Edges
                .Where(e => e.RelationshipType.Equals(relationshipType, StringComparison.OrdinalIgnoreCase))
                .Select(e => (FromNode: nodeLookup.TryGetValue(e.FromId, out var fromNode) ? fromNode : null,
                              ToNode: nodeLookup.TryGetValue(e.ToId, out var toNode) ? toNode : null))
                .Where(pair => pair.FromNode != null && pair.ToNode != null)
                .Where(pair => startCondition(pair.FromNode) && endCondition(pair.ToNode))
                .ToList();

            return matches;
        }



        private bool IsIndexedProperty(string propertyName)
        {
            // Here, define logic to determine if a property is indexed
            // For example, if indexing edges by 'type', then:
            return propertyName.Equals("type", StringComparison.OrdinalIgnoreCase);
            // Extend this logic based on actual indexed properties
        }


        // Method to add a relationship between nodes
        public ApiResponse<RelationshipResponse> AddRelationship(string fromNodeId, string toNodeId, string relationshipType, Dictionary<string, string> properties)
        {
            var fromNode = _currentGraph.Nodes.FirstOrDefault(n => n.Id == fromNodeId);
            var toNode = _currentGraph.Nodes.FirstOrDefault(n => n.Id == toNodeId);
            if (fromNode == null || toNode == null)
            {
                return ApiResponse<RelationshipResponse>.ErrorResponse("One or both specified nodes do not exist.");
            }

            var existingEdge = _currentGraph.Edges.FirstOrDefault(e => e.FromId == fromNodeId && e.ToId == toNodeId && e.RelationshipType == relationshipType);
            if (existingEdge != null)
            {
                return ApiResponse<RelationshipResponse>.ErrorResponse($"A relationship of type {relationshipType} from {fromNodeId} to {toNodeId} already exists.");
            }

            var objectProperties = properties.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value);
            var edge = new Edge
            {
                FromId = fromNodeId,
                ToId = toNodeId,
                RelationshipType = relationshipType,
                Properties = objectProperties
            };

            _currentGraph.Edges.Add(edge);
            _currentGraph.UpdateEdgeIndex(edge); // Assume UpdateEdgeIndex is a method of Graph to update edge indexes
            SaveGraph(); // Method to save the current graph state

            var relationshipResponse = new RelationshipResponse
            {
                FromId = edge.FromId,
                ToId = edge.ToId,
                RelationshipType = edge.RelationshipType,
                Properties = edge.Properties
            };

            return ApiResponse<RelationshipResponse>.SuccessResponse(relationshipResponse, $"Created relationship of type {relationshipType} from {fromNodeId} to {toNodeId}.");
        }

        public ApiResponse<string> HandleMatchNode(string cypher)
        {
            var pattern = new Regex(@"MATCH \((\w+):(\w+) \{(.+): '(.+)'\}\)", RegexOptions.IgnoreCase);
            var match = pattern.Match(cypher);
            var graph = GetCurrentGraph();

            if (match.Success)
            {
                string label = match.Groups[2].Value; // Assuming label might be used for querying
                string propertyName = match.Groups[3].Value;
                string propertyValue = match.Groups[4].Value;

                // Leveraging any indexing mechanisms for nodes, if applicable
                var matchingNodes = graph.Nodes
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
                return ApiResponse<string>.SuccessResponse(null, response.ToString());
            }
            else
            {
                pattern = new Regex(@"MATCH \(n\) WHERE n\.(\w+) = '([^']*)'");
                match = pattern.Match(cypher);
                if (match.Success)
                {
                    string propertyName = match.Groups[1].Value;
                    string propertyValue = match.Groups[2].Value;

                    var nodes = graph.QueryNodesByProperty(propertyName, propertyValue);
                    var result = new StringBuilder($"Found {nodes.Count} nodes with {propertyName} = '{propertyValue}':");
                    foreach (var node in nodes)
                    {
                        result.AppendLine($"Node ID: {node.Id}, Label: {node.Label}");
                    }

                    return ApiResponse<string>.SuccessResponse(null,result.ToString());
                }
                else
                {
                    return ApiResponse<string>.ErrorResponse("Invalid MATCH syntax for node.");
                }
            }
        }

            public ApiResponse<string> QueryNodesByProperty(string propertyName, string propertyValue)
{
            var graph = GetCurrentGraph();
            if (graph == null)
            {
                return ApiResponse<string>.ErrorResponse("No graph loaded.");
            }

            var matchingNodes = graph.QueryNodesByProperty(propertyName, propertyValue);

            if (matchingNodes.Count > 0)
            {
                var response = new StringBuilder($"Found {matchingNodes.Count} nodes with {propertyName} = '{propertyValue}':");
                foreach (var node in matchingNodes)
                {
                    response.AppendLine($"Node ID: {node.Id}, Label: {node.Label}");
                    // Optionally add more details about each node's properties if needed
                }
                return ApiResponse<string>.SuccessResponse(null, response.ToString());
            }
            else
            {
                return ApiResponse<string>.ErrorResponse($"No nodes found with {propertyName} = '{propertyValue}'.");
            }
        }

        // Handling the Cypher command to query edges by a specific property
        public List<Edge> HandleQueryEdgesByProperty(string cypher)
        {
            var regex = new Regex(@"MATCH \(:\w+\)-\[r\]-(?:\:\w+)? WHERE r\.(\w+) = '([^']*)'");
            var match = regex.Match(cypher);
            if (match.Success)
            {
                string propertyName = match.Groups[1].Value;
                string propertyValue = match.Groups[2].Value;
                var graph = GetCurrentGraph();
                return graph.QueryEdgesByProperty(propertyName, propertyValue);
            }
            else
            {
                throw new ArgumentException("Invalid syntax for querying edges by property.");
            }
        }

        public ApiResponse<List<Edge>> HandleMatchRelationship(string cypher)
        {
            var graph = GetCurrentGraph(); // Access the current graph from the service
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

            // Utilize indexing for improved query performance if applicable
            IEnumerable<Edge> potentialEdges = graph.Edges; // Directly use Edges from the current graph

            // Filter edges based on the detailed criteria without direct index usage
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


        public ApiResponse<object> HandleDeleteNode(string cypher)
        {
            var graph = GetCurrentGraph();
            var deleteByIdPattern = new Regex(@"MATCH \(n\) WHERE n\.id = '(\w+)' DELETE n", RegexOptions.IgnoreCase);
            var deleteByLabelPattern = new Regex(@"MATCH \(n:(\w+)\) DELETE n", RegexOptions.IgnoreCase);

            // Attempt to match DELETE by ID pattern
            var matchById = deleteByIdPattern.Match(cypher);
            if (matchById.Success)
            {
                string nodeId = matchById.Groups[1].Value;
                if (graph.Nodes.Any(n => n.Id == nodeId))
                {
                    graph.DeleteNode(nodeId);
                    SaveGraph();
                    return ApiResponse<object>.SuccessResponse(null, $"Node with ID {nodeId} deleted successfully.");
                }
                return ApiResponse<object>.ErrorResponse($"No node found with ID {nodeId}.");
            }

            // Attempt to match DELETE by Label pattern
            var matchByLabel = deleteByLabelPattern.Match(cypher);
            if (matchByLabel.Success)
            {
                string nodeLabel = matchByLabel.Groups[1].Value;
                var nodesToDelete = graph.Nodes.Where(n => n.Label == nodeLabel).ToList();
                if (nodesToDelete.Any())
                {
                    foreach (var node in nodesToDelete)
                    {
                        graph.DeleteNode(node.Id);
                    }
                    SaveGraph();
                    return ApiResponse<object>.SuccessResponse(null, $"Nodes with label {nodeLabel} deleted successfully.");
                }
                return ApiResponse<object>.ErrorResponse($"No nodes found with label {nodeLabel}.");
            }

            return ApiResponse<object>.ErrorResponse("Invalid DELETE syntax.");
        }




        public ApiResponse<object> HandleDetachDelete(string cypher)
        {
            var graph = GetCurrentGraph(); // Use the current graph from singleton service
            var pattern = new Regex(@"DETACH DELETE (\w+)(?:\s*:\s*(\w+))?", RegexOptions.IgnoreCase);
            var match = pattern.Match(cypher);
            if (!match.Success)
            {
                return ApiResponse<object>.ErrorResponse("Invalid DETACH DELETE syntax.");
            }

            string nodeIdOrLabel = match.Groups[1].Value;
            string label = match.Groups[2].Success ? match.Groups[2].Value : null;

            List<Node> nodesToDelete;
            if (string.IsNullOrEmpty(label))
            {
                // If deleting by ID, find the node directly
                var node = graph.Nodes.FirstOrDefault(n => n.Id == nodeIdOrLabel);
                if (node == null) return ApiResponse<object>.ErrorResponse($"Node {nodeIdOrLabel} does not exist.");
                nodesToDelete = new List<Node> { node };
            }
            else
            {
                // If deleting by label, find all nodes matching the label
                nodesToDelete = graph.Nodes.Where(n => n.Properties.TryGetValue("label", out var nodeLabel) && nodeLabel.ToString() == label).ToList();
                if (!nodesToDelete.Any()) return ApiResponse<object>.ErrorResponse($"No nodes with label {label} exist.");
            }

            foreach (var node in nodesToDelete)
            {
                // Remove the node from its index
                graph.RemoveNodeFromIndex(node);

                // Detach and delete all connected edges
                var connectedEdges = graph.Edges.Where(e => e.FromId == node.Id || e.ToId == node.Id).ToList();
                foreach (var edge in connectedEdges)
                {
                    graph.RemoveEdgeFromIndex(edge);
                    graph.Edges.Remove(edge);
                }

                // Finally, remove the node
                graph.Nodes.Remove(node);
            }

            SaveGraph(); // Save changes after all deletions
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

            // Access the graph from the singleton instance
            var graph = GetCurrentGraph();

            // Find the specific edge to remove
            var edgeToRemove = graph.Edges.FirstOrDefault(e => e.FromId == fromNodeId && e.ToId == toNodeId && e.RelationshipType == relationshipType);
            if (edgeToRemove == null)
            {
                return ApiResponse<object>.ErrorResponse($"Relationship {relationshipType} from {fromNodeId} to {toNodeId} does not exist.");
            }

            // Remove the edge from the edge index if necessary
            if (edgeToRemove.Properties.ContainsKey("type"))
            {
                var typeValue = edgeToRemove.Properties["type"].ToString();
                if (graph._edgeIndex.ContainsKey(typeValue))
                {
                    graph._edgeIndex[typeValue].Remove(edgeToRemove);
                    if (graph._edgeIndex[typeValue].Count == 0)
                    {
                        graph._edgeIndex.Remove(typeValue); // Remove the entry from the index if no more edges of this type exist
                    }
                }
            }

            // Remove the edge from the Edges list
            graph.Edges.Remove(edgeToRemove);

            SaveGraph(); // Save changes after the deletion
            return ApiResponse<object>.SuccessResponse(null, $"Relationship {relationshipType} from {fromNodeId} to {toNodeId} has been deleted.");
        }



        public string HandleSetNodeProperty(string cypher)
        {
            var graph = GetCurrentGraph(); // Use the current graph instance
            var pattern = new Regex(@"SET NODE \((\w+)\) SET (\w+) = '(.+)'", RegexOptions.IgnoreCase);
            var match = pattern.Match(cypher);
            if (!match.Success) return "Invalid SET NODE syntax.";

            string nodeId = match.Groups[1].Value;
            string propertyName = match.Groups[2].Value;
            string propertyValue = match.Groups[3].Value;

            var node = graph.Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node == null)
            {
                return $"Node {nodeId} does not exist.";
            }

            bool needsReindexing = node.Properties.ContainsKey(propertyName) && !node.Properties[propertyName].Equals(propertyValue);
            needsReindexing = needsReindexing || IsIndexedProperty(propertyName);

            node.Properties[propertyName] = propertyValue;

            if (needsReindexing)
            {
                graph.UpdateNodeIndex(node); // Now an instance method on Graph
            }

            SaveGraph(); // Save the updated graph
            return $"Property {propertyName} of node {nodeId} has been set to {propertyValue}.";
        }



        public string FormatRelationships(List<Edge> edges)
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
            var graph = GetCurrentGraph(); // Work with the current graph instance
            var pattern = new Regex(@"SET RELATIONSHIP \((\w+)\)-\[(\w+)\]->\((\w+)\) \{(.+)\}", RegexOptions.IgnoreCase);
            var match = pattern.Match(cypher);
            if (!match.Success)
            {
                return ApiResponse<RelationshipResponse>.ErrorResponse("Invalid syntax for SET RELATIONSHIP.");
            }

            string fromId = match.Groups[1].Value;
            string toId = match.Groups[3].Value;
            string relationshipType = match.Groups[2].Value;
            var propertiesString = match.Groups[4].Value;
            var properties = ParseProperties(propertiesString);

            var edge = graph.Edges.FirstOrDefault(e => e.FromId == fromId && e.ToId == toId && e.RelationshipType == relationshipType);
            if (edge == null)
            {
                return ApiResponse<RelationshipResponse>.ErrorResponse("Relationship does not exist.");
            }

            bool needsReindexing = false;
            foreach (var prop in properties)
            {
                if (edge.Properties.ContainsKey(prop.Key) && edge.Properties[prop.Key] != prop.Value)
                {
                    needsReindexing = true;
                    edge.Properties[prop.Key] = prop.Value;
                }
                else if (!edge.Properties.ContainsKey(prop.Key))
                {
                    edge.Properties.Add(prop.Key, prop.Value);
                }
            }

            if (needsReindexing)
            {
                graph.UpdateEdgeIndex(edge);
            }

            SaveGraph();

            return ApiResponse<RelationshipResponse>.SuccessResponse(
                new RelationshipResponse
                {
                    FromId = edge.FromId,
                    ToId = edge.ToId,
                    RelationshipType = edge.RelationshipType,
                    Properties = edge.Properties
                },
                $"Updated properties of relationship from {fromId} to {toId}.");
        }


        // Example method to handle importing CSV files
        public string HandleImportCsv(string cypher)
        {
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
                var existingNodeIds = new HashSet<string>(_currentGraph.Nodes.Select(n => n.Id));

                using (var reader = new StreamReader(filePath))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    csv.Context.RegisterClassMap<NodeMap>(); // Assuming NodeMap is defined elsewhere
                    var records = csv.GetRecords<Node>();

                    foreach (var record in records)
                    {
                        if (!existingNodeIds.Contains(record.Id))
                        {
                            _currentGraph.Nodes.Add(record);
                            existingNodeIds.Add(record.Id);
                            addedCount++;
                        }
                        else
                        {
                            duplicateCount++;
                        }
                    }
                }

                SaveGraph(); // Persist changes
                return $"{addedCount} nodes imported successfully. {duplicateCount} duplicates found and ignored.";
            }
            catch (Exception ex)
            {
                return $"Failed to import nodes: {ex.Message}";
            }
        }

       

        // Define the NodeMap class here or elsewhere if shared
        public class NodeMap : ClassMap<Node>
        {
            public NodeMap()
            {
                Map(m => m.Id).Name("Id");
                Map(m => m.Label).Name("Label");
                // Additional mappings as needed
            }
        }

        private string ImportCsvEdges(string filePath)
        {
            try
            {
                int addedCount = 0, duplicateCount = 0;
                // Define a unique identifier for each edge to check for duplicates
                var existingEdges = new HashSet<string>(_currentGraph.Edges.Select(e => $"{e.FromId}-{e.ToId}-{e.RelationshipType}"));

                using (var reader = new StreamReader(filePath))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    // Assuming EdgeMap is defined similar to NodeMap for mapping CSV columns to Edge properties
                    csv.Context.RegisterClassMap<EdgeMap>();
                    var records = csv.GetRecords<Edge>();

                    foreach (var record in records)
                    {
                        // Construct a unique identifier for the current record to check for duplicates
                        string edgeIdentifier = $"{record.FromId}-{record.ToId}-{record.RelationshipType}";

                        if (!existingEdges.Contains(edgeIdentifier))
                        {
                            _currentGraph.Edges.Add(record);
                            existingEdges.Add(edgeIdentifier); // Keep track of added edges
                            addedCount++;
                        }
                        else
                        {
                            duplicateCount++; // Increment the duplicate counter if this edge is already present
                        }
                    }
                }

                SaveGraph(); // Save changes to the graph
                return $"{addedCount} edges imported successfully. {duplicateCount} duplicates found and ignored.";
            }
            catch (Exception ex)
            {
                return $"Failed to import edges: {ex.Message}";
            }
        }

        // Assuming an EdgeMap class for CSV column to Edge property mapping
        public class EdgeMap : ClassMap<Edge>
        {
            public EdgeMap()
            {
                Map(m => m.FromId).Name("FromId");
                Map(m => m.ToId).Name("ToId");
                Map(m => m.RelationshipType).Name("RelationshipType");
                // Define additional mappings for any other properties present in your CSV and Edge class
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

        public string HandleImportJSON(string filePath)
        {
            try
            {
                var jsonData = File.ReadAllText(filePath);
                var graphData = JsonConvert.DeserializeObject<GraphData>(jsonData);

                if (graphData == null) throw new InvalidOperationException("Unable to parse JSON data.");

                int nodesAdded = 0, edgesAdded = 0;
                foreach (var node in graphData.Nodes ?? Enumerable.Empty<Node>())
                {
                    if (!_currentGraph.Nodes.Any(n => n.Id == node.Id))
                    {
                        _currentGraph.Nodes.Add(node);
                        nodesAdded++;
                    }
                }

                foreach (var edge in graphData.Edges ?? Enumerable.Empty<Edge>())
                {
                    _currentGraph.Edges.Add(edge);
                    edgesAdded++;
                }

                SaveGraph();
                return $"{nodesAdded} nodes and {edgesAdded} edges imported successfully from JSON.";
            }
            catch (Exception ex)
            {
                return $"Failed to import from JSON: {ex.Message}";
            }
        }

        public string HandleExportCsvNodes(string cypher)
        {
            var match = Regex.Match(cypher, @"EXPORT CSV NODES '([^']+)'", RegexOptions.IgnoreCase);
            if (!match.Success) return "Invalid syntax for EXPORT CSV NODES.";

            string filePath = match.Groups[1].Value;
            try
            {
                using (var writer = new StreamWriter(filePath))
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                {
                    csv.WriteRecords(_currentGraph.Nodes);
                }
                return $"Nodes exported to CSV successfully at {filePath}.";
            }
            catch (Exception ex)
            {
                return $"Failed to export nodes to CSV: {ex.Message}";
            }
        }
        public string HandleExportCsvEdges(string cypher)
        {
            // Using a regular expression to parse the cypher command for exporting edges
            var match = Regex.Match(cypher, @"EXPORT CSV EDGES '([^']+)'", RegexOptions.IgnoreCase);
            if (!match.Success) return "Invalid syntax for EXPORT CSV EDGES.";

            string filePath = match.Groups[1].Value;
            try
            {
                using (var writer = new StreamWriter(filePath))
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                {
                    // Writing the records of edges to the CSV file
                    csv.WriteRecords(_currentGraph.Edges);
                }
                return $"Edges exported to CSV successfully at {filePath}.";
            }
            catch (Exception ex)
            {
                return $"Failed to export edges to CSV: {ex.Message}";
            }
        }



        

        // Handling the Cypher command for deleting an edge
        public string HandleDeleteEdge(string cypher)
        {
            var graph = GetCurrentGraph(); // Work with the current graph instance
            var regex = new Regex(@"DELETE EDGE FROM \((\w+)\) TO \((\w+)\)");
            var match = regex.Match(cypher);
            if (match.Success)
            {
                string fromNodeId = match.Groups[1].Value;
                string toNodeId = match.Groups[2].Value;

                if (!graph.CheckEdgeExists(fromNodeId, toNodeId))
                {
                    return $"No edge exists from node {fromNodeId} to node {toNodeId}.";
                }

                DeleteEdge(fromNodeId, toNodeId);
                return $"Edge from node {fromNodeId} to node {toNodeId} has been deleted successfully.";
            }
            else
            {
                return "Invalid syntax for DELETE EDGE command.";
            }
        }


        // Method to delete an edge based on 'from' and 'to' node IDs
        public void DeleteEdge(string fromNodeId, string toNodeId)
        {
            var graph = GetCurrentGraph(); // Work with the current graph instance
            var edgesToRemove = graph.Edges.Where(e => e.FromId == fromNodeId && e.ToId == toNodeId).ToList();
            foreach (var edge in edgesToRemove)
            {
                graph.Edges.Remove(edge);
                graph.RemoveEdgeFromIndex(edge);
            }
            SaveGraph();
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
                var graph = GetCurrentGraph(); // Work with the current graph instance
                return graph.CheckEdgeExists(fromNodeId, toNodeId);
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

        public bool CheckNodeExists(string nodeId)
        {
            return _currentGraph.CheckNodeExists(nodeId);
        }

        public bool CheckNodeProperty(string nodeId, string propertyName, string propertyValue)
        {

            return _currentGraph.CheckNodeProperty(nodeId, propertyName, propertyValue);
        }


        public bool CheckNumericNodeProperty(string nodeId, string propertyName, string comparison, double comparisonValue)
        {

            return _currentGraph.CheckNumericNodeProperty(nodeId,  propertyName,  comparison,  comparisonValue);
        }
        

        // Utility method to parse properties from a string
        private Dictionary<string, object> ParseProperties(string propertiesString)
        {
            var properties = new Dictionary<string, object>();
            // Assuming properties are in format: Key: 'Value', Key2: 'Value2'
            var matches = Regex.Matches(propertiesString, @"(\w+): '([^']*)'");
            foreach (Match match in matches)
            {
                properties.Add(match.Groups[1].Value, match.Groups[2].Value);
            }
            return properties;
        }

        

       

        // Method to save the current graph state to a file
        //public void SaveGraph()
        //{
        //    var graph = GetCurrentGraph(); // Ensure you have the current graph
        //    var graphData = new GraphData // Assuming GraphData is a suitable DTO
        //    {
        //        Nodes = graph.Nodes,
        //        Edges = graph.Edges
        //    };

        //    try
        //    {
        //        // Serialize GraphData to JSON
        //        var jsonData = JsonConvert.SerializeObject(graphData, Formatting.Indented);
        //        File.WriteAllText(DefaultFilePath, jsonData);
        //        Console.WriteLine("Graph saved successfully.");
        //    }
        //    catch (Exception ex)
        //    {
        //        // Handle potential errors, e.g., file access issues
        //        Console.Error.WriteLine($"Error saving the graph: {ex.Message}");
        //    }
        //}

        //public void LoadDatabase(string databaseName)
        //{
        //    if (string.IsNullOrEmpty(databaseName))
        //    {
        //        throw new ArgumentException("Database name must be provided.", nameof(databaseName));
        //    }

        //    // Attempt to load an existing graph or create a new one if it doesn't exist
        //    _databaseName = databaseName;
        //    _currentGraph = new Graph(databaseName); // Graph constructor handles loading
        //    if (!_currentGraph.IsDatabaseLoaded)
        //    {
        //        // Handle the logic if the graph couldn't be loaded - possibly initialize a new graph or log this event
        //        Console.WriteLine($"No existing database found for '{databaseName}'. A new one will be initialized.");
        //    }
        //}


        public void AddNodeToCurrentGraph(Node node)
        {
            var graph = GetCurrentGraph(); // Retrieve the current graph instance

            // Directly call the AddNode method of the Graph class
            graph.AddNode(node);

            SaveGraph(); // save the graph after modification
        }


        public void LoadDatabase(string databaseName)
        {
            if (string.IsNullOrEmpty(databaseName))
            {
                throw new ArgumentException("Database name must be provided.", nameof(databaseName));
            }

            // Attempt to load an existing graph or create a new one if it doesn't exist
            _databaseName = databaseName;
            _currentGraph = new Graph(databaseName); // Graph constructor handles loading

            string _graphPath = ConstructGraphPath(databaseName);
            if (File.Exists(_graphPath))
            {
                try
                {
                    var json = File.ReadAllText(_graphPath);
                    var graphData = JsonConvert.DeserializeObject<GraphData>(json);
                    if (graphData == null) throw new JsonException("Deserialised graph data is null.");

                    _currentGraph.loadNodes(graphData.Nodes ?? new List<Node>());
                    _currentGraph.loadEdges(graphData.Edges ?? new List<Edge>());
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Failed to parse the graph data: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred while loading the graph: {ex.Message}"); 
                }
            }

            IsDatabaseLoaded = _currentGraph.IsDatabaseLoaded;

            if (!_currentGraph.IsDatabaseLoaded)
            {
                // Handle the logic if the graph couldn't be loaded - possibly initialize a new graph or log this event
                Console.WriteLine($"No existing database found for '{databaseName}'. A new one will be initialized.");
            }
        }

       

        public string CreateDatabase(string databaseName)
        {
            if (string.IsNullOrWhiteSpace(databaseName))
            {
                return "Database name must be provided.";
            }

            string path = Path.Combine(DefaultFilePath, $"{databaseName}.json");
            if (File.Exists(path))
            {
                return $"Database '{databaseName}' already exists.";
            }

            try
            {
                _databaseName = databaseName;
                _currentGraph = new Graph(databaseName);
                IsDatabaseLoaded=_currentGraph.IsDatabaseLoaded;

                if (_currentGraph.IsDatabaseNew)
                {
                    SaveGraph(); // Assuming a SaveGraph method that handles serialization
                    return $"Database '{databaseName}' created successfully at '{path}'.";
                }
                else
                {
                    return $"Database '{databaseName}' already exists, data loaded from '{path}'.";
                }
            }
            catch (Exception ex)
            {
                LogException(ex);
                return $"Failed to create the database '{databaseName}': {ex.Message}";
            }
        }

        public void SaveGraph()
        {
            if (_currentGraph == null || string.IsNullOrWhiteSpace(_databaseName))
            {
                Console.WriteLine("No graph loaded or database name is missing.");
                return;
            }

            string path = GetDatabasePath();
            if (string.IsNullOrEmpty(path))
            {
                Console.WriteLine("Failed to get the database path.");
                return;
            }

            try
            {
                var json = JsonConvert.SerializeObject(_currentGraph, Formatting.Indented);
                File.WriteAllText(path, json);
                Console.WriteLine("Graph saved successfully.");
            }
            catch (Exception ex)
            {
                LogException(ex);
            }
        }

        // Helper method to construct the file path for a graph database
        private string ConstructGraphPath(string graphName)
        {
            return Path.Combine(DefaultFilePath, $"{graphName}.json");
        }


        private string ExtractDatabaseName(string cypher)
        {
            var match = Regex.Match(cypher, @"CREATE DATABASE\s+'([^']+)'", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            throw new ArgumentException("Invalid CREATE DATABASE syntax.");
        }
        // Exception logging method
        private void LogException(Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}\n{ex.StackTrace}");
        }





    }



}

