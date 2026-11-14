import { unwrap, wrap } from '../msil-type-helpers.js';
import { invokeDelegate } from '../jit-engine/calls.js';

globalThis.DotNet.Methods['Interop.JS.React.React.Component<TProps>(System.Func`3[T1, T2, TResult])'] = function (ctx, render) {
    const component = props => unwrap(invokeDelegate(
        ctx,
        render,
        [props.__fecsManagedProps, props.__fecsManagedChildren]));
    const genericTypeMap = ctx.genericParams === undefined ? null : { ...ctx.genericParams };
    return wrap(component, 'Interop.JS.React.ReactComponent`1[TProps]', genericTypeMap);
};
