namespace LibLoader.Interfaces;

/// <summary>
/// Interface for building definitions for native libraries.
/// </summary>
public interface ILibraryDefinitionBuilder
{
    /// <summary>
    /// Sets the name of the library.
    /// </summary>
    /// <param name="libraryName">The name of the library.</param>
    /// <param name="usePlatformSpecificName">If true, automatically converts the name to the platform-specific format.</param>
    /// <returns>The ILibraryDefinitionBuilder instance for chaining method calls.</returns>
    ILibraryDefinitionBuilder WithName(string libraryName, bool usePlatformSpecificName = true);

    /// <summary>
    /// Sets the version of the library.
    /// </summary>
    /// <param name="version">The version of the library (optional).</param>
    /// <returns>The ILibraryDefinitionBuilder instance for chaining method calls.</returns>
    ILibraryDefinitionBuilder WithVersion(string? version);

    /// <summary>
    /// Sets the custom path to the library.
    /// </summary>
    /// <param name="customPath">The custom path to the library.</param>
    /// <returns>The ILibraryDefinitionBuilder instance for chaining method calls.</returns>
    ILibraryDefinitionBuilder FromCustomPath(string? customPath);

    /// <summary>
    /// Sets the remote URL from which to download the library.
    /// </summary>
    /// <param name="remoteUrl">The remote URL.</param>
    /// <param name="remoteFileName">The file name of the library in the remote location (optional).</param>
    /// <returns>The ILibraryDefinitionBuilder instance for chaining method calls.</returns>
    ILibraryDefinitionBuilder FromRemoteUrl(string? remoteUrl, string? remoteFileName = null);

    /// <summary>
    /// Sets the dependencies of the library.
    /// </summary>
    /// <param name="dependencies">An array of (name, version) tuples representing the dependencies.</param>
    /// <returns>The ILibraryDefinitionBuilder instance for chaining method calls.</returns>
    ILibraryDefinitionBuilder WithDependencies(params (string name, string version)[] dependencies);

    /// <summary>
    /// Sets a condition function that determines whether to load this library.
    /// </summary>
    /// <param name="condition">The condition function.</param>
    /// <returns>The ILibraryDefinitionBuilder instance for chaining method calls.</returns>
    ILibraryDefinitionBuilder WithCondition(Func<bool>? condition);

    /// <summary>
    /// Starts building a native function definition for the library.
    /// </summary>
    /// <returns>A native function builder instance.</returns>
    INativeFunctionBuilder WithNativeFunction();

    /// <summary>
    /// Adds the library definition to the LibLoader instance.
    /// </summary>
    /// <returns>The platform builder instance for chaining method calls.</returns>
    IPlatformBuilder Add();
}