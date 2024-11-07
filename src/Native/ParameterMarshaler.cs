using System.Reflection;
using System.Runtime.InteropServices;
using LibLoader.Models;

namespace LibLoader.Native;

/// <summary>
/// Provides methods for marshalling parameters between managed and native code.
/// </summary>
public static class ParameterMarshaler
{
    private static readonly Dictionary<Type, Func<object, nint>> MarshalConverters = new()
    {
        { typeof(string), obj => Marshal.StringToHGlobalAnsi((string)obj) },
        { typeof(int), obj => (int)obj },
        { typeof(long), obj => (nint)(long)obj },
        { typeof(bool), obj => (bool)obj ? 1 : 0 }
    };
    
    /// <summary>
    /// Frees any memory allocated for marshaled strings in the provided arguments.
    /// </summary>
    /// <param name="args">The arguments to free memory from.</param>
    public static void FreeMarshaledStrings(object[] args)
    {
        foreach (var arg in args)
        {
            if (arg is nint ptr && ptr != nint.Zero)
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
    }

    /// <summary>
    /// Marshals the provided arguments based on the defined native function parameters.
    /// </summary>
    /// <param name="parameters">The native function parameters.</param>
    /// <param name="arguments">The arguments to marshal.</param>
    /// <returns>An array of marshaled arguments ready for native function invocation.</returns>
    public static object[] MarshalParameters(List<NativeFunctionParameter> parameters, object[] arguments)
    {
        if (parameters.Count != arguments.Length)
        {
            throw new ArgumentException(
                $"Incorrect number of arguments. Expected {parameters.Count}, received {arguments.Length}.");
        }

        var marshaledArguments = new object[arguments.Length];
        for (var i = 0; i < arguments.Length; i++)
        {
            var parameter = parameters[i];
            var argument = arguments[i];
            
            // For struct parameters passed by value, we need to copy the struct
            if (parameter.Type.IsValueType && !parameter.Type.IsPrimitive)
            {
                marshaledArguments[i] = argument;
            }
            else
            {
                marshaledArguments[i] = MarshalParameter(parameter, argument);
            }
        }

        return marshaledArguments;
    }

    /// <summary>
    /// Marshals a single parameter based on its type and the provided argument.
    /// </summary>
    /// <param name="parameter">The native function parameter definition.</param>
    /// <param name="argument">The argument to marshal.</param>
    /// <returns>The marshaled argument.</returns>
    private static object MarshalParameter(NativeFunctionParameter parameter, object? argument)
    {
        if (argument == null)
        {
            return nint.Zero;
        }

        var parameterType = parameter.Type;

        // Handle strings
        if (parameterType == typeof(string))
        {
            return Marshal.StringToHGlobalAnsi((string)argument);
        }

        switch (parameterType.IsValueType)
        {
            // Handle struct types differently - pass by value
            case true when !parameterType.IsPrimitive:
                return argument;
            // Handle value types
            case true when IsBlittableType(parameterType):
                return argument;
            case true when parameterType.IsEnum:
                return Convert.ChangeType(argument, Enum.GetUnderlyingType(parameterType));
        }

        // Handle arrays
        if (parameterType.IsArray)
        {
            return MarshalArray(parameter, argument);
        }

        // Handle classes
        if (parameterType.IsClass)
        {
            return MarshalClass(parameter, argument);
        }

        // Default case
        return argument;
    }

    /// <summary>
    /// Marshals an array parameter based on its element type and the provided argument.
    /// </summary>
    /// <param name="parameter">The native function parameter definition.</param>
    /// <param name="argument">The argument to marshal.</param>
    /// <returns>The marshaled argument.</returns>
    private static object MarshalArray(NativeFunctionParameter parameter, object argument)
    {
        var array = (Array)argument;
        var elementType = parameter.Type.GetElementType();
        
        if (elementType == null)
        {
            throw new ArgumentException($"Cannot determine element type for parameter {parameter.Name}");
        }

        // Handle array of primitives
        if (IsBlittableType(elementType))
        {
            var size = Marshal.SizeOf(elementType) * array.Length;
            var ptr = Marshal.AllocHGlobal(size);

            var buffer = new byte[size];

            // Get the raw data into a byte array:
            Buffer.BlockCopy(array, 0, buffer, 0, size);
            Marshal.Copy(buffer, 0, ptr, size);       
            return ptr;
        }

        // Handle array of structs
        if (elementType is { IsValueType: true, IsPrimitive: false })
        {
            var size = Marshal.SizeOf(elementType) * array.Length;
            var ptr = Marshal.AllocHGlobal(size);
            
            for (var i = 0; i < array.Length; i++)
            {
                var offset = i * Marshal.SizeOf(elementType);
                Marshal.StructureToPtr(array.GetValue(i)!, ptr + offset, false);
            }
            
            return ptr;
        }

        // Handle array of objects/classes
        var marshaledArray = new object[array.Length];
        for (var i = 0; i < array.Length; i++)
        {
            var elementParam = new NativeFunctionParameter 
            { 
                Type = elementType, 
                Name = $"{parameter.Name}[{i}]" 
            };
            marshaledArray[i] = MarshalParameter(elementParam, array.GetValue(i));
        }

        return marshaledArray;
    }

    /// <summary>
    /// Marshals a class parameter based on its type and the provided argument.
    /// </summary>
    /// <param name="parameter">The native function parameter definition.</param>
    /// <param name="argument">The argument to marshal.</param>
    /// <returns>The marshaled argument.</returns>
    private static object MarshalClass(NativeFunctionParameter parameter, object argument)
    {
        var type = parameter.Type;
        
        // Handle special known types
        if (MarshalConverters.TryGetValue(type, out var converter))
        {
            return converter(argument);
        }

        // Marshal class as struct if it has StructLayout attribute
        var layoutAttribute = type.GetCustomAttribute<StructLayoutAttribute>();
        if (layoutAttribute != null)
        {
            var size = Marshal.SizeOf(type);
            var ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(argument, ptr, false);
            return ptr;
        }

        // For other classes, marshal public properties recursively
        var properties = type.GetProperties()
            .Where(p => p is { CanRead: true, CanWrite: true })
            .ToList();
        
        var marshaledObject = Activator.CreateInstance(type);
        foreach (var prop in properties)
        {
            var propParam = new NativeFunctionParameter 
            { 
                Type = prop.PropertyType, 
                Name = prop.Name 
            };
            var marshaledValue = MarshalParameter(propParam, prop.GetValue(argument));
            prop.SetValue(marshaledObject, marshaledValue);
        }

        return marshaledObject!;
    }

    /// <summary>
    /// Determines if a type is blittable (can be directly copied between managed and native memory).
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type is blittable; otherwise, false.</returns>
    private static bool IsBlittableType(Type type)
    {
        return type.IsPrimitive || 
               type == typeof(decimal) ||
               (type is { IsValueType: true, IsEnum: false } && 
                type.GetFields(BindingFlags.Instance | 
                               BindingFlags.Public | 
                               BindingFlags.NonPublic)
                    .All(field => IsBlittableType(field.FieldType)));
    }
}