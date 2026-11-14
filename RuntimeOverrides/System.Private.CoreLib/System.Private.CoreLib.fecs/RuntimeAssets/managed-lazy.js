import { invokeManagedDelegate } from '../../jit-engine/calls.js';
import { throwManagedException } from '../../managed-exceptions.js';
import { isManagedNull, throwDotNetException, unwrap } from '../../msil-type-helpers.js';

const states = new WeakMap();

function requireState(ctx, lazy) {
    const raw = unwrap(lazy);
    const state = raw !== null && typeof raw === 'object'
        ? states.get(raw)
        : null;
    if (state === undefined || state === null) {
        throwManagedException(ctx, 'System.InvalidOperationException', 'The Lazy value was not initialized.');
    }
    return state;
}

export function initializeLazy(ctx, lazy, valueFactory) {
    if (isManagedNull(valueFactory)) {
        throwManagedException(ctx, 'System.ArgumentNullException', "Value cannot be null. (Parameter 'valueFactory')");
    }
    states.set(unwrap(lazy), {
        status: 'uninitialized',
        valueFactory,
        value: null,
        exception: null
    });
}

export function isLazyValueCreated(ctx, lazy) {
    return requireState(ctx, lazy).status === 'succeeded';
}

export function getLazyValue(ctx, lazy) {
    const state = requireState(ctx, lazy);
    if (state.status === 'succeeded') return state.value;
    if (state.status === 'faulted') throwDotNetException(ctx, state.exception);
    if (state.status === 'evaluating') {
        throwManagedException(
            ctx,
            'System.InvalidOperationException',
            'ValueFactory attempted to access the Value property of this instance.');
    }

    state.status = 'evaluating';
    try {
        state.value = invokeManagedDelegate(ctx, state.valueFactory, []);
        state.valueFactory = null;
        state.status = 'succeeded';
        return state.value;
    } catch (error) {
        if (error?.dotNetException === undefined) {
            state.status = 'uninitialized';
            throw error;
        }
        state.exception = error.dotNetException;
        state.valueFactory = null;
        state.status = 'faulted';
        throw error;
    }
}
