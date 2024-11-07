using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using LibLoader.Models;

namespace LibLoader.Native;

/// <summary>
/// Provides methods for creating and managing dynamic delegate types for native function calls.
/// </summary>
public static class DelegateFactory
{
    private static readonly Dictionary<string, WeakReference<Type>> DynamicDelegateTypes = new();

    /// <summary>
    /// Gets or creates a dynamic delegate type matching the specified native function definition.
    /// </summary>
    /// <param name="functionDefinition">The native function definition.</param>
    /// <returns>The dynamic delegate type.</returns>
    public static Type GetOrCreateDelegateType(NativeFunctionDefinition functionDefinition)
    {
        // Create a unique key for this delegate type signature
        var key =
            $"{functionDefinition.ReturnType.FullName}_{string.Join("_", functionDefinition.Parameters.Select(p => p.Type.FullName))}";

        if (DynamicDelegateTypes.TryGetValue(key, out var weakRef) && weakRef.TryGetTarget(out var existingType))
        {
            return existingType;
        }

        // Create a new delegate type using TypeBuilder
        var assemblyName = new AssemblyName($"DynamicDelegateAssembly_{Guid.NewGuid()}");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("DynamicDelegateModule");

        var typeBuilder = moduleBuilder.DefineType(
            $"DynamicDelegate_{Guid.NewGuid()}",
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class,
            typeof(MulticastDelegate)
        );

        // Add UnmanagedFunctionPointerAttribute
        var ctorBuilder = typeBuilder.DefineConstructor(
            MethodAttributes.RTSpecialName | MethodAttributes.HideBySig | MethodAttributes.Public,
            CallingConventions.Standard,
            [typeof(object), typeof(IntPtr)]
        );

        ctorBuilder.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

        // Define the Invoke method
        var parameterTypes = functionDefinition.Parameters.Select(p => p.Type).ToArray();
        var invokeBuilder = typeBuilder.DefineMethod(
            "Invoke",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
            functionDefinition.ReturnType,
            parameterTypes
        );

        invokeBuilder.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

        // Add UnmanagedFunctionPointer attribute
        var attributeConstructor =
            typeof(UnmanagedFunctionPointerAttribute).GetConstructor([typeof(CallingConvention)]);
        var attributeBuilder = new CustomAttributeBuilder(
            attributeConstructor!,
            [functionDefinition.CallingConvention]
        );
        typeBuilder.SetCustomAttribute(attributeBuilder);

        var delegateType = typeBuilder.CreateType();
        DynamicDelegateTypes[key] = new WeakReference<Type>(delegateType);

        return delegateType;
    }

    /// <summary>
    /// Creates a delegate wrapper for the specified native function definition.
    /// </summary>
    /// <param name="functionDefinition">The native function definition.</param>
    /// <param name="delegateType">The type of the delegate to create.</param>
    /// <returns>A delegate instance that wraps the native function call.</returns>
    public static Delegate CreateWrapperDelegate(NativeFunctionDefinition functionDefinition, Type delegateType)
    {
        var parameters = functionDefinition.Parameters
            .Select((p, i) => Expression.Parameter(p.Type, $"param{i}"))
            .ToArray();

        var argsArrayExpr = Expression.NewArrayInit(
            typeof(object),
            parameters.Select(p => Expression.Convert(p, typeof(object)))
        );

        var internalCallMethod = typeof(LibLoader).GetMethod(
            "InternalCall",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        var callExpr = Expression.Call(
            Expression.Constant(LibLoader.Instance),
            internalCallMethod,
            Expression.Constant(functionDefinition),
            argsArrayExpr
        );

        if (functionDefinition.ReturnType == typeof(void))
        {
            var voidBody = Expression.Block(callExpr, Expression.Empty());
            return Expression.Lambda(delegateType, voidBody, parameters).Compile();
        }

        var convertedResult = Expression.Convert(callExpr, functionDefinition.ReturnType);
        return Expression.Lambda(delegateType, convertedResult, parameters).Compile();
    }
    
    /// <summary>
    /// Clears the cache of dynamic delegate types.
    /// </summary>
    public static void Clear() => DynamicDelegateTypes.Clear();
}