using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeCracker {
	public static class CSharpAnalyzerExtensions {
		public static bool IsKind( this SyntaxTrivia trivia, params SyntaxKind[] kinds ) {
			foreach( var kind in kinds )
				if( Microsoft.CodeAnalysis.CSharpExtensions.IsKind( trivia, kind ) ) return true;
			return false;
		}

		public static bool IsKind( this SyntaxNode node, params SyntaxKind[] kinds ) {
			foreach( var kind in kinds )
				if( Microsoft.CodeAnalysis.CSharpExtensions.IsKind( node, kind ) ) return true;
			return false;
		}


		public static bool HasAttributeOnAncestorOrSelf( this SyntaxNode node, params string[] attributeNames ) {
			var csharpNode = node as CSharpSyntaxNode;
			if( csharpNode == null ) throw new Exception( "Node is not a C# node" );
			foreach( var attributeName in attributeNames )
				if( csharpNode.HasAttributeOnAncestorOrSelf( attributeName ) ) return true;
			return false;
		}

		public static bool HasAttributeOnAncestorOrSelf( this CSharpSyntaxNode node, string attributeName ) {
			var parentMethod = (BaseMethodDeclarationSyntax)node.FirstAncestorOrSelfOfType( typeof( MethodDeclarationSyntax ), typeof( ConstructorDeclarationSyntax ) );
			if( parentMethod?.AttributeLists.HasAttribute( attributeName ) ?? false )
				return true;
			var type = (TypeDeclarationSyntax)node.FirstAncestorOrSelfOfType( typeof( ClassDeclarationSyntax ), typeof( StructDeclarationSyntax ) );
			while( type != null ) {
				if( type.AttributeLists.HasAttribute( attributeName ) )
					return true;
				type = (TypeDeclarationSyntax)type.FirstAncestorOfType( typeof( ClassDeclarationSyntax ), typeof( StructDeclarationSyntax ) );
			}
			var property = node.FirstAncestorOrSelfOfType<PropertyDeclarationSyntax>();
			if( property?.AttributeLists.HasAttribute( attributeName ) ?? false )
				return true;
			var accessor = node.FirstAncestorOrSelfOfType<AccessorDeclarationSyntax>();
			if( accessor?.AttributeLists.HasAttribute( attributeName ) ?? false )
				return true;
			var anInterface = node.FirstAncestorOrSelfOfType<InterfaceDeclarationSyntax>();
			if( anInterface?.AttributeLists.HasAttribute( attributeName ) ?? false )
				return true;
			var anEvent = node.FirstAncestorOrSelfOfType<EventDeclarationSyntax>();
			if( anEvent?.AttributeLists.HasAttribute( attributeName ) ?? false )
				return true;
			var anEnum = node.FirstAncestorOrSelfOfType<EnumDeclarationSyntax>();
			if( anEnum?.AttributeLists.HasAttribute( attributeName ) ?? false )
				return true;
			var field = node.FirstAncestorOrSelfOfType<FieldDeclarationSyntax>();
			if( field?.AttributeLists.HasAttribute( attributeName ) ?? false )
				return true;
			var eventField = node.FirstAncestorOrSelfOfType<EventFieldDeclarationSyntax>();
			if( eventField?.AttributeLists.HasAttribute( attributeName ) ?? false )
				return true;
			var parameter = node as ParameterSyntax;
			if( parameter?.AttributeLists.HasAttribute( attributeName ) ?? false )
				return true;
			var aDelegate = node as DelegateDeclarationSyntax;
			if( aDelegate?.AttributeLists.HasAttribute( attributeName ) ?? false )
				return true;
			return false;
		}

		public static bool HasAttribute( this SyntaxList<AttributeListSyntax> attributeLists, string attributeName ) =>
			attributeLists.SelectMany( a => a.Attributes ).Any( a => a.Name.ToString().EndsWith( attributeName, StringComparison.OrdinalIgnoreCase ) );


		private static InitializerState DoesBlockContainCertainInitializer( this StatementSyntax statement, SyntaxNodeAnalysisContext context, ISymbol symbol ) {
			return new[] { statement }.DoesBlockContainCertainInitializer( context, symbol );
		}

		/// <summary>
		/// This method can be used to determine if the specified block of
		/// statements contains an initializer for the specified symbol.
		/// </summary>
		/// <param name="context">The context.</param>
		/// <param name="symbol">The symbol.</param>
		/// <param name="statements">The statements.</param>
		/// <returns>
		/// The initializer state found
		/// </returns>
		/// <remarks>
		/// Code blocks that might not always be called are:
		/// - An if or else statement.
		/// - The body of a for, while or for-each loop.
		/// - Switch statements
		///
		/// The following exceptions are taken into account:
		/// - If both if and else statements contain a certain initialization.
		/// - If all cases in a switch contain a certain initialization (this means a default case must exist as well).
		///
		/// Please note that this is a recursive function so we can check a block of code in an if statement for example.
		/// </remarks>
		private static InitializerState DoesBlockContainCertainInitializer( this IEnumerable<StatementSyntax> statements, SyntaxNodeAnalysisContext context, ISymbol symbol ) {
			// Keep track of the current initializer state. This can only be None
			// or Initializer, WayToSkipInitializer will always be returned immediately.
			// Only way to go back from Initializer to None is if there is an assignment
			// to null after a previous assignment to a non-null value.
			var currentState = InitializerState.None;

			foreach( var statement in statements ) {
				if( statement.IsKind( SyntaxKind.ReturnStatement ) && currentState == InitializerState.None ) {
					return InitializerState.WayToSkipInitializer;
				} else if( statement.IsKind( SyntaxKind.Block ) ) {
					var blockResult = ( (BlockSyntax)statement ).Statements.DoesBlockContainCertainInitializer( context, symbol );
					if( CanSkipInitializer( blockResult, currentState ) )
						return InitializerState.WayToSkipInitializer;
					if( blockResult == InitializerState.Initializer )
						currentState = blockResult;
				} else if( statement.IsKind( SyntaxKind.UsingStatement ) ) {
					var blockResult = ( (UsingStatementSyntax)statement ).Statement.DoesBlockContainCertainInitializer( context, symbol );
					if( CanSkipInitializer( blockResult, currentState ) )
						return InitializerState.WayToSkipInitializer;
					if( blockResult == InitializerState.Initializer )
						currentState = blockResult;
				} else if( statement.IsKind( SyntaxKind.ExpressionStatement ) ) {
					var expression = ( (ExpressionStatementSyntax)statement ).Expression;
					if( expression.IsKind( SyntaxKind.SimpleAssignmentExpression ) ) {
						var assignmentExpression = (AssignmentExpressionSyntax)expression;
						var identifier = assignmentExpression.Left;
						if( identifier != null ) {
							var right = assignmentExpression.Right;
							if( right != null ) {
								if( right.IsKind( SyntaxKind.NullLiteralExpression ) )
									currentState = InitializerState.None;
								else if( symbol.Equals( context.SemanticModel.GetSymbolInfo( identifier ).Symbol ) )
									currentState = InitializerState.Initializer;
							}
						}
					}
				} else if( statement.IsKind( SyntaxKind.SwitchStatement ) ) {
					var switchStatement = (SwitchStatementSyntax)statement;
					if( switchStatement.Sections.Any( s => s.Labels.Any( l => l.IsKind( SyntaxKind.DefaultSwitchLabel ) ) ) ) {
						var sectionInitializerStates = switchStatement.Sections.Select( s => s.Statements.DoesBlockContainCertainInitializer( context, symbol ) ).ToList();
						if( sectionInitializerStates.All( sectionInitializerState => sectionInitializerState == InitializerState.Initializer ) )
							currentState = InitializerState.Initializer;
						else if( sectionInitializerStates.Any( sectionInitializerState => CanSkipInitializer( sectionInitializerState, currentState ) ) )
							return InitializerState.WayToSkipInitializer;
					}
				} else if( statement.IsKind( SyntaxKind.IfStatement ) ) {
					var ifStatement = (IfStatementSyntax)statement;

					var ifResult = ifStatement.Statement.DoesBlockContainCertainInitializer( context, symbol );
					if( ifStatement.Else != null ) {
						var elseResult = ifStatement.Else.Statement.DoesBlockContainCertainInitializer( context, symbol );

						if( ifResult == InitializerState.Initializer && elseResult == InitializerState.Initializer )
							currentState = InitializerState.Initializer;
						if( CanSkipInitializer( elseResult, currentState ) )
							return InitializerState.WayToSkipInitializer;
					}
					if( CanSkipInitializer( ifResult, currentState ) ) {
						return InitializerState.WayToSkipInitializer;
					}
				}
			}
			return currentState;
		}

		private static bool CanSkipInitializer( InitializerState foundState, InitializerState currentState ) =>
			foundState == InitializerState.WayToSkipInitializer && currentState == InitializerState.None;

	}
}