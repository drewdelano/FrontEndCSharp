import { getCustomAttributes } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/reflection.js';
globalThis.DotNet.Methods['System.Reflection.MemberInfo.GetCustomAttributes(System.Boolean)'] = function (ctx, member) {
    return getCustomAttributes(member);
};
