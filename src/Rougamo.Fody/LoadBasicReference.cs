﻿using Mono.Cecil.Cil;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace Rougamo.Fody
{
    partial class ModuleWeaver
    {
        void LoadBasicReference()
        {
            _typeListDef = FindTypeDefinition(typeof(List<>).FullName);
            _typeSystemDef = FindTypeDefinition(typeof(Type).FullName);
            _typeMethodBaseDef = FindTypeDefinition(typeof(MethodBase).FullName);

            _typeVoidRef = FindTypeDefinition(typeof(void).FullName).ImportInto(ModuleDefinition);
            _typeSystemRef = _typeSystemDef.ImportInto(ModuleDefinition);
            _typeMethodBaseRef = _typeMethodBaseDef.ImportInto(ModuleDefinition);
            _typeIntRef = FindTypeDefinition(typeof(int).FullName).ImportInto(ModuleDefinition);
            _typeBoolRef = FindTypeDefinition(typeof(bool).FullName).ImportInto(ModuleDefinition);
            _typeObjectRef = FindTypeDefinition(typeof(object).FullName).ImportInto(ModuleDefinition);
            _typeExceptionRef = FindTypeDefinition(typeof(Exception).FullName).ImportInto(ModuleDefinition);
            _typeCancellationTokenRef = FindTypeDefinition(typeof(CancellationToken).FullName).ImportInto(ModuleDefinition);
            _typeDebuggerStepThroughAttributeRef = FindTypeDefinition(typeof(DebuggerStepThroughAttribute).FullName).ImportInto(ModuleDefinition);
            _typeListRef = _typeListDef.ImportInto(ModuleDefinition);
            var typeAsyncStateMachineAttributeRef = FindTypeDefinition(typeof(AsyncStateMachineAttribute).FullName).ImportInto(ModuleDefinition);
            var typeIteratorStateMachineAttributeRef = FindTypeDefinition(typeof(IteratorStateMachineAttribute).FullName).ImportInto(ModuleDefinition);
            var typeExceptionDispatchInfoDef = FindTypeDefinition(typeof(ExceptionDispatchInfo).FullName);

            _methodGetTypeFromHandleRef = _typeSystemDef.Methods.Single(x => x.Name == Constants.METHOD_GetTypeFromHandle).ImportInto(ModuleDefinition);
            _methodGetMethodFromHandleRef = _typeMethodBaseDef.Methods.Single(x => x.Name == Constants.METHOD_GetMethodFromHandle && x.Parameters.Count == 2).ImportInto(ModuleDefinition);
            _methodListAddRef = _typeListDef.Methods.Single(x => x.Name == Constants.METHOD_Add && x.Parameters.Count == 1 && x.Parameters[0].ParameterType == _typeListDef.GenericParameters[0]);
            _methodListToArrayRef = _typeListDef.Methods.Single(x => x.Name == Constants.METHOD_ToArray && !x.HasParameters);
            _methodIEnumeratorMoveNextRef = FindTypeDefinition(typeof(IEnumerator).FullName).Methods.Single(x => x.Name == Constants.METHOD_MoveNext).ImportInto(ModuleDefinition);
            _methodDebuggerStepThroughCtorRef = _typeDebuggerStepThroughAttributeRef.Resolve().Methods.Single(x => x.IsConstructor && !x.IsStatic && !x.Parameters.Any()).ImportInto(ModuleDefinition);
            _methodExceptionDispatchInfoCaptureRef = typeExceptionDispatchInfoDef.Methods.Single(x => x.IsStatic && x.Name == Constants.METHOD_Capture).ImportInto(ModuleDefinition);
            _methodExceptionDispatchInfoThrowRef = typeExceptionDispatchInfoDef.Methods.Single(x => x.Parameters.Count == 0 && x.Name == Constants.METHOD_Throw).ImportInto(ModuleDefinition);
            var asyncStateMachineAttributeCtorRef = typeAsyncStateMachineAttributeRef.Resolve().Methods.Single(x => x.IsConstructor && !x.IsStatic && x.Parameters.Single().ParameterType.Is(Constants.TYPE_Type)).ImportInto(ModuleDefinition);
            var iteratorStateMachineAttributeCtorRef = typeIteratorStateMachineAttributeRef.Resolve().Methods.Single(x => x.IsConstructor && !x.IsStatic && x.Parameters.Single().ParameterType.Is(Constants.TYPE_Type)).ImportInto(ModuleDefinition);

            _stateMachineCtorRefs = new()
            {
                { Constants.TYPE_AsyncStateMachineAttribute, asyncStateMachineAttributeCtorRef },
                { Constants.TYPE_IteratorStateMachineAttribute, iteratorStateMachineAttributeCtorRef }
            };
        }
    }
}
