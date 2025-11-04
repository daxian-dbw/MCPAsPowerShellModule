using System.Text.Json;
using System.Text.Json.Nodes;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Microsoft.Extensions.AI;
using System.Management.Automation.Language;
using System.Collections.ObjectModel;

namespace MyFirstMCP.Tools;

public class PSScriptMcpServerTool : McpServerTool
{
    private readonly string _scriptPath;
    private readonly InitialSessionState _iss;
    private readonly PowerShell _pwsh;
    private readonly Tool _tool;

    internal PSScriptMcpServerTool(string scriptPath)
    {
        _scriptPath = scriptPath;
        _iss = InitialSessionState.CreateDefault2();
        if (OperatingSystem.IsWindows())
        {
            _iss.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.Unrestricted;
        }

        _pwsh = PowerShell.Create(_iss);
        _tool = CreateTool();
    }

    private Tool CreateTool()
    {
        ExternalScriptInfo scriptInfo = _pwsh
            .AddCommand("Get-Command")
            .AddParameter("Name", _scriptPath)
            .Execute<ExternalScriptInfo>() ?? throw new ArgumentException($"The script '{_scriptPath}' cannot be found.");

        if (scriptInfo.ParameterSets.Count > 1)
        {
            throw new InvalidDataException("The script cannot have more than 1 parameter set.");
        }

        dynamic help = _pwsh
            .AddCommand("Get-Help")
            .AddParameter("Name", _scriptPath)
            .AddParameter("Full")
            .Execute<PSObject>() ?? throw new InvalidDataException("The script has no comment based help defined.");

        string toolName = Path.GetFileNameWithoutExtension(_scriptPath).Replace('-', '_');
        string toolDescription = ValueOf<string>(help.description?[0]?.Text) ?? throw new InvalidDataException("No description found for the script.");
        PSObject[] parameters = ValueOf<PSObject[]>(help.parameters?.parameter) ?? Array.Empty<PSObject>();

        JsonObject schema = new()
        {
            { "type", "object" },
            { "additionalProperties", false },
            { "$schema", "http://json-schema.org/draft-07/schema#" }
        };

        JsonObject parameterSchemas = [];
        JsonArray requiredProperties = null;

        foreach (dynamic parameter in parameters)
        {
            ParameterMetadata paramInfo = scriptInfo.Parameters[ValueOf<string>(parameter.name)];
            string paramDescription = ValueOf<string>(parameter?.description[0].Text);

            var paramAttr = paramInfo.Attributes.OfType<ParameterAttribute>().FirstOrDefault();
            object defaultValue = paramAttr.Mandatory ? null : GetDefaultValue(scriptInfo, paramInfo.Name, paramInfo.ParameterType);

            var paramSchema = AIJsonUtilities.CreateJsonSchema(
                type: paramInfo.ParameterType,
                description: paramDescription,
                hasDefaultValue: defaultValue is { },
                defaultValue: defaultValue);

            parameterSchemas.Add(paramInfo.Name, JsonSerializer.SerializeToNode(paramSchema));
            if (paramAttr.Mandatory)
            {
                (requiredProperties ??= []).Add((JsonNode)paramInfo.Name);
            }
        }

        schema.Add("properties", parameterSchemas);
        if (requiredProperties is { })
        {
            schema.Add("required", requiredProperties);
        }

        return new Tool()
        {
            Name = toolName,
            Description = toolDescription,
            InputSchema = JsonSerializer.SerializeToElement(schema)
        };
    }

    private static T ValueOf<T>(dynamic value)
    {
        if (value is T ret)
        {
            return ret;
        }

        object v = value is PSObject psobj ? psobj.BaseObject : value;
        return (T)v;
    }

    private static object GetDefaultValue(ExternalScriptInfo scriptInfo, string paramName, Type paramType)
    {
        var ast = (ScriptBlockAst)scriptInfo.ScriptBlock.Ast;
        var paramAst = ast.ParamBlock.Parameters.FirstOrDefault(pAst => pAst.Name.VariablePath.UserPath == paramName);

        if (paramAst is { } && paramAst.DefaultValue is ConstantExpressionAst constantAst)
        {
            return constantAst.Value;
        }

        if (paramType == typeof(int))
        {
            return 0;
        }

        if (paramType == typeof(string))
        {
            return string.Empty;
        }

        return typeof(PSScriptMcpServerTool)
            .GetMethod(nameof(GetDefaultValueGeneric), BindingFlags.NonPublic | BindingFlags.Static)
            .MakeGenericMethod(paramType)
            .Invoke(null, null);
    }

    private static T GetDefaultValueGeneric<T>() => default;

    public override Tool ProtocolTool => _tool;

    public override ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        _pwsh.AddCommand(_scriptPath);
        if (request.Params?.Arguments is { } argDict)
        {
            foreach (var kvp in argDict)
            {
                _pwsh.AddParameter(kvp.Key, kvp.Value);
            }
        }

        try
        {
            Collection<PSObject> results = _pwsh.Execute();
            if (results is null || results.Count is 0)
            {
                return ValueTask.FromResult(new CallToolResult() { Content = [] });
            }

            if (results.Count is 1 && results[0].BaseObject is string text)
            {
                return ValueTask.FromResult(new CallToolResult() { Content = [new TextContentBlock { Text = text }] });
            }

            string json = _pwsh
                .AddCommand("ConvertTo-Json")
                .AddParameter("InputObject", results)
                .AddParameter("Depth", 5)
                .AddParameter("EnumsAsStrings", true)
                .AddParameter("Compress", true)
                .ExecuteAndReturnString(errorTemplate: string.Empty);

            return ValueTask.FromResult(new CallToolResult()
            {
                Content = [new TextContentBlock { Text = json }],
            });
        }
        catch (Exception e)
        {
            string error = $"""
                Failed to run the tool due to the following error:
                ```
                {e.Message}
                ```
                Check to see if it's caused by the passed-in command name or parameter name(s), and if so, please try again.
                """;

            return ValueTask.FromResult(new CallToolResult()
            {
                Content = [new TextContentBlock { Text = error }],
                IsError = true
            });
        }
    }
}
