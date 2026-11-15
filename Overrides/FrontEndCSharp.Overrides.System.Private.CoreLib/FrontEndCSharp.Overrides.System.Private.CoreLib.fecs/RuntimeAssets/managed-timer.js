import { invokeManagedDelegate } from '../../jit-engine/calls.js';
import { throwManagedException } from '../../managed-exceptions.js';
import { isManagedNull, unwrap, wrap } from '../../msil-type-helpers.js';

const timerStates = new WeakMap();

export function initializeTimer(ctx, timer, callback, callbackState, dueTime, period) {
    if (isManagedNull(callback)) {
        throwManagedException(
            ctx,
            'System.ArgumentNullException',
            "Value cannot be null. (Parameter 'callback')");
    }

    const state = {
        callback,
        callbackState,
        context: ctx,
        disposed: false,
        generation: 0,
        handle: null,
        period: -1
    };
    timerStates.set(unwrap(timer), state);
    changeTimer(ctx, timer, dueTime, period);
}

export function changeTimer(ctx, timer, dueTime, period) {
    const state = requireTimerState(ctx, timer);
    if (state.disposed) {
        throwManagedException(
            ctx,
            'System.ObjectDisposedException',
            'Cannot access a disposed Timer.');
    }

    const dueMilliseconds = requireTimeout(ctx, dueTime, 'dueTime');
    state.period = requireTimeout(ctx, period, 'period');
    state.generation++;
    clearScheduledCallback(state);
    if (dueMilliseconds !== -1) scheduleCallback(state, dueMilliseconds, state.generation);
    return wrap(true, 'System.Boolean');
}

export function disposeTimer(ctx, timer) {
    const state = requireTimerState(ctx, timer);
    if (state.disposed) return;
    state.disposed = true;
    state.generation++;
    clearScheduledCallback(state);
}

function requireTimerState(ctx, timer) {
    const rawTimer = unwrap(timer);
    const state = rawTimer !== null && typeof rawTimer === 'object'
        ? timerStates.get(rawTimer)
        : null;
    if (state === undefined || state === null) {
        throwManagedException(ctx, 'System.InvalidOperationException', 'The Timer was not initialized.');
    }
    return state;
}

function requireTimeout(ctx, value, parameterName) {
    const milliseconds = unwrap(value);
    if (!Number.isInteger(milliseconds) || milliseconds < -1) {
        throwManagedException(
            ctx,
            'System.ArgumentOutOfRangeException',
            `Timer ${parameterName} must be -1 or greater.`);
    }
    return milliseconds;
}

function scheduleCallback(state, delay, generation) {
    state.handle = setTimeout(() => {
        state.handle = null;
        if (state.disposed || state.generation !== generation) return;

        invokeManagedDelegate(state.context, state.callback, [state.callbackState]);
        if (!state.disposed && state.generation === generation && state.period !== -1) {
            scheduleCallback(state, state.period, generation);
        }
    }, delay);
}

function clearScheduledCallback(state) {
    if (state.handle === null) return;
    clearTimeout(state.handle);
    state.handle = null;
}
