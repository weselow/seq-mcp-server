using Microsoft.Extensions.DependencyInjection;
using SeqMcp.Core.Prompts;
using SeqMcp.Core.Resources;
using SeqMcp.Core.Tools;

namespace SeqMcp.Core.Hosting;

/// <summary>
/// Extension methods for registering the shared MCP primitives (tools,
/// resources, prompts) on an <see cref="IMcpServerBuilder"/>. Both entry
/// points (HTTP host and Stdio host) call the same helper so they stay in
/// sync if the SDK or the primitive surface ever changes.
/// </summary>
public static class McpBuilderExtensions
{
    /// <summary>
    /// Registers <see cref="SeqTools"/>, <see cref="SeqResources"/> and
    /// <see cref="SeqPrompts"/> on the MCP server builder.
    /// </summary>
    public static IMcpServerBuilder AddSeqMcpPrimitives(this IMcpServerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder
            .WithTools<SeqTools>()
            .WithResources<SeqResources>()
            .WithPrompts<SeqPrompts>();
    }
}
