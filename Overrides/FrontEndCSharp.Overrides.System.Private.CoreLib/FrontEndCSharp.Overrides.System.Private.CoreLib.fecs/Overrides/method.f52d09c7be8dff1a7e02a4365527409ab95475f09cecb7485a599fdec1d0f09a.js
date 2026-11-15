import { getCultureName } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-culture.js';
globalThis.DotNet.Methods['System.Globalization.CultureInfo.get_Name()'] = function (_ctx, culture) {
    return getCultureName(culture);
};
