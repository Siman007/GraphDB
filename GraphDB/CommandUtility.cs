using System;
namespace GraphDB
{
    public static class CommandUtility
    {
        public static string GetHelpResponse()
        {
            return @"
<pre>
Available Commands:

'create [dbname]' - Creates a new database with the specified name.
'load [dbname]' - Loads an existing database with the specified name.
'help', 'h', '?' - Displays this help information.

Note: 'save', 'close', 'delete', and other data manipulation commands require a database to be loaded first.
</pre>
";
        }


        // Add more utilities here as needed
    }

}

