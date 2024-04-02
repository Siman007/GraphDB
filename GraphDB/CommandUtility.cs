using System;
namespace GraphDB
{
    public static class CommandUtility
    {
        public static ApiResponse<string> GetHelpResponse()
        {
            string helpText = @"
<pre>
Available Commands:

'create [dbname]' - Creates a new database with the specified name.
'load [dbname]' - Loads an existing database with the specified name.
'help', 'h', '?' - Displays this help information.

Note: 'save', 'close', 'delete', and other data manipulation commands require a database to be loaded first.
</pre>
";

            // Assuming ApiResponse<string> is structured to take a success flag, a message, and the data
            return ApiResponse<string>.SuccessResponse(helpText, "Help information retrieved successfully.");
        }

        // Add more utilities here as needed
    }

}

