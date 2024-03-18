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


        public string ExecuteCypherCommand(string cypher)
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
                    return "Unsupported Cypher command.";
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

        

        private string HandleDisplayHelp(string cypher)
        {
            // Splitting the input to separate "HELP" from the actual command it's asking help for
            var parts = cypher.Trim().Split(new char[] { ' ' }, 2);
            var command = parts[0].ToUpper();

            // If the command starts with HELP, provide detailed help for the specified command
            if (command == "HELP")
            {
                var specificCommand = parts.Length > 1 ? parts[1] : "";
                return GraphHelp.GetHelp(specificCommand);
            }
            return GraphHelp.GetHelp();
        }

        private string HandleCreateCypher(string cypher)
        {
            // First, try to match node creation or merge syntax
            var nodePattern = new Regex(@"(CREATE|MERGE) \((\w+):(\w+) \{(.+)\}\)", RegexOptions.IgnoreCase);
            var nodeMatch = nodePattern.Match(cypher);
            if (nodeMatch.Success)
            {
                return HandleNodeCreationOrMerge(nodeMatch);
            }

            // Next, try to match relationship creation syntax
            var relationshipPattern = new Regex(@"CREATE \((\w+)\)-\[:(\w+)\]->\((\w+)\) \{(.*)\}", RegexOptions.IgnoreCase);
            var relationshipMatch = relationshipPattern.Match(cypher);
            if (relationshipMatch.Success)
            {
                return HandleRelationshipCreation(relationshipMatch);
            }

            return "Cypher command not recognized or supported.";
        }

        private string HandleNodeCreationOrMerge(Match nodeMatch)
        {
            string operation = nodeMatch.Groups[1].Value.ToUpper();
            string nodeId = nodeMatch.Groups[2].Value; // nodeName is actually nodeId in this context
            string label = nodeMatch.Groups[3].Value; // Example uses label but might be ignored depending on your implementation
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
                    return $"Merged (created) new node with id {nodeId}.";
                }
                else
                {
                    foreach (var prop in objectProperties)
                    {
                        node.Properties[prop.Key] = prop.Value;
                    }
                    SaveToFile();
                    return $"Merged (updated) node {nodeId} with new properties.";
                }
            }
            else // CREATE
            {
                if (node != null)
                {
                    return $"Node with id {nodeId} already exists. Cannot create duplicate.";
                }
                node = new Node { Id = nodeId, Properties = objectProperties };
                Nodes.Add(node);
                SaveToFile();
                return $"Created new node with id {nodeId}.";
            }
        }

        private string HandleRelationshipCreation(Match relationshipMatch)
        {
            string fromNodeId = relationshipMatch.Groups[1].Value;
            string relationshipType = relationshipMatch.Groups[2].Value;
            string toNodeId = relationshipMatch.Groups[3].Value;
            var properties = ParseProperties(relationshipMatch.Groups[4].Value);
            var objectProperties = properties.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value);

            var fromNode = Nodes.FirstOrDefault(n => n.Id == fromNodeId);
            var toNode = Nodes.FirstOrDefault(n => n.Id == toNodeId);

            if (fromNode == null || toNode == null)
            {
                return "One or both specified nodes do not exist.";
            }

            var edge = new Edge
            {
                FromId = fromNodeId,
                ToId = toNodeId,
                RelationshipType = relationshipType,
                Properties = objectProperties
            };
            Edges.Add(edge);
            SaveToFile();
            return $"Created relationship of type {relationshipType} from {fromNodeId} to {toNodeId}.";
        }

        private string HandleCreateRelationship(string cypher)
        {
            var pattern = new Regex(@"CREATE \((\w+)\)-\[:(\w+)\]->\((\w+)\) \{(.*)\}", RegexOptions.IgnoreCase);
            var match = pattern.Match(cypher);
            if (!match.Success) return "Invalid CREATE syntax for relationship.";

            string fromNodeId = match.Groups[1].Value;
            string relationshipType = match.Groups[2].Value;
            string toNodeId = match.Groups[3].Value;
            var propertiesString = match.Groups[4].Value;

            // Check if both nodes exist
            var fromNode = Nodes.FirstOrDefault(n => n.Id == fromNodeId);
            var toNode = Nodes.FirstOrDefault(n => n.Id == toNodeId);
            if (fromNode == null || toNode == null)
            {
                return "One or both specified nodes do not exist.";
            }

            // Parse properties, if any
            var properties = new Dictionary<string, object>();
            if (!string.IsNullOrWhiteSpace(propertiesString))
            {
                properties = ParseProperties(propertiesString);
            }

            // Check if a similar relationship already exists to avoid duplicates, based on your application's needs
            var existingEdge = Edges.FirstOrDefault(e => e.FromId == fromNodeId && e.ToId == toNodeId && e.RelationshipType == relationshipType);
            if (existingEdge != null)
            {
                return $"A relationship of type {relationshipType} from {fromNodeId} to {toNodeId} already exists.";
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
            SaveToFile(); // Persist changes if applicable

            return $"Created relationship of type {relationshipType} from {fromNodeId} to {toNodeId}.";
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


        private string HandleDeleteNode(string cypher)
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
                    return $"Node with ID {nodeId} deleted successfully.";
                }
                return $"No node found with ID {nodeId}.";
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
                    return $"Nodes with label {nodeLabel} deleted successfully.";
                }
                return $"No nodes found with label {nodeLabel}.";
            }

            return "Invalid DELETE syntax.";
        }


        public string HandleDetachDelete(string cypher)
        {
            var pattern = new Regex(@"DETACH DELETE (\w+)(?:\s*:\s*(\w+))?", RegexOptions.IgnoreCase);
            var match = pattern.Match(cypher);
            if (!match.Success) return "Invalid DETACH DELETE syntax.";

            string nodeIdOrLabel = match.Groups[1].Value;
            string label = match.Groups[2].Success ? match.Groups[2].Value : null;

            Predicate<Node> deletionCriteria; // Change to Predicate<Node>
            if (string.IsNullOrEmpty(label))
            {
                deletionCriteria = n => n.Id == nodeIdOrLabel;
            }
            else
            {
                deletionCriteria = n => n.Properties.TryGetValue("label", out var nodeLabel) && nodeLabel.ToString() == label;
            }

            // Use the predicate directly without conversion
            Edges.RemoveAll(e => deletionCriteria(Nodes.FirstOrDefault(n => n.Id == e.FromId)) || deletionCriteria(Nodes.FirstOrDefault(n => n.Id == e.ToId)));

            var removed = Nodes.RemoveAll(deletionCriteria) > 0; // Use directly

            if (removed)
            {
                SaveToFile(); // Save changes to the graph
                return label == null ?
                    $"Node {nodeIdOrLabel} and all its relationships have been deleted." :
                    $"Nodes with label {label} and all their relationships have been deleted.";
            }
            else
            {
                return label == null ?
                    $"Node {nodeIdOrLabel} does not exist." :
                    $"No nodes with label {label} exist.";
            }
        }



        private string HandleDeleteRelationship(string cypher)
        {
            var pattern = new Regex(@"DELETE RELATIONSHIP FROM \((\w+)\) TO \((\w+)\) TYPE (\w+)", RegexOptions.IgnoreCase);
            var match = pattern.Match(cypher);
            if (!match.Success) return "Invalid DELETE RELATIONSHIP syntax.";

            string fromNodeId = match.Groups[1].Value;
            string toNodeId = match.Groups[2].Value;
            string relationshipType = match.Groups[3].Value;

            var removed = Edges.RemoveAll(e => e.FromId == fromNodeId && e.ToId == toNodeId && e.RelationshipType == relationshipType) > 0;

            if (removed)
            {
                SaveToFile(); // Assuming you have a method to save changes to the graph
                return $"Relationship {relationshipType} from {fromNodeId} to {toNodeId} has been deleted.";
            }
            else
            {
                return $"Relationship {relationshipType} from {fromNodeId} to {toNodeId} does not exist.";
            }
        }
        private string HandleMatchRelationship(string cypher)
        {
            // Extending the command format to include optional weight conditions:
            // MATCH (a)-[r:RELATIONSHIP_TYPE {weight: '>value'}]->(b) RETURN r
            var pattern = new Regex(@"MATCH \((\w+)\)-\[r:(\w+)\s*\{(?:weight: '([><=]?)(\d+)')?\}\]->\((\w+)\) WHERE r\.(\w+) = '([^']+)' RETURN r", RegexOptions.IgnoreCase);
            var match = pattern.Match(cypher);
            if (!match.Success) return "Invalid MATCH syntax for relationship.";

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

            if (!filteredEdges.Any())
            {
                return "No relationships found matching criteria.";
            }

            return FormatRelationships(filteredEdges);
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


      


        private string HandleSetRelationshipProperty(string cypher)
        {
            // Example Cypher: SET RELATIONSHIP (fromId)-(toId) {property: 'value'}
            var pattern = new Regex(@"SET RELATIONSHIP \((\w+)\)-\((\w+)\) \{(.+)\}", RegexOptions.IgnoreCase);
            var match = pattern.Match(cypher);
            if (!match.Success) return "Invalid syntax for SET RELATIONSHIP.";

            string fromId = match.Groups[1].Value;
            string toId = match.Groups[2].Value;
            var properties = ParseProperties(match.Groups[3].Value);

            var edge = Edges.FirstOrDefault(e => e.FromId == fromId && e.ToId == toId);
            if (edge == null) return "Relationship does not exist.";

            foreach (var prop in properties)
            {
                edge.Properties[prop.Key] = prop.Value;
            }

            SaveToFile();
            return $"Updated properties of relationship from {fromId} to {toId}.";
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


        public List<Node> HandleFindNeighbors(string cypher)
        {
            var pattern = new Regex(@"FIND NEIGHBORS \(id:\s*'([^']*)'(?:,\s*label:\s*'([^']*)')?\)", RegexOptions.IgnoreCase);
            var match = pattern.Match(cypher);

            if (!match.Success)
            {
                return new List<Node>(); // Return an empty list if the syntax is invalid
            }

            string nodeId = match.Groups[1].Value;
            string label = match.Groups[2].Success ? match.Groups[2].Value : null;

            return FindNeighbors(nodeId, label);
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


        private string HandleCreateNode(string cypher)
        {
            var pattern = new Regex(@"CREATE \((\w+):(\w+) \{(.+)\}\)", RegexOptions.IgnoreCase);
            var match = pattern.Match(cypher);
            if (!match.Success) return "Invalid CREATE syntax for node.";

            string nodeId = match.Groups[1].Value;
            string label = match.Groups[2].Value; // This example uses label, which you may or may not need.
            var properties = ParseProperties(match.Groups[3].Value);

            if (Nodes.Any(n => n.Id == nodeId))
            {
                return $"Node with id {nodeId} already exists.";
            }

            var node = new Node { Id = nodeId, Properties = properties };
            Nodes.Add(node);
            SaveToFile();
            return $"Node {nodeId} created successfully.";
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

        private string HandleMatchNode(string cypher)
        {
            var pattern = new Regex(@"MATCH \((\w+):(\w+) \{(.+): '(.+)'\}\)", RegexOptions.IgnoreCase);
            var match = pattern.Match(cypher);
            if (!match.Success) return "Invalid MATCH syntax for node.";

            string label = match.Groups[2].Value; // Example uses label, which you may or may not need.
            string propertyName = match.Groups[3].Value;
            string propertyValue = match.Groups[4].Value;

            var matchingNodes = Nodes.Where(n => n.Properties.ContainsKey(propertyName) && n.Properties[propertyName].ToString() == propertyValue).ToList();

            return $"Found {matchingNodes.Count} nodes matching {propertyName} = {propertyValue}.";
        }


       

        public List<(Node, Node)> MatchPattern(Func<Node, bool> startCondition, string relationshipType, Func<Node, bool> endCondition)
        {
            var matches = new List<(Node, Node)>();
            foreach (var edge in Edges.Where(e => e.Relationship.Equals(relationshipType, StringComparison.OrdinalIgnoreCase)))
            {
                var fromNode = Nodes.FirstOrDefault(n => n.Id == edge.FromId);
                var toNode = Nodes.FirstOrDefault(n => n.Id == edge.ToId);
                if (fromNode != null && toNode != null && startCondition(fromNode) && endCondition(toNode))
                {
                    matches.Add((fromNode, toNode));
                }
            }
            return matches;
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




