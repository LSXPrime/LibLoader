namespace LibLoader.Models;

/// <summary>
/// Represents a definition for a native library.
/// </summary>
public class LibraryDefinition
{
    /// <summary>
    /// Gets or sets the name of the library.
    /// </summary>
    public string LibraryName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the version of the library.
    /// </summary>
    public Version Version { get; set; } = new(0, 0, 0);

    /// <summary>
    /// Gets or sets a custom path to the library.
    /// </summary>
    public string? CustomPath { get; set; }

    /// <summary>
    /// Gets or sets the remote URL from which to download the library.
    /// </summary>
    public string? RemoteUrl { get; set; }

    /// <summary>
    /// Gets or sets the file name of the library in the remote location.
    /// </summary>
    public string? RemoteFileName { get; set; }

    /// <summary>
    /// Gets or sets an array of dependency library names and versions.
    /// </summary>
    public (string name, string version)[]? Dependencies { get; set; }

    /// <summary>
    /// Gets or sets a condition function that determines whether to load this library.
    /// </summary>
    public Func<bool>? Condition { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the library has been loaded.
    /// </summary>
    public bool Loaded { get; set; }

    /// <summary>
    /// Gets or sets the path to the loaded library.
    /// </summary>
    public string LoadedLibraryPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the handle to the loaded library.
    /// </summary>
    public nint LibraryHandle { get; set; }

    /// <summary>
    /// Gets or sets a dictionary of native functions defined in the library.
    /// </summary>
    public Dictionary<string, NativeFunctionDefinition> NativeFunctions { get; set; } = new();
}