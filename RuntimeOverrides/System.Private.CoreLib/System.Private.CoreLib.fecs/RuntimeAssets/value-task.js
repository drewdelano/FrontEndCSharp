import { createAsyncContinuationContext } from '../../managed-debugger-hooks.js';
import { deRef, throwDotNetException, unwrap, wrap } from '../../msil-type-helpers.js';
import {
    getTaskCompletionSource,
    invokeValueTaskSourceMethod,
    JavaScriptTaskCompletionSource,
    registerAwaiterContinuation,
    registerSafeAwaiterContinuation,
    setTaskExceptionOrCancellation
} from './task-completion-source.js';

function builderState(builder) {
    const state = unwrap(builder);
    if (!(state.completionSource instanceof JavaScriptTaskCompletionSource)) {
        throw new Error('AsyncValueTaskMethodBuilder<TResult> was not initialized by Create().');
    }
    return state;
}

export function createAsyncValueTaskMethodBuilder(ctx) {
    return wrap(
        { completionSource: new JavaScriptTaskCompletionSource() },
        'System.Runtime.CompilerServices.AsyncValueTaskMethodBuilder`1[TResult]',
        ctx.genericParams);
}

export function createNonGenericAsyncValueTaskMethodBuilder() {
    return wrap(
        { completionSource: new JavaScriptTaskCompletionSource() },
        'System.Runtime.CompilerServices.AsyncValueTaskMethodBuilder');
}

export function startAsyncValueTaskMethodBuilder(ctx, stateMachineReference) {
    const stateMachine = deRef(stateMachineReference);
    globalThis.DotNet.Methods[`${stateMachine.t}.MoveNext()`](ctx, stateMachineReference);
}

export function setAsyncValueTaskResult(builder, result) {
    builderState(builder).completionSource.setResult(result);
}

export function setNonGenericAsyncValueTaskResult(builder) {
    builderState(builder).completionSource.setResult(null);
}

export function setAsyncValueTaskException(builder, exception) {
    setTaskExceptionOrCancellation(builderState(builder).completionSource, exception);
}

export function getBuilderValueTask(ctx, builder) {
    const task = wrap(
        builderState(builder).completionSource.task,
        'System.Threading.Tasks.Task`1[TResult]',
        ctx.genericParams);
    return wrap(
        { task },
        'System.Threading.Tasks.ValueTask`1[TResult]',
        ctx.genericParams);
}

export function getNonGenericBuilderValueTask(builder) {
    const task = wrap(
        builderState(builder).completionSource.task,
        'System.Threading.Tasks.Task');
    return wrap({ task }, 'System.Threading.Tasks.ValueTask');
}

function registerBuilderContinuation(ctx, awaiter, stateMachineReference, safe) {
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

export function awaitAsyncValueTaskMethodBuilderUnsafe(ctx, awaiter, stateMachineReference) {
    registerBuilderContinuation(ctx, awaiter, stateMachineReference, false);
}

export function awaitAsyncValueTaskMethodBuilderSafe(ctx, awaiter, stateMachineReference) {
    registerBuilderContinuation(ctx, awaiter, stateMachineReference, true);
}

export function initializeValueTaskFromTask(valueTask, task) {
    unwrap(valueTask).task = task;
}

export function initializeValueTaskFromResult(ctx, valueTask, result) {
    const source = new JavaScriptTaskCompletionSource();
    source.setResult(result);
    unwrap(valueTask).task = wrap(
        source.task,
        'System.Threading.Tasks.Task`1[TResult]',
        ctx.genericParams);
}

export function initializeValueTaskFromSource(valueTask, source, token) {
    const state = unwrap(valueTask);
    state.source = source;
    state.token = unwrap(token);
}

export function getValueTaskAwaiter(ctx, valueTask, generic) {
    const valueTaskState = unwrap(valueTask);
    const awaiterState = "source" in valueTaskState
        ? {
            source: valueTaskState.source,
            token: valueTaskState.token,
            generic
        }
        : "task" in valueTaskState
            ? { task: valueTaskState.task }
            : { completed: true, result: null };
    return wrap(
        awaiterState,
        generic
            ? 'System.Runtime.CompilerServices.ValueTaskAwaiter`1[TResult]'
            : 'System.Runtime.CompilerServices.ValueTaskAwaiter',
        generic ? ctx.genericParams : null);
}

export function isValueTaskAwaiterCompleted(ctx, awaiter) {
    const state = unwrap(awaiter);
    if ("source" in state) {
        const status = invokeValueTaskSourceMethod(
            ctx,
            state.source,
            state.generic,
            "GetStatus",
            [wrap(state.token, "System.Int16")]);
        return wrap(unwrap(status) !== 0, 'System.Boolean');
    }
    if (state.completed === true) return wrap(true, 'System.Boolean');
    return wrap(getTaskCompletionSource(unwrap(state.task)).isCompleted, 'System.Boolean');
}

export function getValueTaskAwaiterResult(ctx, awaiter, generic) {
    const state = unwrap(awaiter);
    if ("source" in state) {
        return invokeValueTaskSourceMethod(
            ctx,
            state.source,
            state.generic,
            "GetResult",
            [wrap(state.token, "System.Int16")]);
    }
    if (state.completed === true) return generic ? state.result : undefined;
    const task = state.task;
    const source = getTaskCompletionSource(unwrap(task));
    if (!source.isCompleted) {
        throw new Error('ValueTaskAwaiter.GetResult() cannot run before its Task is complete.');
    }
    if (source.isFaulted || source.isCanceled) {
        throwDotNetException(ctx, source.exception);
    }
    const result = source.result;
    return generic ? result : undefined;
}
