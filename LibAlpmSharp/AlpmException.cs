using System;
using LibAlpmSharp.Interop;

namespace LibAlpmSharp;

/// <summary>
/// Represents errors that occur during libalpm operations.
/// </summary>
public class AlpmException : Exception
{
    /// <summary>
    /// Gets the error code associated with this exception.
    /// </summary>
    public AlpmErrno ErrorCode { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AlpmException"/> class.
    /// </summary>
    public AlpmException()
        : base("An error occurred in libalpm")
    {
        ErrorCode = AlpmErrno.ALPM_ERR_OK;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AlpmException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public AlpmException(string message)
        : base(message)
    {
        ErrorCode = AlpmErrno.ALPM_ERR_OK;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AlpmException"/> class with a specified error code.
    /// </summary>
    /// <param name="errorCode">The libalpm error code.</param>
    public AlpmException(AlpmErrno errorCode)
        : base(LibAlpm.GetErrorString(errorCode))
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AlpmException"/> class with a specified error code and message.
    /// </summary>
    /// <param name="errorCode">The libalpm error code.</param>
    /// <param name="message">The message that describes the error.</param>
    public AlpmException(AlpmErrno errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AlpmException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public AlpmException(string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = AlpmErrno.ALPM_ERR_OK;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AlpmException"/> class with a specified error code,
    /// error message, and a reference to the inner exception.
    /// </summary>
    /// <param name="errorCode">The libalpm error code.</param>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public AlpmException(AlpmErrno errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
