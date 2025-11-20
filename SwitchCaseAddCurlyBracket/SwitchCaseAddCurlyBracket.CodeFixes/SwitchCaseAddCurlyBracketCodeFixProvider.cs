using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace SwitchCaseAddCurlyBracket {
	[ExportCodeFixProvider( LanguageNames.CSharp, Name = nameof( SwitchCaseAddCurlyBracketCodeFixProvider ) ), Shared]
	public class SwitchCaseAddCurlyBracketCodeFixProvider : CodeFixProvider {
		public sealed override ImmutableArray<string> FixableDiagnosticIds {
			get { return ImmutableArray.Create( SwitchCaseAddCurlyBracketAnalyzer.DiagnosticId ); }
		}

		public sealed override FixAllProvider GetFixAllProvider() {
			// See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
			return WellKnownFixAllProviders.BatchFixer;
		}

		public sealed override Task RegisterCodeFixesAsync( CodeFixContext context ) {
			var diagnostic = context.Diagnostics.First();

			context.RegisterCodeFix( CodeAction.Create(
					title: CodeFixResources.CodeFixTitle,
					ct => AddBracesAsync( context.Document, diagnostic, ct ),
					SwitchCaseAddCurlyBracketAnalyzer.DiagnosticId ),
				diagnostic );

			return Task.CompletedTask;
		}

		private static async Task<Document> AddBracesAsync( Document document, Diagnostic diagnostic, CancellationToken cancellationToken ) {
			var root = await document.GetSyntaxRootAsync( cancellationToken ).ConfigureAwait( false );
			var syntax = (SwitchStatementSyntax)root.FindNode( diagnostic.Location.SourceSpan );
			var sections = syntax.Sections.Select( x => SwitchCaseAddCurlyBracketAnalyzer.HasBraces( x ) ? x : AddBraces( x ) );

			var newSwitch = syntax.WithSections( SyntaxFactory.List( sections ) ).WithAdditionalAnnotations( Formatter.Annotation );
			var newRoot = root.ReplaceNode( syntax, newSwitch );
			var newDocument = document.WithSyntaxRoot( newRoot );
			return newDocument;
		}

		private static SwitchSectionSyntax AddBraces( SwitchSectionSyntax section ) {
			StatementSyntax blockStatement = SyntaxFactory.Block( section.Statements ).WithoutTrailingTrivia();
			return section.Update( section.Labels, SyntaxFactory.SingletonList( blockStatement ) );
		}
	}
}
