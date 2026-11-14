import { throwDotNetException, wrap } from "./msil-type-helpers.js";

const defaultMessages = Object.freeze({
    "System.ArgumentException": "Value does not fall within the expected range.",
    "System.ArgumentNullException": "Value cannot be null.",
    "System.ArgumentOutOfRangeException": "Specified argument was out of the range of valid values.",
    "System.ArithmeticException": "Overflow or underflow in the arithmetic operation.",
    "System.ArrayTypeMismatchException": "Attempted to access an element as a type incompatible with the array.",
    "System.DivideByZeroException": "Attempted to divide by zero.",
    "System.IndexOutOfRangeException": "Index was outside the bounds of the array.",
    "System.InvalidCastException": "Specified cast is not valid.",
    "System.MissingFieldException": "Attempted to access a non-existing field.",
    "System.MissingMethodException": "Attempted to access a missing method.",
    "System.NullReferenceException": "Object reference not set to an instance of an object.",
    "System.OverflowException": "Arithmetic operation resulted in an overflow.",
    "System.Threading.Tasks.TaskCanceledException": "A task was canceled."
});

const defaultHResults = Object.freeze({
    "System.ArgumentException": -2147024809,
    "System.ArgumentNullException": -2147467261,
    "System.ArgumentOutOfRangeException": -2146233086,
    "System.ArithmeticException": -2147024362,
    "System.ArrayTypeMismatchException": -2146233085,
    "System.DivideByZeroException": -2147352558,
    "System.IndexOutOfRangeException": -2146233080,
    "System.InvalidCastException": -2147467262,
    "System.MissingFieldException": -2146233071,
    "System.MissingMethodException": -2146233069,
    "System.NullReferenceException": -2147467261,
    "System.OverflowException": -2146233066,
    "System.AggregateException": -2146233088,
    "System.Threading.Tasks.TaskCanceledException": -2146233029,
    "System.TypeInitializationException": -2146233036
});

export function createManagedException(
    typeName,
    message = defaultMessages[typeName] ?? null,
    innerException = null,
    additionalFields = {}) {
    const value = {
        ...additionalFields,
        _message: message === null ? null : wrap(message, "System.String"),
        _innerException: innerException,
        _stackTraceString: null
    };
    const hResult = defaultHResults[typeName];
    if (hResult !== undefined) {
        value._HResult = wrap(hResult, "System.Int32");
    }
    return wrap(value, typeName);
}

export function createTypeInitializationException(typeName, innerException) {
    const exception = createManagedException(
        "System.TypeInitializationException",
        `The type initializer for '${typeName}' threw an exception.`,
        innerException
    );
    exception.v._typeName = wrap(typeName, "System.String");
    return exception;
}

export function throwManagedException(
    ctx,
    typeName,
    message,
    innerException = null,
    additionalFields = {}) {
    throwDotNetException(
        ctx ?? { stackTrace: "" },
        createManagedException(typeName, message, innerException, additionalFields)
    );
}
