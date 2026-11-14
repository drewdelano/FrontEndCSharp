import { getCustomAttributes } from '../runtime-assets/System.Private.CoreLib.fecs/reflection.js';
globalThis.DotNet.Methods['System.Reflection.MemberInfo.GetCustomAttributes(System.Boolean)'] = function (ctx, member) {
    return getCustomAttributes(member);
};
