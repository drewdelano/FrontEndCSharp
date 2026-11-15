import { getDateTimeNow } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-temporal.js';

globalThis.DotNet.Methods['System.DateTime.get_Now()'] = function () {
    return getDateTimeNow();
};
