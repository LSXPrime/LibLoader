using System.Runtime.InteropServices;

namespace LibLoader.Interfaces;

/// <summary>
/// Interface for building definitions for native functions within a library.
/// </summary>
public interface INativeFunctionBuilder
{
    /// <summary>
    /// Sets the native name of the function.
    /// </summary>
    /// <param name="nativeName">The native name of the function.</param>
    /// <returns>The INativeFunctionBuilder instance for chaining method calls.</returns>
    INativeFunctionBuilder Named(string nativeName);

    /// <summary>
    /// Sets the calling name of the function (used in managed code).
    /// </summary>
    /// <param name="callingName">The calling name of the function.</param>
    /// <returns>The INativeFunctionBuilder instance for chaining method calls.</returns>
    INativeFunctionBuilder WithCallingName(string callingName);

    /// <summary>
    /// Sets the calling convention used for the function.
    /// </summary>
    /// <param name="callingConvention">The calling convention.</param>
    /// <returns>The INativeFunctionBuilder instance for chaining method calls.</returns>
    INativeFunctionBuilder WithCallingConvention(CallingConvention callingConvention);

    /// <summary>
    /// Sets the return type of the function using a generic type parameter.
    /// </summary>
    /// <typeparam name="T">The return type of the function.</typeparam>
    /// <returns>The INativeFunctionBuilder instance for chaining method calls.</returns>
    INativeFunctionBuilder WithReturnType<T>();

    /// <summary>
    /// Sets the return type of the function using a Type object.
    /// </summary>
    /// <param name="returnType">The return type of the function.</param>
    /// <returns>The INativeFunctionBuilder instance for chaining method calls.</returns>
    INativeFunctionBuilder WithReturnType(Type returnType);

    /// <summary>
    /// Adds a parameter to the function using a generic type parameter.
    /// </summary>
    /// <typeparam name="T">The type of the parameter.</typeparam>
    /// <param name="name">The name of the parameter.</param>
    /// <returns>The INativeFunctionBuilder instance for chaining method calls.</returns>
    INativeFunctionBuilder WithParameter<T>(string name);

    /// <summary>
    /// Adds the native function definition to the library.
    /// </summary>
    /// <returns>The library definition builder instance for chaining method calls.</returns>
    ILibraryDefinitionBuilder Add();
}