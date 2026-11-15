import { invokeManagedDelegate } from '../../jit-engine/calls.js';
import { managedPrimitiveEquals } from '../FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-object.js';
import { isManagedNull, unwrap } from '../../msil-type-helpers.js';

const dictionaries = new WeakMap();

export function initializeConcurrentDictionary(dictionary) {
    dictionaries.set(unwrap(dictionary), []);
}

function keysEqual(left, right) {
    const primitiveResult = managedPrimitiveEquals(left, right);
    if (primitiveResult !== null) return primitiveResult;
    if (left?.t === 'System.Type' && right?.t === 'System.Type') {
        return unwrap(left) === unwrap(right);
    }
    return unwrap(left) === unwrap(right);
}

export function concurrentDictionaryGetOrAdd(ctx, dictionary, key, valueFactory, factoryArgument) {
    if (isManagedNull(key)) throw new TypeError('ConcurrentDictionary keys cannot be null.');
    const entries = dictionaries.get(unwrap(dictionary));
    if (entries === undefined) throw new Error('The concurrent dictionary has not been initialized.');
    const existing = entries.find(entry => keysEqual(entry.key, key));
    if (existing !== undefined) return existing.value;
    const factoryArguments = arguments.length === 5 ? [key, factoryArgument] : [key];
    const value = invokeManagedDelegate(ctx, valueFactory, factoryArguments);
    entries.push({ key, value });
    return value;
}
