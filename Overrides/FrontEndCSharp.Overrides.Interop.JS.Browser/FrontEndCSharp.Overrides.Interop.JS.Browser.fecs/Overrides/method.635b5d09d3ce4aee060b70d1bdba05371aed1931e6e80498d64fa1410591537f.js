import { wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Window.get_DevicePixelRatio()'] = function () {
    return wrap(globalThis.window.devicePixelRatio, 'System.Double');
};
