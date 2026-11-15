import { createElement } from 'react';
import { unwrap, wrap } from '../msil-type-helpers.js';
import { materializeReactProps } from '../runtime-assets/FrontEndCSharp.Overrides.Interop.JS.React.fecs/react-props.js';

globalThis.DotNet.Methods['Interop.JS.React.React.CreateElement(System.String, Interop.JS.React.ReactProps, Interop.JS.React.ReactNode[])'] = function (ctx, elementName, props, children) {
    const nativeChildren = unwrap(children).map(child => unwrap(child));
    return wrap(
        createElement(unwrap(elementName), materializeReactProps(ctx, props), ...nativeChildren),
        'Interop.JS.React.ReactElement');
};
