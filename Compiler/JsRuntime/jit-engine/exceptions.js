import { throwManagedException } from "../managed-exceptions.js";
import { isManagedNull, throwDotNetException } from "../msil-type-helpers.js";

function throwException(opts) {
    const wrappedException = opts.stack.pop();
    if (typeof wrappedException === "undefined") {
        throw new Error("Evaluation stack underflow executing throw.");
    }
    if (isManagedNull(wrappedException)) {
        throwManagedException(opts.ctx, "System.NullReferenceException");
    }
    throwDotNetException(opts.ctx, wrappedException);
}

export const instructionHandlers = Object.freeze({
    "endfinally": opts => opts.completeFinally(),
    "endfilter": opts => opts.completeFilter(),
    "rethrow": opts => opts.rethrowCurrentException(),
    "break": () => {},
    "throw": throwException
});
