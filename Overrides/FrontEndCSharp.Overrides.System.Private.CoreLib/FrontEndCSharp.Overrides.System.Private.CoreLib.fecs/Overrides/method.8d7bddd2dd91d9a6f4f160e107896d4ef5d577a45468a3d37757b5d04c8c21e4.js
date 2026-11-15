import { isNeutralCulture } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-culture.js';
globalThis.DotNet.Methods['System.Globalization.CultureInfo.get_IsNeutralCulture()'] = function () {
    return isNeutralCulture();
};
