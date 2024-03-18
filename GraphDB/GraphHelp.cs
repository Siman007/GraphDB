using System.Collections.Generic;

namespace GraphDB
{
    public static class GraphHelp
    {
        private static readonly Dictionary<string, string> commandHelp = new Dictionary<string, string>()
        {
            ["CREATE NODE"] = "Creates a node with specified properties.\nExample: CREATE NODE {id: '1', label: 'Person', properties: {name: 'Alice', age: 30}}",
            ["MERGE NODE"] = "Merges a node by id, creating it if it does not exist, or updating it if it does.\nExample: MERGE NODE {id: '1', properties: {name: 'Alice', age: 31}}",
            ["DELETE NODE"] = "Deletes a node by its ID or label.\nExample: DELETE NODE {id: '1'}\nDELETE NODE {label: 'Person'}",
            ["CREATE EDGE"] = "Creates an edge between two nodes with specified properties.\nExample: CREATE EDGE {fromId: '1', toId: '2', relationship: 'KNOWS', properties: {since: 2020}}",
            ["DELETE EDGE"] = "Deletes an edge by specifying its start and end node IDs.\nExample: DELETE EDGE {fromId: '1', toId: '2'}",
            ["SET NODE PROPERTY"] = "Updates properties of a node.\nExample: SET NODE PROPERTY {id: '1', properties: {name: 'Alice', age: 32}}",
            ["SET EDGE PROPERTY"] = "Updates properties of an edge.\nExample: SET EDGE PROPERTY {fromId: '1', toId: '2', properties: {since: 2021}}",
            ["MATCH EDGE"] = "Finds edges that match specified criteria.\nExample: MATCH EDGE {fromId: '1', toId: '2'}",
            ["DETACH DELETE"] = "Deletes a node by ID or label and all its relationships.\nExample: DETACH DELETE {id: '1'}\nDETACH DELETE {label: 'Person'}",
            ["FIND RELATIONSHIPS"] = "Finds relationships between nodes based on criteria.\nExample: FIND RELATIONSHIPS {fromId: '1', toId: '2', type: 'KNOWS'}",
            ["EXPORT CSV NODES"] = "Exports nodes to a CSV file.\nExample: EXPORT CSV NODES {filePath: 'path/to/nodes.csv'}",
            ["EXPORT CSV EDGES"] = "Exports edges to a CSV file.\nExample: EXPORT CSV EDGES {filePath: 'path/to/edges.csv'}",
            ["IF CONDITION"] = "Executes a command based on a condition.\nExample: IF CONDITION {condition: 'node exists', then: 'CREATE NODE (...)', else: 'DELETE NODE (...)'}",
            ["CASE"] = "Executes commands based on multiple conditions.\nExample: CASE WHEN {condition: 'node exists', then: 'SET NODE PROPERTY (...)', else: 'CREATE NODE (...)'}",
            ["MATCH PATTERN"] = "Matches a pattern within the graph.\nExample: MATCH PATTERN {startNodeId: '1', relationshipType: 'KNOWS', endNodeId: '2'}",
            ["AGGREGATE AVG"] = "Calculates the average of a specified property across all nodes or edges.\nExample: AGGREGATE AVG {property: 'age'}",
            ["AGGREGATE SUM"] = "Calculates the sum of a specified property across all nodes or edges.\nExample: AGGREGATE SUM {property: 'score'}",
            ["COUNT EDGES"] = "Counts all edges in the graph.\nExample: COUNT EDGES",
            ["COUNT NODES"] = "Counts all nodes in the graph.\nExample: COUNT NODES",
            ["IMPORT JSON"] = "Imports nodes or edges from a JSON file.\nExample JSON format for nodes: [{\"id\": \"1\", \"label\": \"Person\", \"properties\": {\"name\": \"Alice\"}}]\nExample: IMPORT JSON {filePath: 'path/to/nodes.json', type: 'node'}",
            ["IMPORT CSV"] = "Imports nodes or edges from a CSV file.\nExample CSV format for nodes: id,label,name\n                                 1,Person,Alice\nExample: IMPORT CSV {filePath: 'path/to/nodes.csv', type: 'node'}",
            ["SET RELATIONSHIP PROPERTY"] = "Updates properties of an edge.\nExample: SET RELATIONSHIP PROPERTY {fromId: '1', toId: '2', properties: {since: 2021}}",
            ["SET NODE PROPERTY"] = "Updates properties of a node.\nExample: SET NODE PROPERTY {id: '1', properties: {name: 'Bob', age: 32}}",
            ["DELETE RELATIONSHIP"] = "Deletes a relationship by specifying its start and end node IDs and type.\nExample: DELETE RELATIONSHIP {fromId: '1', toId: '2', type: 'KNOWS'}",
            ["DETACH DELETE NODE"] = "Deletes a node by ID or label and all its relationships.\nExample: DETACH DELETE NODE {id: '1'}",
            ["MATCH RELATIONSHIP"] = "Finds relationships that match specified criteria.\nExample: MATCH RELATIONSHIP {type: 'KNOWS'}",
            ["MATCH NODE"] = "Finds nodes that match specified criteria.\nExample: MATCH NODE {label: 'Person'}  or MATCH NODE {id: '1'}",
            ["CREATE RELATIONSHIP"] = "Creates a relationship between two nodes.\nExample: CREATE RELATIONSHIP {fromId: '1', toId: '2', type: 'KNOWS', properties: {since: 2010}}"

        };

        public static string GetHelp(string command = "")
        {
            if (string.IsNullOrWhiteSpace(command) || command.ToUpper() == "HELP")
            {
                return DisplayGeneralHelp();
            }
            else if (commandHelp.ContainsKey(command.ToUpper()))
            {
                return commandHelp[command.ToUpper()];
            }
            else
            {
                return $"No help available for '{command}'.";
            }
        }

        private static string DisplayGeneralHelp()
        {
            return @"
GraphDB Command Syntax:

Node Operations:
- CREATE NODE: Creates a node with specified properties. Use HELP CREATE NODE for more.
- MERGE NODE: Merges a node by id, creating it if it does not exist, or updating it if it does.
- DELETE NODE: Deletes a node by its ID or label.
- SET NODE PROPERTY: Updates properties of a node.

Edge Operations:
- CREATE EDGE: Creates an edge between two nodes with specified properties.
- DELETE EDGE: Deletes an edge by specifying its start and end node IDs.
- SET EDGE PROPERTY: Updates properties of an edge.

Advanced Queries:
- MATCH: Finds nodes or edges that match specified criteria.
- DETACH DELETE: Deletes a node by ID or label and all its relationships.
- FIND RELATIONSHIPS: Finds relationships between nodes based on criteria.

Data Import/Export:
- IMPORT CSV: Imports nodes or edges from a CSV file.
- EXPORT CSV: Exports nodes or edges to a CSV file.

Conditional Logic:
- IF CONDITION: Executes a command based on a condition.
- CASE: Executes commands based on multiple conditions.

For more detailed information on each command, use: HELP [command]";
        }
    }
}
