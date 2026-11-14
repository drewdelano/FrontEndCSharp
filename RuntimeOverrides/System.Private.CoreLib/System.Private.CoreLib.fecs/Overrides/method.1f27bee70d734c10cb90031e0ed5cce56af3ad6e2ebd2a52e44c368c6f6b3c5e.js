import { getDateTimeOffsetNow } from '../runtime-assets/System.Private.CoreLib.fecs/managed-temporal.js';

globalThis.DotNet.Methods['System.DateTimeOffset.get_Now()'] = function () {
    return getDateTimeOffsetNow();
};
