import { unwrap, wrap } from '../msil-type-helpers.js';
import { invokeDelegate } from '../jit-engine/calls.js';

globalThis.DotNet.Methods['Interop.JS.React.React.Component(System.Func`2[T, TResult])'] = function (ctx, render) {
    const component = props => {
        const managedProps = props.__fecsManagedReactProps;
        if (managedProps === undefined) {
            throw new Error('A managed React component requires a managed ReactProps snapshot.');
        }
        return unwrap(invokeDelegate(ctx, render, [managedProps]));
    };
    return wrap(component, 'Interop.JS.React.ReactComponent');
};
