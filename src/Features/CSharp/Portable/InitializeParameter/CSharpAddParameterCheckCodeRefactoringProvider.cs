﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System;
using System.Composition;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.CSharp.CodeStyle;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.InitializeParameter;
using Microsoft.CodeAnalysis.Options;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.CodeAnalysis.CSharp.InitializeParameter
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = PredefinedCodeRefactoringProviderNames.AddParameterCheck), Shared]
    [ExtensionOrder(Before = PredefinedCodeRefactoringProviderNames.ChangeSignature)]
    internal class CSharpAddParameterCheckCodeRefactoringProvider :
        AbstractAddParameterCheckCodeRefactoringProvider<
            BaseTypeDeclarationSyntax,
            ParameterSyntax,
            StatementSyntax,
            ExpressionSyntax,
            BinaryExpressionSyntax>
    {
        [ImportingConstructor]
        [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
        public CSharpAddParameterCheckCodeRefactoringProvider()
        {
        }

        protected override bool IsFunctionDeclaration(SyntaxNode node)
            => InitializeParameterHelpers.IsFunctionDeclaration(node);

        protected override SyntaxNode GetBody(SyntaxNode functionDeclaration)
            => InitializeParameterHelpers.GetBody(functionDeclaration);

        protected override void InsertStatement(SyntaxEditor editor, SyntaxNode functionDeclaration, bool returnsVoid, SyntaxNode statementToAddAfterOpt, StatementSyntax statement)
            => InitializeParameterHelpers.InsertStatement(editor, functionDeclaration, returnsVoid, statementToAddAfterOpt, statement);

        protected override bool IsImplicitConversion(Compilation compilation, ITypeSymbol source, ITypeSymbol destination)
            => InitializeParameterHelpers.IsImplicitConversion(compilation, source, destination);

        protected override bool CanOffer(SyntaxNode body)
        {
            if (InitializeParameterHelpers.IsExpressionBody(body))
            {
                return InitializeParameterHelpers.TryConvertExpressionBodyToStatement(body,
                    semicolonToken: Token(SyntaxKind.SemicolonToken),
                    createReturnStatementForExpression: false,
                    statement: out var _);
            }

            return true;
        }

        protected override bool PrefersThrowExpression(DocumentOptionSet options)
            => options.GetOption(CSharpCodeStyleOptions.PreferThrowExpression).Value;

        protected override string EscapeResourceString(string input)
            => input.Replace("\\", "\\\\").Replace("\"", "\\\"");

        protected override StatementSyntax CreateParameterCheckIfStatement(DocumentOptionSet options, ExpressionSyntax condition, StatementSyntax ifTrueStatement)
        {
            var withBlock = options.GetOption(CSharpCodeStyleOptions.PreferBraces).Value == CodeAnalysis.CodeStyle.PreferBracesPreference.Always;
            var singleLine = options.GetOption(CSharpCodeStyleOptions.AllowEmbeddedStatementsOnSameLine).Value;
            var ifTruePart = withBlock
                ? Block(ifTrueStatement) // wrap the if-true part in braces
                : singleLine
                    ? ifTrueStatement.WithoutLeadingTrivia() // if single line is allowed, any elastic trivia between the closing brace of if and the statement must be removed
                    : ifTrueStatement;
            var closeParenTrailingTrivia = singleLine && !withBlock
                ? Space // Remove any elastic marker and replace it with a space
                : ElasticMarker;

            return IfStatement(
                attributeLists: default,
                ifKeyword: Token(SyntaxKind.IfKeyword),
                openParenToken: Token(SyntaxKind.OpenParenToken),
                condition: condition,
                closeParenToken: Token(SyntaxKind.CloseParenToken).WithTrailingTrivia(closeParenTrailingTrivia),
                statement: ifTruePart,
                @else: null);
        }
    }
}
