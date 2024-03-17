using System;

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection.Emit;
    using System.Text.RegularExpressions;
    using Newtonsoft.Json;
    using System.IO;

    using System.Xml;
    using CsvHelper;
    using CsvHelper.Configuration;
    using CsvHelper.TypeConversion;
using System.Globalization;

namespace GraphDB
    {


        public class Graph
        {
            private const string DefaultFilePath = "data"; // Relative path to store graph data
            private  static  string _graphPath;
        private static bool IsDatabaseLoaded=false;

            public List<Node> Nodes { get; set; } = new List<Node>();
            public List<Edge> Edges { get; set; } = new List<Edge>();

            public Graph(string graphName)
            {
                _graphPath = Path.Combine(DefaultFilePath, $"{graphName}.json");
                LoadGraph();
            }

            public void AddNode(Node node)
            {
                if (!Nodes.Exists(n => n.Id == node.Id))
                {
                    Nodes.Add(node);
                    SaveToFile();
                }
            }

            public void AddEdge(Edge edge)
            {
                if (!Edges.Exists(e => e.FromId == edge.FromId && e.ToId == edge.ToId))
                {
                    // Ensure referenced nodes exist
                    edge.From = Nodes.FirstOrDefault(n => n.Id == edge.FromId);
                    edge.To = Nodes.FirstOrDefault(n => n.Id == edge.ToId);
                    if (edge.From != null && edge.To != null)
                    {
                        Edges.Add(edge);
                        SaveToFile();
                    }
                }
            }

            public void DeleteNode(string nodeId)
            {
                Nodes.RemoveAll(n => n.Id == nodeId);
                Edges.RemoveAll(e => e.FromId == nodeId || e.ToId == nodeId);
                SaveToFile();
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

            // Find all neighbors of a specific node
            public List<Node> FindNeighbors(string nodeId)
            {
                var neighbors = new List<Node>();

                // Add nodes that are at the end of an outgoing edge from the given node
                var outgoingNeighbors = Edges.Where(e => e.From.Id == nodeId).Select(e => e.To).ToList();
                neighbors.AddRange(outgoingNeighbors);

                // Add nodes that are at the start of an incoming edge to the given node
                var incomingNeighbors = Edges.Where(e => e.To.Id == nodeId).Select(e => e.From).ToList();
                neighbors.AddRange(incomingNeighbors);

                return neighbors.Distinct().ToList(); // Remove duplicates and return
            }





            // Creating Nodes: CREATE(n:Label { id: '1', property: 'value'})
            // Creating Relationships: CREATE(n)-[:RELATES_TO {property: 'value'}]->(m)
            // Querying Nodes: MATCH(n:Label) WHERE n.property = 'value' RETURN n
            public string ExecuteCypherCommand(string cypher)
            {
                try
                {
                if ((cypher.Equals("help", StringComparison.OrdinalIgnoreCase) ||
                    cypher.Equals("h", StringComparison.OrdinalIgnoreCase) ||
                    cypher.Equals("?", StringComparison.OrdinalIgnoreCase)) && !IsDatabaseLoaded)
                    {
                        return CommandUtility.GetHelpResponse();
                    }
                else if (!IsDatabaseLoaded)
                    {
                        return "No database is loaded. Please load or create a database first. Use 'help' for more information.";
                    } else if (cypher.StartsWith("CREATE", StringComparison.OrdinalIgnoreCase))
                    {
                        return HandleCreateCypher(cypher);
                    }
                    else if (cypher.StartsWith("MATCH", StringComparison.OrdinalIgnoreCase))
                    {
                        return HandleMatchCypher(cypher);
                    }
                    else if (cypher.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase))
                    {
                        return HandleDeleteCypher(cypher);
                    }
                    else if (cypher.StartsWith("SET", StringComparison.OrdinalIgnoreCase))
                    {
                        return HandleSetCypher(cypher);
                    }
                    else if (cypher.Contains("COUNT", StringComparison.OrdinalIgnoreCase) || cypher.Contains("SUM", StringComparison.OrdinalIgnoreCase) || cypher.Contains("AVG", StringComparison.OrdinalIgnoreCase)) // Expand as necessary for SUM, AVG, etc.
                    {
                        return HandleAggregation(cypher);
                    }
                    else if (cypher.StartsWith("DETACH DELETE", StringComparison.OrdinalIgnoreCase))
                    {
                        return HandleDetachDelete(cypher);
                    }
                else if (cypher.StartsWith("FIND RELATIONSHIPS", StringComparison.OrdinalIgnoreCase))
                    {

                    // Return relationships found by FindRelationships
                    return HandleFindRelationshipsCypher(cypher);
                }
                    else if (cypher.StartsWith("MATCH PATTERN", StringComparison.OrdinalIgnoreCase))
                    {

                        // Return nodes and edges that match the pattern using MatchPattern
                        return HandleMatchPatternCypher(cypher);
                    }
                else if (cypher.StartsWith("IMPORT CSV", StringComparison.OrdinalIgnoreCase))
                {
                    return HandleImportCsvCypher(cypher);
                }
                if (cypher.StartsWith("EXPORT CSV", StringComparison.OrdinalIgnoreCase))
                {
                    return HandleExportCsvCypher(cypher);
                }
                else if ( cypher.Contains("IF CONDITION", StringComparison.OrdinalIgnoreCase))
                {
                    return HandleConditional(cypher);
                }
                else if (cypher.StartsWith("CASE", StringComparison.OrdinalIgnoreCase))
                {
                    return HandleCaseCommand(cypher);
                }
               else if (cypher.Equals("help", StringComparison.OrdinalIgnoreCase) ||
                        cypher.Equals("h", StringComparison.OrdinalIgnoreCase) ||
                        cypher.Equals("?", StringComparison.OrdinalIgnoreCase))
                {
                    return DisplayHelp();
                }
                
                else
                    {
                        return "Unsupported Cypher command.";
                    }
                }
                catch (Exception ex)
                {
                    return $"Error executing Cypher command: {ex.Message}";
                }
            }

        private string ExtractFilePath(string graphName)
        {
            return Path.Combine(DefaultFilePath, $"{graphName}.json");
            
        }
        public static  string GetDatabasePath()
        {
            return _graphPath;

        }


        private string HandleCreateCypher(string cypher)
            {
                var nodePattern = new Regex(@"(CREATE|MERGE) \((\w+):(\w+) \{(.+)\}\)", RegexOptions.IgnoreCase);
                var nodeMatch = nodePattern.Match(cypher);
                if (nodeMatch.Success)
                {
                    string operation = nodeMatch.Groups[1].Value.ToUpper();
                    string nodeName = nodeMatch.Groups[2].Value;
                    string label = nodeMatch.Groups[3].Value;
                    string propertiesString = nodeMatch.Groups[4].Value;

                    var properties = ParseProperties(propertiesString);
                    var objectProperties = properties.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value);

                    if (operation == "MERGE")
                    {
                        // Find an existing node with the same ID or create a new one
                        var node = Nodes.FirstOrDefault(n => n.Id == properties["id"]);
                        if (node == null)
                        {
                            node = new Node { Id = properties["id"], Properties = objectProperties };
                            AddNode(node);
                            return $"Merged (created) new node with id {node.Id}.";
                        }
                        else
                        {
                            // Update the existing node's properties
                            foreach (var prop in objectProperties)
                            {
                                node.Properties[prop.Key] = prop.Value;
                            }
                            return $"Merged (updated) node {node.Id} with new properties.";
                        }
                    }
                    else if (operation == "CREATE")
                    {
                        // Check if a node with the same ID already exists to prevent duplicates
                        if (Nodes.Any(n => n.Id == properties["id"]))
                        {
                            return $"Node with id {properties["id"]} already exists. Cannot create duplicate.";
                        }
                        var node = new Node { Id = properties["id"], Properties = objectProperties };
                        AddNode(node);
                        return $"Created new node with id {node.Id}.";
                    }
                }

                // Continue with the relationship handling as before, adjusted for CREATE and MERGE distinction if needed

                return "Cypher command not recognized or supported.";
            }

        private string DisplayHelp()
        {
            return @"
GraphDB Command Syntax:

CREATE (node) - Creates a node with specified properties.
  Example: CREATE (n:Label { id: '1', property: 'value'})

MATCH (node) - Finds nodes that match specified criteria.
  Example: MATCH (n:Label) WHERE n.property = 'value' RETURN n

DELETE (node) - Deletes a node by its ID.
  Example: DELETE (n {id: '1'})

SET - Updates properties of a node or edge.
  Example: SET NODE (n {id: '1'}) SET property = 'newValue'
           SET EDGE (sourceId-targetId) SET property = 'newValue'

DETACH DELETE (node) - Deletes a node and its relationships.
  Example: DETACH DELETE (n {id: '1'})

FIND RELATIONSHIPS - Finds relationships between nodes.
  Example: FIND RELATIONSHIPS FROM (sourceId) TO (targetId) TYPE (type)

MATCH PATTERN - Matches a pattern within the graph.
  Example: MATCH PATTERN (source)-[relationship]->(target)

IMPORT CSV - Imports nodes or edges from a CSV file.
  Example: IMPORT CSV filePath='path/to/file.csv', type='node|edge'

EXPORT CSV - Exports nodes or edges to a CSV file.
  Example: EXPORT CSV NODES filePath='path/to/nodes.csv'
           EXPORT CSV EDGES filePath='path/to/edges.csv'

IF CONDITION - Executes a command based on a condition.
  Example: IF CONDITION [condition] THEN [action] ELSE [alternativeAction]

CASE - Executes commands based on multiple conditions.
  Example: CASE WHEN [condition] THEN [action] ELSE [alternativeAction] END

Help commands: help, h, ?
";
        }


        private string HandleMatchCypher(string cypher)
            {
                // Simplified MATCH handling, assuming fixed format for demonstration
                var match = Regex.Match(cypher, @"MATCH \(n:(\w+)\) WHERE n\.(\w+) = '(.+)' RETURN n");
                if (!match.Success) return "Invalid MATCH syntax.";

                string label = match.Groups[1].Value;
                string propertyName = match.Groups[2].Value;
                string propertyValue = match.Groups[3].Value;

                var nodes = Nodes.Where(n => n.Properties.ContainsKey(propertyName) && n.Properties[propertyName].ToString() == propertyValue && n.Properties.ContainsValue(label)).ToList();
                return $"Found {nodes.Count} nodes matching criteria.";
            }
            private string HandleDeleteCypher(string cypher)
            {
                // Simplified DELETE handling for nodes by ID. In real Cypher, DELETE has broader use.
                var match = Regex.Match(cypher, @"DELETE \(n {id: '(\w+)'}\)");
                if (!match.Success) return "Invalid DELETE syntax.";

                string nodeId = match.Groups[1].Value;
                DeleteNode(nodeId);
                return $"Deleted node {nodeId}.";
            }
        private string HandleDetachDelete(string cypher)
        {
            var match = Regex.Match(cypher, @"DETACH DELETE (\w+)");
            if (match.Success)
            {
                string nodeId = match.Groups[1].Value;

                // Remove the node
                Nodes.RemoveAll(n => n.Id == nodeId);

                // Remove all edges associated with the node
                Edges.RemoveAll(e => e.FromId == nodeId || e.ToId == nodeId);

                return $"Deleted node {nodeId} and all associated relationships.";
            }
            return "Invalid DETACH DELETE syntax.";
        }


        private string HandleSetCypher(string cypher)
        {
            var nodeSetMatch = Regex.Match(cypher, @"SET NODE (\w+) SET (\w+) = '(.+)'");
            var edgeSetMatch = Regex.Match(cypher, @"SET EDGE (\w+)-(\w+) SET (\w+) = '(.+)'");

            if (nodeSetMatch.Success)
            {
                string nodeId = nodeSetMatch.Groups[1].Value;
                string propertyName = nodeSetMatch.Groups[2].Value;
                string propertyValue = nodeSetMatch.Groups[3].Value;

                var node = Nodes.FirstOrDefault(n => n.Id == nodeId);
                if (node == null) return $"Node {nodeId} not found.";

                node.Properties[propertyName] = propertyValue;
                return $"Updated node {nodeId} set {propertyName} = {propertyValue}.";
            }
            else if (edgeSetMatch.Success)
            {
                string fromNodeId = edgeSetMatch.Groups[1].Value;
                string toNodeId = edgeSetMatch.Groups[2].Value;
                string propertyName = edgeSetMatch.Groups[3].Value;
                string propertyValue = edgeSetMatch.Groups[4].Value;

                var edge = Edges.FirstOrDefault(e => e.FromId == fromNodeId && e.ToId == toNodeId);
                if (edge == null) return $"Edge from {fromNodeId} to {toNodeId} not found.";

                edge.Properties[propertyName] = propertyValue;
                return $"Updated edge from {fromNodeId} to {toNodeId} set {propertyName} = {propertyValue}.";
            }
            else
            {
                return "Invalid SET syntax.";
            }
        }

        private string HandleAggregation(string cypher)
        {
            var countMatch = Regex.Match(cypher, @"COUNT (\w+)");
            if (countMatch.Success)
            {
                string type = countMatch.Groups[1].Value.ToUpper();
                switch (type)
                {
                    case "NODES":
                        return $"Total nodes: {Nodes.Count()}";
                    case "EDGES":
                        return $"Total edges: {Edges.Count()}";
                    default:
                        return "Unsupported aggregation type.";
                }
            }
            return "Invalid aggregation syntax.";
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


        // Simplified regex pattern for matching CASE statement components
        // This pattern is extremely simplified and assumes well-formed input 

        private string HandleCaseCommand(string cypher)
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

        private string HandleMatchPatternCypher(string cypher)
            {
                var match = Regex.Match(cypher, @"MATCH PATTERN \((\w+)\)-\[(\w+)\]->\((\w+)\)");
                if (!match.Success) return "Invalid MATCH PATTERN syntax.";

                string startNodeId = match.Groups[1].Value;
                string relationshipType = match.Groups[2].Value;
                string endNodeId = match.Groups[3].Value;

                var startCondition = new Func<Node, bool>(node => node.Id == startNodeId);
                var endCondition = new Func<Node, bool>(node => node.Id == endNodeId);

                var matches = MatchPattern(startCondition, relationshipType, endCondition);
                return $"Found {matches.Count} pattern matches for {startNodeId}-{relationshipType}->{endNodeId}.";

            }
        private string HandleFindRelationshipsCypher(string cypher)
        {
            var match = Regex.Match(cypher, @"FIND RELATIONSHIPS FROM (\w+) TO (\w+) TYPE (\w+)");
            if (!match.Success) return "Invalid FIND RELATIONSHIPS syntax.";

            string fromNodeId = match.Groups[1].Value;
            string toNodeId = match.Groups[2].Value;
            string relationshipType = match.Groups[3].Value;

            var relationships = FindRelationships(fromNodeId, toNodeId, relationshipType);
            return $"Found {relationships.Count} relationships of type '{relationshipType}' from {fromNodeId} to {toNodeId}.";

        }

        public List<Edge> FindRelationships(string fromNodeId, string toNodeId, string relationshipType = null)
        {
            return Edges.Where(e =>
                e.FromId == fromNodeId &&
                e.ToId == toNodeId &&
                (relationshipType == null || e.Relationship.Equals(relationshipType, StringComparison.OrdinalIgnoreCase))
            ).ToList();
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

        private Dictionary<string, string> ParseProperties(string data)
            {
                var properties = new Dictionary<string, string>();
                var matches = Regex.Matches(data, @"(\w+)=([^\s,]+)");
                foreach (Match match in matches)
                {
                    properties[match.Groups[1].Value] = match.Groups[2].Value;
                }
                return properties;
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


        private string HandleImportCsvCypher(string cypher)
        {
            var match = Regex.Match(cypher, @"IMPORT CSV filePath='(?<filePath>.+?)', type='(?<type>node|edge)'", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return "Invalid IMPORT CSV syntax.";
            }

            var filePath = match.Groups["filePath"].Value;
            var type = match.Groups["type"].Value.Equals("node", StringComparison.OrdinalIgnoreCase);

            ImportCsv(filePath, type);

            return type ? "Nodes imported from CSV successfully." : "Edges imported from CSV successfully.";
        }
        public void ImportCsv(string filePath, bool isNodeCsv = true)
        {
            try
            {
                using (var reader = new StreamReader(filePath))
                using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    PrepareHeaderForMatch = args => args.Header.ToLower(),
                }))
                {
                    if (isNodeCsv)
                    {
                        var records = csv.GetRecords<dynamic>().ToList();
                        foreach (IDictionary<string, object> record in records)
                        {
                            var node = new Node
                            {
                                Id = record["id"].ToString(),
                                Properties = record.ToDictionary(k => k.Key, k => k.Value) // Explicitly converting to Dictionary<string, object>
                            };
                            AddNode(node);
                        }
                    }
                    else // Assuming it's an edge CSV
                    {
                        var records = csv.GetRecords<dynamic>().ToList();
                        foreach (IDictionary<string, object> record in records)
                        {
                            var edge = new Edge
                            {
                                FromId = record["sourceid"].ToString(),
                                ToId = record["targetid"].ToString(),
                                Relationship = record["relationshiptype"].ToString(),
                                Properties = record.ToDictionary(k => k.Key, k => k.Value) // Explicitly converting to Dictionary<string, object>
                            };

                            // Ensure From and To nodes exist in the graph
                            edge.From = Nodes.FirstOrDefault(n => n.Id == edge.FromId);
                            edge.To = Nodes.FirstOrDefault(n => n.Id == edge.ToId);
                            if (edge.From != null && edge.To != null)
                            {
                                AddEdge(edge);
                            }
                        }
                    }
                }

                Console.WriteLine("CSV data imported successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while importing CSV data: {ex.Message}");
            }
        }


            // EXPORT CSV NODES filePath='path/to/export/nodes.csv'
            // EXPORT CSV EDGES filePath = 'path/to/export/edges.csv'
             public string HandleExportCsvCypher(string cypher)
            {
                var matchNodes = Regex.Match(cypher, @"EXPORT CSV NODES filePath='(?<filePath>.+?)'", RegexOptions.IgnoreCase);
                var matchEdges = Regex.Match(cypher, @"EXPORT CSV EDGES filePath='(?<filePath>.+?)'", RegexOptions.IgnoreCase);

                if (matchNodes.Success)
                {
                    var filePath = matchNodes.Groups["filePath"].Value;
                    return ExportNodesToCsv(filePath) ? $"Nodes exported to CSV successfully at {filePath}." : "Failed to export nodes to CSV.";
                }
                else if (matchEdges.Success)
                {
                    var filePath = matchEdges.Groups["filePath"].Value;
                    return ExportEdgesToCsv(filePath) ? $"Edges exported to CSV successfully at {filePath}." : "Failed to export edges to CSV.";
                }

                return "Invalid EXPORT CSV command.";
            }
            public bool ExportNodesToCsv(string filePath)
            {
                try
                {
                    using (var writer = new StreamWriter(filePath))
                    using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                    {
                        csv.WriteRecords(Nodes);
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error exporting nodes to CSV: {ex.Message}");
                    return false;
                }
            }

            public bool ExportEdgesToCsv(string filePath)
            {
                try
                {
                    using (var writer = new StreamWriter(filePath))
                    using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                    {
                        csv.WriteRecords(Edges.Select(e => new
                        {
                            e.FromId,
                            e.ToId,
                            e.Relationship,
                            e.Weight,
                            Properties = string.Join(", ", e.Properties.Select(p => $"{p.Key}: {p.Value}"))
                        }));
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error exporting edges to CSV: {ex.Message}");
                    return false;
                }
            }


            public void SaveToFile()
            {
                try
                {
                    var graphData = new GraphData
                    {
                        Nodes = this.Nodes,
                        Edges = this.Edges.Select(e => new Edge
                        {
                            FromId = e.FromId,
                            ToId = e.ToId,
                            Relationship = e.Relationship,
                            Weight = e.Weight,
                            Properties = e.Properties
                        }).ToList()
                    };

                    var json = JsonConvert.SerializeObject(graphData, Newtonsoft.Json.Formatting.Indented);
                    var directory = Path.GetDirectoryName(_graphPath);
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory ?? string.Empty);
                    }

                    File.WriteAllText(_graphPath, json);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Failed to save the graph to {_graphPath}: {ex.Message}");
                }
            }

            public void LoadGraph()
            {
                IsDatabaseLoaded = false;
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
                IsDatabaseLoaded = true;
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




