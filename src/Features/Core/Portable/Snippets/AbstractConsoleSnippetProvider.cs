﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Shared.Extensions.ContextQuery;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.CodeAnalysis.Snippets;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Snippets
{
    internal abstract class AbstractConsoleSnippetProvider : AbstractSnippetProvider
    {
        protected abstract bool ShouldDisplaySnippet(SyntaxContext context);
        protected abstract SyntaxNode? GetAsyncSupportingDeclaration(SyntaxToken token);

        public AbstractConsoleSnippetProvider()
        {
        }

        protected override async Task<bool> IsValidSnippetLocationAsync(Document document, int position, CancellationToken cancellationToken)
        {
            var semanticModel = await document.ReuseExistingSpeculativeModelAsync(position, cancellationToken).ConfigureAwait(false);
            var syntaxContext = document.GetRequiredLanguageService<ISyntaxContextService>().CreateContext(document, semanticModel, position, cancellationToken);
            var compilation = await document.Project.GetRequiredCompilationAsync(cancellationToken).ConfigureAwait(false);
            var symbol = compilation.GetTypeByMetadataName(typeof(Console).FullName);
            if (symbol is null)
            {
                return false;
            }

            if (!ShouldDisplaySnippet(syntaxContext))
            {
                return false;
            }

            return true;
        }

        protected override string GetSnippetDisplayName()
        {
            return FeaturesResources.Write_to_the_Console;
        }

        protected override async Task<ImmutableArray<TextChange>> GenerateSnippetTextChangesAsync(Document document, int position, CancellationToken cancellationToken)
        {
            using var _ = ArrayBuilder<TextChange>.GetInstance(out var arrayBuilder);
            var snippetTextChange = await GenerateSnippetAsync(document, position, cancellationToken).ConfigureAwait(false);
            arrayBuilder.AddRange(snippetTextChange);
            return arrayBuilder.ToImmutableArray();
        }

        private async Task<TextChange> GenerateSnippetAsync(Document document, int position, CancellationToken cancellationToken)
        {
            var symbol = await GetSymbolFromMetaDataNameAsync(document, cancellationToken).ConfigureAwait(false);
            var generator = SyntaxGenerator.GetGenerator(document);
            var tree = await document.GetRequiredSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
            var token = tree.FindTokenOnLeftOfPosition(position, cancellationToken);
            var typeExpression = generator.TypeExpression(symbol.GetSymbolType());
            var declaration = GetAsyncSupportingDeclaration(token);
            var isAsync = generator.GetModifiers(declaration).IsAsync;
            SyntaxNode? invocation;
            invocation = isAsync
                ? generator.ExpressionStatement(generator.AwaitExpression(generator.InvocationExpression(
                    generator.MemberAccessExpression(generator.MemberAccessExpression(typeExpression, generator.IdentifierName(nameof(Console.Out))), generator.IdentifierName(nameof(Console.Out.WriteLineAsync))))))
                : generator.ExpressionStatement(generator.InvocationExpression(generator.MemberAccessExpression(typeExpression, generator.IdentifierName(nameof(Console.WriteLine)))));
            return new TextChange(TextSpan.FromBounds(position, position), invocation.NormalizeWhitespace().ToFullString());
        }

        protected override int GetTargetCaretPosition(ISyntaxFactsService syntaxFacts, SyntaxNode caretTarget)
        {
            var invocationExpression = caretTarget.DescendantNodes().Where(syntaxFacts.IsInvocationExpression).First();
            var argumentListNode = syntaxFacts.GetArgumentListOfInvocationExpression(invocationExpression);
            return argumentListNode!.GetLocation().SourceSpan.End - 1;
        }

        protected override async Task<SyntaxNode> AnnotateNodesToReformatAsync(Document document,
            SyntaxAnnotation findSnippetAnnotation, SyntaxAnnotation cursorAnnotation, int position, CancellationToken cancellationToken)
        {
            var root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var syntaxFacts = document.GetRequiredLanguageService<ISyntaxFactsService>();
            var snippetExpressionNode = GetConsoleExpressionStatement(syntaxFacts, root, position);
            if (snippetExpressionNode is null)
            {
                return root;
            }

            var symbol = await GetSymbolFromMetaDataNameAsync(document, cancellationToken).ConfigureAwait(false);

            var reformatSnippetNode = snippetExpressionNode.WithAdditionalAnnotations(findSnippetAnnotation, cursorAnnotation, Simplifier.Annotation, SymbolAnnotation.Create(symbol!), Formatter.Annotation);
            return root.ReplaceNode(snippetExpressionNode, reformatSnippetNode);
        }

        private static SyntaxNode? GetConsoleExpressionStatement(ISyntaxFactsService syntaxFacts, SyntaxNode root, int position)
        {
            var closestNode = root.FindNode(TextSpan.FromBounds(position, position));
            return closestNode.FirstAncestorOrSelf<SyntaxNode>(syntaxFacts.IsExpressionStatement);
        }

        private static async Task<ISymbol?> GetSymbolFromMetaDataNameAsync(Document document, CancellationToken cancellationToken)
        {
            var compilation = await document.Project.GetRequiredCompilationAsync(cancellationToken).ConfigureAwait(false);
            var symbol = compilation.GetBestTypeByMetadataName(typeof(Console).FullName);
            return symbol;
        }

        protected override Task<ImmutableArray<TextSpan>> GetRenameLocationsAsync(Document document, int position, CancellationToken cancellationToken)
        {
            return Task.FromResult(ImmutableArray<TextSpan>.Empty);
        }
    }
}
