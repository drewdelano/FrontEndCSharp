import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.DomEvent.StopImmediatePropagation()'] = function (ctx, domEvent) {
    unwrap(domEvent).stopImmediatePropagation();
};
