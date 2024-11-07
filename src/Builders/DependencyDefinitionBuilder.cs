using LibLoader.Interfaces;
using LibLoader.Models;

namespace LibLoader.Builders;

/// <summary>
/// Builds a definition for a dependency library for a specific platform and bitness.
/// </summary>
public class DependencyDefinitionBuilder(IPlatformBuilder platformBuilder, Platform platform, Bitness bitness)
    : ILibraryDefinitionBuilder
{
    private string _libraryName = string.Empty;
    private string? _version;
    private string? _customPath;
    private Func<bool>? _condition;
    private string? _remoteUrl;
    private string? _remoteFileName;

    /// <summary>
    /// Sets the name of the dependency library.
    /// </summary>
    /// <param name="libraryName">The name of the library.</param>
    /// <param name="usePlatformSpecificName">If true, automatically converts the name to the platform-specific format.</param>
    /// <returns>The DependencyDefinitionBuilder instance for chaining method calls.</returns>
    public ILibraryDefinitionBuilder WithName(string libraryName, bool usePlatformSpecificName = false)
    {
        _libraryName = usePlatformSpecificName ? LibLoader.GetPlatformSpecificName(libraryName, platform) : libraryName;
        return this;
    }

    /// <summary>
    /// Sets the version of the dependency library.
    /// </summary>
    /// <param name="version">The version of the library (optional).</param>
    /// <returns>The DependencyDefinitionBuilder instance for chaining method calls.</returns>
    public ILibraryDefinitionBuilder WithVersion(string? version)
    {
        _version = version;
        return this;
    }

    /// <summary>
    /// Sets the custom path to the dependency library.
    /// </summary>
    /// <param name="customPath">The custom path to the library.</param>
    /// <returns>The DependencyDefinitionBuilder instance for chaining method calls.</returns>
    public ILibraryDefinitionBuilder FromCustomPath(string? customPath)
    {
        _customPath = customPath;
        return this;
    }
    
    /// <summary>
    /// **NOT SUPPORTED**: Sets the dependencies of the dependency library.
    /// </summary>
    /// <param name="dependencies">An array of (name, version) tuples representing the dependencies.</param>
    /// <returns>The DependencyDefinitionBuilder instance for chaining method calls.</returns>
    /// <remarks>
    /// Dependencies cannot depend on other dependencies at this time. This method will throw a <see cref="NotImplementedException"/>.
    /// </remarks>
    public ILibraryDefinitionBuilder WithDependencies(params (string name, string version)[] dependencies)
    {
        throw new NotImplementedException("Dependencies can't depend on other dependencies yet.");
    }

    /// <summary>
    /// Sets a condition function that determines whether to load this dependency library.
    /// </summary>
    /// <param name="condition">The condition function.</param>
    /// <returns>The DependencyDefinitionBuilder instance for chaining method calls.</returns>
    public ILibraryDefinitionBuilder WithCondition(Func<bool>? condition)
    {
        _condition = condition;
        return this;
    }

    /// <summary>
    /// **NOT SUPPORTED**: Adds a native function to the dependency library definition.
    /// </summary>
    /// <returns>A native function builder instance.</returns>
    /// <remarks>
    /// Higher-level implementations should not call lower-level dependencies. This method will throw a <see cref="NotImplementedException"/>.
    /// </remarks>
    public INativeFunctionBuilder WithNativeFunction()
    {
        throw new NotImplementedException("Higher level implementation shouldn't call lower level dependencies.");
    }

    /// <summary>
    /// Sets the remote URL from which to download the dependency library.
    /// </summary>
    /// <param name="remoteUrl">The remote URL.</param>
    /// <param name="remoteFileName">The file name of the library in the remote location (optional).</param>
    /// <returns>The DependencyDefinitionBuilder instance for chaining method calls.</returns>
    public ILibraryDefinitionBuilder FromRemoteUrl(string? remoteUrl, string? remoteFileName = null)
    {
        _remoteUrl = remoteUrl;
        _remoteFileName = remoteFileName;
        return this;
    }

    /// <summary>
    /// Adds the dependency library definition to the LibLoader instance.
    /// </summary>
    /// <returns>The platform builder instance for chaining method calls.</returns>
    public IPlatformBuilder Add()
    {
        if (!LibLoader.Instance.Dependencies.ContainsKey(Tuple.Create(platform, bitness)))
            LibLoader.Instance.Dependencies.Add(Tuple.Create(platform, bitness), []);

        var parsedVersion = Version.Parse("0.0.0");
        if (!string.IsNullOrEmpty(_version))
            if (!Version.TryParse(_version, out parsedVersion))
                throw new ArgumentException($"Invalid version string: {_version}");

        var platformSpecificName = LibLoader.GetPlatformSpecificName(_libraryName, platform);
        LibLoader.Instance.Dependencies[Tuple.Create(platform, bitness)].Add(new LibraryDefinition
        {
            LibraryName = platformSpecificName,
            Version = parsedVersion,
            CustomPath = _customPath,
            RemoteUrl = _remoteUrl,
            RemoteFileName = _remoteFileName,
            Condition = _condition
        });
        
        return platformBuilder;
    }
}