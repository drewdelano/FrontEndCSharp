import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.DomEvent.StopPropagation()'] = function (ctx, domEvent) {
    unwrap(domEvent).stopPropagation();
};
