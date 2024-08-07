﻿using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using BindingFlags = System.Reflection.BindingFlags;

namespace Rougamo.Fody
{
    internal static class MonoExtension
    {
        public static bool Is(this TypeReference typeRef, string fullName)
        {
            return typeRef.Resolve()?.FullName == fullName;
        }

        public static bool Is(this CustomAttribute attribute, string fullName)
        {
            return attribute.AttributeType.Is(fullName);
        }

        public static bool Is(this MethodReference methodRef, string fullName)
        {
            return methodRef.FullName == fullName;
        }

        public static bool IsObject(this TypeReference typeRef) => typeRef.Is(typeof(object).FullName);

        public static bool IsGeneric(this TypeReference typeRef, string genericName, out TypeReference[]? genericTypeRefs)
        {
            var typeDef = typeRef.Resolve();
            if (typeRef is GenericInstanceType git && typeDef != null && typeDef.FullName == genericName)
            {
                genericTypeRefs = git.GenericArguments.ToArray();
                return true;
            }
            genericTypeRefs = null;
            return false;
        }

        public static bool IsOrDerivesFrom(this TypeReference typeRef, string className)
        {
            return Is(typeRef, className) || DerivesFrom(typeRef, className);
        }

        public static bool Implement(this TypeReference typeRef, string @interface) => Implement(typeRef.Resolve(), @interface);

        public static bool Implement(this TypeDefinition typeDef, string @interface)
        {
            do
            {
                if (typeDef.Interfaces.Any(x => x.InterfaceType.FullName == @interface)) return true;
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                typeDef = typeDef.BaseType?.Resolve();
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
            } while (typeDef != null);

            return false;
        }

        public static bool DerivesFrom(this TypeReference typeRef, string baseClass)
        {
            do
            {
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                if ((typeRef = typeRef.Resolve()?.BaseType)?.FullName == baseClass) return true;
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
            } while (typeRef != null);

            return false;
        }

        public static bool DerivesFromAny(this TypeReference typeRef, params string[] baseClasses)
        {
            foreach (var baseClass in baseClasses)
            {
                if (typeRef.DerivesFrom(baseClass)) return true;
            }

            return false;
        }

        public static bool DerivesFrom(this CustomAttribute attribute, string baseClass)
        {
            return attribute.AttributeType.DerivesFrom(baseClass);
        }

        public static bool IsDelegate(this TypeReference typeRef)
        {
            return DerivesFrom(typeRef, Constants.TYPE_MulticastDelegate);
        }

        public static bool IsArray(this TypeReference typeRef, out TypeReference? elementType)
        {
            elementType = null;
            if (!typeRef.IsArray)
                return false;

            elementType = ((ArrayType)typeRef).ElementType;
            return true;
        }

        public static bool IsEnum(this TypeReference typeRef)
        {
            var typeDef = typeRef.Resolve();
            return typeDef != null && typeDef.IsEnum;
        }

        public static bool IsEnum(this TypeReference typeRef, out TypeReference? underlyingType)
        {
            var typeDef = typeRef.Resolve();
            if (typeDef != null && typeDef.IsEnum)
            {
                underlyingType = typeDef.Fields.First(f => f.Name == "value__").FieldType;
                return true;
            }

            underlyingType = null;
            return false;
        }

        public static bool IsString(this TypeReference typeRef)
        {
            return typeRef.Resolve().FullName == typeof(string).FullName;
        }

        public static bool IsNullable(this TypeReference typeRef)
        {
            return typeRef.Resolve().FullName.StartsWith("System.Nullable");
        }

        [Obsolete]
        public static bool IsUnboxable(this TypeReference typeRef)
        {
            return typeRef.IsValueType || typeRef.IsEnum(out _) && !typeRef.IsArray || typeRef.IsGenericParameter;
        }

        public static bool NeedBox(this TypeReference typeRef) => typeRef.IsValueType || typeRef.IsGenericParameter;

        public static OpCode GetStElemCode(this TypeReference typeRef)
        {
            var typeDef = typeRef.Resolve();
            if (typeDef.IsEnum(out TypeReference? underlying))
                return underlying!.MetadataType.GetStElemCode();
            if (typeRef.IsValueType)
                return typeRef.MetadataType.GetStElemCode();
            return OpCodes.Stelem_Ref;
        }

        public static OpCode GetStElemCode(this MetadataType type)
        {
            switch (type)
            {
                case MetadataType.Boolean:
                case MetadataType.Int32:
                case MetadataType.UInt32:
                    return OpCodes.Stelem_I4;
                case MetadataType.Byte:
                case MetadataType.SByte:
                    return OpCodes.Stelem_I1;
                case MetadataType.Char:
                case MetadataType.Int16:
                case MetadataType.UInt16:
                    return OpCodes.Stelem_I2;
                case MetadataType.Double:
                    return OpCodes.Stelem_R8;
                case MetadataType.Int64:
                case MetadataType.UInt64:
                    return OpCodes.Stelem_I8;
                case MetadataType.Single:
                    return OpCodes.Stelem_R4;
                default:
                    return OpCodes.Stelem_Ref;
            }
        }

        public static OpCode GetLdElemCode(this TypeReference typeRef)
        {
            var typeDef = typeRef.Resolve();
            if (typeDef.IsEnum(out TypeReference? underlying))
                return underlying!.MetadataType.GetLdElemCode();
            if (typeRef.IsValueType)
                return typeRef.MetadataType.GetLdElemCode();
            return OpCodes.Ldelem_Ref;
        }

        public static OpCode GetLdElemCode(this MetadataType type)
        {
            switch (type)
            {
                case MetadataType.Boolean:
                case MetadataType.Int32:
                case MetadataType.UInt32:
                    return OpCodes.Ldelem_I4;
                case MetadataType.Byte:
                case MetadataType.SByte:
                    return OpCodes.Ldelem_I1;
                case MetadataType.Char:
                case MetadataType.Int16:
                case MetadataType.UInt16:
                    return OpCodes.Ldelem_I2;
                case MetadataType.Double:
                    return OpCodes.Ldelem_R8;
                case MetadataType.Int64:
                case MetadataType.UInt64:
                    return OpCodes.Ldelem_I8;
                case MetadataType.Single:
                    return OpCodes.Ldelem_R4;
                default:
                    return OpCodes.Ldelem_Ref;
            }
        }

        public static MethodDefinition GetZeroArgsCtor(this TypeDefinition typeDef)
        {
            var zeroCtor = typeDef.GetConstructors().FirstOrDefault(ctor => !ctor.HasParameters);
            if (zeroCtor == null)
                throw new RougamoException($"could not found zero arguments constructor from {typeDef.FullName}");
            return zeroCtor;
        }

        public static MethodReference? RecursionImportPropertySet(this CustomAttribute attribute,
            ModuleDefinition moduleDef, string propertyName)
        {
            return RecursionImportPropertySet(attribute.AttributeType.Resolve(), moduleDef, propertyName);
        }

        public static MethodReference? RecursionImportPropertySet(this TypeDefinition typeDef,
            ModuleDefinition moduleDef, string propertyName)
        {
            var propertyDef = typeDef.Properties.FirstOrDefault(pd => pd.Name == propertyName);
            if (propertyDef != null)
            {
                return propertyDef.SetMethod == null ? null : moduleDef.ImportReference(propertyDef.SetMethod);
            }

            var baseTypeDef = typeDef.BaseType.Resolve();
            if (baseTypeDef.FullName == typeof(object).FullName)
                throw new RougamoException($"can not find property({propertyName}) from {typeDef.FullName}");
            return RecursionImportPropertySet(baseTypeDef, moduleDef, propertyName);
        }

        public static MethodReference RecursionImportPropertyGet(this TypeDefinition typeDef,
            ModuleDefinition moduleDef, string propertyName)
        {
            var propertyDef = typeDef.Properties.FirstOrDefault(pd => pd.Name == propertyName);
            if (propertyDef != null) return moduleDef.ImportReference(propertyDef.GetMethod);

            var baseTypeDef = typeDef.BaseType.Resolve();
            if (baseTypeDef.FullName == typeof(object).FullName)
                throw new RougamoException($"can not find property({propertyName}) from {typeDef.FullName}");
            return RecursionImportPropertyGet(baseTypeDef, moduleDef, propertyName);
        }

        public static MethodReference RecursionImportMethod(this CustomAttribute attribute, ModuleDefinition moduleDef,
            string methodName, Func<MethodDefinition, bool> predicate)
        {
            return RecursionImportMethod(attribute.AttributeType.Resolve(), moduleDef, methodName, predicate);
        }

        public static MethodReference RecursionImportMethod(this TypeDefinition typeDef, ModuleDefinition moduleDef,
            string methodName, Func<MethodDefinition, bool> predicate)
        {
            var methodDef = typeDef.Methods.FirstOrDefault(md => md.Name == methodName && predicate(md));
            if (methodDef != null) return moduleDef.ImportReference(methodDef);

            var baseTypeDef = typeDef.BaseType.Resolve();
            if (baseTypeDef.FullName == typeof(object).FullName)
                throw new RougamoException($"can not find method({methodName}) from {typeDef.FullName}");
            return RecursionImportMethod(baseTypeDef, moduleDef, methodName, predicate);
        }

        public static MethodDefinition GetPropertySetterDef(this TypeDefinition typeDef, string propertyName)
        {
            var propertyDef = typeDef.Properties.FirstOrDefault(pd => pd.Name == propertyName);
            if (propertyDef != null) return propertyDef.SetMethod ?? throw new RougamoException($"{typeDef.FullName} has property({propertyName}) but this property does not contain set method.");

            var baseTypeDef = typeDef.BaseType.Resolve();
            if (baseTypeDef.FullName == typeof(object).FullName)
                throw new RougamoException($"can not find property({propertyName}) from {typeDef.FullName}");
            return GetPropertySetterDef(baseTypeDef, propertyName);
        }

        public static MethodDefinition GetPropertyGetterDef(this TypeDefinition typeDef, string propertyName)
        {
            var propertyDef = typeDef.Properties.FirstOrDefault(pd => pd.Name == propertyName);
            if (propertyDef != null) return propertyDef.GetMethod ?? throw new RougamoException($"{typeDef.FullName} has property({propertyName}) but this property does not contain get method.");

            var baseTypeDef = typeDef.BaseType.Resolve();
            if (baseTypeDef.FullName == typeof(object).FullName)
                throw new RougamoException($"can not find property({propertyName}) from {typeDef.FullName}");
            return GetPropertyGetterDef(baseTypeDef, propertyName);
        }

        public static VariableDefinition CreateVariable(this MethodBody body, TypeReference variableTypeReference)
        {
            var variable = new VariableDefinition(variableTypeReference);
            body.Variables.Add(variable);
            return variable;
        }

        public static List<GenericInstanceType> GetGenericInterfaces(this TypeDefinition typeDef, string interfaceName)
        {
            var interfaces = new List<GenericInstanceType>();
            do
            {
                var titf = typeDef.Interfaces.Select(itf => itf.InterfaceType)
                    .Where(itfRef => itfRef.FullName.StartsWith(interfaceName + "<"));
                interfaces.AddRange(titf.Cast<GenericInstanceType>());
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                typeDef = typeDef.BaseType?.Resolve();
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
            } while (typeDef != null);

            return interfaces;
        }

        public static TypeDefinition? GetInterfaceDefinition(this TypeDefinition typeDef, string interfaceName)
        {
            do
            {
                var interfaceRef = typeDef.Interfaces.Select(itf => itf.InterfaceType)
                    .Where(itfRef => itfRef.FullName == interfaceName);
                if (interfaceRef.Any()) return interfaceRef.First().Resolve();
                typeDef = typeDef.BaseType.Resolve();
            } while (typeDef != null);

            return null;
        }

        #region Import

        public static TypeReference ImportInto(this TypeReference typeRef, ModuleDefinition moduleDef)
        {
            if (typeRef.IsByReference) return new ByReferenceType(typeRef.GetElementType().ImportInto(moduleDef));
            if (typeRef is ArrayType at) return new ArrayType(at.ElementType.ImportInto(moduleDef), at.Rank);
            if (typeRef is GenericParameter) return typeRef;

            var iTypeRef = moduleDef.ImportReference(typeRef.Resolve());
            if (typeRef is GenericInstanceType git)
            {
                var igas = new List<TypeReference>(git.GenericArguments.Count);
                foreach (var ga in git.GenericArguments)
                {
                    var iga = ga.ImportInto(moduleDef);
                    igas.Add(iga);
                }
                iTypeRef = iTypeRef.MakeGenericInstanceType(igas.ToArray());
            }

            return iTypeRef;
        }

        public static FieldReference ImportInto(this FieldReference fieldRef, ModuleDefinition moduleDef) =>
            moduleDef.ImportReference(fieldRef);

        public static MethodReference ImportInto(this MethodReference methodRef, ModuleDefinition moduleDef) =>
            moduleDef.ImportReference(methodRef);

        public static TypeReference ImportInto(this ParameterDefinition parameterDef, ModuleDefinition moduleDef)
        {
            var parameterTypeRef = parameterDef.ParameterType.ImportInto(moduleDef);
            var isByReference = parameterDef.ParameterType.IsByReference;
            if (isByReference)
            {
                parameterTypeRef = ((ByReferenceType)parameterDef.ParameterType).ElementType.ImportInto(moduleDef);
            }

            return parameterTypeRef;
        }

        public static TypeReference Import(this TypeReference typeRef, params TypeReference[] inferenceChain)
        {
            var chainLast = inferenceChain.Last();
            TypeReference[] genericArguments;
            if (chainLast is GenericInstanceType importingTypeRef)
            {
                genericArguments = importingTypeRef.GenericArguments.ToArray();
            }
            else if (chainLast is GenericParameter)
            {
                importingTypeRef = null!;
                genericArguments = [chainLast];
            }
            else
            {
                return chainLast.ImportInto(typeRef.Module);
            }

            for (var i = inferenceChain.Length - 2; i >= 0; i--)
            {
                if (inferenceChain[i] is not GenericInstanceType preTypeRef) throw new RougamoException($"Item in inference chain must be GenericInstanceType, but {inferenceChain[i]} is not. Cannot import {importingTypeRef} into {typeRef}");

                MappingGenerics(genericArguments, preTypeRef);
            }

            MappingGenerics(genericArguments, typeRef);

            if (importingTypeRef == null) return genericArguments[0].ImportInto(typeRef.Module);

            var git = new GenericInstanceType(importingTypeRef.ElementType.ImportInto(typeRef.Module));
            git.GenericArguments.Add(genericArguments);
            return git;
        }

        private static void MappingGenerics(TypeReference[] generics, TypeReference typeRef)
        {
            if (typeRef is GenericInstanceType git)
            {
                MappingGenerics(generics, git);
            }
            else if (typeRef is TypeDefinition typeDef)
            {
                MappingGenerics(generics, typeDef);
            }
        }

        private static void MappingGenerics(TypeReference[] generics, GenericInstanceType git)
        {
            var typeDef = git.Resolve();
            for (var j = 0; j < generics.Length; j++)
            {
                var generic = generics[j];
                if (generic is GenericParameter gp)
                {
                    var gpIndex = typeDef.GetGenericParameterIndexByName(gp.Name);
                    generics[j] = git.GenericArguments[gpIndex];
                }
                else
                {
                    generics[j] = generic;
                }
            }
        }

        private static void MappingGenerics(TypeReference[] generics, TypeDefinition typeDef)
        {
            for (var j = 0; j < generics.Length; j++)
            {
                var generic = generics[j];
                if (generic is GenericParameter gp)
                {
                    var gpIndex = typeDef.GetGenericParameterIndexByName(gp.Name);
                    generics[j] = typeDef.GenericParameters[gpIndex];
                }
                else
                {
                    generics[j] = generic;
                }
            }
        }

        #endregion Import

        public static int GetGenericParameterIndexByName(this TypeDefinition typeDef, string name)
        {
            for (var i = 0; i < typeDef.GenericParameters.Count; i++)
            {
                if (typeDef.GenericParameters[i].Name == name) return i;
            }

            throw new RougamoException($"Cannot find the generic parameter named {name} in the type {typeDef}");
        }

        public static TypeDefinition ResolveStateMachine(this MethodDefinition methodDef, string stateMachineAttributeName)
        {
            var stateMachineAttr = methodDef.CustomAttributes.Single(attr => attr.Is(stateMachineAttributeName));
            var obj = stateMachineAttr.ConstructorArguments[0].Value;
            return obj as TypeDefinition ?? ((TypeReference)obj).Resolve();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Instruction Ldloc(this VariableDefinition variable)
        {
            return Instruction.Create(OpCodes.Ldloc, variable);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Instruction Ldloca(this VariableDefinition variable)
        {
            return Instruction.Create(OpCodes.Ldloca, variable);
        }

        public static Instruction LdlocAny(this VariableDefinition variable)
        {
            var opLdloc = variable.VariableType.IsValueType ? OpCodes.Ldloca : OpCodes.Ldloc;
            return Instruction.Create(opLdloc, variable);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Instruction Stloc(this VariableDefinition variable)
        {
            return Instruction.Create(OpCodes.Stloc, variable);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Instruction Ldfld(this FieldReference fieldRef)
        {
            return Instruction.Create(OpCodes.Ldfld, fieldRef);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Instruction Ldflda(this FieldReference fieldRef)
        {
            return Instruction.Create(OpCodes.Ldflda, fieldRef);
        }

        public static Instruction LdfldAny(this FieldReference fieldRef)
        {
            var opLdfld = fieldRef.FieldType.IsValueType ? OpCodes.Ldflda : OpCodes.Ldfld;
            return Instruction.Create(opLdfld, fieldRef);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Instruction Stfld(this FieldReference fieldRef)
        {
            return Instruction.Create(OpCodes.Stfld, fieldRef);
        }

        public static Instruction CallAny(this MethodReference methodRef)
        {
            var methodDef = methodRef.Resolve();
            if (methodDef.IsConstructor) return Instruction.Create(OpCodes.Newobj, methodRef);

            var opCall = methodDef.IsVirtual ? OpCodes.Callvirt : OpCodes.Call;
            return Instruction.Create(opCall, methodRef);
        }

        public static Instruction Ldind(this TypeReference typeRef)
        {
            if (typeRef == null) throw new ArgumentNullException(nameof(typeRef), "Ldind argument null");
            var typeDef = typeRef.Resolve();
            if (typeDef == null) return Instruction.Create(OpCodes.Ldobj, typeRef);
            if (!typeRef.IsValueType) return Instruction.Create(OpCodes.Ldind_Ref);
            if (typeDef.Is(typeof(byte).FullName)) return Instruction.Create(OpCodes.Ldind_I1);
            if (typeDef.Is(typeof(short).FullName)) return Instruction.Create(OpCodes.Ldind_I2);
            if (typeDef.Is(typeof(int).FullName)) return Instruction.Create(OpCodes.Ldind_I4);
            if (typeDef.Is(typeof(long).FullName)) return Instruction.Create(OpCodes.Ldind_I8);
            if (typeDef.Is(typeof(sbyte).FullName)) return Instruction.Create(OpCodes.Ldind_U1);
            if (typeDef.Is(typeof(ushort).FullName)) return Instruction.Create(OpCodes.Ldind_U2);
            if (typeDef.Is(typeof(uint).FullName)) return Instruction.Create(OpCodes.Ldind_U4);
            if (typeDef.Is(typeof(ulong).FullName)) return Instruction.Create(OpCodes.Ldind_I8);
            if (typeDef.Is(typeof(float).FullName)) return Instruction.Create(OpCodes.Ldind_R4);
            if (typeDef.Is(typeof(double).FullName)) return Instruction.Create(OpCodes.Ldind_R8);
            if (typeDef.IsEnum)
            {
                if (typeDef.Fields.Count == 0) return Instruction.Create(OpCodes.Ldind_I);
                return Ldind(typeDef.Fields[0].FieldType);
            }
            return Instruction.Create(OpCodes.Ldobj, typeRef); // struct & enum & generic parameter
        }

        public static Instruction Stind(this TypeReference typeRef)
        {
            if (typeRef == null) throw new ArgumentNullException(nameof(typeRef), "Stind argument null");
            if (typeRef.IsGenericParameter) return Instruction.Create(OpCodes.Stobj, typeRef);
            var typeDef = typeRef.Resolve();
            if (!typeRef.IsValueType) return Instruction.Create(OpCodes.Stind_Ref);
            if (typeDef.Is(typeof(byte).FullName) || typeDef.Is(typeof(sbyte).FullName)) return Instruction.Create(OpCodes.Stind_I1);
            if (typeDef.Is(typeof(short).FullName) || typeDef.Is(typeof(ushort).FullName)) return Instruction.Create(OpCodes.Stind_I2);
            if (typeDef.Is(typeof(int).FullName) || typeDef.Is(typeof(uint).FullName)) return Instruction.Create(OpCodes.Stind_I4);
            if (typeDef.Is(typeof(long).FullName) || typeDef.Is(typeof(ulong).FullName)) return Instruction.Create(OpCodes.Stind_I8);
            if (typeDef.Is(typeof(float).FullName)) return Instruction.Create(OpCodes.Stind_R4);
            if (typeDef.Is(typeof(double).FullName)) return Instruction.Create(OpCodes.Stind_R8);
            if (typeDef.IsEnum)
            {
                if (typeDef.Fields.Count == 0) return Instruction.Create(OpCodes.Stind_I);
                return Stind(typeDef.Fields[0].FieldType);
            }
            return Instruction.Create(OpCodes.Stobj, typeRef); // struct & enum & generic parameter
        }

        public static bool ShouldInitobj(this TypeReference typeRef, out Instruction[] defaultValueOp)
        {
            switch (typeRef.FullName)
            {
                case Constants.TYPE_Boolean:
                case Constants.TYPE_Byte:
                case Constants.TYPE_Int16:
                case Constants.TYPE_Int32:
                case Constants.TYPE_SByte:
                case Constants.TYPE_UInt16:
                case Constants.TYPE_UInt32:
                    defaultValueOp = [Instruction.Create(OpCodes.Ldc_I4_0)];
                    return false;
                case Constants.TYPE_Int64:
                case Constants.TYPE_UInt64:
                    defaultValueOp = [Instruction.Create(OpCodes.Ldc_I4_0), Instruction.Create(OpCodes.Conv_I8)];
                    return false;
                case Constants.TYPE_Single:
                    defaultValueOp = [Instruction.Create(OpCodes.Ldc_R4, 0)];
                    return false;
                case Constants.TYPE_Double:
                    defaultValueOp = [Instruction.Create(OpCodes.Ldc_R8, 0)];
                    return false;
                default:
                    if (!typeRef.IsArray && (typeRef.IsGenericParameter || typeRef.IsValueType))
                    {
                        defaultValueOp = [Instruction.Create(OpCodes.Initobj, typeRef)];
                        return true;
                    }
                    defaultValueOp = [Instruction.Create(OpCodes.Ldnull)];
                    return false;
            }
        }

        public static TypeReference MakeReference(this TypeDefinition typeDef)
        {
            if (!typeDef.HasGenericParameters) return typeDef;

            var generic = new GenericInstanceType(typeDef);
            foreach (var genericParameter in typeDef.GenericParameters)
            {
                generic.GenericArguments.Add(genericParameter);
            }

            return generic;
        }

        public static MethodReference MakeReference(this MethodDefinition medthodDef)
        {
            if (!medthodDef.DeclaringType.HasGenericParameters) return medthodDef;

            var declaringTypeRef = medthodDef.DeclaringType.MakeReference();
            return medthodDef.WithGenericDeclaringType(declaringTypeRef);
        }

        public static MethodReference WithGenericDeclaringType(this MethodReference methodRef, TypeReference typeRef)
        {
            methodRef = methodRef.ImportInto(typeRef.Module);

            var genericMethodRef = new MethodReference(methodRef.Name, methodRef.ReturnType, typeRef)
            {
                HasThis = methodRef.HasThis,
                ExplicitThis = methodRef.ExplicitThis,
                CallingConvention = methodRef.CallingConvention
            };
            foreach (var parameter in methodRef.Parameters)
            {
                genericMethodRef.Parameters.Add(new ParameterDefinition(parameter.ParameterType));
            }
            foreach (var parameter in methodRef.GenericParameters)
            {
                genericMethodRef.GenericParameters.Add(new GenericParameter(parameter.Name, genericMethodRef));
            }

            return genericMethodRef;
        }

        public static MethodReference WithGenerics(this MethodReference methodRef, params TypeReference[] genericTypeRefs)
        {
            if (genericTypeRefs.Length == 0) return methodRef;

            var genericInstanceMethod = new GenericInstanceMethod(methodRef.GetElementMethod());
            genericInstanceMethod.GenericArguments.Add(genericTypeRefs);

            return genericInstanceMethod;
        }

        public static TypeReference ReplaceGenericArgs(this TypeReference typeRef, Dictionary<string, TypeReference>? genericMap)
        {
            if (genericMap == null) return typeRef;

            if (typeRef is GenericParameter gp && genericMap.TryGetValue(gp.Name, out var value)) return value;

            if (typeRef is GenericInstanceType git)
            {
                var replacedGit = new GenericInstanceType(git.GetElementType());
                foreach (var generic in git.GenericArguments)
                {
                    replacedGit.GenericArguments.Add(generic.ReplaceGenericArgs(genericMap));
                }

                return replacedGit;
            }

            if (typeRef is TypeDefinition typeDef && typeDef.HasGenericParameters)
            {
                var replacedGit = new GenericInstanceType(typeDef.GetElementType());
                foreach (var generic in typeDef.GenericParameters)
                {
                    replacedGit.GenericArguments.Add(generic.ReplaceGenericArgs(genericMap));
                }

                return replacedGit;
            }

            return typeRef;
        }

        public static void DebuggerStepThrough(this MethodDefinition methodDef, MethodReference ctor)
        {
            methodDef.CustomAttributes.Add(new CustomAttribute(ctor));
        }

        public static void Clear(this MethodDefinition methodDef)
        {
            methodDef.Body.Instructions.Clear();
            methodDef.Body.Variables.Clear();
            methodDef.Body.ExceptionHandlers.Clear();
            methodDef.HardClearCustomDebugInformation();
            methodDef.DebugInformation.Clear();
        }

        public static void Clear(this MethodDebugInformation debugInformation)
        {
            debugInformation.CustomDebugInformations.Clear();
            debugInformation.SequencePoints.Clear();
            debugInformation.Scope.Constants.Clear();
            debugInformation.Scope.Variables.Clear();
            debugInformation.Scope.Scopes.Clear();
            debugInformation.Scope.CustomDebugInformations.Clear();
        }

        public static void HardClearCustomDebugInformation(this MethodDefinition methodDef)
        {
            methodDef.CustomDebugInformations.Clear();

            var module = methodDef.Module;
            var metadata = module.GetType().GetField("MetadataSystem", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(module);
            if (metadata == null) return;

            var customDebugInformations = (IDictionary)metadata.GetType().GetField("CustomDebugInformations", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(metadata);
            if (customDebugInformations == null) return;

            customDebugInformations.Remove(methodDef.MetadataToken);
        }

        private static Code[] _EmptyCodes = new[] { Code.Nop, Code.Ret };
        public static bool IsEmpty(this MethodDefinition methodDef)
        {
            foreach (var instruction in methodDef.Body.Instructions)
            {
                if (!_EmptyCodes.Contains(instruction.OpCode.Code)) return false;
            }

            return true;
        }

        private static readonly Dictionary<Code, OpCode> _OptimizeCodes = new Dictionary<Code, OpCode>
        {
            { Code.Leave_S, OpCodes.Leave }, { Code.Br_S, OpCodes.Br },
            { Code.Brfalse_S, OpCodes.Brfalse }, { Code.Brtrue_S, OpCodes.Brtrue },
            { Code.Beq_S, OpCodes.Beq }, { Code.Bne_Un_S, OpCodes.Bne_Un },
            { Code.Bge_S, OpCodes.Bge }, { Code.Bgt_S, OpCodes.Bgt },
            { Code.Ble_S, OpCodes.Ble }, { Code.Blt_S, OpCodes.Blt },
            { Code.Bge_Un_S, OpCodes.Bge_Un }, { Code.Bgt_Un_S, OpCodes.Bgt_Un },
            { Code.Ble_Un_S, OpCodes.Ble_Un }, { Code.Blt_Un_S, OpCodes.Blt_Un }
        };
        public static void OptimizePlus(this MethodBody body, Instruction[]? nops = null)
        {
            nops ??= [];

            body.OptimizeShort();
            body.OptimizeUselessBr();
            body.OptimizeNops(nops);
            body.Optimize();
        }

        private static void OptimizeShort(this MethodBody body)
        {
            foreach (var instruction in body.Instructions)
            {
                if (_OptimizeCodes.TryGetValue(instruction.OpCode.Code, out var opcode))
                {
                    instruction.OpCode = opcode;
                }
            }
        }

        private static void OptimizeUselessBr(this MethodBody body)
        {
            var cannotRemoves = new List<Instruction>();
            var canRemoves = new List<Instruction>();
            foreach (var instruction in body.Instructions)
            {
                if (instruction.Operand is Instruction br2)
                {
                    cannotRemoves.Add(br2);
                    if (instruction.OpCode.Code == Code.Br)
                    {
                        var current = instruction.Next;
                        while (current != br2)
                        {
                            if (current.OpCode.Code != Code.Nop) break;
                            current = current.Next;
                        }
                        if (current == br2) canRemoves.Add(instruction);
                    }
                }
            }
            foreach (var handler in body.ExceptionHandlers)
            {
                cannotRemoves.Add(handler.TryStart);
                cannotRemoves.Add(handler.TryEnd);
                cannotRemoves.Add(handler.HandlerStart);
                cannotRemoves.Add(handler.HandlerEnd);
            }
            foreach (var item in canRemoves)
            {
                if (cannotRemoves.Contains(item)) continue;

                body.Instructions.Remove(item);
            }
        }

        private static void OptimizeNops(this MethodBody body, Instruction[] nops)
        {
            foreach (var handler in body.ExceptionHandlers)
            {
                if (NeedUpdateHandlerAnchor(handler.TryStart, handler.TryEnd, nops, out var newTryStart))
                {
                    handler.TryStart = newTryStart;
                }
                if (handler.TryEnd != handler.HandlerStart)
                {
                    if (NeedUpdateHandlerAnchor(handler.TryEnd, handler.HandlerStart, nops, out var newTryEnd))
                    {
                        handler.TryEnd = newTryEnd;
                    }
                    if (NeedUpdateHandlerAnchor(handler.HandlerStart, handler.HandlerEnd, nops, out var newHandlerStart))
                    {
                        handler.HandlerStart = newHandlerStart;
                    }
                }
                else
                {
                    if (NeedUpdateHandlerAnchor(handler.TryEnd, handler.HandlerEnd, nops, out var newTryEndHandlerStart))
                    {
                        handler.TryEnd = newTryEndHandlerStart;
                        handler.HandlerStart = newTryEndHandlerStart;
                    }
                }
                if (NeedUpdateHandlerAnchor(handler.HandlerEnd, null, nops, out var newHandlerEnd))
                {
                    handler.HandlerEnd = newHandlerEnd;
                }
            }

            foreach (var instruction in body.Instructions)
            {
                if (instruction.Operand is Instruction to)
                {
                    var current = to;
                    while (nops.Contains(current))
                    {
                        current = current.Next;
                    }
                    if (current != to && current != null)
                    {
                        instruction.Operand = current;
                    }
                }
            }
            foreach (var nop in nops)
            {
                body.Instructions.Remove(nop);
            }
        }

        private static bool NeedUpdateHandlerAnchor(Instruction? target, Instruction? checkLimit, Instruction[] nops, out Instruction? updateTarget)
        {
            updateTarget = null;
            if (target == null) return false;

            var current = target;
            while (current != checkLimit)
            {
                if (!nops.Contains(current)) break;
                current = current.Next;
            }
            if (current == checkLimit || current == target)
            {
                return false;
            }
            updateTarget = current;
            return true;
        }
    }
}