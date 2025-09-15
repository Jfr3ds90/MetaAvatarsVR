using System;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;

[AttributeUsage(AttributeTargets.Enum)]
internal class OVRSResultStatus : Attribute { }

internal static class OVRSResult
{
    public static OVRSResult<TStatus> From<TStatus>(TStatus status) where TStatus : struct, Enum, IConvertible
        => OVRSResult<TStatus>.From(status);

    public static OVRSResult<TResult, TStatus> From<TResult, TStatus>(TResult result, TStatus status)
        where TStatus : struct, Enum, IConvertible
        => OVRSResult<TResult, TStatus>.From(result, status);
}
/// \endcond

/// <summary>
/// Represents the result of an operation.
/// </summary>
/// <remarks>
/// An <see cref="OVRSResult"/>&lt;TStatus&gt; is effectively an `enum` but with some additional machinery to test for
/// success and to distinguish between a real value and a default-initialized value. This is important because an
/// `enum` constant of zero is often used to indicate <see cref="Success"/>.
///
/// Many asynchronous methods return an <see cref="OVRSResult"/>, for example many <see cref="OVRSAnchor"/> and
/// <see cref="OVRSpatialAnchor"/> methods are asynchronous.
///
/// For results that also contain a value, see <see cref="OVRSResult"/>&lt;TValue, TStatus&gt;.
/// </remarks>
/// <typeparam name="TStatus">The type of the status code.</typeparam>
public struct OVRSResult<TStatus> : IEquatable<OVRSResult<TStatus>>
    where TStatus : struct, Enum, IConvertible
{

    private readonly bool _initialized;

    private readonly int _statusCode;

    private readonly TStatus _status;

    /// <summary>
    /// Whether this <see cref="OVRSResult"/>&lt;TStatus&gt; represents a successful result.
    /// </summary>
    public bool Success => _initialized && ((OVRPlugin.Result)_statusCode).IsSuccess();

    /// <summary>
    /// The status of the result.
    /// </summary>
    /// <remarks>
    /// The status could represent success (see <see cref="Success"/>) or a failure. If it is a failure, use this
    /// property to obtain more detailed error information.
    /// </remarks>
    public TStatus Status
    {
        get
        {
            if (_initialized)
            {
                return _status;
            }

            var invalid = OVRPlugin.Result.Failure_DataIsInvalid;
            return UnsafeUtility.As<OVRPlugin.Result, TStatus>(ref invalid);
        }
    }

    private OVRSResult(TStatus status)
    {
        if (UnsafeUtility.SizeOf<TStatus>() != sizeof(int))
            throw new InvalidOperationException($"{nameof(TStatus)} must have a 4 byte underlying storage type.");

        _initialized = true;
        _status = status;
        _statusCode = UnsafeUtility.EnumToInt(_status);
    }

    /// <summary>
    /// Creates a new <see cref="OVRSResult"/>&lt;TStatus&gt; with the specified status.
    /// </summary>
    /// <param name="status">The status of the result.</param>
    /// <returns>Returns a new <see cref="OVRSResult"/>&lt;TStatus&gt; with the specified status.</returns>
    /// <seealso cref="FromSuccess"/>
    /// <seealso cref="FromFailure"/>
    public static OVRSResult<TStatus> From(TStatus status) => new(status);

    /// <summary>
    /// Creates a new <see cref="OVRSResult"/>&lt;TStatus&gt; with the specified success status.
    /// </summary>
    /// <param name="status">The status (must be a valid success status).</param>
    /// <returns>Returns a new <see cref="OVRSResult"/>&lt;TStatus&gt; with the specified status.</returns>
    /// <exception cref="ArgumentException">Thrown when status is not a valid success status.</exception>
    /// <seealso cref="From"/>
    /// <seealso cref="FromFailure"/>
    public static OVRSResult<TStatus> FromSuccess(TStatus status)
    {
        var result = UnsafeUtility.As<TStatus, OVRPlugin.Result>(ref status);
        if (!result.IsSuccess())
            throw new ArgumentException("Not of a valid success status", nameof(status));

        return new OVRSResult<TStatus>(status);
    }

    /// <summary>
    /// Creates a new <see cref="OVRSResult"/>&lt;TStatus&gt; with the specified failure status.
    /// </summary>
    /// <param name="status">The status (must be a valid failure status).</param>
    /// <returns>Returns a new <see cref="OVRSResult"/>&lt;TStatus&gt; with the specified status.</returns>
    /// <exception cref="ArgumentException">Thrown when status is not a valid failure status.</exception>
    /// <seealso cref="From"/>
    /// <seealso cref="FromSuccess"/>
    public static OVRSResult<TStatus> FromFailure(TStatus status)
    {
        var result = UnsafeUtility.As<TStatus, OVRPlugin.Result>(ref status);
        if (result.IsSuccess())
            throw new ArgumentException("Not of a valid failure status", nameof(status));

        return new OVRSResult<TStatus>(status);
    }

    /// <summary>
    /// Determines whether the current <see cref="OVRSResult"/>&lt;TStatus&gt; is equal to another <see cref="OVRSResult"/>&lt;TStatus&gt;.
    /// </summary>
    /// <param name="other">The <see cref="OVRSResult"/>&lt;TStatus&gt; to compare with the current one.</param>
    /// <returns>Returns `true` if the specified <see cref="OVRSResult"/>&lt;TStatus&gt; is equal to this
    /// <see cref="OVRSResult"/>&lt;TStatus&gt;, otherwise `false`.</returns>
    public bool Equals(OVRSResult<TStatus> other) => _initialized == other._initialized && _statusCode == other._statusCode;

    /// <summary>
    /// Determines whether the current <see cref="OVRSResult"/>&lt;TStatus&gt; is equal to another <see cref="object"/>.
    /// </summary>
    /// <param name="obj">The <see cref="object"/> to compare with the current <see cref="OVRSResult"/>&lt;TStatus&gt;.</param>
    /// <returns>Returns `true` if <paramref name="obj"/> is of type <see cref="OVRSResult"/>&lt;TStatus&gt; and is equal
    /// to this <see cref="OVRSResult"/>&lt;TStatus&gt;, otherwise `false`.</returns>
    public override bool Equals(object obj) => obj is OVRSResult<TStatus> other && Equals(other);

    /// <summary>
    /// Gets a hashcode suitable for use in a Dictionary or HashSet.
    /// </summary>
    /// <returns>Returns a hashcode suitable for use in a Dictionary or HashSet.</returns>
    public override int GetHashCode()
    {
        unchecked
        {
            const int primeBase = 17; // Starting prime number
            const int primeMultiplier = 31; // Multiplier prime number

            var hash = primeBase;
            hash = hash * primeMultiplier + _initialized.GetHashCode();
            hash = hash * primeMultiplier + _statusCode.GetHashCode();

            return hash;
        }
    }

    /// <summary>
    /// Generates a string representation of this result object.
    /// </summary>
    /// <remarks>
    /// The string representation is the stringification of <see cref="Status"/>, or "(invalid result)" if this result
    /// object has not been initialized.
    /// </remarks>
    /// <returns>Returns a string representation of this <see cref="OVRSResult"/>&lt;TStatus&gt;</returns>
    public override string ToString() => _initialized ? _status.ToString() : "(invalid result)";

    /// <summary>
    /// Implicitly casts an <see cref="OVRSResult"/> to a `bool`.
    /// </summary>
    /// <remarks>
    /// If the <see cref="OVRSResult"/> represents success, then it will convert to `true`. Otherwise, it will convert to `false`.
    /// <example>
    /// For example, this allows you to write code like:
    /// <code><![CDATA[
    /// var result = DoOperation();
    /// if (result) {
    ///   Debug.Log("Operation succeeded.");
    /// } else {
    ///   Debug.LogError($"Operation failed with error {result.Status}.");
    /// }
    /// ]]></code>
    /// </example>
    /// </remarks>
    /// <param name="value">The <see cref="OVRSResult"/> to cast.</param>
    /// <returns>Returns the value of <see cref="Success"/>.</returns>
    public static implicit operator bool(OVRSResult<TStatus> value) => value.Success;

    /// <summary>
    /// (Internal) Implicitly converts an <see cref="OVRPlugin.Result"/> to an <see cref="OVRSResult{TStatus}"/>.
    /// </summary>
    /// <remarks>
    /// This implicit conversion is provided mostly for internal Meta use (C# requires user-defined conversion operators
    /// to be public). It simplifies early returns in methods that return an <see cref="OVRSResult{TStatus}"/> and need
    /// to early out when a call fails.
    ///
    /// Such methods can simply return the result rather than constructing an <see cref="OVRSResult{TStatus}"/>
    /// with a cast.
    /// </remarks>
    /// <param name="result">The result.</param>
    /// <returns>Returns an <see cref="OVRSResult{TStatus}"/> whose <see cref="Status"/> is <paramref name="result"/>
    /// cast to a <typeparamref name="TStatus"/>.</returns>
    public static implicit operator OVRSResult<TStatus>(OVRPlugin.Result result)
        => From(UnsafeUtility.As<OVRPlugin.Result, TStatus>(ref result));
}

/// <summary>
/// Represents a result with a value of type <typeparamref name="TValue"/> and a status code of type
/// <typeparamref name="TStatus"/>.
/// </summary>
/// <typeparam name="TValue">The type of the value.</typeparam>
/// <typeparam name="TStatus">The type of the status code.</typeparam>
/// <remarks>
/// An <see cref="OVRSResult"/>&lt;TValue, TStatus&gt; represents the result of an operation which may fail. If the operation
/// succeeds (<see cref="Success"/> is `true`), then you can access the value using the <see cref="Value"/> property.
///
/// If the operation fails (<see cref="Success"/> is `false`), then the `OVRSResult` does not have a value, and it is an
/// error to access the <see cref="Value"/> property. In this case, the <see cref="Status"/> property will contain an
/// error code.
/// </remarks>
public struct OVRSResult<TValue, TStatus> : IEquatable<OVRSResult<TValue, TStatus>>
    where TStatus : struct, Enum, IConvertible
{
    private readonly bool _initialized;

    private readonly TValue _value;

    private readonly int _statusCode;

    private readonly TStatus _status;

    /// <summary>
    /// Whether this <see cref="OVRSResult"/>&lt;TValue, TStatus&gt; represents a successful result.
    /// </summary>
    /// <remarks>
    /// This must be `true` in order to access <see cref="Value"/>.
    /// </remarks>
    public bool Success => _initialized && ((OVRPlugin.Result)_statusCode).IsSuccess();

    /// <summary>
    /// The status of the result.
    /// </summary>
    /// <remarks>
    /// If the operation fails (<see cref="Success"/> is `false`), then the <see cref="Status"/> enum indicates a
    /// more specific reason for the failure.
    /// </remarks>
    public TStatus Status
    {
        get
        {
            if (_initialized)
            {
                return _status;
            }

            var invalid = OVRPlugin.Result.Failure_DataIsInvalid;
            return UnsafeUtility.As<OVRPlugin.Result, TStatus>(ref invalid);
        }
    }

    /// <summary>
    /// Indicates whether the result has a value.
    /// </summary>
    /// <remarks>
    /// It is an error to access <see cref="Value"/> when this property is `false`.
    /// </remarks>
    public bool HasValue => Success;

    /// <summary>
    /// The value of the result.
    /// </summary>
    /// <remarks>
    /// It is an error to access this property unless <see cref="Success"/> is `true`. See also
    /// <see cref="TryGetValue"/>.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown if <see cref="Success"/> is `false`.</exception>
    /// <seealso cref="TryGetValue"/>
    /// <seealso cref="HasValue"/>
    public TValue Value
    {
        get
        {
            if (!_initialized)
            {
                throw new InvalidOperationException($"The {nameof(OVRSResult)} object is not valid.");
            }

            if (_statusCode < 0)
            {
                throw new InvalidOperationException($"The {nameof(OVRSResult)} does not have a value because the " +
                                                    $"operation failed with {_status}.");
            }

            return _value;
        }
    }

    /// <summary>
    /// Tries to retrieve the value of the result.
    /// </summary>
    /// <remarks>
    /// This provides an exception-free method of obtaining the result's <see cref="Value"/> if one is available.
    /// <example>
    /// This allows you to simplify your code. Instead of this:
    /// <code><![CDATA[
    /// if (result.HasValue) {
    ///   var value = result.Value;
    ///   // use value
    /// }
    /// ]]></code>
    /// You can instead combine the HasValue check with the extraction of the result:
    /// <code><![CDATA[
    /// if (result.TryGetValue(out var value)) {
    ///   // use value
    /// }
    /// ]]></code>
    /// </example>
    /// </remarks>
    /// <param name="value">When this method returns, contains the value of the result if the result was successful; otherwise, the default value.</param>
    /// <returns>Returns <c>true</c> if the result was successful and the value is retrieved; otherwise, <c>false</c>.</returns>
    /// <seealso cref="Value"/>
    /// <seealso cref="Success"/>
    /// <seealso cref="HasValue"/>
    public bool TryGetValue(out TValue value)
    {
        if (HasValue)
        {
            value = _value;
            return true;
        }

        value = default;
        return false;
    }

    private OVRSResult(TValue value, TStatus status)
    {
        if (UnsafeUtility.SizeOf<TStatus>() != sizeof(int))
            throw new InvalidOperationException($"{nameof(TStatus)} must have a 4 byte underlying storage type.");

        _initialized = true;
        _value = value;
        _status = status;
        _statusCode = UnsafeUtility.EnumToInt(_status);
    }

    /// <summary>
    /// Creates a new <see cref="OVRSResult"/>&lt;TValue, TStatus&gt; with the specified value and status.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="status">The status.</param>
    /// <returns>Returns a new <see cref="OVRSResult"/>&lt;TValue, TStatus&gt; with the specified value and status.</returns>
    /// <seealso cref="FromSuccess"/>
    /// <seealso cref="FromFailure"/>
    public static OVRSResult<TValue, TStatus> From(TValue value, TStatus status) => new(value, status);

    /// <summary>
    /// Creates a new <see cref="OVRSResult"/>&lt;TValue, TStatus&gt; with the specified value and a success status.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="status">The status (must be a valid success status).</param>
    /// <returns>Returns a new <see cref="OVRSResult"/>&lt;TValue, TStatus&gt; with the specified value and status.</returns>
    /// <exception cref="ArgumentException">Thrown when status is not a valid success status.</exception>
    /// <seealso cref="From"/>
    /// <seealso cref="FromFailure"/>
    public static OVRSResult<TValue, TStatus> FromSuccess(TValue value, TStatus status)
    {
        if (!UnsafeUtility.As<TStatus, OVRPlugin.Result>(ref status).IsSuccess())
            throw new ArgumentException("Not of a valid success status. Success values must have an integral value >= 0.", nameof(status));

        return new OVRSResult<TValue, TStatus>(value, status);
    }

    /// <summary>
    /// Creates a new <see cref="OVRSResult"/>&lt;TValue, TStatus&gt; with the specified failure status.
    /// </summary>
    /// <param name="status">The status (must be a valid failure status).</param>
    /// <returns>Returns a new <see cref="OVRSResult"/>&lt;TValue, TStatus&gt; with the specified status.</returns>
    /// <exception cref="ArgumentException">Thrown when status is not a valid failure status.</exception>
    /// <seealso cref="From"/>
    /// <seealso cref="FromSuccess"/>
    public static OVRSResult<TValue, TStatus> FromFailure(TStatus status)
    {
        if (UnsafeUtility.As<TStatus, OVRPlugin.Result>(ref status).IsSuccess())
            throw new ArgumentException("Not of a valid failure status. Failure values must have an integral value < 0.", nameof(status));

        return new OVRSResult<TValue, TStatus>(default, status);
    }

    /// <summary>
    /// Determines whether the current <see cref="OVRSResult"/>&lt;TValue, TStatus&gt; is equal to another
    /// <see cref="OVRSResult"/>&lt;TValue, TStatus&gt;.
    /// </summary>
    /// <param name="other">The <see cref="OVRSResult"/>&lt;TValue, TStatus&gt; to compare with the current one.</param>
    /// <returns>
    /// Returns <c>true</c> if the specified <see cref="OVRSResult"/>&lt;TValue, TStatus&gt; is equal to the current
    /// <see cref="OVRSResult"/>&lt;TValue, TStatus&gt;; otherwise, <c>false</c>.
    /// </returns>
    public bool Equals(OVRSResult<TValue, TStatus> other) => _initialized == other._initialized
        && EqualityComparer<TValue>.Default.Equals(_value, other._value) && _statusCode == other._statusCode;

    /// <summary>
    /// Determines whether the current <see cref="OVRSResult"/>&lt;TValue, TStatus&gt; is equal to another <see cref="object"/>.
    /// </summary>
    /// <param name="obj">The <see cref="object"/> to compare with the current <see cref="OVRSResult"/>&lt;TValue, TStatus&gt;.</param>
    /// <returns>
    /// Returns <c>true</c> if the specified <see cref="object"/> is equal to the current <see cref="OVRSResult"/>&lt;TValue, TStatus&gt;; otherwise, <c>false</c>.
    /// </returns>
    public override bool Equals(object obj) => obj is OVRSResult<TValue, TStatus> other && Equals(other);

    /// <summary>
    /// Gets a hashcode suitable for use in a Dictionary or HashSet.
    /// </summary>
    /// <returns>Returns a hashcode for this result.</returns>
    public override int GetHashCode()
    {
        unchecked
        {
            const int primeBase = 17; // Starting prime number
            const int primeMultiplier = 31; // Multiplier prime number

            var hash = primeBase;
            hash = hash * primeMultiplier + _initialized.GetHashCode();
            hash = hash * primeMultiplier + _statusCode.GetHashCode();
            hash = hash * primeMultiplier + (_value?.GetHashCode() ?? 0);

            return hash;
        }
    }

    /// <summary>
    /// Generates a string representation of this result object.
    /// </summary>
    /// <remarks>
    /// If this result object has not been initialized, the string is "(invalid result)". Otherwise, if
    /// <see cref="Success"/> is `true`, then the string is the stringification of the <see cref="Status"/> and
    /// <see cref="Value"/>. If <see cref="Success"/> is `false`, then it is just the stringification of
    /// <see cref="Status"/>.
    /// </remarks>
    /// <returns>Returns a string representation of this <see cref="OVRSResult"/>&lt;TStatus&gt;</returns>
    public override string ToString() => _initialized
        ? HasValue
            ? $"(Value={_value}, Status={_status})"
            : _status.ToString()
        : "(invalid result)";

    /// <summary>
    /// Implicitly casts an <see cref="OVRSResult"/> to a `bool`.
    /// </summary>
    /// <remarks>
    /// If the <see cref="OVRSResult"/> represents success, then it will convert to `true`. Otherwise, it will convert to `false`.
    /// <example>
    /// For example, this allows you to write code like:
    /// <code><![CDATA[
    /// var result = DoOperation();
    /// if (result) {
    ///   Debug.Log("Operation succeeded.");
    /// } else {
    ///   Debug.LogError($"Operation failed with error {result.Status}.");
    /// }
    /// ]]></code>
    /// </example>
    /// </remarks>
    /// <param name="value">The <see cref="OVRSResult"/> to cast.</param>
    /// <returns>Returns the value of <see cref="Success"/>.</returns>
    public static implicit operator bool(OVRSResult<TValue, TStatus> value) => value.Success;

    /// <summary>
    /// (Internal) Implicitly converts an <see cref="OVRPlugin.Result"/> to a failed <see cref="OVRSResult{TValue,TStatus}"/>.
    /// </summary>
    /// <remarks>
    /// This implicit conversion is provided mostly for internal Meta use (C# requires user-defined conversion operators
    /// to be public). It simplifies early returns in methods that return an <see cref="OVRSResult{TValue,TStatus}"/> and
    /// need to early out when a call fails.
    ///
    /// Such methods can simply return the result rather than constructing an <see cref="OVRSResult{TValue,TStatus}"/>
    /// with potentially complex type parameters.
    /// </remarks>
    /// <param name="result">The result, which must represent failure (be less than zero).</param>
    /// <returns>Returns an <see cref="OVRSResult{TValue,TStatus}"/> whose <see cref="Success"/> is false.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="result"/> does not represent a failure case.</exception>
    public static implicit operator OVRSResult<TValue, TStatus>(OVRPlugin.Result result)
        => FromFailure(UnsafeUtility.As<OVRPlugin.Result, TStatus>(ref result));
}

