using System;
namespace GraphDB
{
    public enum CypherCommandType
    {
        Unknown,
        CreateNode,
        MergeNode,
        CreateRelationship,
        MatchNode,
        MatchRelationship,
        DeleteNode,
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

            if (cypher.StartsWith("CREATE") && cypher.Contains(")-[") && cypher.Contains("]->("))
                return CypherCommandType.CreateRelationship;
            if (cypher.StartsWith("CREATE"))
                return CypherCommandType.CreateNode;
            if (cypher.StartsWith("MERGE"))
                return CypherCommandType.MergeNode;
            if (cypher.StartsWith("MATCH") && cypher.Contains(")-[") && cypher.Contains("]->("))
                return CypherCommandType.MatchRelationship;
            if (cypher.StartsWith("MATCH"))
                return CypherCommandType.MatchNode;
            if (cypher.StartsWith("DELETE"))
                return CypherCommandType.DeleteNode;
            if (cypher.StartsWith("DETACH DELETE"))
                return CypherCommandType.DetachDeleteNode;
            if (cypher.StartsWith("SET") && cypher.Contains("NODE"))
                return CypherCommandType.SetNodeProperty;
            if (cypher.StartsWith("SET") && cypher.Contains("EDGE"))
                return CypherCommandType.SetRelationshipProperty;
            if (cypher.StartsWith("IMPORT CSV"))
                return CypherCommandType.ImportCsv;
            if (cypher.StartsWith("IMPORT JSON"))
                return CypherCommandType.ImportJSON;
            if (cypher.StartsWith("EXPORT CSV NODES"))
                return CypherCommandType.ExportCsvNodes;
            if (cypher.StartsWith("EXPORT CSV EDGES"))
                return CypherCommandType.ExportCsvEdges;
            if (cypher.Contains("IF CONDITION"))
                return CypherCommandType.Conditional;
            if (cypher.StartsWith("CASE"))
                return CypherCommandType.Case;
            if (cypher.StartsWith("help", StringComparison.OrdinalIgnoreCase))
                return CypherCommandType.Help;
            if (cypher.Contains("COUNT") && cypher.Contains("NODES"))
                return CypherCommandType.CountNodes;
            if (cypher.Contains("COUNT") && cypher.Contains("EDGES"))
                return CypherCommandType.CountEdges;
            if (cypher.Contains("SUM"))
                return CypherCommandType.AggregateSum;
            if (cypher.Contains("AVG"))
                return CypherCommandType.AggregateAvg;
            if (cypher.StartsWith("FIND RELATIONSHIPS"))
                return CypherCommandType.FindRelationships;
            if (cypher.StartsWith("MATCH PATTERN"))
                return CypherCommandType.MatchPattern;

            return CypherCommandType.Unknown;
        }
    }

}

