import { createElement, Fragment } from 'react';
import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.React.React.CreateFragment(Interop.JS.React.ReactNode[])'] = function (ctx, children) {
    const nativeChildren = unwrap(children).map(child => unwrap(child));
    return wrap(
        createElement(Fragment, null, ...nativeChildren),
        'Interop.JS.React.ReactElement');
};
