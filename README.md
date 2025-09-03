### Wrap .NET MCP Server as a PowerShell module

This project serves as an example to demonstrate how to create a .NET MCP server as a PowerShell module.

To build this project, run the following command from the root directory of your local repo:

```pwsh
dotnet publish .\MyFirstMCP.csproj
```

It will produce the module `MyFirstMCP` at `.\out\MyFirstMCP`.

Once you deploy the module to your module path, you can simply run `pwsh -noprofile -c MyFirstMCP\Start-MyMCP` to start the MCP server.

### Benefit of Shipping as PowerShell module

1. This works with both Windows PowerShel v5.1 and PowerShell v7+.
   So technically, users on Windows don't need to pre-install any runtime in order to use your MCP server.

1. Comparing to having a self-contained application,
   - The size of a module is very small.
     For this particular project, the size of the module is 3.3mb, almost all of which are the necessary assemblies for using the .NET model context protocol SDK.
   - It's easier to distribute a PowerShell module.

### Use it in VSCode

```json
{
    "servers": {
        "MyFirstMCP": {
            "type": "stdio",
            "command": "pwsh",
            "args": [
                "-noprofile",
                "-c",
                "MyFirstMCP\\Start-MyMCP"
            ]
        }
    },
    "inputs": []
}
```
