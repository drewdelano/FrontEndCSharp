using System;
using Compiler.DTOs;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Compiler.Helpers
{
    public class GenericsHelper
    {
        public static TypeInfo GetPossiblyGenericReferenceFromType(TypeInfo type)
        {
            if (!type.IsGenericType)
            {
                return type;
            }
            return (TypeInfo)type.GetGenericTypeDefinition();
        }

        public static MethodBase GetPossiblyGenericReferenceFromMethod(MethodBase method)
        {
            var methodName = method.Name;
            var declType = method.DeclaringType;
            if (!declType.IsGenericType)
            {
                return method;
            }
            declType = declType.GetGenericTypeDefinition();
            if (method is ConstructorInfo)
            {
                method = declType
                    .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public)
                    .Single(m => m.HasSameMetadataDefinitionAs(method));
            }
            else
            {
                method = declType
                    .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public)
                    .Single(m => m.HasSameMetadataDefinitionAs(method));
            }

            return method;
        }

        public static FieldInfo GetPossiblyGenericReferenceFromField(FieldInfo field)
        {
            var fieldName = field.Name;
            var declType = field.DeclaringType;
            if (!declType.IsGenericType)
            {
                return field;
            }
            declType = declType.GetGenericTypeDefinition();
            field = declType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);

            return field;
        }

        public static object GetTypeReference(Type type)
        {
            if (type.IsArray)
            {
                return new ManagedTypeReference
                {
                    ArrayElementType = GetTypeReference(type.GetElementType()!),
                    ArrayKind = type.IsSZArray ? "szarray" : "array",
                    ArrayRank = type.GetArrayRank(),
                    ArraySizes = Array.Empty<int>(),
                    ArrayLowerBounds = Array.Empty<int>(),
                };
            }
            if (!type.IsConstructedGenericType)
            {
                return NameNormalizers.GetFullTypeName(type);
            }

            return new ManagedTypeReference
            {
                TypeName = NameNormalizers.GetFullTypeName(type),
                GenericParameters = GetGenericTypeMap(type)
            };
        }

        public static Dictionary<string, object> GetGenericTypeMap(Type declType)
        {
            if (!declType.IsConstructedGenericType)
            {
                return null;
            }
            var dicValues = declType.GenericTypeArguments.Select(GetTypeReference).ToList();
            var typeDef = declType.GetGenericTypeDefinition();
            var dicKeys = ((TypeInfo)typeDef).GenericTypeParameters;
            var dic = new Dictionary<string, object>();
            for (var i = 0; i < dicKeys.Count(); i++)
            {
                dic.Add(dicKeys[i].Name, dicValues[i]);
            }
            return dic;
        }

        public static Dictionary<string, object> GetGenericMethodMap(MethodInfo methodInfo)
        {
            if (!methodInfo.IsGenericMethod || methodInfo.IsGenericMethodDefinition)
            {
                return null;
            }
            
            var dicValues = methodInfo.GetGenericArguments().Select(GetTypeReference).ToList();
            var typeDef = methodInfo.GetGenericMethodDefinition();
            var dicKeys = typeDef.GetGenericArguments();
            var dic = new Dictionary<string, object>();
            for (var i = 0; i < dicKeys.Count(); i++)
            {
                dic.Add(dicKeys[i].Name, dicValues[i]);
            }
            return dic;
        }
    }
}
