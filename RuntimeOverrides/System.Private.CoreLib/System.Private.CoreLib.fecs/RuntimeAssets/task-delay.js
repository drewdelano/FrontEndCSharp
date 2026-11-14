import { throwManagedException } from '../../managed-exceptions.js';
import { unwrap, wrap } from '../../msil-type-helpers.js';
import {
    createTaskCanceledException,
    isCancellationRequested,
    registerCancellationCallback
} from './task-cancellation.js';
import { JavaScriptTaskCompletionSource } from './task-completion-source.js';
import { getTimeSpanTicks } from './managed-temporal.js';

const maximumDelayMilliseconds = 4294967294;
const maximumHostTimeoutMilliseconds = 2147483647;
const ticksPerMillisecond = 10000n;

export function createIntegerDelay(ctx, delay, cancellationToken = null) {
    const milliseconds = unwrap(delay);
    if (!Number.isInteger(milliseconds) || milliseconds < -1) {
        throwInvalidDelay(ctx);
    }
    return createDelay(milliseconds, cancellationToken);
}

export function createTimeSpanDelay(ctx, delay, cancellationToken = null) {
    const milliseconds = getTimeSpanTicks(delay) / ticksPerMillisecond;
    if (milliseconds < -1n || milliseconds > BigInt(maximumDelayMilliseconds)) {
        throwInvalidDelay(ctx);
    }
    return createDelay(Number(milliseconds), cancellationToken);
}

function createDelay(milliseconds, cancellationToken) {
    const source = new JavaScriptTaskCompletionSource();
    const task = wrap(source.task, 'System.Threading.Tasks.Task');

    if (cancellationToken !== null && isCancellationRequested(cancellationToken)) {
        source.setCanceled(createTaskCanceledException());
        return task;
    }
    if (milliseconds === 0) {
        source.setResult(null);
        return task;
    }

    let timeoutHandle = null;
    let unregisterCancellation = () => {};
    const cancel = () => {
        if (!source.trySetCanceled(createTaskCanceledException())) return;
        if (timeoutHandle !== null) clearTimeout(timeoutHandle);
        timeoutHandle = null;
        unregisterCancellation();
    };
    if (cancellationToken !== null) {
        unregisterCancellation = registerCancellationCallback(cancellationToken, cancel);
    }

    if (milliseconds !== -1) {
        const deadline = Date.now() + milliseconds;
        const scheduleRemaining = () => {
            const remaining = deadline - Date.now();
            if (remaining <= 0) {
                timeoutHandle = null;
                if (source.trySetResult(null)) unregisterCancellation();
                return;
            }
            timeoutHandle = setTimeout(
                scheduleRemaining,
                Math.min(remaining, maximumHostTimeoutMilliseconds));
        };
        scheduleRemaining();
    }

    return task;
}

function throwInvalidDelay(ctx) {
    throwManagedException(
        ctx,
        'System.ArgumentOutOfRangeException',
        'Task.Delay requires a timeout of -1 milliseconds or greater and no more than 4294967294 milliseconds.');
}
