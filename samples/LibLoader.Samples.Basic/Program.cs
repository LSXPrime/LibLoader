using System.Runtime.InteropServices;

namespace LibLoader.Samples.Basic;

internal static class Program
{
    private static async Task Main()
    {
        Console.WriteLine("1 - Load a library from a local file with dependencies");
        Console.WriteLine("2 - Load a library from embedded resources");
        Console.WriteLine("3 - Load a library from a remote URL with progress reporting");
        Console.WriteLine("4 - Load a library from a local file & Test custom data structures");
        Console.WriteLine("Choose a scenario (1-3):");
        var scenario = int.Parse(Console.ReadKey().KeyChar.ToString());

        switch (scenario)
        {
            case 1:
                await LoadLibraryFromLocalFile();
                break;
            case 2:
                await LoadLibraryFromEmbeddedResources();
                break;
            case 3:
                await LoadLibraryFromRemoteUrl();
                break;
            case 4:
                await TestCustomDataStructures();
                break;
            default:
                Console.WriteLine("Invalid scenario selected.");
                break;
        }


        Console.ReadKey();
    }

  
    
    
    private static async Task LoadLibraryFromLocalFile()
    {
        // Example 1: Loading a library from a local file with dependencies
        var loader = LibLoader.Instance
            .WithTargetDirectory(string.Empty)
            .WithImplicitLoading()
            .ForPlatform(Platform.Windows, Bitness.X64)
            .WithLibrary()
                .WithName("libMyNativeLib", usePlatformSpecificName: true) // Use platform-specific naming
                .FromCustomPath("./libMyNativeLib.dll") // Path to your local library file
                .WithDependencies(("DependencyLib", "0.0"))
                .WithNativeFunction()
                    .Named("my_function") // Native function name
                    .WithCallingName("MyFunction") // Optional C# calling name
                    .WithReturnType<int>()
                    .WithParameter<int>("value")
                .Add()
            .Add()
            .WithDependency()
                .WithName("DependencyLib")
                .FromCustomPath("./DependencyLib.dll")
                .Add()
            .Build();

        await loader.LoadAsync();
        var result = loader.Call<int>("libMyNativeLib", "MyFunction", 10);
        Console.WriteLine($"Result from MyFunction: {result}");
        loader.Unload();
    }

    private static async Task LoadLibraryFromEmbeddedResources()
    {
        // Example 2: Loading a library from embedded resources
        var resourceLoader = LibLoader.Instance
            .ForPlatform(Platform.Windows, Bitness.X64)
            .WithLibrary()
                .WithName("libMyNativeLib", usePlatformSpecificName: true)
                .FromCustomPath("embedded:LibLoader.Samples.Basic.libMyNativeLib.dll") // Path to embedded resource
                .WithNativeFunction()
                    .Named("embedded_function")
                    .WithCallingName("EmbeddedFunction")
                    .WithReturnType(typeof(void))
                    .Add()
            .Add()
            .Build();

        await resourceLoader.LoadAsync();
        resourceLoader.Call(typeof(void), "libMyNativeLib", "EmbeddedFunction");
        Console.WriteLine("Embedded function called successfully.");
        resourceLoader.Unload();
    }

    private static async Task LoadLibraryFromRemoteUrl()
    {
        // Example 3: Loading from a remote URL with progress reporting
        var localServer = new LocalFileServer("LibLoader.Samples.Basic.libMyNativeLib.dll", 75);
        _ = localServer.StartAsync();
        var progress = new Progress<float>(p => Console.WriteLine($"Download Progress: {p * 100:F2}%"));
        var remoteLoader = LibLoader.Instance
            .WithProgress(progress)
            .ForPlatform(Platform.Windows, Bitness.X64)
            .WithLibrary()
                .WithName("libMyNativeLib", usePlatformSpecificName: true)
                .FromRemoteUrl($"http://127.0.0.1:{localServer.Port}", "libMyNativeLib.dll")
                .WithNativeFunction()
                    .Named("remote_function")
                    .WithCallingName("RemoteFunction")
                    .WithReturnType<string>()
                    .Add()
            .Add()
            .Build();

        await remoteLoader.LoadAsync();
        var remoteResult = remoteLoader.Call<string>("libMyNativeLib", "RemoteFunction");
        Console.WriteLine($"Remote Function Result: {remoteResult}");

        remoteLoader.Unload();
        localServer.Stop();
    }
    
    private static async Task TestCustomDataStructures()
    {
        // Example 4: Loading a library from a local file & Test custom data structures
        var loader = LibLoader.Instance
            .WithTargetDirectory(string.Empty)
            .WithImplicitLoading()
            .ForPlatform(Platform.Windows, Bitness.X64)
            .WithLibrary()
                .WithName("libMyNativeLib")
                .FromCustomPath("./libMyNativeLib.dll")
                .WithNativeFunction()
                    .Named("get_data")
                    .WithCallingName("GetData")
                    .WithReturnType<MyData>()
                    .Add()
                .WithNativeFunction()
                    .Named("process_data")
                    .WithCallingName("ProcessData")
                    .WithReturnType(typeof(void))
                    .WithParameter<MyData>("data")
                    .Add()
                .WithNativeFunction()
                    .Named("modify_data")
                    .WithCallingName("ModifyData")
                    .WithReturnType<MyData>()
                    .WithParameter<MyData>("data")
                    .Add()
                // Test nested structs
                .WithNativeFunction()
                    .Named("get_nested_data")
                    .WithCallingName("GetNestedData")
                    .WithReturnType<NestedData>()
                    .Add()
                // Test arrays of structs
                .WithNativeFunction()
                    .Named("get_array_data")
                    .WithCallingName("GetArrayData")
                    .WithReturnType<ArrayData>()
                   .Add()
                .WithNativeFunction()
                    .Named("process_array_data")
                    .WithCallingName("ProcessArrayData")
                    .WithReturnType(typeof(void))
                    .WithParameter<ArrayData>("data")
                    .Add()
                .Add()
            .Build();

        await loader.LoadAsync();


        var data = loader.Call<MyData>("libMyNativeLib", "GetData");
        Console.WriteLine($"Received data: Value = {data.value}, Message = {data.message}");


        var modifiedData = loader.Call<MyData>("libMyNativeLib", "ModifyData", data);
        Console.WriteLine($"Modified data: Value = {modifiedData.value}, Message = {modifiedData.message}");

        // Test processing data
        loader.Call("libMyNativeLib", "ProcessData", data);
        
        // Nested Structs test:
        var nestedData = loader.Call<NestedData>("libMyNativeLib", "GetNestedData");
        Console.WriteLine($"Nested Data: Id={nestedData.id}, Name={nestedData.name}, " +
                          $"InnerValue={nestedData.inner_data.value}, InnerMessage={nestedData.inner_data.message}");

        // Array of Structs test
        var arrayData = loader.Call<ArrayData>("libMyNativeLib", "GetArrayData");
        Console.WriteLine($"Array Data Count: {arrayData.count}");
        for (var i = 0; i < arrayData.count; i++)
        {
            Console.WriteLine($"  Data[{i}]: Value = {arrayData.data[i].value}, Message = {arrayData.data[i].message}");
        }

        loader.Call("libMyNativeLib", "ProcessArrayData", arrayData);

        loader.Unload();
    }

}


// Define the MyData struct to match the C definition
public struct MyData
{
    public int value;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] // Important for string marshaling
    public string message;
}

public struct NestedData
{
    public int id;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string name;
    public MyData inner_data;
}


public struct ArrayData
{
    public int count;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
    public MyData[] data;
}



// The C code for the MyNativeLib library that used in the sample
/*
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

// Custom data structure
typedef struct MyData {
    int value;
    char message[256];  // Fixed size buffer to match C# marshaling
} MyData;


// Nested struct example
typedef struct NestedData {
    int id;
    char name[64];
    MyData inner_data;
} NestedData;


// Array example
typedef struct ArrayData {
    int count;
    MyData data[10]; // Fixed size array
} ArrayData;


// MyNativeLib functions
__declspec(dllexport) int my_function(int value) {
    printf("Hello from MyNativeLib! Value received: %d\n", value);
    return value * 2;
}

// DependencyLib functions
__declspec(dllexport) void dependency_function() {
    printf("Hello from DependencyLib!\n");
}

// EmbeddedLib functions
__declspec(dllexport) void embedded_function() {
    printf("Hello from EmbeddedLib!\n");
}

// RemoteLib functions
__declspec(dllexport) const char *remote_function() {
    printf("Hello from RemoteLib!\n");
    return "Message from remote library";
}

// New functions with custom data structures
__declspec(dllexport) MyData get_data() {
    MyData data;
    data.value = 42;
    strncpy_s(data.message, sizeof(data.message), "Data from get_data()", _TRUNCATE);
    printf("get_data() called\n");
    return data;
}

__declspec(dllexport) void process_data(const MyData data) {
    printf("process_data() called. Value: %d, Message: %s\n", data.value, data.message);
}

__declspec(dllexport) MyData modify_data(const MyData data) {
    MyData modified_data;
    modified_data.value = data.value * 2;

    strcat_s(modified_data.message, sizeof(modified_data.message), " (modified)");


    printf("modify_data() called\n");
    return modified_data;
}


__declspec(dllexport) NestedData get_nested_data() {
    NestedData nested;
    nested.id = 1;
    strncpy_s(nested.name, sizeof(nested.name), "Nested Data", _TRUNCATE);
    nested.inner_data = get_data();
    printf("get_nested_data() called\n");
    return nested;
}

__declspec(dllexport) ArrayData get_array_data() {
    ArrayData array;
    array.count = 3;
    for (int i = 0; i < array.count; i++) {
        array.data[i] = get_data();
        array.data[i].value += i;
    }
    printf("get_array_data() called\n");
    return array;
}

// Function to process an array of MyData
__declspec(dllexport) void process_array_data(const ArrayData data) {
    printf("process_array_data() called\n");
    for (int i = 0; i < data.count; i++) {
        printf("  Data[%d]: Value = %d, Message = %s\n", i, data.data[i].value, data.data[i].message);
    }
}
*/