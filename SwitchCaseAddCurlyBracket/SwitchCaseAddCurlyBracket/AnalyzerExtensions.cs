using Microsoft.CodeAnalysis;
using System;

namespace CodeCracker {
	public static class AnalyzerExtensions {
		public static T FirstAncestorOrSelfOfType<T>( this SyntaxNode node ) where T : SyntaxNode =>
			(T)node.FirstAncestorOrSelfOfType( typeof( T ) );

		public static SyntaxNode FirstAncestorOrSelfOfType( this SyntaxNode node, params Type[] types ) {
			var currentNode = node;
			while( true ) {
				if( currentNode == null ) break;
				foreach( var type in types ) {
					if( currentNode.GetType() == type ) return currentNode;
				}
				currentNode = currentNode.Parent;
			}
			return null;
		}

		public static SyntaxNode FirstAncestorOfType( this SyntaxNode node, params Type[] types ) {
			var currentNode = node;
			while( true ) {
				var parent = currentNode.Parent;
				if( parent == null ) break;
				foreach( var type in types ) {
					if( parent.GetType() == type ) return parent;
				}
				currentNode = parent;
			}
			return null;
		}
	}
}