# LibLoader: Cross-Platform Native Library Loader

LibLoader is a powerful and versatile library designed to simplify the process of loading and interacting with native libraries (DLLs, SOs, dylibs, etc.) in C# applications. It provides a fluent API, cross-platform compatibility, dependency management, and various loading options, making it easier to integrate native code into your .NET projects.

## Features

* **Cross-Platform Compatibility:**  Leverages .NET's `NativeLibrary` for loading, supporting Windows, Linux, macOS, Android, and iOS.
* **Dependency Management:** Automatically loads dependencies before the main library, handling versioning to ensure compatibility.
* **Flexible Loading:** Load libraries from local files, embedded resources, or remote URLs with progress reporting and cancellation support.
* **Progress Reporting:** Track download and loading progress using `IProgress<float>`.
* **Customizable Logging:** Control how and where log messages are written using a `LogDelegate`.
* **Simplified Function Calling:** Invoke native functions with type safety and automatic marshaling using the intuitive `Call` method.
* **Data Structure Marshaling:** Supports basic types, strings, structs (including nested structs and arrays of structs), and more.
* **Delegate Caching:** Optimizes performance by caching delegate types, reducing overhead on repeated calls.
* **Error Handling:** Provides clear and specific exceptions for various error conditions, such as missing libraries or functions.
* **Fluent API:**  Configure library loading using a clean and readable builder pattern, making configuration easy and maintainable.
* **Singleton Instance:** Easily access the LibLoader instance via `LibLoader.Instance` for a streamlined workflow.
* **Search Paths:**  Specify additional search paths to locate native libraries, simplifying dependency management.
* **Conditional Loading:** Load a library only if a condition is met, allowing for loading libraries based on runtime conditions.
* **Implicit Loading:** Optionally enable implicit loading to avoid loading libraries on startup. Libraries will be loaded on the first call to a native function.

## Installation

Install LibLoader using the NuGet Package Manager or the .NET CLI:

```
Install-Package LibLoader
```

## Usage

### Basic Example: Loading a Local Library

```csharp
using LibLoader;
using System.Runtime.InteropServices; // For StructLayout

// Define a struct matching the native library's data structure
[StructLayout(LayoutKind.Sequential)]
public struct MyData
{
    public int Value;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Message; 
}

public async Task Example()
{
    var loader = LibLoader.Instance
        .ForPlatform(Platform.Windows, Bitness.X64) // Or your target platform and bitness
        .WithLibrary()
            .WithName("MyNativeLibrary", usePlatformSpecificName:true)  // Library name (e.g., libMyNativeLibrary.so, MyNativeLibrary.dll)
            .FromCustomPath("./MyNativeLibrary.dll") // Path to the library file
            .WithNativeFunction()
                .Named("my_native_function")
                .WithReturnType<int>()
                .WithParameter<string>("input")
                .WithParameter<MyData>("data")
            .Add()
        .Add()
        .Build();

    await loader.LoadAsync();

    var myData = new MyData { Value = 42, Message = "Some data" };
    var result = loader.Call<int>("MyNativeLibrary", "my_native_function", "Hello from C#", myData);

    Console.WriteLine($"Result from native function: {result}");

    loader.Unload(); // Unload the library when finished
}
```



### Advanced Scenarios

#### Loading from Embedded Resources

```csharp
// ... within your LibLoader configuration ...
.WithLibrary()
    .WithName("MyEmbeddedLibrary")
    .FromCustomPath("embedded:MyNamespace.MyEmbeddedLibrary.dll") // Path to the embedded resource.  MyNamespace.MyEmbeddedLibrary.dll should exist as an Embedded Resource in your project
    // ... Add your native functions
.Add();
```


#### Loading from Remote URL with Progress and Cancellation

```csharp
using System.Threading;

// ...

var cts = new CancellationTokenSource();
var progress = new Progress<float>(p => Console.WriteLine($"Download progress: {p * 100:F2}%"));

var loader = LibLoader.Instance
   .WithProgress(progress)
   .WithCancellationToken(cts.Token) // Pass the cancellation token
   .ForPlatform(Platform.Windows, Bitness.X64)
    .WithLibrary()
        .WithName("MyRemoteLibrary")
        .FromRemoteUrl("https://example.com/MyRemoteLibrary.zip", "MyRemoteLibrary.dll")  // URL and filename (if inside an archive)
        // ... native functions
        .Add()
.Build();


try
{
    await loader.LoadAsync();
    // ... call native functions ...
}
catch (OperationCanceledException)
{
    Console.WriteLine("Library loading cancelled.");
}
finally
{
    loader.Unload();
    cts.Dispose(); // Dispose of cancellation token source
}
```


#### Specifying Dependencies (with versioning)

```csharp
// ...
.WithDependency()  // Define the dependencies themselves
    .WithName("DependencyA")
    .WithVersion("1.2.3")
    .FromCustomPath("./path/to/DependencyA.dll") // Or a remote URL, etc.
    .Add()
.WithDependency()
    .WithName("DependencyB")
    .FromRemoteUrl("https://example.com/DependencyB.so")
    .Add();
.WithLibrary()
    .WithName("MyLibrary")
    .WithDependencies(("DependencyA", "1.2.3"), ("DependencyB", null)) // Dependency name and optional version
    // ... native functions
    .Add()
```

#### Conditional Loading (Platform-Specific Code)

```csharp
.WithLibrary()
    .WithName("MyOptionalLibrary")
    .WithCondition(() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) // Load only on Windows
    // ... Windows-specific native functions ...
    .Add()
.WithLibrary()
    .WithName("MyLinuxLibrary")
    .WithCondition(() => RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) // Load only on Linux
    // ... Linux-specific native functions ...
    .Add();
```

#### Custom Data Structures (Nested Structs and Arrays)

```csharp
[StructLayout(LayoutKind.Sequential)]
public struct InnerData
{
    public int Id;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Name;
}

[StructLayout(LayoutKind.Sequential)]
public struct MyData
{
    public int Value;
    public InnerData Inner;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)] public int[] ArrayData;
}


// ... within the WithLibrary() builder
.WithNativeFunction()
    .Named("process_complex_data")
    .WithReturnType<bool>()
    .WithParameter<MyData>("data")
    .Add()
    // ... other native functions ...
.Add();

// ... After loading the library
var data = new MyData
{
    Value = 123,
    Inner = new InnerData { Id = 456, Name = "Nested" },
    ArrayData = new int[] { 1, 2, 3, 4, 5 }
};

var result = loader.Call<bool>("MyLibrary", "process_complex_data", data);
Console.WriteLine($"Result: {result}");

```


#### Implicit Loading (Automatic Loading on First Call)


```csharp
// Enable implicit loading to avoid explicitly calling LoadAsync()
var loader = LibLoader.Instance.WithImplicitLoading();
// ... the rest of your LibLoader configuration (platforms, libraries, functions)

//  The libraries are loaded automatically when a native function is called:

var result = loader.Call<int>("MyNativeLibrary", "my_function", 10); 
// ...
```

#### Search Paths (For Dependencies or Non-Standard Locations)

```csharp
// Add search paths for native libraries
var searchPaths = new List<string>();
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    searchPaths.Add(@"C:\Path\To\My\Libraries");
}
else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
{
    searchPaths.Add("/usr/local/lib");
}


var loader = LibLoader.Instance.WithSearchPaths(searchPaths);

// ... rest of the LibLoader configuration
```

## API Documentation

Full API documentation is available in the code comments and through IntelliSense within your IDE. Here's a brief overview:

* **`LibLoader.Instance`:**  The singleton instance of the `LibLoader` class.
* **`ForPlatform(Platform platform, Bitness bitness)`:**  Specifies the target platform and bitness (x86, x64, Arm, Arm64).
* **`WithLibrary()`:** Starts a builder for defining a native library.
* **`WithName(string libraryName)`:** Sets the name of the native library (without extension).
* **`FromCustomPath(string path)`:** Loads the library from a specific file path.
* **`FromRemoteUrl(string url, string? filename)`:** Downloads and loads the library from a remote URL.  `filename` is optional and specifies the filename within a zip archive.
* **`WithDependencies(params (string name, string version)[] dependencies)`:**  Specifies dependencies for a library.
* **`WithCondition(Func<bool> condition)`:** Loads the library only if the condition is true.
* **`WithNativeFunction()`:** Starts a builder for defining a native function.
* **`Named(string nativeName)`:** Sets the name of the native function.
* **`WithCallingName(string callingName)`:**  Specifies the C# name for the function (optional, defaults to the native name).
* **`WithReturnType<T>()` / `WithReturnType(Type returnType)`:** Sets the return type of the function.
* **`WithParameter<T>(string name)`:** Adds a parameter to the function signature.
* **`Add()`:** Adds the defined library or function to the loader.
* **`Build()`:** Finalizes the configuration and returns the `LibLoader` instance.
* **`LoadAsync()`:** Asynchronously loads the configured libraries. (Not necessary with implicit loading.)
* **`Call<T>(string libraryName, string functionName, params object[] arguments)`:** Calls a native function with the specified name and arguments, returning a value of type T.  Overloads exist for `void` return types and to specify the return type using a `Type` object.
* **`Unload()`:** Unloads all loaded libraries.


## Diagram/Architecture

```
+---------------------+     +---------------------+     +---------------------+
|    Application     | ====> |    LibLoader      | ====> |  Native Library  |
+---------------------+     +---------------------+     +---------------------+
                         (1) Configure               (2) Load & Call
                                                      Functions
```


1. **Configure:** The application configures `LibLoader` with information about the native libraries to load (name, location, dependencies, functions).
2. **Load & Call Functions:** `LibLoader` loads the specified native libraries and provides a way for the application to call functions within those libraries.  It handles dependency resolution, platform-specific loading, and data marshaling.


## Error Handling

LibLoader throws the following exceptions:

* **`MissingLibraryException`:** Thrown when a required native library cannot be found.
* **`MissingFunctionException`:** Thrown when a native function cannot be found within a loaded library.
* **`LibraryLoadException`:** Thrown when an error occurs while loading a native library (e.g., invalid format, dependencies not met).
* **`NativeFunctionCallException`:**  Thrown when an error occurs during a native function call (e.g., incorrect arguments, access violation).
* `UnsupportedPlatformException`: Thrown when the current platform is not supported.
* `UnsupportedArchitectureException`: Thrown when the current process architecture is not supported.

## Contributing

Contributions are welcome!  Please follow these guidelines:

* Fork the repository.
* Create a new branch for your feature or bug fix.
* Write clear and concise code with comments.
* Submit a pull request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE.md) file for details.
