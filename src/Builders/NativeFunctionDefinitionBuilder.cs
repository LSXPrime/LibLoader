using System.Linq.Expressions;
using System.Runtime.InteropServices;
using LibLoader.Interfaces;
using LibLoader.Models;
using LibLoader.Native;

namespace LibLoader.Builders;

/// <summary>
/// Builds a definition for a native function within a library.
/// </summary>
public class NativeFunctionBuilder(ILibraryDefinitionBuilder builder, LibraryDefinition library) : INativeFunctionBuilder
{
    private string _nativeName = string.Empty;
    private string _callingName = string.Empty;
    private CallingConvention _callingConvention = CallingConvention.Cdecl;
    private Type _returnType = typeof(void);
    private readonly List<NativeFunctionParameter> _parameters = [];

    /// <summary>
    /// Sets the native name of the function.
    /// </summary>
    /// <param name="nativeName">The native name of the function.</param>
    /// <returns>The NativeFunctionBuilder instance for chaining method calls.</returns>
    public INativeFunctionBuilder Named(string nativeName)
    {
        _nativeName = nativeName;
        return this;
    }

    /// <summary>
    /// Sets the calling name of the function (used in managed code).
    /// </summary>
    /// <param name="callingName">The calling name of the function.</param>
    /// <returns>The NativeFunctionBuilder instance for chaining method calls.</returns>
    public INativeFunctionBuilder WithCallingName(string callingName)
    {
        _callingName = callingName;
        return this;
    }
    
    /// <summary>
    /// Sets the calling convention used for the function.
    /// </summary>
    /// <param name="callingConvention">The calling convention, defaults to <see cref="CallingConvention.Cdecl"/>.</param>
    /// <returns>The NativeFunctionBuilder instance for chaining method calls.</returns>
    public INativeFunctionBuilder WithCallingConvention(CallingConvention callingConvention)
    {
        _callingConvention = callingConvention;
        return this;
    }

    /// <summary>
    /// Sets the return type of the function using a generic type parameter.
    /// </summary>
    /// <typeparam name="T">The return type of the function.</typeparam>
    /// <returns>The NativeFunctionBuilder instance for chaining method calls.</returns>
    public INativeFunctionBuilder WithReturnType<T>()
    {
        _returnType = typeof(T);
        return this;
    }

    /// <summary>
    /// Sets the return type of the function using a Type object.
    /// </summary>
    /// <param name="returnType">The return type of the function.</param>
    /// <returns>The NativeFunctionBuilder instance for chaining method calls.</returns>
    public INativeFunctionBuilder WithReturnType(Type returnType)
    {
        _returnType = returnType;
        return this;
    }

    /// <summary>
    /// Adds a parameter to the function using a generic type parameter.
    /// </summary>
    /// <typeparam name="T">The type of the parameter.</typeparam>
    /// <param name="name">The name of the parameter.</param>
    /// <returns>The NativeFunctionBuilder instance for chaining method calls.</returns>
    public INativeFunctionBuilder WithParameter<T>(string name)
    {
        _parameters.Add(new NativeFunctionParameter { Name = name, Type = typeof(T) });
        return this;
    }

    /// <summary>
    /// Adds the native function definition to the library.
    /// </summary>
    /// <returns>The library definition builder instance for chaining method calls.</returns>
    public ILibraryDefinitionBuilder Add()
    {
        if (string.IsNullOrEmpty(_nativeName))
            throw new InvalidOperationException("Native function name cannot be empty.");

        if (string.IsNullOrEmpty(_callingName)) 
            _callingName = _nativeName;
        
        if (library == null)
            throw new InvalidOperationException(
                $"Cannot add native function '{_nativeName}' to the library: Library '{library}' was not found in the LibLoader.");

        // Create the function definition
        var functionDefinition = new NativeFunctionDefinition
        {
            Library = library,
            CallingName = _callingName,
            NativeName = _nativeName,
            CallingConvention = _callingConvention,
            ReturnType = _returnType,
            Parameters = _parameters,
        };

        // Create a wrapper method that matches the expected delegate signature
        var parameterTypes = _parameters.Select(p => p.Type).ToArray();
        var delegateType = Expression.GetDelegateType(parameterTypes.Concat(new[] { _returnType }).ToArray());

        // Create wrapper delegate that will forward the call to InternalCall
        functionDefinition.Delegate = DelegateFactory.CreateWrapperDelegate(functionDefinition, delegateType);
        
        // Store the function definition in the library
        library.NativeFunctions.Add(functionDefinition.NativeName, functionDefinition);
        if (!string.IsNullOrEmpty(functionDefinition.CallingName) && functionDefinition.CallingName != functionDefinition.NativeName)
            library.NativeFunctions.Add(functionDefinition.CallingName, functionDefinition);

        return builder;
    }
    
}