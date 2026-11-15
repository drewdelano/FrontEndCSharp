import { wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.HttpRequestMessage.get_DefaultRequestVersion()'] = function () {
    return wrap({
        _Major: wrap(1, 'System.Int32'),
        _Minor: wrap(1, 'System.Int32'),
        _Build: wrap(-1, 'System.Int32'),
        _Revision: wrap(-1, 'System.Int32')
    }, 'System.Version');
};
