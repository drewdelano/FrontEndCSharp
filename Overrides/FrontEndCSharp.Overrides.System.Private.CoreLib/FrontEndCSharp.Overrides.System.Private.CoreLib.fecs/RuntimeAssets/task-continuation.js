import { invokeDelegate } from '../../jit-engine/calls.js';
import { throwManagedException } from '../../managed-exceptions.js';
import { unwrap, wrap } from '../../msil-type-helpers.js';
import { requireNonNullTaskArgument } from './task-argument-validation.js';
import { createTaskCanceledException, isCancellationRequested } from './task-cancellation.js';
import { getTaskCompletionSource, JavaScriptTaskCompletionSource } from './task-completion-source.js';

const preferFairness = 1;
const longRunning = 2;
const attachedToParent = 4;
const denyChildAttach = 8;
const hideScheduler = 16;
const lazyCancellation = 32;
const runContinuationsAsynchronously = 64;
const notOnRanToCompletion = 65536;
const notOnFaulted = 131072;
const notOnCanceled = 262144;
const executeSynchronously = 524288;
const statusMask = notOnRanToCompletion | notOnFaulted | notOnCanceled;
const validOptionsMask = preferFairness
    | longRunning
    | attachedToParent
    | denyChildAttach
    | hideScheduler
    | lazyCancellation
    | runContinuationsAsynchronously
    | statusMask
    | executeSynchronously;

const defaultSchedulerValue = Object.freeze({ kind: 'default' });
export const defaultTaskScheduler = wrap(defaultSchedulerValue, 'System.Threading.Tasks.TaskScheduler');
export const noCancellationToken = wrap(
    { $isCancellationRequested: false },
    'System.Threading.CancellationToken');
export const noContinuationOptions = wrap(0, 'System.Threading.Tasks.TaskContinuationOptions');

export function createContinuationTask(
    ctx,
    antecedentTask,
    continuation,
    continuationArguments,
    cancellationToken,
    continuationOptions,
    scheduler,
    resultTypeName,
    returnsResult,
    continuationParameterName) {
    requireNonNullTaskArgument(ctx, continuation, continuationParameterName);
    requireNonNullTaskArgument(ctx, scheduler, 'scheduler');
    requireDefaultScheduler(scheduler);

    const options = requireContinuationOptions(ctx, continuationOptions);
    const source = new JavaScriptTaskCompletionSource();
    const antecedent = getTaskCompletionSource(unwrap(antecedentTask));
    const complete = () => {
        if (isCancellationRequested(cancellationToken) || continuationIsFiltered(antecedent, options)) {
            source.setCanceled(createTaskCanceledException());
            return;
        }

        try {
            const result = invokeDelegate(ctx, continuation, continuationArguments);
            source.setResult(returnsResult ? result : null);
        } catch (error) {
            if (!error?.dotNetException) {
                throw error;
            }
            source.setException(error.dotNetException);
        }
    };

    if (isCancellationRequested(cancellationToken) && (options & lazyCancellation) === 0) {
        source.setCanceled(createTaskCanceledException());
    } else if (antecedent.isCompleted
        && (isCancellationRequested(cancellationToken) || continuationIsFiltered(antecedent, options))) {
        complete();
    } else {
        antecedent.registerContinuation(complete, (options & executeSynchronously) !== 0);
    }

    return wrap(source.task, resultTypeName);
}

function requireDefaultScheduler(scheduler) {
    if (unwrap(scheduler) !== defaultSchedulerValue) {
        throw new Error('Only TaskScheduler.Default is supported by the JavaScript scheduler.');
    }
}

function requireContinuationOptions(ctx, continuationOptions) {
    const options = unwrap(continuationOptions);
    const invalidBits = options & ~validOptionsMask;
    const excludesEveryStatus = (options & statusMask) === statusMask;
    const contradictoryScheduling = (options & longRunning) !== 0
        && (options & executeSynchronously) !== 0;
    if (!Number.isInteger(options)
        || invalidBits !== 0
        || excludesEveryStatus
        || contradictoryScheduling) {
        throwManagedException(
            ctx,
            'System.ArgumentOutOfRangeException',
            "The specified TaskContinuationOptions value is invalid. (Parameter 'continuationOptions')",
            null,
            { _paramName: wrap('continuationOptions', 'System.String') });
    }
    return options;
}

function continuationIsFiltered(antecedent, options) {
    if (antecedent.isCompletedSuccessfully) {
        return (options & notOnRanToCompletion) !== 0;
    }
    if (antecedent.isFaulted) {
        return (options & notOnFaulted) !== 0;
    }
    return (options & notOnCanceled) !== 0;
}

