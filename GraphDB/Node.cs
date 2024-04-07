using System;
using System.Collections.Generic;
namespace GraphDB
{

 
    public class Node
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }




}

