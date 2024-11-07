namespace LibLoader.Exceptions;

public class NativeFunctionCallException : Exception
{
    public NativeFunctionCallException(string message, Exception innerException) : base(message, innerException)
    {

    }
    
    public NativeFunctionCallException(string message) : base(message)
    {
    }
}