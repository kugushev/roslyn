﻿' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Collections.Generic
Imports System.Collections.Immutable
Imports System.Diagnostics
Imports System.Linq
Imports Microsoft.CodeAnalysis.CodeGen

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols
    Friend NotInheritable Class SynthesizedPrivateImplementationDetailsSharedConstructor
        Inherits SynthesizedConstructorSymbol

        Private _containingModule As SourceModuleSymbol
        Private _privateImplementationType As PrivateImplementationDetails

        Friend Sub New(
            containingModule As SourceModuleSymbol,
            privateImplementationType As PrivateImplementationDetails,
            diagnostics As DiagnosticBag,
            voidType As NamedTypeSymbol
        )
            MyBase.New(Nothing, Nothing, True, False, Nothing, diagnostics, voidType)

            _containingModule = containingModule
            _privateImplementationType = privateImplementationType
        End Sub

        Friend Overrides Function GetBoundMethodBody(compilationState As TypeCompilationState, diagnostics As DiagnosticBag, Optional ByRef methodBodyBinder As Binder = Nothing) As BoundBlock
            methodBodyBinder = Nothing

            Dim factory As New SyntheticBoundNodeFactory(Me, Me, VisualBasicSyntaxTree.Dummy.GetRoot(), compilationState, diagnostics)

            ' Initialize the payload root for for each kind of dynamic analysis instrumentation.
            ' A payload root Is an array of arrays of per-method instrumentation payloads.
            ' For each kind of instrumentation:
            '
            '     payloadRoot = New T(MaximumMethodDefIndex)() {}
            '
            ' where T Is the type of the payload at each instrumentation point, And MaximumMethodDefIndex Is the 
            ' index portion of the greatest method definition token in the compilation. This guarantees that any
            ' method can use the index portion of its own method definition token as an index into the payload array.

            Dim payloadRootFields As IReadOnlyCollection(Of KeyValuePair(Of Integer, InstrumentationPayloadRootField)) = _privateImplementationType.GetInstrumentationPayloadRoots()
            Debug.Assert(payloadRootFields.Count > 0)

            Dim body As ArrayBuilder(Of BoundStatement) = ArrayBuilder(Of BoundStatement).GetInstance(2 + payloadRootFields.Count)

            For Each payloadRoot As KeyValuePair(Of Integer, InstrumentationPayloadRootField) In payloadRootFields.OrderBy(Function(analysis) analysis.Key)

                Dim analysisKind As Integer = payloadRoot.Key
                Dim payloadArrayType As ArrayTypeSymbol = DirectCast(payloadRoot.Value.Type, ArrayTypeSymbol)

                body.Add(
                    factory.Assignment(
                        factory.InstrumentationPayloadRoot(analysisKind, payloadArrayType, True),
                        factory.Array(payloadArrayType.ElementType, ImmutableArray.Create(factory.MaximumMethodDefIndex()), ImmutableArray(Of BoundExpression).Empty)))
            Next

            ' Initialize the module version ID (MVID) field. Dynamic instrumentation requires the MVID of the executing module, and this field makes that accessible.
            ' MVID = Guid.Parse(ModuleVersionIdString)

            Dim guidParse As MethodSymbol = factory.WellKnownMember(Of MethodSymbol)(WellKnownMember.System_Guid__Parse)
            If guidParse IsNot Nothing Then
                body.Add(
                    factory.Assignment(
                       factory.ModuleVersionId(True),
                       factory.Call(Nothing, guidParse, ImmutableArray.Create(factory.ModuleVersionIdString()))))
            End If

            body.Add(factory.Return())

            Dim block As BoundBlock = factory.Block(body.ToImmutableAndFree())
            ' factory.CloseMethod(block)

            Return block
        End Function

        Public Overrides ReadOnly Property ContainingModule As ModuleSymbol
            Get
                Return _containingModule
            End Get
        End Property
    End Class
End Namespace
