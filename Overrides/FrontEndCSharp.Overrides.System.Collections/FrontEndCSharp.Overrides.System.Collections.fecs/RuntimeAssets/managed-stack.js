import { throwManagedException } from '../../managed-exceptions.js';
import { unwrap, wrap } from '../../msil-type-helpers.js';

const stacks = new WeakMap();
const enumerators = new WeakMap();

function fail(ctx, message) {
    throwManagedException(ctx, 'System.InvalidOperationException', message);
}

function stackState(ctx, stack) {
    const state = stacks.get(unwrap(stack));
    if (!state) fail(ctx, 'The stack has not been initialized.');
    return state;
}

function enumeratorState(ctx, enumerator) {
    const state = enumerators.get(unwrap(enumerator));
    if (!state) fail(ctx, 'The stack enumerator has not been initialized.');
    if (state.version !== state.stack.version) {
        fail(ctx, 'Collection was modified; enumeration operation may not execute.');
    }
    return state;
}

export function initializeStack(stack) {
    stacks.set(unwrap(stack), {
        items: [],
        version: 0,
        genericArguments: stack?.$typeArgs ?? null
    });
}

export function stackPush(ctx, stack, value) {
    const state = stackState(ctx, stack);
    state.items.push(value);
    state.version++;
}

export function stackPop(ctx, stack) {
    const state = stackState(ctx, stack);
    if (state.items.length === 0) fail(ctx, 'Stack empty.');
    state.version++;
    return state.items.pop();
}

export function stackCount(ctx, stack) {
    return wrap(stackState(ctx, stack).items.length, 'System.Int32');
}

export function stackGetEnumerator(ctx, stack) {
    const state = stackState(ctx, stack);
    const enumerator = wrap(
        {},
        'System.Collections.Generic.Stack`1+Enumerator[T]',
        state.genericArguments);
    enumerators.set(unwrap(enumerator), {
        stack: state,
        version: state.version,
        index: state.items.length,
        current: null
    });
    return enumerator;
}

export function stackEnumeratorMoveNext(ctx, enumerator) {
    const state = enumeratorState(ctx, enumerator);
    if (state.index <= 0) {
        state.index = -1;
        state.current = null;
        return wrap(false, 'System.Boolean');
    }
    state.index--;
    state.current = state.stack.items[state.index];
    return wrap(true, 'System.Boolean');
}

export function stackEnumeratorCurrent(ctx, enumerator) {
    const state = enumeratorState(ctx, enumerator);
    if (state.index < 0 || state.current === null) {
        fail(ctx, 'Enumeration has either not started or has already finished.');
    }
    return state.current;
}

export function stackEnumeratorDispose() {
}
