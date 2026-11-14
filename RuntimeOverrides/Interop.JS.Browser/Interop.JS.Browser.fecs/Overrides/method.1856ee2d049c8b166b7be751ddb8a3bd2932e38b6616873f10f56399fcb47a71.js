import { wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Window.get_InnerHeight()'] = function () {
    return wrap(globalThis.window.innerHeight, 'System.Int32');
};
