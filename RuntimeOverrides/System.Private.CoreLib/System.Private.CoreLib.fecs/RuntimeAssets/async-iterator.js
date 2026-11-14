import { createAsyncContinuationContext } from '../../managed-debugger-hooks.js';
import { invokeManagedDelegate } from '../../jit-engine/calls.js';
import { throwManagedException } from '../../managed-exceptions.js';
import { deRef, throwDotNetException, unwrap, wrap } from '../../msil-type-helpers.js';
import {
    registerAwaiterContinuation,
    registerSafeAwaiterContinuation
} from './task-completion-source.js';

export function createAsyncIteratorMethodBuilder() {
    return wrap({}, 'System.Runtime.CompilerServices.AsyncIteratorMethodBuilder');
}

export function moveNextAsyncIterator(ctx, stateMachineReference) {
    const stateMachineCell = deRef(stateMachineReference);
    const stateMachine = deRef(stateMachineCell);
    globalThis.DotNet.Methods[`${stateMachine.t}.MoveNext()`](ctx, stateMachineCell);
}

function registerIteratorContinuation(ctx, awaiter, stateMachineReference, safe) {
    const awaiterValue = deRef(awaiter);
    const stateMachineCell = deRef(stateMachineReference);
    const stateMachine = deRef(stateMachineCell);
    const continuationContext = createAsyncContinuationContext(ctx);
    const continuation = () => {
        globalThis.DotNet.Methods[`${stateMachine.t}.MoveNext()`](continuationContext, stateMachineCell);
    };
    if (safe) registerSafeAwaiterContinuation(ctx, awaiterValue, continuation);
    else registerAwaiterContinuation(awaiterValue, continuation, ctx);
}

export function awaitAsyncIteratorSafe(ctx, awaiter, stateMachineReference) {
    registerIteratorContinuation(ctx, awaiter, stateMachineReference, true);
}

export function awaitAsyncIteratorUnsafe(ctx, awaiter, stateMachineReference) {
    registerIteratorContinuation(ctx, awaiter, stateMachineReference, false);
}

export function completeAsyncIterator() {
}

function coreState(core) {
    const raw = unwrap(deRef(core));
    if (raw === null || typeof raw !== 'object') {
        throw new TypeError('ManualResetValueTaskSourceCore requires addressable managed storage.');
    }
    raw.$manualResetValueTaskSource ??= {
        version: 0,
        status: 0,
        result: null,
        exception: null,
        continuation: null
    };
    return raw.$manualResetValueTaskSource;
}

function requireVersion(ctx, state, token) {
    if (state.version !== unwrap(token)) {
        throwManagedException(ctx, 'System.InvalidOperationException', 'The ValueTask source token is no longer valid.');
    }
}

function signalCompletion(state) {
    const continuation = state.continuation;
    state.continuation = null;
    if (continuation !== null) queueMicrotask(continuation);
}

export function resetManualResetValueTaskSource(core) {
    const state = coreState(core);
    state.version = (state.version + 1) & 0xffff;
    if (state.version >= 0x8000) state.version -= 0x10000;
    state.status = 0;
    state.result = null;
    state.exception = null;
    state.continuation = null;
}

export function setManualResetValueTaskSourceResult(core, result) {
    const state = coreState(core);
    state.result = result;
    state.status = 1;
    signalCompletion(state);
}

export function setManualResetValueTaskSourceException(core, exception) {
    const state = coreState(core);
    state.exception = exception;
    state.status = exception?.t === 'System.OperationCanceledException'
        || exception?.t === 'System.Threading.Tasks.TaskCanceledException'
        ? 3
        : 2;
    signalCompletion(state);
}

export function getManualResetValueTaskSourceVersion(core) {
    return wrap(coreState(core).version, 'System.Int16');
}

export function getManualResetValueTaskSourceStatus(ctx, core, token) {
    const state = coreState(core);
    requireVersion(ctx, state, token);
    return wrap(state.status, 'System.Threading.Tasks.Sources.ValueTaskSourceStatus');
}

export function getManualResetValueTaskSourceResult(ctx, core, token) {
    const state = coreState(core);
    requireVersion(ctx, state, token);
    if (state.status === 0) {
        throwManagedException(ctx, 'System.InvalidOperationException', 'The ValueTask source has not completed.');
    }
    if (state.exception !== null) throwDotNetException(ctx, state.exception);
    return state.result;
}

export function onManualResetValueTaskSourceCompleted(
    ctx,
    core,
    continuation,
    continuationState,
    token) {
    const state = coreState(core);
    requireVersion(ctx, state, token);
    const callback = () => invokeManagedDelegate(ctx, continuation, [continuationState]);
    if (state.status === 0) {
        if (state.continuation !== null) {
            throwManagedException(ctx, 'System.InvalidOperationException', 'Only one continuation may await this ValueTask source.');
        }
        state.continuation = callback;
    } else {
        queueMicrotask(callback);
    }
}
