using System.Runtime.InteropServices;

namespace LibLoader.Models;

/// <summary>
/// Represents a definition for a native function within a library.
/// </summary>
public class NativeFunctionDefinition
{
    /// <summary>
    /// Gets the library that contains the function.
    /// </summary>
    public LibraryDefinition Library { get; init; }

    /// <summary>
    /// Gets or sets the calling name of the function (used in managed code).
    /// </summary>
    public string CallingName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the native name of the function (as it appears in the native library).
    /// </summary>
    public string NativeName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the calling convention used for the function.
    /// </summary>
    public CallingConvention CallingConvention { get; init; } = CallingConvention.Cdecl;

    /// <summary>
    /// Gets or sets the return type of the function.
    /// </summary>
    public Type ReturnType { get; init; } = typeof(void);

    /// <summary>
    /// Gets or sets a list of parameters for the function.
    /// </summary>
    public List<NativeFunctionParameter> Parameters { get; init; } = [];

    /// <summary>
    /// Gets or sets the delegate instance that wraps the native function call.
    /// </summary>
    public Delegate Delegate { get; set; }
}

/// <summary>
/// Represents a parameter for a native function.
/// </summary>
public class NativeFunctionParameter
{
    /// <summary>
    /// Gets or sets the name of the parameter.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of the parameter.
    /// </summary>
    public Type Type { get; init; }
}