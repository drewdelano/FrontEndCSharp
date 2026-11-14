import { getInvariantCulture } from '../runtime-assets/System.Private.CoreLib.fecs/managed-culture.js';
globalThis.DotNet.Methods['System.Globalization.CultureInfo.get_CurrentUICulture()'] = function () {
    return getInvariantCulture();
};
