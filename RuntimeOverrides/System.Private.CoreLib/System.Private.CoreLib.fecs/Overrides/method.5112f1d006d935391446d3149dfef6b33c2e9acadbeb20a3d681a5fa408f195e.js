import { initializeObject } from '../msil.js';
import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Text.Encoding.get_Unicode()'] = function () {
    const encoding = initializeObject('System.Text.Encoding');
    unwrap(encoding).$fecsWebName = 'utf-16';
    return encoding;
};
