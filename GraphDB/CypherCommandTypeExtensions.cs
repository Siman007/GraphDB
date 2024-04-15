using System;
namespace GraphDB
{
    public enum CypherCommandType
    {
        Unknown,
        CreateNode,
        MergeNode,
        CreateRelationship,
        CreateDatabase,
        MatchNode,
        MatchRelationship,
        DeleteNode,
        DeleteEdge,
        DetachDeleteNode,
        DeleteRelationship,
        SetNodeProperty,
        SetRelationshipProperty,
        ImportCsv,
        ImportJSON,
        ExportCsvNodes,
        ExportCsvEdges,
        Conditional,
        Case,
        Help,
        CountNodes,
        CountEdges,
        AggregateSum, 
        AggregateAvg,
        FindRelationships,
        FindNeighbors,
        MatchPattern,

        // Extend with other command types as necessary
    }

    public static class CypherCommandTypeExtensions
    {
        public static CypherCommandType ToCommandType(this string cypher)
        {
            if (string.IsNullOrWhiteSpace(cypher)) return CypherCommandType.Unknown;

            // Normalize the Cypher command to handle multiline inputs and standardize case
            string normalizedCypher = string.Join(" ", cypher
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim()))
                .ToUpperInvariant(); // Convert to upper case to make checks case-insensitive

            if (normalizedCypher.StartsWith("CREATE DATABASE"))
                return CypherCommandType.CreateDatabase;
            if (normalizedCypher.StartsWith("CREATE") && cypher.Contains(")-[") && cypher.Contains("]->("))
                return CypherCommandType.CreateRelationship;
            if (normalizedCypher.StartsWith("CREATE"))
                return CypherCommandType.CreateNode;
            if (normalizedCypher.StartsWith("MERGE"))
                return CypherCommandType.MergeNode;
            if (normalizedCypher.StartsWith("MATCH PATTERN"))
                return CypherCommandType.MatchPattern;
            if (normalizedCypher.StartsWith("MATCH") && cypher.Contains(")-[") && cypher.Contains("]->("))
                return CypherCommandType.MatchRelationship;
            if (normalizedCypher.StartsWith("MATCH"))
                return CypherCommandType.MatchNode;
            if (normalizedCypher.StartsWith("DELETE NODE"))
                return CypherCommandType.DeleteNode;
            if (normalizedCypher.StartsWith("DELETE EDGE"))
                return CypherCommandType.DeleteEdge;
            if (normalizedCypher.StartsWith("DETACH DELETE"))
                return CypherCommandType.DetachDeleteNode;
            if (normalizedCypher.StartsWith("SET") && cypher.Contains("NODE"))
                return CypherCommandType.SetNodeProperty;
            if (normalizedCypher.StartsWith("SET") && cypher.Contains("EDGE"))
                return CypherCommandType.SetRelationshipProperty;
            if (normalizedCypher.StartsWith("IMPORT CSV"))
                return CypherCommandType.ImportCsv;
            if (normalizedCypher.StartsWith("IMPORT JSON"))
                return CypherCommandType.ImportJSON;
            if (normalizedCypher.StartsWith("EXPORT CSV NODES"))
                return CypherCommandType.ExportCsvNodes;
            if (normalizedCypher.StartsWith("EXPORT CSV EDGES"))
                return CypherCommandType.ExportCsvEdges;
            if (cypher.Contains("IF CONDITION"))
                return CypherCommandType.Conditional;
            if (normalizedCypher.StartsWith("CASE"))
                return CypherCommandType.Case;
            if (normalizedCypher.StartsWith("help", StringComparison.OrdinalIgnoreCase))
                return CypherCommandType.Help;
            if (cypher.Contains("COUNT") && cypher.Contains("NODES"))
                return CypherCommandType.CountNodes;
            if (cypher.Contains("COUNT") && cypher.Contains("EDGES"))
                return CypherCommandType.CountEdges;
            if (cypher.Contains("SUM"))
                return CypherCommandType.AggregateSum;
            if (cypher.Contains("AVG"))
                return CypherCommandType.AggregateAvg;
            if (normalizedCypher.StartsWith("FIND RELATIONSHIPS"))
                return CypherCommandType.FindRelationships;


            return CypherCommandType.Unknown;
        }
    }

}

