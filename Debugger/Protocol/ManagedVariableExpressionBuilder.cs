using System;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;

namespace FrontEndCSharp.Debugging;

internal static class ManagedVariableExpressionBuilder
{
    internal const char ManagedTypeSeparator = '\uFDD0';
    internal const char ManagedArrayLengthSeparator = '\uFDD1';
    internal const char ManagedStorageSeparator = '\uFDD2';
    private const string ManagedValuePresenter =
        "const views=new WeakMap();const runtime=wrapped=>{let value=wrapped;"
        + "while(value&&typeof value==='object'&&'value' in value"
        + "&&!('t' in value&&'v' in value))value=value.value;return value;};"
        + "const managedType=wrapped=>{const value=runtime(wrapped);"
        + "if(!(value&&typeof value==='object'&&'t' in value&&'v' in value))return '';"
        + "const type=String(value.t||'Object');return Array.isArray(value.v)"
        + "?type+'\uFDD1'+value.v.length:type;};"
        + "const fail=message=>{throw new Error('FrontEndCSharp: '+message);};"
        + "const convert=(wrapped,next)=>{const value=runtime(wrapped);"
        + "if(!(value&&typeof value==='object'&&'t' in value&&'v' in value))"
        + "fail('this value has no managed type information.');"
        + "const type=String(value.t||'').split('!').pop();if(next===null){"
        + "if(type==='System.String'||type==='System.Object'||type.endsWith('[]'))return null;"
        + "fail('null cannot be assigned to '+type+'.');}"
        + "if(type==='System.String'){if(typeof next!=='string')fail('a string value is required.');return next;}"
        + "if(type==='System.Char'){if(typeof next!=='string'||[...next].length!==1)fail('a char requires exactly one character.');return next;}"
        + "if(type==='System.Boolean'){if(typeof next!=='boolean')fail('a bool value is required.');return next;}"
        + "const ranges={'System.SByte':[-128,127],'System.Byte':[0,255],'System.Int16':[-32768,32767],"
        + "'System.UInt16':[0,65535],'System.Int32':[-2147483648,2147483647],'System.UInt32':[0,4294967295]};"
        + "if(type in ranges){const range=ranges[type];if(!Number.isSafeInteger(next)||next<range[0]||next>range[1])"
        + "fail('the value is outside the range of '+type+'.');return next;}"
        + "if(type==='System.Int64'||type==='System.UInt64'){let parsed;try{parsed=typeof next==='bigint'?next:BigInt(next);}catch{fail('an integer value is required.');}"
        + "if(type==='System.Int64'&&(parsed< -9223372036854775808n||parsed>9223372036854775807n)||type==='System.UInt64'&&(parsed<0n||parsed>18446744073709551615n))"
        + "fail('the value is outside the range of '+type+'.');return parsed;}"
        + "if(type==='System.Single'||type==='System.Double'||type==='System.Decimal'){if(typeof next!=='number'||!Number.isFinite(next))fail('a finite number is required.');return next;}"
        + "fail('assignments to '+(type||'this value')+' are not supported.');};"
        + "const assign=(read,write,next)=>{const wrapped=read();const value=runtime(wrapped);"
        + "const converted=convert(wrapped,next);if(value&&typeof value==='object'&&'t' in value&&'v' in value)value.v=converted;"
        + "else write(converted);};"
        + "const readPath=(wrapped,path)=>{let value=runtime(wrapped);for(const name of path){"
        + "if(!(value&&typeof value==='object'&&'t' in value&&'v' in value))return undefined;"
        + "const raw=value.v;if(raw===null||typeof raw!=='object')return undefined;value=runtime(raw[name]);}return value;};"
        + "const writePath=(wrapped,path,next)=>{let value=runtime(wrapped);for(let index=0;index<path.length-1;index++){"
        + "if(!(value&&typeof value==='object'&&'t' in value&&'v' in value))return;"
        + "const raw=value.v;if(raw===null||typeof raw!=='object')return;value=runtime(raw[path[index]]);}"
        + "if(value&&typeof value==='object'&&'t' in value&&'v' in value&&value.v!==null&&typeof value.v==='object')"
        + "value.v[path[path.length-1]]=next;};"
        + "const add=(target,name,read,write,storage)=>Object.defineProperty(target,"
        + "name+(storage?'\\uFDD2'+storage:'')+'\\uFDD0'+managedType(read()),{enumerable:true,get:()=>managed(read()),set:next=>assign(read,write,next)});"
        + "const fieldDefinition=storageKey=>{for(const type of Object.values(globalThis.DotNet?.Types??{})){"
        + "const definition=type?.instances?.[storageKey];if(definition!==undefined)return definition;}return undefined;};"
        + "const fieldKey=(wrapped,name)=>{const value=runtime(wrapped);let typeName=value?.t;while(typeof typeName==='string'){"
        + "const type=globalThis.DotNet?.Types?.[typeName];if(type===undefined)break;"
        + "const match=Object.entries(type.instances??{}).find(([,definition])=>definition?.name===name);"
        + "if(match!==undefined)return match[0];typeName=typeof type.baseType==='string'?type.baseType:type.baseType?.typeName;}return name;};"
        + "function managed(wrapped){const value=runtime(wrapped);"
        + "if(!(value&&typeof value==='object'&&'t' in value&&'v' in value))return value;"
        + "const raw=value.v;if(raw===null||typeof raw!=='object')return raw;"
        + "if(views.has(raw))return views.get(raw);"
        + "if(Array.isArray(raw)){const array=[];views.set(raw,array);"
        + "for(let index=0;index<raw.length;index++)add(array,String(index),()=>raw[index],next=>raw[index]=next);return array;}"
        + "const type=String(value.t||'Object').split('.').pop();"
        + "const prototype={};Object.defineProperty(prototype,Symbol.toStringTag,{value:type});"
        + "const view=Object.create(prototype);"
        + "views.set(raw,view);const entries=Object.entries(raw).map(([rawName,field])=>({rawName,field,definition:fieldDefinition(rawName)}));"
        + "const counts=new Map();for(const entry of entries){const name=entry.definition?.name??entry.rawName;counts.set(name,(counts.get(name)??0)+1);}"
        + "for(const entry of entries){const logical=entry.definition?.name??entry.rawName;const match=/^<(.+)>k__BackingField$/.exec(logical);"
        + "let name=match?match[1]:logical;if(counts.get(logical)>1)name=(entry.definition?.declaringType??'unknown')+'.'+name;"
        + "add(view,name,()=>raw[entry.rawName],next=>raw[entry.rawName]=next);}return view;}";

    private static string GetManagedStorageKind(ManagedRuntimeValueKind kind) => kind switch
    {
        ManagedRuntimeValueKind.Argument => "argument",
        ManagedRuntimeValueKind.Local => "local",
        ManagedRuntimeValueKind.LocalField => "local",
        ManagedRuntimeValueKind.Field => "field",
        _ => "unknown"
    };

    internal static string BuildManagedLocalsExpression(ManagedFrameLayout layout)
    {
        var properties = string.Join(string.Empty, layout.Variables.Select(variable =>
            "add(scope," + JsonConvert.SerializeObject(variable.Name) + ",()=>" + (variable.Kind switch
            {
                ManagedRuntimeValueKind.Argument => string.Format(
                    CultureInfo.InvariantCulture,
                    "frame?.methodArgs?.[{0}]",
                    variable.Index),
                ManagedRuntimeValueKind.Local => string.Format(
                    CultureInfo.InvariantCulture,
                    "frame?.locals?.[{0}]",
                    variable.Index),
                ManagedRuntimeValueKind.LocalField => string.Format(
                    CultureInfo.InvariantCulture,
                    "readPath(frame?.locals?.[{0}],{1})",
                    variable.Index,
                    JsonConvert.SerializeObject(variable.FieldPath)),
                ManagedRuntimeValueKind.Field => "instance?.[fieldKey(instanceValue,"
                    + JsonConvert.SerializeObject(variable.FieldName ?? string.Empty) + ")]",
                _ => "undefined"
            }) + ",next=>" + (variable.Kind switch
            {
                ManagedRuntimeValueKind.Argument => string.Format(
                    CultureInfo.InvariantCulture,
                    "frame.methodArgs[{0}]=next",
                    variable.Index),
                ManagedRuntimeValueKind.Local => string.Format(
                    CultureInfo.InvariantCulture,
                    "frame.locals[{0}]=next",
                    variable.Index),
                ManagedRuntimeValueKind.LocalField => string.Format(
                    CultureInfo.InvariantCulture,
                    "writePath(frame?.locals?.[{0}],{1},next)",
                    variable.Index,
                    JsonConvert.SerializeObject(variable.FieldPath)),
                ManagedRuntimeValueKind.Field => "instance[fieldKey(instanceValue,"
                    + JsonConvert.SerializeObject(variable.FieldName ?? string.Empty) + ")]=next",
                _ => "undefined"
            }) + "," + JsonConvert.SerializeObject(GetManagedStorageKind(variable.Kind)) + ");"));
        return "(()=>{" + ManagedValuePresenter
            + "const frame=typeof opts==='object'&&opts?.ctx?opts:"
            + "globalThis.__FrontEndCSharpDebugger?.currentStop?.frame;"
            + "const instanceValue=runtime(frame?.methodArgs?.[0]);"
            + "const instance=instanceValue&&typeof instanceValue==='object'"
            + "&&'t' in instanceValue&&'v' in instanceValue?instanceValue.v:instanceValue;"
            + "const scope=Object.create(null);" + properties + "return scope;})()";
    }

    internal static bool TryResolveManagedHoverName(
        ManagedFrameLayout layout,
        string expression,
        out string name)
    {
        name = expression;
        if (layout.Variables.Any(variable => string.Equals(
            variable.Name,
            expression,
            StringComparison.Ordinal)))
        {
            return true;
        }
        if (!layout.Variables.Any(variable => string.Equals(
                variable.Name,
                "this",
                StringComparison.Ordinal)))
        {
            return false;
        }
        if (name.StartsWith("this.", StringComparison.Ordinal))
        {
            name = name.Substring("this.".Length);
        }
        if (name.StartsWith("@", StringComparison.Ordinal))
        {
            name = name.Substring(1);
        }
        return System.Text.RegularExpressions.Regex.IsMatch(
            name,
            @"^[_\p{L}][_\p{L}\p{Nd}]*$",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }

    internal static string BuildManagedHoverExpression(string scopeExpression, string variableName) =>
        "(()=>{const scope=(" + scopeExpression + ");const prefix="
        + JsonConvert.SerializeObject(variableName)
        + ";const key=Object.keys(scope).find(name=>name.startsWith(prefix+'\\uFDD0')"
        + "||name.startsWith(prefix+'\\uFDD2'));"
        + "if(key!==undefined)return scope[key];"
        + "const thisKey=Object.keys(scope).find(name=>name.startsWith('this\\uFDD0')"
        + "||name.startsWith('this\\uFDD2'));const instance=thisKey===undefined?undefined:scope[thisKey];"
        + "const memberKey=instance==null?undefined:Object.keys(instance).find(name=>name.startsWith(prefix+'\\uFDD0')"
        + "||name.startsWith(prefix+'\\uFDD2'));return memberKey===undefined?undefined:instance[memberKey];})()";

    internal static string BuildManagedParentLocalsExpression(
        ManagedFrameLayout layout,
        int frameIndex,
        bool isSnapshot)
    {
        if (!isSnapshot)
        {
            var liveProperties = string.Join(string.Empty, layout.Variables.Select(variable =>
                "add(scope," + JsonConvert.SerializeObject(variable.Name) + ",()=>" + (variable.Kind switch
                {
                    ManagedRuntimeValueKind.Argument => string.Format(
                        CultureInfo.InvariantCulture,
                        "frame?.methodArgs?.[{0}]",
                        variable.Index),
                    ManagedRuntimeValueKind.Local => string.Format(
                        CultureInfo.InvariantCulture,
                        "frame?.locals?.[{0}]",
                        variable.Index),
                    ManagedRuntimeValueKind.LocalField => string.Format(
                        CultureInfo.InvariantCulture,
                        "readPath(frame?.locals?.[{0}],{1})",
                        variable.Index,
                        JsonConvert.SerializeObject(variable.FieldPath)),
                    ManagedRuntimeValueKind.Field => "instance?.[fieldKey(instanceValue,"
                        + JsonConvert.SerializeObject(variable.FieldName ?? string.Empty) + ")]",
                    _ => "undefined"
                }) + ",next=>" + (variable.Kind switch
                {
                    ManagedRuntimeValueKind.Argument => string.Format(
                        CultureInfo.InvariantCulture,
                        "frame.methodArgs[{0}]=next",
                        variable.Index),
                    ManagedRuntimeValueKind.Local => string.Format(
                        CultureInfo.InvariantCulture,
                        "frame.locals[{0}]=next",
                        variable.Index),
                    ManagedRuntimeValueKind.LocalField => string.Format(
                        CultureInfo.InvariantCulture,
                        "writePath(frame?.locals?.[{0}],{1},next)",
                        variable.Index,
                        JsonConvert.SerializeObject(variable.FieldPath)),
                    ManagedRuntimeValueKind.Field => "instance[fieldKey(instanceValue,"
                        + JsonConvert.SerializeObject(variable.FieldName ?? string.Empty) + ")]=next",
                    _ => "undefined"
                }) + "," + JsonConvert.SerializeObject(GetManagedStorageKind(variable.Kind)) + ");"));
            return string.Format(
                CultureInfo.InvariantCulture,
                "(()=>{{const ctx=typeof opts==='object'&&opts?.ctx?opts.ctx:globalThis.__FrontEndCSharpDebugger?.currentStop?.frame?.ctx;const frame=ctx?.debugFrames?.[{0}];{2}const instanceValue=runtime(frame?.methodArgs?.[0]);const instance=instanceValue&&typeof instanceValue==='object'&&'t' in instanceValue&&'v' in instanceValue?instanceValue.v:instanceValue;const scope=Object.create(null);{1}return scope;}})()",
                frameIndex,
                liveProperties,
                ManagedValuePresenter);
        }
        var properties = string.Join(",", layout.Variables
            .Where(variable => variable.Kind != ManagedRuntimeValueKind.LocalField)
            .Select(variable =>
            JsonConvert.SerializeObject(
                variable.Name + ManagedStorageSeparator + GetManagedStorageKind(variable.Kind))
            + ":" + (variable.Kind switch
            {
                ManagedRuntimeValueKind.Argument => string.Format(
                    CultureInfo.InvariantCulture,
                    "display(frame?.arguments?.[{0}])",
                    variable.Index),
                ManagedRuntimeValueKind.Local => string.Format(
                    CultureInfo.InvariantCulture,
                    "display(frame?.locals?.[{0}])",
                    variable.Index),
                ManagedRuntimeValueKind.Field => "display(frame?.fields?.find(item=>item?.name==="
                    + JsonConvert.SerializeObject(variable.FieldName ?? string.Empty) + "))",
                _ => "undefined"
            })));
        return string.Format(
            CultureInfo.InvariantCulture,
            "(()=>{{const ctx=typeof opts==='object'&&opts?.ctx?opts.ctx:globalThis.__FrontEndCSharpDebugger?.currentStop?.frame?.ctx;const frame=ctx?.asyncParents?.[{0}];const display=snapshot=>snapshot?.display;return Object.assign(Object.create(null),{{{1}}});}})()",
            frameIndex,
            properties);
    }


}
