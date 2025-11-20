using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using CodeCracker;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SwitchCaseAddCurlyBracket {
	[DiagnosticAnalyzer( LanguageNames.CSharp )]
	public class SwitchCaseAddCurlyBracketAnalyzer : DiagnosticAnalyzer {
		public const string DiagnosticId = "SwitchCaseAddCurlyBracket";

		// You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
		// See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Localizing%20Analyzers.md for more on localization
		private static readonly LocalizableString Title = new LocalizableResourceString( nameof( Resources.AnalyzerTitle ), Resources.ResourceManager, typeof( Resources ) );
		private static readonly LocalizableString MessageFormat = new LocalizableResourceString( nameof( Resources.AnalyzerMessageFormat ), Resources.ResourceManager, typeof( Resources ) );
		private static readonly LocalizableString Description = new LocalizableResourceString( nameof( Resources.AnalyzerDescription ), Resources.ResourceManager, typeof( Resources ) );
		private const string Category = "Formatting";

		private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor( DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Hidden, isEnabledByDefault: true, description: Description );

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create( Rule ); } }

		public override void Initialize( AnalysisContext context ) {
			context.RegisterSyntaxNodeAction( AnalyzeNode, SyntaxKind.SwitchStatement );
		}

		private static void AnalyzeNode( SyntaxNodeAnalysisContext context ) {
			if( context.IsGenerated() ) return;

			var sw = (SwitchStatementSyntax)context.Node;
			if( !sw.Sections.All( HasBraces ) ) {
				context.ReportDiagnostic( Diagnostic.Create( Rule, sw.GetLocation() ) );
			}
		}

		public static bool HasBraces( SwitchSectionSyntax section ) {
			switch( section.Statements.Count ) {
				case 1:
					if( section.Statements.First() is BlockSyntax )
						return true;
					break;
				case 2:
					if( section.Statements.First() is BlockSyntax && section.Statements.Last() is BreakStatementSyntax )
						return true;
					break;
			}
			return false;
		}
	}
}
