namespace LibLoader.Interfaces;

/// <summary>
/// Interface for building definitions for native libraries and dependencies for a specific platform.
/// </summary>
public interface IPlatformBuilder
{
    /// <summary>
    /// Starts building a definition for a native library.
    /// </summary>
    /// <returns>A library definition builder instance.</returns>
    ILibraryDefinitionBuilder WithLibrary();

    /// <summary>
    /// Starts building a definition for a dependency library.
    /// </summary>
    /// <returns>A dependency definition builder instance.</returns>
    ILibraryDefinitionBuilder WithDependency();

    /// <summary>
    /// Builds the LibLoader instance with the defined libraries and dependencies.
    /// </summary>
    /// <returns>The LibLoader instance.</returns>
    LibLoader Build();
}