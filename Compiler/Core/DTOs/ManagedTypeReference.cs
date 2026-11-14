using Newtonsoft.Json;
using System.Collections.Generic;

namespace Compiler.DTOs
{
    public sealed class ManagedTypeReference
    {
        [JsonProperty("typeName", NullValueHandling = NullValueHandling.Ignore)]
        public string TypeName { get; init; }

        [JsonProperty("genericParams", NullValueHandling = NullValueHandling.Ignore)]
        public IReadOnlyDictionary<string, object> GenericParameters { get; init; }

        [JsonProperty("arrayElementType", NullValueHandling = NullValueHandling.Ignore)]
        public object ArrayElementType { get; init; }

        [JsonProperty("arrayKind", NullValueHandling = NullValueHandling.Ignore)]
        public string ArrayKind { get; init; }

        [JsonProperty("arrayRank", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int ArrayRank { get; init; }

        [JsonProperty("arraySizes", NullValueHandling = NullValueHandling.Ignore)]
        public IReadOnlyList<int> ArraySizes { get; init; }

        [JsonProperty("arrayLowerBounds", NullValueHandling = NullValueHandling.Ignore)]
        public IReadOnlyList<int> ArrayLowerBounds { get; init; }
    }
}
