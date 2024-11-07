using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using LibLoader.Builders;
using LibLoader.Exceptions;
using LibLoader.Interfaces;
using LibLoader.Models;
using LibLoader.Native;

namespace LibLoader;

/// <summary>
/// Defines the platform for which the native libraries are targeted.
/// </summary>
public enum Platform
{
    /// <summary>
    /// Represents the Windows platform.
    /// </summary>
    Windows,

    /// <summary>
    /// Represents the Linux platform.
    /// </summary>
    Linux,

    /// <summary>
    /// Represents the macOS platform.
    /// </summary>
    MacOs,

    /// <summary>
    /// Represents the Android platform.
    /// </summary>
    Android,

    /// <summary>
    /// Represents the iOS platform.
    /// </summary>
    Ios
}

/// <summary>
/// Defines the bitness (32-bit or 64-bit) of the native libraries.
/// </summary>
public enum Bitness
{
    /// <summary>
    /// Represents 32-bit native libraries.
    /// </summary>
    X86,

    /// <summary>
    /// Represents 64-bit native libraries.
    /// </summary>
    X64,
    
    /// <summary>
    /// Represents ARM 32-bit native libraries.
    /// </summary>
    Arm,
    
    /// <summary>
    /// Represents ARM 64-bit native libraries.
    /// </summary>
    Arm64
}

/// <summary>
/// Defines the log levels for library loading operations.
/// </summary>
public enum LogLevel
{
    /// <summary>
    /// Informational log level.
    /// </summary>
    Info,
    
    /// <summary>
    /// Warning log level.
    /// </summary>
    Warning,
    
    /// <summary>
    /// Error log level.
    /// </summary>
    Error
}

/// <summary>
/// Delegate for logging events during library loading.
/// </summary>
/// <param name="level">The log level of the event.</param>
/// <param name="message">The log message.</param>
/// <param name="ex">An optional exception associated with the event.</param>
public delegate void LogDelegate(LogLevel level, string message, Exception? ex = null);

/// <summary>
/// Manages the loading and unloading of native libraries.
/// </summary>
public class LibLoader
{
    public LogDelegate Log { get; set; } = (level, s, _) =>
        Console.WriteLine($"[{level}] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {s}");

    internal readonly List<LibraryDefinition> Libraries = [];
    internal readonly Dictionary<Tuple<Platform, Bitness>, List<LibraryDefinition>> Dependencies = new();
    private readonly Dictionary<Tuple<Platform, Bitness>, List<LibraryDefinition>> _loadedLibraries = new();
    private readonly Dictionary<string, string> _downloadedFiles = new();
    private readonly HashSet<string> _loadedLibraryPaths = new();
    private string _targetDirectory = Path.Combine(Path.GetTempPath(), "Native");
    private bool _loadLibraryExplicit = true;
    private IProgress<float>? _progress;
    private float _overallProgress;
    private int _totalLibrariesToLoad;
    private CancellationToken _cancellationToken;
    private readonly List<string> _searchPaths = [];

    /// <summary>
    /// Gets the singleton instance of the LibLoader class.
    /// </summary>
    public static LibLoader Instance => _instance.Value;
    private static readonly Lazy<LibLoader> _instance = new(() => new LibLoader());

    /// <summary>
    /// Initializes a new instance of the LibLoader class.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This constructor is private to ensure that the singleton instance is initialized only once and only accessible through the <see cref="Instance"/> property.
    /// </para>
    /// </remarks>
    private LibLoader() { }

    
    /// <summary>
    /// Sets the target directory for storing downloaded or extracted native libraries.
    /// </summary>
    /// <param name="targetDirectory">The target directory path.</param>
    /// <returns>The LibLoader instance for chaining method calls.</returns>
    public LibLoader WithTargetDirectory(string targetDirectory)
    {
        _targetDirectory = targetDirectory;
        return this;
    }

    /// <summary>
    /// Sets whether libraries should be loaded implicitly or explicitly.
    /// </summary>
    /// <param name="loadLibraryImplicit">True to load libraries implicitly; false to load explicitly.</param>
    /// <returns>The LibLoader instance for chaining method calls.</returns>
    public LibLoader WithImplicitLoading(bool loadLibraryImplicit = true)
    {
        _loadLibraryExplicit = !loadLibraryImplicit;
        return this;
    }
    
    /// <summary>
    /// Sets a progress reporter for tracking library loading progress.
    /// </summary>
    /// <param name="progress">The progress reporter.</param>
    /// <returns>The LibLoader instance for chaining method calls.</returns>
    public LibLoader WithProgress(IProgress<float> progress)
    {
        _progress = progress;
        return this;
    }

    /// <summary>
    /// Sets a cancellation token for library loading operations.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The LibLoader instance for chaining method calls.</returns>
    public LibLoader WithCancellationToken(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        return this;
    }

    /// <summary>
    /// Adds search paths for locating native libraries.
    /// </summary>
    /// <param name="searchPaths">The search paths to add.</param>
    /// <returns>The LibLoader instance for chaining method calls.</returns>
    public LibLoader WithSearchPaths(IEnumerable<string> searchPaths)
    {
        _searchPaths.AddRange(searchPaths);
        return this;
    }

    /// <summary>
    /// Starts building a library definition for a specific platform and bitness.
    /// </summary>
    /// <param name="platform">The target platform.</param>
    /// <param name="bitness">The target bitness.</param>
    /// <returns>A platform builder instance.</returns>
    public IPlatformBuilder ForPlatform(Platform platform, Bitness bitness)
    {
        return new PlatformBuilder(platform, bitness);
    }

    /// <summary>
    /// Asynchronously loads the defined native libraries.
    /// </summary>
    /// <returns>A task representing the loading operation.</returns>
    public async Task LoadAsync()
    {
        var platform = GetPlatform();
        var bitness = GetBitness();

        _totalLibrariesToLoad = Libraries.Count(lib => lib.Condition == null || lib.Condition());
        _overallProgress = 0;
        _progress?.Report(0);

        foreach (var library in Libraries)
        {
            try
            {
                await LoadLibraryAsync(library, platform, bitness);
                _overallProgress += 1f / _totalLibrariesToLoad;
                _progress?.Report(_overallProgress);
            }
            catch (Exception ex) when (ex is not MissingLibraryException)
            {
                Log(LogLevel.Error, $"Failed to load library '{library.LibraryName}' asynchronously", ex);
                throw; // Re-throw if it's not a MissingLibraryException
            }
        }

        _progress?.Report(1f);
    }
    
    /// <summary>
    /// Unloads all loaded native libraries.
    /// </summary>
    public void Unload()
    {
        foreach (var libInfo in _loadedLibraries.Keys.SelectMany(platformBitness =>
                     _loadedLibraries[platformBitness].Where(libInfo => libInfo.Loaded)))
        {
            try
            {
                if (_loadLibraryExplicit && libInfo.LibraryHandle != nint.Zero)
                    NativeLibrary.Free(libInfo.LibraryHandle);

                libInfo.Loaded = false;
                libInfo.LibraryHandle = nint.Zero;
                libInfo.LoadedLibraryPath = string.Empty;
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Failed to unload library '{libInfo.LibraryName}'", ex);
            }
        }

        _loadedLibraries.Clear();
        DelegateFactory.Clear();
    }

    /// <summary>
    /// Loads a single native library asynchronously.
    /// </summary>
    /// <param name="libraryInfo">The library definition.</param>
    /// <param name="platform">The target platform.</param>
    /// <param name="bitness">The target bitness.</param>
    /// <returns>A task representing the loading operation.</returns>
    private async Task LoadLibraryAsync(LibraryDefinition libraryInfo, Platform platform, Bitness bitness)
    {
        if (libraryInfo.Loaded) return; // Don't load if already loaded
        // Check the condition if it exists
        if (libraryInfo.Condition != null && !libraryInfo.Condition())
        {
            Log(LogLevel.Info, $"Skipping library '{libraryInfo.LibraryName}' because the condition was not met.");
            return; // Skip loading if the condition is false
        }

        var libraryPath = await ResolveLibraryPathAsync(libraryInfo);
        if (string.IsNullOrEmpty(libraryPath))
        {
            Log(LogLevel.Error, $"Native library '{libraryInfo.LibraryName}' not found.");
            throw new MissingLibraryException(
                $"Native library '{libraryInfo.LibraryName}' not found.");
        }

        // Dependency Loading
        if (libraryInfo.Dependencies != null)
        {
            foreach (var (depName, depVersion) in libraryInfo.Dependencies)
            {
                await LoadDependencyAsync(depName, depVersion, platform, bitness);
            }
        }

        // Duplicate Loading Prevention
        if (_loadedLibraryPaths.Contains(libraryPath))
        {
            Log(LogLevel.Warning,
                $"Library '{libraryInfo.LibraryName}' already loaded from path '{libraryPath}'. Skipping.");
            libraryInfo.Loaded = true;
            return;
        }

        if (_loadLibraryExplicit)
        {
            libraryInfo.LibraryHandle = LoadNativeLibraryExplicitly(libraryPath);
            libraryInfo.LoadedLibraryPath = libraryPath;

            if (!_loadedLibraries.TryGetValue(Tuple.Create(platform, bitness), out var libraryInfos))
            {
                libraryInfos = new List<LibraryDefinition>();
                _loadedLibraries.Add(Tuple.Create(platform, bitness), libraryInfos);
            }

            libraryInfos.Add(libraryInfo);
            _loadedLibraryPaths.Add(libraryPath);
        }
        else
        {
            libraryInfo.LoadedLibraryPath = libraryPath;
            _loadedLibraryPaths.Add(libraryPath);
        }

        libraryInfo.Loaded = true;
    }

    /// <summary>
    /// Loads a single dependency asynchronously.
    /// </summary>
    /// <param name="depName">The dependency name.</param>
    /// <param name="depVersion">The dependency version.</param>
    /// <param name="platform">The target platform.</param>
    /// <param name="bitness">The target bitness.</param>
    /// <returns>A task representing the loading operation.</returns>
    private async Task LoadDependencyAsync(string depName, string depVersion, Platform platform, Bitness bitness)
    {
        if (!Dependencies.TryGetValue(Tuple.Create(platform, bitness), out var dependencyList))
        {
            Log(LogLevel.Error, $"No dependencies defined for platform '{platform}' and bitness '{bitness}'.");
            throw new MissingLibraryException(
                $"No dependencies defined for platform '{platform}' and bitness '{bitness}'.");
        }

        // Find matching dependency, considering version
        var dependency = dependencyList.FirstOrDefault(depInfo =>
            depInfo.LibraryName == GetPlatformSpecificName(depName, platform) &&
            (string.IsNullOrEmpty(depVersion) || depInfo.Version.ToString() == depVersion));

        if (dependency == null)
        {
            throw new MissingLibraryException($"Dependency '{depName}' (version '{depVersion}') not found.");
        }

        // Handle remote dependencies:
        if (!string.IsNullOrEmpty(dependency.RemoteUrl))
        {
            var dependencyPath = await DownloadAndExtractLibraryAsync(dependency.LibraryName, dependency.RemoteUrl,
                dependency.RemoteFileName);
            if (string.IsNullOrEmpty(dependencyPath))
            {
                throw new MissingLibraryException($"Failed to download and extract dependency '{depName}'.");
            }

            dependency.CustomPath = dependencyPath;
        }

        // Use LoadLibrary to handle the dependency's dependencies recursively and loading
        await LoadLibraryAsync(dependency, platform, bitness);
    }
    
    /// <summary>
    /// Resolves the path to a native library.
    /// </summary>
    /// <param name="libraryInfo">The library definition.</param>
    /// <returns>A task that returns the path to the library, or an empty string if not found.</returns>
    private async Task<string> ResolveLibraryPathAsync(LibraryDefinition libraryInfo)
    {
        if (!string.IsNullOrEmpty(libraryInfo.RemoteUrl))
        {
            // Check if the library has already been downloaded
            return _downloadedFiles.TryGetValue(libraryInfo.RemoteUrl, out var value)
                ? value
                : // Return the local path if already downloaded
                await DownloadAndExtractLibraryAsync(libraryInfo.LibraryName, libraryInfo.RemoteUrl,
                    libraryInfo.RemoteFileName);
        }

        if (!string.IsNullOrEmpty(libraryInfo.CustomPath))
        {
            // Check if custom path refers to embedded resource
            if (libraryInfo.CustomPath.StartsWith("embedded:"))
            {
                var resourcePath = libraryInfo.CustomPath["embedded:".Length..];
                return ExtractLibraryFromResources(libraryInfo.LibraryName, resourcePath);
            }

            // Treat custom path as a direct path or relative to the target directory
            var path = Path.IsPathRooted(libraryInfo.CustomPath)
                ? libraryInfo.CustomPath
                : Path.Combine(_targetDirectory, libraryInfo.CustomPath);

            if (File.Exists(path))
                return path;

            Log(LogLevel.Warning,
                $"Custom path '{libraryInfo.CustomPath}' for '{libraryInfo.LibraryName}' does not exist.");
        }

        // Fallback: Search in standard search paths
        return FindLibraryInSearchPaths(libraryInfo.LibraryName);
    }

    /// <summary>
    /// Downloads and extracts a native library from a remote URL.
    /// </summary>
    /// <param name="libraryName">The name of the library.</param>
    /// <param name="remoteUrl">The URL to download the library from.</param>
    /// <param name="remoteFileName">The file name of the remote library (optional).</param>
    /// <returns>A task that returns the path to the downloaded library, or an empty string if the download or extraction fails.</returns>
    private async Task<string> DownloadAndExtractLibraryAsync(string libraryName, string remoteUrl,
        string? remoteFileName)
    {
        try
        {
            var httpClient = new HttpClient();
            var response =
                await httpClient.GetAsync(remoteUrl, HttpCompletionOption.ResponseHeadersRead, _cancellationToken);
            response.EnsureSuccessStatusCode();

            var contentLength = response.Content.Headers.ContentLength ?? 0;
            var buffer = new byte[8192];
            long receivedBytes = 0;

            var tempDir = Path.Combine(Path.GetTempPath(), "LibLoaderRemote");
            Directory.CreateDirectory(tempDir);

            // Always preserve the original extension from the URL for downloaded file
            var originalExtension = Path.GetExtension(remoteUrl);
            var downloadFileName = remoteFileName ?? libraryName;

            // If remoteFileName doesn't match the URL extension, use URL extension for download
            var downloadPath = Path.Combine(tempDir,
                Path.GetExtension(downloadFileName).Equals(originalExtension, StringComparison.OrdinalIgnoreCase)
                    ? downloadFileName
                    : Path.ChangeExtension(downloadFileName, originalExtension));

            // Save the downloaded file
            await using (var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write))
            await using (var contentStream = await response.Content.ReadAsStreamAsync(_cancellationToken))
            {
                int bytesRead;
                while ((bytesRead = await contentStream.ReadAsync(buffer, _cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), _cancellationToken);
                    receivedBytes += bytesRead;
                    var downloadProgress = (float)receivedBytes / contentLength;
                    _progress?.Report(_overallProgress + (downloadProgress / _totalLibrariesToLoad));
                }
            }

            _downloadedFiles[remoteUrl] = downloadPath;

            // Final path for the extracted/renamed library
            var finalLibraryPath = Path.Combine(tempDir, remoteFileName ?? libraryName);

            // Handle archives
            if (originalExtension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                await using var archiveStream = File.OpenRead(downloadPath);
                using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read);

                // Look for the library file in the archive
                var libraryFile = archive.Entries.FirstOrDefault(e =>
                    Path.GetFileName(e.FullName)
                        .Equals(remoteFileName ?? libraryName, StringComparison.OrdinalIgnoreCase));

                if (libraryFile == null)
                    throw new MissingLibraryException(
                        $"Library '{remoteFileName ?? libraryName}' not found in archive.");

                // Extract to the final path
                libraryFile.ExtractToFile(finalLibraryPath, true);
                return finalLibraryPath;
            }

            // If not an archive, just return the download path
            return downloadPath;
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"Failed to download library '{libraryName}' from '{remoteUrl}'", ex);
            return string.Empty;
        }
    }

    /// <summary>
    /// Gets the platform of the current operating system.
    /// </summary>
    /// <returns>The platform of the current operating system.</returns>
    private static Platform GetPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return Platform.Windows;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return Platform.Linux;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return Platform.MacOs;

        if (OperatingSystem.IsAndroid()) return Platform.Android;
        if (OperatingSystem.IsIOS()) return Platform.Ios;

        throw new UnsupportedPlatformException("Unsupported operating system.");
    }

    /// <summary>
    /// Gets the bitness of the current architecture.
    /// </summary>
    /// <returns>The bitness of the current architecture.</returns>
    private static Bitness GetBitness()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X86 => Bitness.X86,
            Architecture.X64 => Bitness.X64,
            Architecture.Arm => Bitness.Arm,
            Architecture.Arm64 => Bitness.Arm64,
            _ => throw new UnsupportedArchitectureException(
                $"Unsupported architecture: {RuntimeInformation.ProcessArchitecture}")
        };
    }

    /// <summary>
    /// Extracts a native library from embedded resources.
    /// </summary>
    /// <param name="libraryName">The name of the library.</param>
    /// <param name="embeddedResourcePath">The path to the embedded resource.</param>
    /// <returns>The path to the extracted library.</returns>
    private string ExtractLibraryFromResources(string libraryName, string embeddedResourcePath)
    {
        var assembly = Assembly.GetEntryAssembly();
        using var resourceStream = assembly?.GetManifestResourceStream(embeddedResourcePath);
        if (resourceStream == null)
            throw new MissingLibraryException(
                $"Native library '{libraryName}' not found in embedded resources at path '{embeddedResourcePath}'.");

        Directory.CreateDirectory(_targetDirectory);
        var libraryPath = Path.Combine(_targetDirectory, libraryName);

        using var fileStream = new FileStream(libraryPath, FileMode.Create, FileAccess.Write);
        resourceStream.CopyTo(fileStream);

        return libraryPath;
    }

    /// <summary>
    /// Loads a native library explicitly.
    /// </summary>
    /// <param name="libraryPath">The path to the library.</param>
    /// <returns>The handle to the library.</returns>
    private nint LoadNativeLibraryExplicitly(string libraryPath)
    {
        if (!File.Exists(libraryPath))
            throw new MissingLibraryException($"Native library '{libraryPath}' not found.");

        var handle = NativeLibrary.Load(libraryPath);
        if (handle == nint.Zero)
            throw new LibraryLoadException($"Failed to load library '{libraryPath}'.");

        return handle; // Return the handle
    }

    /// <summary>
    /// Searches for a native library in the defined search paths.
    /// </summary>
    /// <param name="libraryName">The name of the library to search for.</param>
    /// <returns>The path to the library if found, or an empty string if not found.</returns>
    private string FindLibraryInSearchPaths(string libraryName)
    {
        var allSearchPaths = new List<string>(_searchPaths) { AppDomain.CurrentDomain.BaseDirectory };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            allSearchPaths.Add(Environment.GetEnvironmentVariable("PATH")!);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            allSearchPaths.Add(Environment.GetEnvironmentVariable("LD_LIBRARY_PATH")!);
            allSearchPaths.Add("/usr/local/lib");
            allSearchPaths.Add("/usr/lib");
        }

        foreach (var searchPath in allSearchPaths)
        {
            if (string.IsNullOrEmpty(searchPath)) continue;
            foreach (var path in searchPath.Split(Path.PathSeparator))
            {
                var libraryPath = Path.Combine(path, libraryName);
                if (File.Exists(libraryPath))
                    return libraryPath;
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Gets the platform-specific name for a library.
    /// </summary>
    /// <param name="libraryName">The base name of the library.</param>
    /// <param name="platform">The target platform.</param>
    /// <returns>The platform-specific name of the library.</returns>
    public static string GetPlatformSpecificName(string libraryName, Platform platform)
    {
        string extension;
        var prefix = string.Empty;

        switch (platform)
        {
            case Platform.Windows:
                extension = ".dll";
                break;
            case Platform.Linux:
                prefix = "lib";
                extension = ".so";
                break;
            case Platform.MacOs:
                prefix = "lib";
                extension = ".dylib";
                break;
            case Platform.Android:
                prefix = "lib";
                extension = ".so";
                break;
            case Platform.Ios:
                prefix = "lib";
                extension = ".a"; // Or .dylib, depending on the library type
                break;
            default: throw new UnsupportedPlatformException($"Unsupported Platform {platform}");
        }

        return $"{prefix}{libraryName}{extension}";
    }

    
    /// <summary>
    /// Calls a native function with the specified name in the specified library.
    /// </summary>
    /// <typeparam name="T">The return type of the native function.</typeparam>
    /// <param name="libraryName">The name of the library containing the function.</param>
    /// <param name="functionName">The name of the native function to call.</param>
    /// <param name="arguments">The arguments to pass to the native function.</param>
    /// <returns>The return value of the native function, cast to the specified type T.</returns>
    public T Call<T>(string libraryName, string functionName, params object[] arguments)
    {
        return (T)Call(typeof(T), libraryName, functionName, arguments)!;
    }
    
    /// <summary>
    /// Calls a native function with the specified name in the specified library, with a void return type.
    /// </summary>
    /// <param name="libraryName">The name of the library containing the function.</param>
    /// <param name="functionName">The name of the native function to call.</param>
    /// <param name="arguments">The arguments to pass to the native function.</param>
    public void Call(string libraryName, string functionName, params object[] arguments)
    {
        Call(typeof(void), libraryName, functionName, arguments);
    }

    /// <summary>
    /// Calls a native function with the specified name in the specified library.
    /// </summary>
    /// <param name="returnType">The return type of the native function.</param>
    /// <param name="libraryName">The name of the library containing the function.</param>
    /// <param name="functionName">The name of the native function to call.</param>
    /// <param name="arguments">The arguments to pass to the native function.</param>
    /// <returns>The return value of the native function, cast to the specified returnType.</returns>
    public object? Call(Type returnType, string libraryName, string functionName, params object[] arguments)
    {
        var libraryInfo = Libraries.FirstOrDefault(lib => lib.LibraryName.Contains(libraryName));
        if (libraryInfo is not { Loaded: true })
        {
            throw new MissingLibraryException($"Library '{libraryName}' is not loaded.");
        }

        if (!libraryInfo.NativeFunctions.TryGetValue(functionName, out var functionDefinition))
        {
            throw new MissingFunctionException(
                $"Native library '{libraryName}' does not contain function '{functionName}'.");
        }

        // Invoke and handle return value
        var result = functionDefinition.Delegate.DynamicInvoke(arguments);

        if (returnType == typeof(void))
        {
            return null;
        }

        // Handle potential conversion issues. If the result can't be cast
        try
        {
            return Convert.ChangeType(result, returnType);
        }
        catch (InvalidCastException ex)
        {
            throw new InvalidCastException(
                $"The native function '{functionName}' returned a value that could not be converted to '{returnType}'.",
                ex);
        }
    }
    
    /// <summary>
    /// Internally calls a native function, being called from delegates.
    /// </summary>
    /// <param name="functionDefinition">The definition of the native function.</param>
    /// <param name="arguments">The arguments to pass to the function.</param>
    /// <returns>The result of the function call.</returns>
    private object? InternalCall(NativeFunctionDefinition functionDefinition, object[] arguments)
    {
        if (!_loadLibraryExplicit && functionDefinition.Library.LibraryHandle == nint.Zero)
            functionDefinition.Library.LibraryHandle = NativeLibrary.Load(functionDefinition.Library.LoadedLibraryPath);

        var functionAddress = NativeLibrary.GetExport(functionDefinition.Library.LibraryHandle, functionDefinition.NativeName);
        if (functionAddress == nint.Zero)
        {
            throw new MissingFunctionException($"Function '{functionDefinition.NativeName}' not found in library.");
        }

        // Get or create the appropriate delegate type
        var delegateType = DelegateFactory.GetOrCreateDelegateType(functionDefinition);
        var delegateInstance = Marshal.GetDelegateForFunctionPointer(functionAddress, delegateType);
        var marshaledArguments = ParameterMarshaler.MarshalParameters(functionDefinition.Parameters, arguments);

        try
        {
            return delegateInstance.DynamicInvoke(marshaledArguments);
        }
        catch (TargetInvocationException tie)
        {
            throw tie.InnerException ?? tie;
        }
        finally
        {
            ParameterMarshaler.FreeMarshaledStrings(marshaledArguments);
        }
    }
}