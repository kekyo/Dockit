using Mono.Cecil;
using System.Collections.Generic;
using System.Linq;

namespace Dockit.Internal;

internal enum NullableReferenceTypeState : byte
{
    Oblivious = 0,
    NotNull = 1,
    Nullable = 2,
}

internal sealed class NullableReferenceTypeContext
{
    private readonly Queue<NullableReferenceTypeState> states;

    public NullableReferenceTypeContext(
        NullableReferenceTypeState defaultState,
        IEnumerable<NullableReferenceTypeState> states)
    {
        DefaultState = defaultState;
        this.states = new Queue<NullableReferenceTypeState>(states);
    }

    public NullableReferenceTypeState DefaultState { get; }

    public NullableReferenceTypeState ConsumeState() =>
        this.states.Count >= 1 ?
            this.states.Dequeue() :
            DefaultState;
}

internal static class NullableReferenceTypes
{
    private static GenericParameter[] GetDeclaredGenericParameters(TypeReference type)
    {
        var inheritedCount = type.DeclaringType?.GenericParameters.Count ?? 0;
        var declaredCount = System.Math.Max(0, type.GenericParameters.Count - inheritedCount);
        return type.GenericParameters.
            Skip(System.Math.Max(0, type.GenericParameters.Count - declaredCount)).
            ToArray();
    }

    private static TypeReference[] GetDeclaredGenericArguments(GenericInstanceType genericInstanceType)
    {
        var declaredCount = GetDeclaredGenericParameters(genericInstanceType.ElementType).Length;
        return genericInstanceType.GenericArguments.
            Skip(System.Math.Max(0, genericInstanceType.GenericArguments.Count - declaredCount)).
            ToArray();
    }

    private static NullableReferenceTypeState DecodeState(byte value) =>
        value switch
        {
            1 => NullableReferenceTypeState.NotNull,
            2 => NullableReferenceTypeState.Nullable,
            _ => NullableReferenceTypeState.Oblivious,
        };

    private static NullableReferenceTypeState? TryGetNullableContext(ICustomAttributeProvider provider)
    {
        var attribute = provider.CustomAttributes.FirstOrDefault(customAttribute =>
            customAttribute.AttributeType.FullName == "System.Runtime.CompilerServices.NullableContextAttribute");
        if (attribute is null || attribute.ConstructorArguments.Count != 1)
        {
            return null;
        }

        return attribute.ConstructorArguments[0].Value is byte value ?
            DecodeState(value) :
            null;
    }

    private static NullableReferenceTypeState[] GetNullableStates(ICustomAttributeProvider provider)
    {
        var attribute = provider.CustomAttributes.FirstOrDefault(customAttribute =>
            customAttribute.AttributeType.FullName == "System.Runtime.CompilerServices.NullableAttribute");
        if (attribute is null || attribute.ConstructorArguments.Count != 1)
        {
            return Utilities.Empty<NullableReferenceTypeState>();
        }

        return attribute.ConstructorArguments[0].Value switch
        {
            byte value => new[] { DecodeState(value) },
            CustomAttributeArgument[] values => values.
                Where(value => value.Value is byte).
                Select(value => DecodeState((byte)value.Value!)).
                ToArray(),
            _ => Utilities.Empty<NullableReferenceTypeState>(),
        };
    }

    private static IEnumerable<ICustomAttributeProvider> EnumerateTypeChain(TypeDefinition? type)
    {
        for (var current = type; current is not null; current = current.DeclaringType)
        {
            yield return current;
        }

        if (type?.Module is { } module)
        {
            yield return module;
            yield return module.Assembly;
        }
    }

    private static NullableReferenceTypeContext CreateContext(
        IEnumerable<ICustomAttributeProvider?> directProviders,
        IEnumerable<ICustomAttributeProvider> fallbackProviders)
    {
        var providers = directProviders.
            Where(provider => provider is not null).
            Cast<ICustomAttributeProvider>().
            ToArray();

        var states = providers.
            Select(GetNullableStates).
            FirstOrDefault(currentStates => currentStates.Length >= 1) ??
            Utilities.Empty<NullableReferenceTypeState>();

        var defaultState =
            providers.
            Select(TryGetNullableContext).
            FirstOrDefault(state => state is not null) ??
            fallbackProviders.
            Select(TryGetNullableContext).
            FirstOrDefault(state => state is not null) ??
            NullableReferenceTypeState.Oblivious;

        return new NullableReferenceTypeContext(defaultState, states);
    }

    private static string AppendNullableSuffix(
        string name,
        TypeReference type,
        NullableReferenceTypeState state)
    {
        if (state != NullableReferenceTypeState.Nullable)
        {
            return name;
        }

        if (type is GenericParameter)
        {
            return name + "?";
        }

        return !type.IsValueType && type.FullName != "System.Void" ?
            name + "?" :
            name;
    }

    private static string FormatTypeName(
        TypeReference type,
        NullableReferenceTypeContext context,
        ParameterModifierCandidates pmc)
    {
        if (type is ByReferenceType byReferenceType)
        {
            var parameterModifier = pmc switch
            {
                ParameterModifierCandidates.In => "in",
                ParameterModifierCandidates.Out => "out",
                _ => "ref",
            };
            return $"{parameterModifier} {FormatTypeName(byReferenceType.ElementType, context, ParameterModifierCandidates.Ref)}";
        }
        else if (type is PointerType pointerType)
        {
            return $"{FormatTypeName(pointerType.ElementType, context, ParameterModifierCandidates.Ref)}*";
        }

        var currentState = context.ConsumeState();

        if (type is ArrayType arrayType)
        {
            var rank =
                arrayType.IsVector ?
                "[]" :
                $"[{new string(',', System.Math.Max(0, arrayType.Rank - 1))}]";
            return AppendNullableSuffix(
                $"{FormatTypeName(arrayType.ElementType, context, ParameterModifierCandidates.Ref)}{rank}",
                type,
                currentState);
        }
        else if (type is GenericParameter genericParameter)
        {
            return AppendNullableSuffix(genericParameter.Name, type, currentState);
        }

        string name;
        if (Naming.CSharpKeywords.TryGetValue(type.FullName, out var keywordName))
        {
            name = keywordName;
        }
        else
        {
            var genericTypeParameters = "";
            if (type is GenericInstanceType genericInstanceType)
            {
                var declaredArguments = GetDeclaredGenericArguments(genericInstanceType);
                if (declaredArguments.Length >= 1)
                {
                    genericTypeParameters = $"<{string.Join(",",
                        declaredArguments.Select(argument =>
                            FormatTypeName(argument, context, ParameterModifierCandidates.Ref)))}>";
                }
            }
            else
            {
                var declaredParameters = GetDeclaredGenericParameters(type);
                if (declaredParameters.Length >= 1)
                {
                    genericTypeParameters = $"<{string.Join(",",
                        declaredParameters.Select(parameter =>
                            FormatTypeName(parameter, context, ParameterModifierCandidates.Ref)))}>";
                }
            }

            name = type.DeclaringType is { } declaringType ?
                $"{FormatTypeName(declaringType, context, ParameterModifierCandidates.Ref)}.{Naming.TrimGenericArguments(type.Name)}{genericTypeParameters}" :
                $"{Naming.TrimGenericArguments(type.Name)}{genericTypeParameters}";
        }

        return AppendNullableSuffix(name, type, currentState);
    }

    public static string GetName(
        TypeReference type,
        NullableReferenceTypeContext context,
        ParameterModifierCandidates pmc = ParameterModifierCandidates.Ref) =>
        FormatTypeName(type, context, pmc);

    public static NullableReferenceTypeContext CreateFieldContext(FieldDefinition field) =>
        CreateContext(new ICustomAttributeProvider?[] { field }, EnumerateTypeChain(field.DeclaringType));

    public static NullableReferenceTypeContext CreatePropertyContext(PropertyDefinition property) =>
        CreateContext(
            new ICustomAttributeProvider?[]
            {
                property,
                property.GetMethod?.MethodReturnType,
                property.SetMethod?.Parameters.LastOrDefault(),
            },
            EnumerateTypeChain(property.DeclaringType.Resolve()));

    public static NullableReferenceTypeContext CreateEventContext(EventDefinition @event) =>
        CreateContext(
            new ICustomAttributeProvider?[]
            {
                @event,
                @event.AddMethod?.Parameters.LastOrDefault(),
                @event.RemoveMethod?.Parameters.LastOrDefault(),
            },
            EnumerateTypeChain(@event.DeclaringType.Resolve()));

    public static NullableReferenceTypeContext CreateMethodReturnContext(MethodDefinition method) =>
        CreateContext(
            new ICustomAttributeProvider?[] { method.MethodReturnType, method },
            EnumerateTypeChain(method.DeclaringType));

    public static NullableReferenceTypeContext CreateParameterContext(
        MethodDefinition method,
        ParameterDefinition parameter) =>
        CreateContext(new ICustomAttributeProvider?[] { parameter, method }, EnumerateTypeChain(method.DeclaringType));
}
