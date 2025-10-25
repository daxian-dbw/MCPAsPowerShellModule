using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace MyFirstMCP.Tools;

public class CustomTool : McpServerTool
{
    public override Tool ProtocolTool => throw new NotImplementedException();

    public override ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
