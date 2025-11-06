using System.Text.Json;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Collections.ObjectModel;

namespace MyFirstMCP.Tools;

/// <summary>
/// Function tools from a module share the same Runspace, so that the changes to the module state by a function tool can be seen by other function tools.
/// </summary>
public class PSModuleMcpServerTool : McpServerTool
{
    private readonly string _funcName;
    private readonly Tool _tool;
    private readonly ModuleToolsMetadata _metadata;

    public PSModuleMcpServerTool(string funcName, Tool tool, ModuleToolsMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(tool);

        _funcName = funcName;
        _tool = tool;
        _metadata = metadata;
    }

    public override Tool ProtocolTool => _tool;

    public override ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            Collection<PSObject> results = _metadata.InvokeFunction(_funcName, request.Params?.Arguments);
            return ValueTask.FromResult(GetCallToolResult(results));
        }
        catch (Exception e)
        {
            return ValueTask.FromResult(PSToolUtils.GetErrorResult(_tool.Name, e));
        }
    }

    private CallToolResult GetCallToolResult(Collection<PSObject> results)
    {
        if (results is null || results.Count is 0)
        {
            return new CallToolResult() { Content = [] };
        }

        if (results.Count is 1 && results[0].BaseObject is string text)
        {
            return new CallToolResult() { Content = [new TextContentBlock { Text = text }] };
        }

        string json = _metadata.SerializeToJson(results);
        return new CallToolResult() { Content = [new TextContentBlock { Text = json }] };
    }
}

public class ModuleToolsMetadata
{
    private readonly string _moduleName;
    private readonly PowerShell _pwsh;
    private readonly PSModuleInfo _moduleInfo;

    public ModuleToolsMetadata(string moduleName)
    {
        ArgumentException.ThrowIfNullOrEmpty(moduleName);

        var iss = InitialSessionState.CreateDefault2();
        if (OperatingSystem.IsWindows())
        {
            iss.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.Unrestricted;
        }

        _moduleName = moduleName;
        _pwsh = PowerShell.Create(iss);

        _moduleInfo = _pwsh
            .AddCommand("Import-Module")
            .AddParameter("Name", _moduleName)
            .AddParameter("PassThru", true)
            .Execute<PSModuleInfo>();
    }

    internal IEnumerable<McpServerTool> GetFunctionMcpTools()
    {
        if (_moduleInfo.ExportedFunctions.Count is 0)
        {
            throw new InvalidDataException($"The module '{_moduleInfo.Name}' doesn't expose any functions.");
        }

        foreach (var kvp in _moduleInfo.ExportedFunctions)
        {
            string funcName = kvp.Key;
            FunctionInfo funcInfo = kvp.Value;

            Tool tool = PSToolUtils.CreateToolForScriptOrFunction(_pwsh, funcInfo);
            yield return new PSModuleMcpServerTool(funcName, tool, this);
        }
    }

    internal Collection<PSObject> InvokeFunction(string funcName, IReadOnlyDictionary<string, JsonElement> argDict)
    {
        // We don't support parallel tool calling for function tools exposed from a module.
        // A function tool's change to the module state should be seen by other function tools.
        // So, those function tools should be invoked in the same Runspace.
        lock (_pwsh)
        {
            _pwsh.AddCommand(funcName);
            if (argDict is { })
            {
                foreach (var kvp in argDict)
                {
                    _pwsh.AddParameter(kvp.Key, kvp.Value);
                }
            }

            return _pwsh.Execute();
        }
    }

    internal string SerializeToJson(Collection<PSObject> input)
    {
        lock (_pwsh)
        {
            string json = _pwsh
                .AddCommand("ConvertTo-Json")
                .AddParameter("InputObject", input)
                .AddParameter("Depth", 5)
                .AddParameter("EnumsAsStrings", true)
                .AddParameter("Compress", true)
                .ExecuteAndReturnString(errorTemplate: string.Empty);

            return json;
        }
    }
}
