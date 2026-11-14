import { createElement } from 'react';
import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.React.React.CreateElement<TProps>(Interop.JS.React.ReactComponent`1[TProps], TProps, Interop.JS.React.ReactNode[])'] = function (ctx, component, props, children) {
    const nativeChildren = unwrap(children).map(child => unwrap(child));
    return wrap(
        createElement(
            unwrap(component),
            { __fecsManagedProps: props, __fecsManagedChildren: children },
            ...nativeChildren),
        'Interop.JS.React.ReactElement');
};
