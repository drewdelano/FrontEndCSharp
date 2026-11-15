import { getMemberName } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/reflection.js';
globalThis.DotNet.Methods['System.Reflection.MemberInfo.get_Name()'] = function (ctx, member) {
    return getMemberName(member);
};
