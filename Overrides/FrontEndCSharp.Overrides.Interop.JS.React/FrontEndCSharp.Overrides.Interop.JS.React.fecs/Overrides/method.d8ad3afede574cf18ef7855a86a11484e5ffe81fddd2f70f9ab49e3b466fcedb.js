import { createElement } from 'react';
import { unwrap, wrap } from '../msil-type-helpers.js';
import { materializeReactProps } from '../runtime-assets/FrontEndCSharp.Overrides.Interop.JS.React.fecs/react-props.js';

globalThis.DotNet.Methods['Interop.JS.React.React.CreateElement(Interop.JS.React.ImportedReactComponent, Interop.JS.React.ReactProps, Interop.JS.React.ReactNode[])'] = function (ctx, component, props, children) {
    const nativeChildren = unwrap(children).map(child => unwrap(child));
    const nativeProps = materializeReactProps(ctx, props) ?? {};
    return wrap(
        createElement(unwrap(component), nativeProps, ...nativeChildren),
        'Interop.JS.React.ReactElement');
};
