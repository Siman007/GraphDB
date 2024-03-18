using System.Collections.Generic;
using Newtonsoft.Json;

namespace GraphDB
{
    public class Edge
    {
        public string FromId { get; set; }
        public string ToId { get; set; }
        public string Relationship { get; set; }
        public double Weight { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

        // Added for distinguishing different types of relationships
        public string RelationshipType { get; set; }

        [JsonIgnore]
        public Node From { get; set; }

        [JsonIgnore]
        public Node To { get; set; }
    }
}
