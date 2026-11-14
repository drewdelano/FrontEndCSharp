import { regexReplaceText } from '../runtime-assets/System.Text.RegularExpressions.fecs/managed-regex.js';
globalThis.DotNet.Methods['System.Text.RegularExpressions.Regex.Replace(System.String, System.String)'] = function (_ctx, regex, input, replacement) {
    return regexReplaceText(regex, input, replacement);
};
