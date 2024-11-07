using LibLoader.Interfaces;

namespace LibLoader.Builders;

/// <summary>
/// Builds definitions for native libraries and dependencies for a specific platform and bitness.
/// </summary>
public class PlatformBuilder(Platform platform, Bitness bitness) : IPlatformBuilder
{
    /// <summary>
    /// Starts building a definition for a native library.
    /// </summary>
    /// <returns>A library definition builder instance.</returns>
    public ILibraryDefinitionBuilder WithLibrary() => new LibraryDefinitionBuilder(this, platform);
    
    /// <summary>
    /// Starts building a definition for a dependency library.
    /// </summary>
    /// <returns>A dependency definition builder instance.</returns>
    public ILibraryDefinitionBuilder WithDependency() => new DependencyDefinitionBuilder(this, platform, bitness);

    /// <summary>
    /// Builds the LibLoader instance with the defined libraries and dependencies.
    /// </summary>
    /// <returns>The LibLoader instance.</returns>
    public LibLoader Build() => LibLoader.Instance;
}