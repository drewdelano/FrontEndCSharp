import { regexReplace } from '../runtime-assets/FrontEndCSharp.Overrides.System.Text.RegularExpressions.fecs/managed-regex.js';
globalThis.DotNet.Methods['System.Text.RegularExpressions.Regex.Replace(System.String, System.Text.RegularExpressions.MatchEvaluator)'] = function (ctx, regex, input, evaluator) {
    return regexReplace(ctx, regex, input, evaluator);
};
