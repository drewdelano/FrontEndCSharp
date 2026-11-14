import { getCultureParent } from '../runtime-assets/System.Private.CoreLib.fecs/managed-culture.js';
globalThis.DotNet.Methods['System.Globalization.CultureInfo.get_Parent()'] = function () {
    return getCultureParent();
};
