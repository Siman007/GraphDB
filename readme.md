Simple in memory Graph Database
******************

Key Components
Nodes: Represents entities or objects. Each node can have multiple properties.
Edges: Represents relationships between nodes. An edge connects two nodes and can also have properties.
Graph: Contains lists of nodes and edges, representing the entire dataset.

Basic Operations
AddNode(Node node): Adds a node to the graph if it doesn't already exist.
AddEdge(Edge edge): Adds an edge between two nodes, ensuring both nodes exist in the graph.
DeleteNode(string nodeId) and DeleteEdge(string fromNodeId, string toNodeId): Remove nodes or edges from the graph.
QueryNodesByProperty and QueryEdgesByProperty: Retrieve nodes or edges based on a specific property.
FindNeighbors(string nodeId): Finds all nodes directly connected to a specified node.

Advanced Operations
ExecuteCypherCommand(string cypher): Interprets a simplified version of Cypher query language to manipulate or query the graph database. Supports operations like CREATE, MATCH, DELETE, and more, including basic conditional logic and pattern matching.
ImportCsv(string filePath, bool isNodeCsv): Imports nodes or edges from a CSV file into the graph.
ExportNodesToCsv(string filePath) and ExportEdgesToCsv(string filePath): Exports nodes or edges to a CSV file.
FindPathBellmanFord(string startId, string endId): Finds the shortest path between two nodes using the Bellman-Ford algorithm, accounting for edge weights.

Persistence
SaveToFile(): Serializes the graph (nodes and edges) to a JSON file for persistence.
LoadGraph(): Loads the graph from a JSON file, reconstructing the nodes and edges into the graph structure.

Customizations
The database supports basic Cypher-like syntax for creating, updating, deleting, and querying nodes and edges.
It can handle conditional logic and pattern matching within queries, offering a flexible way to interact with the graph data.

The system is designed to be extendable, allowing for the addition of new features or enhancements to the Cypher command processing.
This implementation offers a foundation for a graph database system with basic CRUD operations, querying capabilities, and support for importing/exporting data. It encapsulates graph operations within a C# class structure, providing an object-oriented approach to graph data management.