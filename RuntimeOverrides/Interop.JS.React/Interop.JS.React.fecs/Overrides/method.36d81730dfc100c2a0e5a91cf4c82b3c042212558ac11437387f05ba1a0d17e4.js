import { createElement } from 'react';
import { unwrap, wrap } from '../msil-type-helpers.js';
import {
    materializeReactProps,
    snapshotReactProps
} from '../runtime-assets/Interop.JS.React.fecs/react-props.js';

globalThis.DotNet.Methods['Interop.JS.React.React.CreateElement(Interop.JS.React.ReactComponent, Interop.JS.React.ReactProps, Interop.JS.React.ReactNode[])'] = function (ctx, component, props, children) {
    const nativeChildren = unwrap(children).map(child => unwrap(child));
    const managedProps = snapshotReactProps(ctx, props);
    const nativeProps = materializeReactProps(ctx, managedProps) ?? {};
    nativeProps.__fecsManagedReactProps = managedProps;
    return wrap(
        createElement(unwrap(component), nativeProps, ...nativeChildren),
        'Interop.JS.React.ReactElement');
};
