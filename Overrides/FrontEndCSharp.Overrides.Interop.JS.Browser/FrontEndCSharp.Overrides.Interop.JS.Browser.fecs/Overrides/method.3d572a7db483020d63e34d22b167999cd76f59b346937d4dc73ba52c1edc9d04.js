import { wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Window.get_InnerWidth()'] = function () {
    return wrap(globalThis.window.innerWidth, 'System.Int32');
};
