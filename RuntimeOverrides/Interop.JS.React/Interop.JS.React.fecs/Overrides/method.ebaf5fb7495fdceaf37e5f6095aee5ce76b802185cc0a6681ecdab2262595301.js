import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.React.React.Text(System.String)'] = function (ctx, value) {
    return wrap(unwrap(value), 'Interop.JS.React.ReactNode');
};
