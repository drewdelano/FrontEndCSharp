import { getInvariantCulture } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-culture.js';
globalThis.DotNet.Methods['System.Globalization.CultureInfo.get_InvariantCulture()'] = function () {
    return getInvariantCulture();
};
