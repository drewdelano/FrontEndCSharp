import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.DomEvent.get_Bubbles()'] = function (ctx, domEvent) {
    return wrap(unwrap(domEvent).bubbles, 'System.Boolean');
};
