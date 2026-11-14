import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.DomEvent.get_DefaultPrevented()'] = function (ctx, domEvent) {
    return wrap(unwrap(domEvent).defaultPrevented, 'System.Boolean');
};
