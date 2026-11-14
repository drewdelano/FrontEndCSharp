import { invokeDelegate } from '../../jit-engine/calls.js';
import { unwrap, wrap } from '../../msil-type-helpers.js';

const getPropertyNamesSignature = 'Interop.JS.React.ReactProps.GetPropertyNames()';
const getPropertyValueSignature = 'Interop.JS.React.ReactProps.GetPropertyValue(System.String)';
const snapshotSignature = 'Interop.JS.React.ReactProps.Snapshot()';

const numericTypes = new Set([
    'System.SByte',
    'System.Byte',
    'System.Int16',
    'System.UInt16',
    'System.Int32',
    'System.UInt32',
    'System.Int64',
    'System.UInt64',
    'System.Single',
    'System.Double'
]);

function managedMethod(signature) {
    const method = globalThis.DotNet.Methods[signature];
    if (typeof method !== 'function') {
        throw new Error(`Managed React property method '${signature}' was not emitted.`);
    }
    return method;
}

function unbox(value) {
    let current = value;
    while (current?.t === 'System.Object'
        && current.v !== null
        && typeof current.v === 'object'
        && Object.prototype.hasOwnProperty.call(current.v, 't')
        && Object.prototype.hasOwnProperty.call(current.v, 'v')) {
        current = current.v;
    }
    return current;
}

function materializeValue(ctx, name, value, nativeTypes) {
    const current = unbox(value);
    if (current === null || current === undefined) return null;

    if (current.t === 'System.String' || current.t === 'System.Char') {
        return unwrap(current);
    }
    if (current.t === 'System.Boolean') {
        return Boolean(unwrap(current));
    }
    if (numericTypes.has(current.t)) {
        return unwrap(current);
    }
    if (typeof current.t === 'string' && current.t.startsWith('System.Action`1')) {
        if (!name.startsWith('on')) {
            throw new TypeError(`React property '${name}' cannot use an event delegate value.`);
        }
        return event => invokeDelegate(ctx, current, [wrap(event, 'Interop.JS.Browser.DomEvent')]);
    }
    if (nativeTypes.has(current.t)) {
        return unwrap(current);
    }
    throw new TypeError(`React property '${name}' has unsupported managed type '${current.t ?? typeof current}'.`);
}

export function materializeReactProps(ctx, props, nativeTypeNames = []) {
    if (props === null || unwrap(props) === null) return null;

    const names = unwrap(managedMethod(getPropertyNamesSignature)(ctx, props));
    const getPropertyValue = managedMethod(getPropertyValueSignature);
    const nativeTypes = new Set(nativeTypeNames);
    const result = {};
    for (const managedName of names) {
        const name = unwrap(managedName);
        const value = getPropertyValue(ctx, props, managedName);
        result[name] = materializeValue(ctx, name, value, nativeTypes);
    }
    return result;
}

export function snapshotReactProps(ctx, props) {
    if (props === null || unwrap(props) === null) return null;
    return managedMethod(snapshotSignature)(ctx, props);
}
