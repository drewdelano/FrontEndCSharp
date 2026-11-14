import { executeInstruction } from "./jit-engine/_executeInstruction.js";
import { getManagedDebuggerHooks } from "./managed-debugger-hooks.js";
import { composeManagedGenericArguments, defineManagedFieldAlias, wrap, unwrap, getDefaultValue, isTypeAssignableTo } from "./msil-type-helpers.js";

export function ExecuteMSIL(ctx, methodInfo, blocks, args) {
    const stack = [];
    const locals = [];
    const debuggerHooks = getManagedDebuggerHooks();

    const paramTypes = methodInfo.paramType;
    const localTypes = methodInfo.localTypes;
    let genericParams = null;
    if (ctx.genericParams) {
        genericParams = ctx.genericParams;
        ctx.genericParams = null;
    }

    // initialize method locals
    const typeArgs = methodInfo.hasThis ? args[0].$typeArgs : {};
    for (let i = 0; i < localTypes.length; i++) {
        locals[i] = {
            value: getDefaultValue(
                localTypes[i],
                composeManagedGenericArguments(typeArgs, genericParams),
                localTypes[i].isFunctionPointer
            )
        };
    }

    const opts = {
        ctx,
        currentBlockIndex: 0,
        instructionIndex: 0,
        instruction: null,
        stack,
        locals,
        methodArgs: args,
        genericParams,
        methodInfo,
        blocks,
        returned: false,
        returnValue: undefined,
        pendingTransfers: [],
        exceptionSearch: null,
        caughtExceptions: new Map(),
        debuggerHooks
    };
    opts.beginLeave = targetOffset => beginLeave(opts, targetOffset);
    opts.completeFinally = () => completeFinally(opts);
    opts.completeFilter = () => completeFilter(opts);
    opts.rethrowCurrentException = () => rethrowCurrentException(opts);
    opts.ctx = createInvocationContext(ctx, opts, methodInfo, debuggerHooks !== null);
    return debuggerHooks === null
        ? executeWithoutDebugger(opts, blocks)
        : executeWithDebugger(opts, blocks, debuggerHooks);
}

function executeWithoutDebugger(opts, blocks) {
    while (true) {
        let block = blocks[opts.currentBlockIndex];

        // continue to next block, if we're out of instructions
        if (block && !block.instructions[opts.instructionIndex]) {
            opts.currentBlockIndex++;
            opts.instructionIndex = 0;
            block = blocks[opts.currentBlockIndex];
        }

        opts.instruction = block.instructions[opts.instructionIndex];

        try {
            const beforeBlockIndex = opts.currentBlockIndex;
            const beforeInstructionIndex = opts.instructionIndex;
            executeInstruction(opts);
            if (opts.returned) {
                return opts.returnValue;
            }
            if (beforeBlockIndex === opts.currentBlockIndex
                && beforeInstructionIndex === opts.instructionIndex) {
                opts.instructionIndex++;
            }
        } catch (ex) {
            dispatchException(opts, ex);
        }
    }
}

function executeWithDebugger(opts, blocks, debuggerHooks) {
    while (true) {
        let block = blocks[opts.currentBlockIndex];

        if (block && !block.instructions[opts.instructionIndex]) {
            opts.currentBlockIndex++;
            opts.instructionIndex = 0;
            block = blocks[opts.currentBlockIndex];
        }

        opts.instruction = block.instructions[opts.instructionIndex];

        try {
            const beforeBlockIndex = opts.currentBlockIndex;
            const beforeInstructionIndex = opts.instructionIndex;
            if (debuggerHooks.isInstructionCheckEnabled()) {
                debuggerHooks.pauseAtManagedInstruction(opts);
            }
            executeInstruction(opts);
            if (opts.returned) {
                return opts.returnValue;
            }
            if (beforeBlockIndex === opts.currentBlockIndex
                && beforeInstructionIndex === opts.instructionIndex) {
                opts.instructionIndex++;
            }
        } catch (ex) {
            dispatchException(opts, ex);
        }
    }
}

function createInvocationContext(parentContext, opts, methodInfo, debuggerEnabled) {
    const invocationContext = {};
    for (const key of Reflect.ownKeys(parentContext)) {
        const descriptor = Object.getOwnPropertyDescriptor(parentContext, key);
        if (key !== "stackTrace" && descriptor?.enumerable) {
            invocationContext[key] = parentContext[key];
        }
    }
    if (debuggerEnabled) {
        invocationContext.debugDepth = (parentContext.debugDepth ?? 0) + 1;
        invocationContext.debugFrames = [opts, ...(parentContext.debugFrames ?? [])];
        invocationContext.asyncParents = parentContext.asyncParents ?? [];
    }
    Object.defineProperty(invocationContext, "stackTrace", {
        configurable: false,
        enumerable: true,
        get() {
            return `   at ${methodInfo.fullMethodName} (Block: ${opts.currentBlockIndex}, Instruction: ${opts.instructionIndex})\n${parentContext.stackTrace ?? ""}`;
        }
    });
    return invocationContext;
}

Object.defineProperty(globalThis, "__FrontEndCSharpExecuteMSIL", {
    configurable: false,
    enumerable: false,
    value: ExecuteMSIL,
    writable: false
});

function dispatchException(opts, error) {
    opts.debuggerHooks?.pauseAtThrownException(opts, error);
    opts.pendingTransfers.length = 0;
    opts.stack.length = 0;

    if (opts.exceptionSearch !== null) {
        if (error?.dotNetException?.t) {
            // ECMA-335 III.3.34: an exception escaping a filter is intercepted
            // and is equivalent to the filter returning continue-search.
            rejectCurrentFilter(opts);
            opts.debuggerHooks?.completeException(error);
            return;
        }

        // Native JavaScript failures represent interpreter or interop failures,
        // not managed exceptions. Do not let a filter hide them.
        opts.exceptionSearch = null;
    }

    beginExceptionSearch(opts, error);
}

function beginExceptionSearch(opts, error) {
    const dotNetException = error?.dotNetException;
    const regions = getProtectedRegions(opts.methodInfo.exceptionHandlers ?? [], opts.currentBlockIndex);
    opts.exceptionSearch = {
        error,
        dotNetException,
        regions,
        regionIndex: 0,
        handlerIndex: 0,
        activeFilter: null,
        finallyHandlers: []
    };
    continueExceptionSearch(opts);
}

function continueExceptionSearch(opts) {
    const search = opts.exceptionSearch;
    while (search.regionIndex < search.regions.length) {
        const region = search.regions[search.regionIndex];
        while (search.handlerIndex < region.handlers.length) {
            const handler = region.handlers[search.handlerIndex];
            if (search.dotNetException?.t
                && handler.Flags === 'Clause'
                && isTypeAssignableTo(search.dotNetException.t, handler.CatchType)) {
                finishExceptionSearch(opts, handler);
                return;
            }

            if (search.dotNetException?.t && handler.Flags === 'Filter') {
                enterFilter(opts, handler);
                return;
            }

            search.handlerIndex++;
        }

        const finallyHandler = region.handlers.find(handler =>
            handler.Flags === 'Finally' || handler.Flags === 'Fault');
        if (finallyHandler) {
            search.finallyHandlers.push(finallyHandler);
        }
        search.regionIndex++;
        search.handlerIndex = 0;
    }

    finishExceptionSearch(opts, null);
}

function enterFilter(opts, handler) {
    if (!Number.isInteger(handler.FilterStartBlockIndex)) {
        throw new Error('An exception filter requires a filter start block.');
    }

    const search = opts.exceptionSearch;
    search.activeFilter = handler;
    opts.stack.length = 0;
    opts.stack.push(search.dotNetException);
    opts.currentBlockIndex = handler.FilterStartBlockIndex;
    opts.instructionIndex = 0;
}

function completeFilter(opts) {
    const search = opts.exceptionSearch;
    if (search === null || search.activeFilter === null) {
        throw new Error('endfilter requires an active exception filter.');
    }
    if (opts.stack.length !== 1) {
        throw new Error('endfilter requires exactly one value on the evaluation stack.');
    }

    const result = unwrap(opts.stack.pop());
    if (result === 1) {
        finishExceptionSearch(opts, search.activeFilter);
        return;
    }
    if (result !== 0) {
        throw new Error('endfilter requires an Int32 value of zero or one.');
    }

    rejectCurrentFilter(opts);
}

function rejectCurrentFilter(opts) {
    const search = opts.exceptionSearch;
    if (search === null || search.activeFilter === null) {
        throw new Error('There is no active exception filter to reject.');
    }

    opts.stack.length = 0;
    search.activeFilter = null;
    search.handlerIndex++;
    continueExceptionSearch(opts);
}

function finishExceptionSearch(opts, catchHandler) {
    const search = opts.exceptionSearch;
    if (catchHandler) {
        opts.debuggerHooks?.completeException(search.error);
    }
    opts.exceptionSearch = null;
    opts.pendingTransfers.push({
        finallyHandlers: search.finallyHandlers,
        catchHandler,
        error: search.error
    });
    continueExceptionTransfer(opts);
}

function getProtectedRegions(exceptionHandlers, blockIndex) {
    const regions = new Map();
    for (const handler of exceptionHandlers) {
        if (handler.TryStartBlockIndex <= blockIndex && blockIndex < handler.TryEndBlockIndex) {
            const key = `${handler.TryStartBlockIndex}:${handler.TryEndBlockIndex}`;
            if (!regions.has(key)) {
                regions.set(key, {
                    tryStartBlockIndex: handler.TryStartBlockIndex,
                    tryEndBlockIndex: handler.TryEndBlockIndex,
                    handlers: []
                });
            }
            regions.get(key).handlers.push(handler);
        }
    }

    return [...regions.values()].sort((left, right) =>
        (left.tryEndBlockIndex - left.tryStartBlockIndex)
        - (right.tryEndBlockIndex - right.tryStartBlockIndex)
        || right.tryStartBlockIndex - left.tryStartBlockIndex);
}

function continueExceptionTransfer(opts) {
    const transfer = opts.pendingTransfers[opts.pendingTransfers.length - 1];
    if (transfer.finallyHandlers.length > 0) {
        enterHandler(opts, transfer.finallyHandlers.shift());
        return;
    }

    opts.pendingTransfers.pop();
    if (transfer.catchHandler) {
        opts.caughtExceptions.set(transfer.catchHandler.HandlerStartBlockIndex, transfer.error);
        opts.stack.push(transfer.error.dotNetException);
        enterHandler(opts, transfer.catchHandler);
        return;
    }

    if (opts.debuggerHooks !== null && opts.ctx?.debugDepth === 1) {
        opts.debuggerHooks.pauseAtUnhandledException(opts, transfer.error);
        opts.debuggerHooks.completeException(transfer.error);
    }
    throw transfer.error;
}

function beginLeave(opts, targetOffset) {
    opts.stack.length = 0;
    const targetBlockIndex = findBlockIndex(opts, targetOffset);
    // ECMA-335 executes fault handlers only for exceptional exits. A normal
    // leave runs finally handlers for each protected region it exits.
    const finallyHandlers = (opts.methodInfo.exceptionHandlers ?? [])
        .filter(handler =>
            handler.Flags === 'Finally'
            && handler.TryStartBlockIndex <= opts.currentBlockIndex
            && opts.currentBlockIndex < handler.TryEndBlockIndex
            && !(handler.TryStartBlockIndex <= targetBlockIndex
                && targetBlockIndex < handler.TryEndBlockIndex))
        .sort((left, right) =>
            (left.TryEndBlockIndex - left.TryStartBlockIndex)
            - (right.TryEndBlockIndex - right.TryStartBlockIndex));

    if (finallyHandlers.length === 0) {
        jumpToOffset(opts, targetOffset);
        return;
    }

    const transfer = {
        finallyHandlers,
        targetOffset
    };
    opts.pendingTransfers.push(transfer);
    enterHandler(opts, transfer.finallyHandlers.shift());
}

function completeFinally(opts) {
    if (opts.pendingTransfers.length === 0) {
        throw new Error('endfinally requires a pending control transfer.');
    }

    const transfer = opts.pendingTransfers[opts.pendingTransfers.length - 1];
    if ('error' in transfer) {
        continueExceptionTransfer(opts);
        return;
    }

    if (transfer.finallyHandlers.length > 0) {
        enterHandler(opts, transfer.finallyHandlers.shift());
        return;
    }

    const targetOffset = transfer.targetOffset;
    opts.pendingTransfers.pop();
    jumpToOffset(opts, targetOffset);
}

function rethrowCurrentException(opts) {
    const catchHandler = (opts.methodInfo.exceptionHandlers ?? [])
        .filter(handler =>
            (handler.Flags === 'Clause' || handler.Flags === 'Filter')
            && handler.HandlerStartBlockIndex <= opts.currentBlockIndex
            && opts.currentBlockIndex < handler.HandlerEndBlockIndex)
        .sort((left, right) =>
            (left.HandlerEndBlockIndex - left.HandlerStartBlockIndex)
            - (right.HandlerEndBlockIndex - right.HandlerStartBlockIndex))[0];
    const error = catchHandler
        && opts.caughtExceptions.get(catchHandler.HandlerStartBlockIndex);
    if (!error) {
        throw new Error('rethrow is only valid inside an active catch handler.');
    }
    throw error;
}

function enterHandler(opts, handler) {
    opts.currentBlockIndex = handler.HandlerStartBlockIndex;
    opts.instructionIndex = 0;
}

function jumpToOffset(opts, targetOffset) {
    opts.currentBlockIndex = findBlockIndex(opts, targetOffset);
    opts.instructionIndex = 0;
}

function findBlockIndex(opts, targetOffset) {
    const targetBlockIndex = opts.blocks.findIndex(block => block.ilOffset === targetOffset);
    if (targetBlockIndex < 0) {
        throw new Error(`Missing IL branch target 0x${targetOffset.toString(16)}.`);
    }
    return targetBlockIndex;
}

export function initializeObject(typeName, genericParams) {
    // Create a new instance (empty object for now)
    const unwrappedInstance = {};
    const instance = wrap(unwrappedInstance, typeName, genericParams);

    // prepare instance defaults
    const type = globalThis.DotNet.Types[typeName];
    const keys = Object.keys(type.instances);
    for (let i = 0; i < keys.length; i++) {
          defineManagedFieldAlias(unwrappedInstance, keys[i], type.instances[keys[i]]);
        unwrappedInstance[keys[i]] = getDefaultValue(type.instances[keys[i]], genericParams);
    }
    return instance;
}
