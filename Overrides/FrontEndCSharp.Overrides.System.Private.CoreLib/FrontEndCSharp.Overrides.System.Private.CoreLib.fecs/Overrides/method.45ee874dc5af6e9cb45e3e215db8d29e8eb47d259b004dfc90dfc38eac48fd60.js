import { getCultureParent } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-culture.js';
globalThis.DotNet.Methods['System.Globalization.CultureInfo.get_Parent()'] = function () {
    return getCultureParent();
};
