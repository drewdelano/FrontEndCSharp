import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.DomEvent.PreventDefault()'] = function (ctx, domEvent) {
    unwrap(domEvent).preventDefault();
};
