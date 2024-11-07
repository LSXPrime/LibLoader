using LibLoader.Interfaces;
using LibLoader.Models;

namespace LibLoader.Builders;

/// <summary>
/// Builds a definition for a native library for a specific platform.
/// </summary>
public class LibraryDefinitionBuilder(IPlatformBuilder platformBuilder, Platform platform)
    : ILibraryDefinitionBuilder
{
    private readonly LibraryDefinition _libraryDefinition = new();

    /// <summary>
    /// Sets the name of the library.
    /// </summary>
    /// <param name="libraryName">The name of the library.</param>
    /// <param name="usePlatformSpecificName">If true, automatically converts the name to the platform-specific format.</param>
    /// <returns>The LibraryDefinitionBuilder instance for chaining method calls.</returns>
    public ILibraryDefinitionBuilder WithName(string libraryName, bool usePlatformSpecificName = true)
    {
        _libraryDefinition.LibraryName = usePlatformSpecificName ? LibLoader.GetPlatformSpecificName(libraryName, platform) : libraryName;
        return this;
    }

    /// <summary>
    /// Sets the version of the library.
    /// </summary>
    /// <param name="version">The version of the library (optional).</param>
    /// <returns>The LibraryDefinitionBuilder instance for chaining method calls.</returns>
    public ILibraryDefinitionBuilder WithVersion(string? version)
    {
        if (!string.IsNullOrEmpty(version) && Version.TryParse(version, out var parsedVersion))
        {
            _libraryDefinition.Version = parsedVersion;
        }
        return this;
    }

    /// <summary>
    /// Sets the custom path to the library.
    /// </summary>
    /// <param name="customPath">The custom path to the library.</param>
    /// <returns>The LibraryDefinitionBuilder instance for chaining method calls.</returns>
    public ILibraryDefinitionBuilder FromCustomPath(string? customPath)
    {
        _libraryDefinition.CustomPath = customPath;
        return this;
    }

    /// <summary>
    /// Sets the remote URL from which to download the library.
    /// </summary>
    /// <param name="remoteUrl">The remote URL.</param>
    /// <param name="remoteFileName">The file name of the library in the remote location (optional).</param>
    /// <returns>The LibraryDefinitionBuilder instance for chaining method calls.</returns>
    public ILibraryDefinitionBuilder FromRemoteUrl(string? remoteUrl, string? remoteFileName = null)
    {
        _libraryDefinition.RemoteUrl = remoteUrl;
        _libraryDefinition.RemoteFileName = remoteFileName;
        return this;
    }

    /// <summary>
    /// Sets the dependencies of the library.
    /// </summary>
    /// <param name="dependencies">An array of (name, version) tuples representing the dependencies.</param>
    /// <returns>The LibraryDefinitionBuilder instance for chaining method calls.</returns>
    public ILibraryDefinitionBuilder WithDependencies(params (string name, string version)[] dependencies)
    {
        _libraryDefinition.Dependencies = dependencies;
        return this;
    }

    /// <summary>
    /// Sets a condition function that determines whether to load this library.
    /// </summary>
    /// <param name="condition">The condition function.</param>
    /// <returns>The LibraryDefinitionBuilder instance for chaining method calls.</returns>
    public ILibraryDefinitionBuilder WithCondition(Func<bool>? condition)
    {
        _libraryDefinition.Condition = condition;
        return this;
    }
    
    /// <summary>
    /// Starts building a native function definition for the library.
    /// </summary>
    /// <returns>A native function builder instance.</returns>
    public INativeFunctionBuilder WithNativeFunction()
    {
        return new NativeFunctionBuilder(this, _libraryDefinition);
    }

    /// <summary>
    /// Adds the library definition to the LibLoader instance.
    /// </summary>
    /// <returns>The platform builder instance for chaining method calls.</returns>
    public IPlatformBuilder Add()
    {
        LibLoader.Instance.Libraries.Add(_libraryDefinition);
        return platformBuilder;
    }
}