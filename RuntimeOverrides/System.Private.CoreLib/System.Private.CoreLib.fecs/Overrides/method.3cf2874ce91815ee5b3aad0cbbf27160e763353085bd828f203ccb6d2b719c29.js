import { getDateTimeToday } from '../runtime-assets/System.Private.CoreLib.fecs/managed-temporal.js';

globalThis.DotNet.Methods['System.DateTime.get_Today()'] = function () {
    return getDateTimeToday();
};
