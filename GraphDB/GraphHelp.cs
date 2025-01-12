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
            ["CREATE RELATIONSHIP"] = "Creates a relationship between two nodes.\nExample: CREATE RELATIONSHIP {fromId: '1', toId: '2', type: 'KNOWS', properties: {since: 2010}}",
            ["MATCH"] = @"
MATCH Command Help:

1. Basic Node Matching:
   ---------------------
   Syntax:
   MATCH (variable:Label {property: 'value'})
   RETURN variable

   - Matches nodes with the specified label and property.
   - Omitting the label matches nodes of any label.
   - Multiple properties can be specified.

   Example:
   MATCH (n:Person {name: 'Alice', age: 30})
   RETURN n

2. Relationship Matching:
   -----------------------
   Syntax:
   MATCH (variable1:Label1)-[relationship:REL_TYPE]->(variable2:Label2)
   RETURN variable1, relationship, variable2

   - Matches relationships of the specified type between nodes.
   - Omitting the labels matches relationships between any nodes.
   - Use `-[]-` for undirected relationships.

   Example:
   MATCH (a:Person)-[:KNOWS]->(b:Person)
   RETURN a, b

3. Variable-Length Path Matching:
   ------------------------------
   Syntax:
   MATCH path = (variable1:Label1)-[:REL_TYPE*minLength..maxLength]->(variable2:Label2)
   RETURN path

   - Matches paths of specified lengths (minLength to maxLength).
   - Use `*` without specifying lengths for paths of any length.

   Example:
   MATCH p = (a:Person)-[:KNOWS*1..3]->(b:Person)
   RETURN p

4. Using WHERE Clause:
   --------------------
   Syntax:
   MATCH (variable:Label)
   WHERE variable.property = 'value' AND variable.property2 > 10
   RETURN variable

   - Filters matched nodes or relationships using conditions.
   - Supported operators: `=`, `<>`, `>`, `<`, `>=`, `<=`.
   - Combine multiple conditions using `AND` and `OR`.

   Example:
   MATCH (n:Person)
   WHERE n.age > 30 AND n.city = 'London'
   RETURN n

5. Using RETURN Clause:
   ---------------------
   Syntax:
   MATCH (variable:Label)
   RETURN variable.property1, variable.property2

   - Specifies which properties or variables to include in the output.
   - Use `RETURN *` to return all properties.

   Example:
   MATCH (n:Person {name: 'Alice'})
   RETURN n.name, n.age

6. Aggregations and Grouping:
   ---------------------------
   Syntax:
   MATCH (variable:Label)
   RETURN COUNT(variable), SUM(variable.property)
   GROUP BY variable.property

   - Aggregation functions: `COUNT`, `SUM`, `AVG`, `MIN`, `MAX`.
   - Use `GROUP BY` to group results based on a property.

   Example:
   MATCH (n:Person)
   RETURN n.city, COUNT(n)
   GROUP BY n.city

7. Ordering Results:
   ------------------
   Syntax:
   MATCH (variable:Label)
   RETURN variable.property
   ORDER BY variable.property ASC|DESC

   - Use `ORDER BY` to sort results in ascending (`ASC`) or descending (`DESC`) order.
   - Default sorting is ascending if no order is specified.

   Example:
   MATCH (n:Person)
   RETURN n.name, n.age
   ORDER BY n.age DESC

8. Limiting and Skipping Results:
   ------------------------------
   Syntax:
   MATCH (variable:Label)
   RETURN variable
   LIMIT number
   OFFSET number

   - Use `LIMIT` to restrict the number of results returned.
   - Use `OFFSET` to skip a number of results before starting to return rows.

   Example:
   MATCH (n:Person)
   RETURN n
   ORDER BY n.age DESC
   LIMIT 10 OFFSET 5

9. Set Operations (UNION, INTERSECT, EXCEPT):
   ------------------------------------------
   Syntax:
   query1
   UNION [ALL] query2
   INTERSECT query3
   EXCEPT query4

   - `UNION`: Combines results from multiple queries, removing duplicates by default.
   - `UNION ALL`: Combines results from multiple queries, keeping duplicates.
   - `INTERSECT`: Returns only the common results from multiple queries.
   - `EXCEPT`: Returns results from the first query that are not present in the second query.

   Example:
   MATCH (n:Person {city: 'New York'})
   RETURN n
   UNION
   MATCH (n:Person {city: 'London'})
   RETURN n

10. Handling Nested Queries:
    ------------------------
    Syntax:
    MATCH (variable:Label)
    RETURN (query1 UNION query2) AS result

    - Supports nested `UNION`, `INTERSECT`, and `EXCEPT` queries.
    - Nested queries are evaluated first, and their results are used in the outer query.

    Example:
    MATCH (n:Person)
    RETURN (
      MATCH (m:Employee)
      RETURN m
      UNION
      MATCH (e:Manager)
      RETURN e
    ) AS allEmployees
"
        };


        public static string GetHelp(string command = "")
        {
            if (string.IsNullOrWhiteSpace(command) || command.Equals("HELP", StringComparison.OrdinalIgnoreCase))
            {
                return DisplayGeneralHelp();
            }
            else
            {
                command = command.Replace("HELP", "", StringComparison.OrdinalIgnoreCase).Trim();
                if (commandHelp.ContainsKey(command.ToUpper()))
                {
                    return commandHelp[command.ToUpper()];
                }
                else
                {
                    return $"No help available for '{command}'.";
                }
            }
        }

        private static string DisplayGeneralHelp()
        {
            return @"
GraphDB Command Syntax:

Database commands:
- CREATE DATABASE: Creates a database of name [dbname], eg. CREATE DATABASE mydatabase  - will create the database called 'mydatabase'
- LOAD DATABASE: Loads a database of name [dbname], eg. LOAD DATABASE mydatabase  - will load the database called 'mydatabase'

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
