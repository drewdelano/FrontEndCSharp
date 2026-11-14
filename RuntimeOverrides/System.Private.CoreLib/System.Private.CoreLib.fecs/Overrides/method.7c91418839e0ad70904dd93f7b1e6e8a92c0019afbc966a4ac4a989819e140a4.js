import { wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.AppContext.TryGetSwitch(System.String, System.Boolean&)'] = function (_ctx, _switchName, isEnabled) {
    isEnabled.value = wrap(false, 'System.Boolean');
    return wrap(false, 'System.Boolean');
};
