import { createRoot } from 'react-dom/client';
import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.React.ReactRoot.Create(Interop.JS.Browser.Element)'] = function (ctx, container) {
    return wrap(createRoot(unwrap(container)), 'Interop.JS.React.ReactRoot');
};
