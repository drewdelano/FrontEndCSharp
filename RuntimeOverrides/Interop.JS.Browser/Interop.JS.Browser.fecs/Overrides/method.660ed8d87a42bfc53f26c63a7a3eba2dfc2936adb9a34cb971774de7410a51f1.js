import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.DomEvent.get_Type()'] = function (ctx, domEvent) {
    return wrap(unwrap(domEvent).type, 'System.String');
};
